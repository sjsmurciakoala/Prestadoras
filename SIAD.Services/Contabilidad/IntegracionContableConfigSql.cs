using System.Data;
using System.Data.Common;
using System.Text.Json;
using Dapper;
using SIAD.Core.DTOs.Bancos;

namespace SIAD.Services.Contabilidad;

/// <summary>
/// Helpers Dapper de la integración contable por configuración (plan F4, D2):
/// lectura de <c>con_integracion_config</c>, resolución de cuentas vía
/// <c>fn_con_resolver_cuenta</c> / <c>fn_con_resolver_cuenta_modo</c> y
/// generación/reverso de comprobantes vía
/// <c>sp_con_generar_comprobante_config</c> / <c>sp_con_revertir_comprobante_config</c>.
/// Reemplaza a las plantillas (<c>con_plantilla_partida_*</c>) y reglas
/// (<c>con_regla_integracion</c>) en los flujos de captación, abonos y misceláneos.
/// </summary>
internal static class IntegracionContableConfigSql
{
    internal sealed record ConfigIntegracion(
        string ModoVentas,
        string ModoCxc,
        bool EncolarSinPeriodo,
        bool ActivoFacturacion,
        bool ActivoCaja,
        bool ActivoBancos,
        bool ActivoNotas,
        bool ActivoMiscelaneos,
        bool ActivoProveedores);

    internal sealed record ComprobanteLinea(long AccountId, decimal Debe, decimal Haber, string? Descripcion);

    /// <summary>Config de integración de la empresa, o null si aún no fue configurada.</summary>
    internal static async Task<ConfigIntegracion?> ObtenerConfigAsync(
        DbConnection connection,
        long companyId,
        IDbTransaction? transaction,
        CancellationToken ct)
    {
        await EnsureOpenAsync(connection, ct);

        const string sql = @"
            SELECT modo_ventas AS ModoVentas,
                   modo_cxc AS ModoCxc,
                   encolar_sin_periodo AS EncolarSinPeriodo,
                   activo_facturacion AS ActivoFacturacion,
                   activo_caja AS ActivoCaja,
                   activo_bancos AS ActivoBancos,
                   activo_notas AS ActivoNotas,
                   activo_miscelaneos AS ActivoMiscelaneos,
                   activo_proveedores AS ActivoProveedores
            FROM public.con_integracion_config
            WHERE company_id = @CompanyId;
        ";

        return await connection.QueryFirstOrDefaultAsync<ConfigIntegracion>(
            new CommandDefinition(sql, new { CompanyId = companyId }, transaction: transaction, cancellationToken: ct));
    }

    /// <summary>Resuelve la cuenta de un uso general (fila comodín) vía fn_con_resolver_cuenta.</summary>
    internal static async Task<long> ResolverCuentaAsync(
        DbConnection connection,
        long companyId,
        string uso,
        IDbTransaction? transaction,
        CancellationToken ct)
    {
        await EnsureOpenAsync(connection, ct);

        const string sql = "SELECT public.fn_con_resolver_cuenta(@CompanyId, @Uso, NULL, NULL, NULL);";
        var accountId = await connection.ExecuteScalarAsync<long?>(
            new CommandDefinition(sql, new { CompanyId = companyId, Uso = uso }, transaction: transaction, cancellationToken: ct));

        if (!accountId.HasValue || accountId.Value <= 0)
        {
            throw new InvalidOperationException(
                $"Integración contable: no se resolvió cuenta para uso={uso} (company_id={companyId}).");
        }

        return accountId.Value;
    }

