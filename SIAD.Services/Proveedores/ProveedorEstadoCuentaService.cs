using System.Data;
using System.Data.Common;
using Dapper;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Proveedores;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Contabilidad;
using SIAD.Services.Infrastructure;

namespace SIAD.Services.Proveedores;

/// <summary>
/// Implementación del estado de cuenta del proveedor.
/// <para>
/// Todo el acceso a datos va por Dapper contra las funciones <c>fn_prv_estado_cuenta_*</c>
/// (script <c>Database/2026-08-13_prv_estado_cuenta.sql</c>). Las reglas de vigencia —CxP
/// anulada, compromiso anulado y la compat legacy del compromiso procesado sin abonos— viven
/// en la BD, en un solo lugar, no repartidas aquí.
/// </para>
/// <para>
/// <b>Tenancy:</b> Dapper NO pasa por el filtro global de <see cref="SiadDbContext"/>, así que
/// la empresa se resuelve con <see cref="ICurrentCompanyService"/> y se pasa explícita como
/// primer parámetro de cada función. El <c>codigo</c> nunca decide la empresa.
/// </para>
/// </summary>
public sealed class ProveedorEstadoCuentaService : IProveedorEstadoCuentaService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _currentCompanyService;
    private readonly IAccountFormatService _accountFormat;

    public ProveedorEstadoCuentaService(
        SiadDbContext context,
        ICurrentCompanyService currentCompanyService,
        IAccountFormatService accountFormat)
    {
        _context = context;
        _currentCompanyService = currentCompanyService;
        _accountFormat = accountFormat;

        // Dapper no sabe pasar DateOnly como parámetro sin este handler. Va aquí (y no solo en
        // AddSiadServices) para que también aplique cuando se instancia el servicio a mano,
        // como hacen los tests. Es idempotente.
        DapperTypeHandlers.EnsureRegistered();
    }

    public async Task<ProveedorEstadoCuentaDto?> GetResumenAsync(
        string codigo, DateOnly? corte = null, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();
        codigo = NormalizarCodigo(codigo);
        if (codigo.Length == 0)
        {
            return null;
        }

        var connection = await AbrirConexionAsync(cancellationToken);

        const string sqlIdentidad = @"
            SELECT p.cod_proveedor   AS Codigo,
                   p.nombre          AS Nombre,
                   p.rtn             AS Rtn,
                   t.nombre          AS TipoNombre,
                   p.cuenta_contable AS CuentaContable,
                   COALESCE(p.status, TRUE) AS Activo
            FROM public.prv_proveedores p
            LEFT JOIN public.prv_tipoproveedor t
                   ON t.cod_tipoproveedor = p.cod_tipoproveedor
            WHERE p.company_id    = @CompanyId
              AND p.cod_proveedor = @Codigo";

        var identidad = await connection.QuerySingleOrDefaultAsync<ProveedorEstadoCuentaDto>(
            new CommandDefinition(sqlIdentidad,
                new { CompanyId = companyId, Codigo = codigo },
                cancellationToken: cancellationToken));

        if (identidad is null)
        {
            return null;
        }

        const string sqlResumen = @"
            SELECT saldo_total           AS SaldoTotal,
                   saldo_vencido         AS SaldoVencido,
                   saldo_por_vencer      AS SaldoPorVencer,
                   saldo_vence_7dias     AS SaldoVence7Dias,
                   documentos_pendientes AS DocumentosPendientes,
                   documento_mas_antiguo AS DocumentoMasAntiguo,
                   ultimo_pago_monto     AS UltimoPagoMonto,
                   ultimo_pago_fecha     AS UltimoPagoFecha,
                   antiguedad_corriente  AS AntiguedadCorriente,
                   antiguedad_30         AS Antiguedad30,
                   antiguedad_60         AS Antiguedad60,
                   antiguedad_90         AS Antiguedad90,
                   antiguedad_mas90      AS AntiguedadMas90
            FROM public.fn_prv_estado_cuenta_resumen(@CompanyId, @Codigo, @Corte)";

        var resumen = await connection.QuerySingleOrDefaultAsync<ProveedorEstadoCuentaResumenDto>(
            new CommandDefinition(sqlResumen,
                new { CompanyId = companyId, Codigo = codigo, Corte = corte },
                cancellationToken: cancellationToken));

        identidad.Resumen = resumen ?? new ProveedorEstadoCuentaResumenDto();
        identidad.Corte = corte ?? DateOnly.FromDateTime(DateTime.Today);
        return identidad;
    }

    public async Task<IReadOnlyList<ProveedorEstadoCuentaDocumentoDto>> GetDocumentosAsync(
        string codigo, DateOnly? corte = null, bool soloPendientes = true,
        CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();
        codigo = NormalizarCodigo(codigo);
        if (codigo.Length == 0)
        {
            return Array.Empty<ProveedorEstadoCuentaDocumentoDto>();
        }

        var connection = await AbrirConexionAsync(cancellationToken);

        const string sql = @"
            SELECT origen            AS Origen,
                   documento_id      AS DocumentoId,
                   numero_documento  AS NumeroDocumento,
                   fecha             AS Fecha,
                   fecha_vencimiento AS FechaVencimiento,
                   concepto          AS Concepto,
                   monto             AS Monto,
                   abonado           AS Abonado,
                   saldo             AS Saldo,
                   dias_vencido      AS DiasVencido,
                   estado_id         AS EstadoId
            FROM public.fn_prv_estado_cuenta_documentos(@CompanyId, @Codigo, @Corte, @SoloPendientes)";

        var filas = await connection.QueryAsync<ProveedorEstadoCuentaDocumentoDto>(
            new CommandDefinition(sql,
                new { CompanyId = companyId, Codigo = codigo, Corte = corte, SoloPendientes = soloPendientes },
                cancellationToken: cancellationToken));

        return new List<ProveedorEstadoCuentaDocumentoDto>(filas);
    }

    public async Task<IReadOnlyList<ProveedorEstadoCuentaMovimientoDto>> GetMovimientosAsync(
        string codigo, DateOnly? desde = null, DateOnly? hasta = null,
        CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();
        codigo = NormalizarCodigo(codigo);
        if (codigo.Length == 0)
        {
            return Array.Empty<ProveedorEstadoCuentaMovimientoDto>();
        }

        var connection = await AbrirConexionAsync(cancellationToken);

        const string sql = @"
            SELECT fecha            AS Fecha,
                   origen           AS Origen,
                   tipo             AS Tipo,
                   numero_documento AS NumeroDocumento,
                   referencia       AS Referencia,
                   cargo            AS Cargo,
                   abono            AS Abono,
                   saldo_corrido    AS SaldoCorrido
            FROM public.fn_prv_estado_cuenta_movimientos(@CompanyId, @Codigo, @Desde, @Hasta)";

        var filas = await connection.QueryAsync<ProveedorEstadoCuentaMovimientoDto>(
            new CommandDefinition(sql,
                new { CompanyId = companyId, Codigo = codigo, Desde = desde, Hasta = hasta },
                cancellationToken: cancellationToken));

        return new List<ProveedorEstadoCuentaMovimientoDto>(filas);
    }

    public async Task<ProveedorEstadoCuentaImpresionDto?> GetDatosImpresionAsync(
        string codigo, DateOnly? corte = null, bool soloPendientes = true, string? impresoPor = null,
        CancellationToken cancellationToken = default)
    {
        var estado = await GetResumenAsync(codigo, corte, cancellationToken);
        if (estado is null)
        {
            return null;
        }

        var documentos = await GetDocumentosAsync(codigo, corte, soloPendientes, cancellationToken);

        var companyId = EnsureCompanyId();
        var empresa = await _context.cfg_companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.company_id == companyId, cancellationToken);

        // La cuenta se imprime con el formato de la empresa, igual que en pantalla.
        var formato = await _accountFormat.GetFormatAsync(cancellationToken);

        var items = new List<ProveedorEstadoCuentaDocumentoDto>(documentos);

        return new ProveedorEstadoCuentaImpresionDto
        {
            EmpresaNombre = empresa?.commercial_name ?? string.Empty,
            EmpresaRazonSocial = empresa?.legal_name,
            EmpresaRtn = empresa?.tax_id,
            EmpresaDireccion = empresa?.address,
            EmpresaTelefono = empresa?.phone,
            EmpresaEmail = empresa?.email,
            EmpresaLogo = empresa?.logo,
            ImpresoPor = string.IsNullOrWhiteSpace(impresoPor) ? "sistema" : impresoPor.Trim(),

            Codigo = estado.Codigo,
            Nombre = estado.Nombre,
            Rtn = estado.Rtn,
            TipoNombre = estado.TipoNombre,
            CuentaContable = formato.Format(estado.CuentaContable),
            Corte = estado.Corte,
            Resumen = estado.Resumen,
            Items = items,
            FiltroTexto = soloPendientes
                ? "Incluye únicamente los documentos con saldo pendiente."
                : "Incluye todos los documentos del proveedor, con y sin saldo."
        };
    }

    private long EnsureCompanyId()
    {
        var companyId = _currentCompanyService.GetCompanyId();
        if (companyId <= 0)
        {
            throw new InvalidOperationException("No se pudo determinar la empresa (tenant) actual.");
        }

        return companyId;
    }

    private async Task<DbConnection> AbrirConexionAsync(CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        return connection;
    }

    private static string NormalizarCodigo(string? codigo) => (codigo ?? string.Empty).Trim();
}
