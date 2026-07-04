using System.Data;
using System.Text.Json;
using Dapper;

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
        IDbConnection connection,
        long companyId,
        IDbTransaction? transaction,
        CancellationToken ct)
    {
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
        IDbConnection connection,
        long companyId,
        string uso,
        IDbTransaction? transaction,
        CancellationToken ct)
    {
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
    /// asociado caen a la fila general por el fallback de la matriz. Devuelve el
    /// mapa código-normalizado → account_id (los códigos se comparan en mayúsculas).
    /// </summary>
    internal static async Task<Dictionary<string, long>> ResolverCuentasCxcPorServicioAsync(
        IDbConnection connection,
        long companyId,
        string modoCxc,
        IEnumerable<string?> codigosServicio,
        int? categoriaServicioId,
        bool? conMedicion,
        IDbTransaction? transaction,
        CancellationToken ct)
    {
        var codigos = codigosServicio
            .Select(c => (c ?? string.Empty).Trim().ToUpperInvariant())
            .Distinct()
            .ToArray();

        if (codigos.Length == 0)
        {
            return new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        }

        const string sql = @"
            SELECT c.codigo AS Codigo,
                   public.fn_con_resolver_cuenta_modo(
                       @CompanyId, 'CXC', @Modo, s.servicio_id, @CategoriaId, @ConMedicion) AS CuentaId
            FROM (SELECT DISTINCT upper(btrim(x)) AS codigo FROM unnest(@Codigos) AS x) c
            LEFT JOIN public.adm_servicio s
                   ON s.company_id = @CompanyId
                  AND upper(btrim(s.codigo)) = c.codigo;
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
        IDbConnection connection,
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
        IDbConnection connection,
        long companyId,
        string module,
        string[] documentTypes,
        long documentId,
        string usuario,
        IDbTransaction? transaction,
        CancellationToken ct)
    {
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

    /// <summary>Agrupa montos por cuenta y arma las líneas Debe caja / Haber CxC de un cobro.</summary>
    internal static List<ComprobanteLinea> ArmarLineasCobro(
        long cuentaCajaId,
        IEnumerable<(long CuentaCxcId, decimal Monto)> aplicaciones,
        string descripcionDebe,
        string descripcionHaber)
    {
        var porCuenta = new Dictionary<long, decimal>();
        foreach (var (cuentaCxcId, monto) in aplicaciones)
        {
            if (monto <= 0)
            {
                continue;
            }

            porCuenta[cuentaCxcId] = porCuenta.GetValueOrDefault(cuentaCxcId) + monto;
        }

        var total = Math.Round(porCuenta.Values.Sum(), 2, MidpointRounding.AwayFromZero);
        if (total <= 0)
        {
            throw new InvalidOperationException("El cobro no tiene montos aplicables para generar comprobante contable.");
        }

        var lineas = new List<ComprobanteLinea>
        {
            new(cuentaCajaId, total, 0m, descripcionDebe)
        };

        lineas.AddRange(porCuenta
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => new ComprobanteLinea(
                kvp.Key,
                0m,
                Math.Round(kvp.Value, 2, MidpointRounding.AwayFromZero),
                descripcionHaber)));

        return lineas;
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