    /// <summary>
    /// Resuelve la cuenta CxC para cada código de servicio del detalle según el
    /// modo configurado (fn_con_resolver_cuenta_modo). Los códigos sin servicio
    /// asociado caen a la fila general por el fallback de la matriz; si dos
    /// servicios colisionan tras normalizar el código, gana el de menor id.
    /// Devuelve el mapa código-normalizado → account_id.
    /// </summary>
    internal static async Task<Dictionary<string, long>> ResolverCuentasCxcPorServicioAsync(
        DbConnection connection,
        long companyId,
        string modoCxc,
        IEnumerable<string?> codigosServicio,
        int? categoriaServicioId,
        bool? conMedicion,
        IDbTransaction? transaction,
        CancellationToken ct)
    {
        var codigos = codigosServicio
            .Select(NormalizarCodigo)
            .Distinct()
            .ToArray();

        if (codigos.Length == 0)
        {
            return new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        }

        await EnsureOpenAsync(connection, ct);

        const string sql = @"
            SELECT c.codigo AS Codigo,
                   public.fn_con_resolver_cuenta_modo(
                       @CompanyId, 'CXC', @Modo, s.servicio_id, @CategoriaId, @ConMedicion) AS CuentaId
            FROM (SELECT DISTINCT upper(btrim(x)) AS codigo FROM unnest(@Codigos) AS x) c
            LEFT JOIN LATERAL (
                SELECT s.servicio_id
                FROM public.adm_servicio s
                WHERE s.company_id = @CompanyId
                  AND upper(btrim(s.codigo)) = c.codigo
                ORDER BY s.servicio_id
                LIMIT 1
            ) s ON true;
        ";

        var rows = await connection.QueryAsync<(string Codigo, long CuentaId)>(
            new CommandDefinition(
                sql,
                new
                {
                    CompanyId = companyId,
                    Modo = modoCxc,
                    Codigos = codigos,
                    CategoriaId = categoriaServicioId,
                    ConMedicion = conMedicion
                },
                transaction: transaction,
                cancellationToken: ct));

        return rows.ToDictionary(r => r.Codigo, r => r.CuentaId, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Genera y postea el comprobante vía sp_con_generar_comprobante_config.
    /// Devuelve el poliza_id, o null si quedó encolado en con_partida_pendiente
    /// (sin período abierto y encolar_sin_periodo=true).
    /// </summary>
    internal static async Task<long?> GenerarComprobanteAsync(
        DbConnection connection,
        long companyId,
        string module,
        string documentType,
        long documentId,
        string documentNumber,
        DateOnly polizaDate,
        string description,
        string usuario,
        IReadOnlyList<ComprobanteLinea> lineas,
        IDbTransaction? transaction,
        CancellationToken ct)
    {
        await EnsureOpenAsync(connection, ct);

        const string sql = @"
            SELECT public.sp_con_generar_comprobante_config(
                @CompanyId, @Module, @DocumentType, @DocumentId, @DocumentNumber,
                @PolizaDate::date, @Description, @Usuario, @Lineas::jsonb);
        ";

        return await connection.ExecuteScalarAsync<long?>(
            new CommandDefinition(
                sql,
                new
                {
                    CompanyId = companyId,
                    Module = module,
                    DocumentType = documentType,
                    DocumentId = documentId,
                    DocumentNumber = documentNumber,
                    PolizaDate = polizaDate.ToDateTime(TimeOnly.MinValue),
                    Description = description,
                    Usuario = string.IsNullOrWhiteSpace(usuario) ? "system" : usuario.Trim(),
                    Lineas = SerializarLineas(lineas)
                },
                transaction: transaction,
                cancellationToken: ct));
    }

    /// <summary>
    /// Revierte el comprobante del documento (y descarta su pendiente viva) vía
    /// sp_con_revertir_comprobante_config. Devuelve el poliza_id revertido o null.
    /// </summary>
    internal static async Task<long?> RevertirComprobanteAsync(
        DbConnection connection,
        long companyId,
        string module,
        string[] documentTypes,
        long documentId,
        string usuario,
        IDbTransaction? transaction,
        CancellationToken ct)
    {
        await EnsureOpenAsync(connection, ct);

        const string sql = @"
            SELECT public.sp_con_revertir_comprobante_config(
                @CompanyId, @Module, @DocumentTypes, @DocumentId, @Usuario);
        ";

        return await connection.ExecuteScalarAsync<long?>(
            new CommandDefinition(
                sql,
                new
                {
                    CompanyId = companyId,
                    Module = module,
                    DocumentTypes = documentTypes,
                    DocumentId = documentId,
                    Usuario = string.IsNullOrWhiteSpace(usuario) ? "system" : usuario.Trim()
                },
                transaction: transaction,
                cancellationToken: ct));
    }

    /// <summary>
    /// Arma las líneas Debe caja / Haber CxC de un cobro. Los haberes se
    /// redondean por cuenta y el debe es la suma de esos redondeos, para que la
    /// partida siempre balancee (redondear la suma cruda por un lado y los
    /// parciales por el otro puede diferir con montos de más de 2 decimales).
    /// </summary>
    internal static List<ComprobanteLinea> ArmarLineasCobro(
        long cuentaCajaId,
        IEnumerable<(long CuentaCxcId, decimal Monto)> aplicaciones,
        string descripcion)
    {
        var lineasHaber = AgruparMontosPorCuenta(aplicaciones)
            .Select(kvp => new ComprobanteLinea(kvp.Key, 0m, kvp.Value, descripcion))
            .ToList();

        var total = lineasHaber.Sum(l => l.Haber);
        if (total <= 0)
        {
            throw new InvalidOperationException("El cobro no tiene montos aplicables para generar comprobante contable.");
        }

        var lineas = new List<ComprobanteLinea> { new(cuentaCajaId, total, 0m, descripcion) };
        lineas.AddRange(lineasHaber);
        return lineas;
    }

    /// <summary>
    /// Contracuentas CxC analíticas del movimiento bancario de un cobro/abono,
    /// resueltas por la matriz de integración según el modo configurado
    /// (Banco / CxC — reemplaza al mapeo legacy por servicio y a la cuenta
    /// única de con_regla_integracion).
    /// </summary>
    internal static async Task<IReadOnlyList<BanTransaccionContraLineaDto>> ConstruirContraCuentasCxcAsync(
        DbConnection connection,
        long companyId,
        IReadOnlyList<(string? ServicioCodigo, decimal Monto)> aplicaciones,
        int? categoriaServicioId,
        bool? conMedicion,
        string descripcion,
        string sourceDocument,
        CancellationToken ct)
    {
        var entradas = aplicaciones.Where(a => a.Monto > 0).ToList();
        if (entradas.Count == 0)
        {
            throw new InvalidOperationException("No se recibieron detalles con monto para construir contracuentas bancarias.");
        }

        var sourceDocumentNormalizado = string.IsNullOrWhiteSpace(sourceDocument)
            ? "CAPTACION"
            : sourceDocument.Trim();
        if (sourceDocumentNormalizado.Length > 120)
        {
            sourceDocumentNormalizado = sourceDocumentNormalizado[..120];
        }

        var config = await ObtenerConfigAsync(connection, companyId, transaction: null, ct)
            ?? throw new InvalidOperationException(
                "La empresa no tiene configuración de integración contable (pantalla Integración Contable / perfil ERSAPS).");

        var cuentasCxc = await ResolverCuentasCxcPorServicioAsync(
            connection,
            companyId,
            config.ModoCxc,
            entradas.Select(a => a.ServicioCodigo),
            categoriaServicioId,
            conMedicion,
            transaction: null,
            ct);

        return AgruparMontosPorCuenta(entradas.Select(a => (cuentasCxc[NormalizarCodigo(a.ServicioCodigo)], a.Monto)))
            .Select(kvp => new BanTransaccionContraLineaDto
            {
                CuentaId = kvp.Key,
                Monto = kvp.Value,
                Descripcion = descripcion,
                SourceDocument = sourceDocumentNormalizado
            })
            .ToList();
    }

    /// <summary>
    /// Resuelve las aplicaciones (código de servicio, monto) a cuentas CxC según
    /// el modo configurado. Punto único que comparten el comprobante de caja y
    /// las contracuentas bancarias.
    /// </summary>
    internal static async Task<List<(long CuentaCxcId, decimal Monto)>> ResolverAplicacionesCxcAsync(
        DbConnection connection,
        long companyId,
        string modoCxc,
        IReadOnlyList<(string? ServicioCodigo, decimal Monto)> aplicaciones,
        int? categoriaServicioId,
        bool? conMedicion,
        IDbTransaction? transaction,
        CancellationToken ct)
    {
        var cuentasCxc = await ResolverCuentasCxcPorServicioAsync(
            connection,
            companyId,
            modoCxc,
            aplicaciones.Select(a => a.ServicioCodigo),
            categoriaServicioId,
            conMedicion,
            transaction,
            ct);

        return aplicaciones
            .Where(a => a.Monto > 0)
            .Select(a => (cuentasCxc[NormalizarCodigo(a.ServicioCodigo)], a.Monto))
            .ToList();
    }

    internal static string NormalizarCodigo(string? codigo) =>
        (codigo ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>Agrupa montos positivos por cuenta y redondea el total por cuenta (2 dec, AwayFromZero).</summary>
    private static IEnumerable<KeyValuePair<long, decimal>> AgruparMontosPorCuenta(
        IEnumerable<(long CuentaId, decimal Monto)> aplicaciones)
    {
        var porCuenta = new Dictionary<long, decimal>();
        foreach (var (cuentaId, monto) in aplicaciones)
        {
            if (monto <= 0)
            {
                continue;
            }

            porCuenta[cuentaId] = porCuenta.GetValueOrDefault(cuentaId) + monto;
        }

        return porCuenta
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => new KeyValuePair<long, decimal>(
                kvp.Key,
                Math.Round(kvp.Value, 2, MidpointRounding.AwayFromZero)))
            .Where(kvp => kvp.Value > 0);
    }

    private static async Task EnsureOpenAsync(DbConnection connection, CancellationToken ct)
    {
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }
    }

    private static string SerializarLineas(IReadOnlyList<ComprobanteLinea> lineas)
    {
        ArgumentNullException.ThrowIfNull(lineas);

        return JsonSerializer.Serialize(lineas.Select(l => new
        {
            account_id = l.AccountId,
            debe = l.Debe,
            haber = l.Haber,
            descripcion = l.Descripcion
        }));
    }
}
