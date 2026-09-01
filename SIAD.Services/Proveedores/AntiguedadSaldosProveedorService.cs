using System.Data;
using System.Data.Common;
using Dapper;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Proveedores;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Infrastructure;

namespace SIAD.Services.Proveedores;

/// <summary>
/// Implementación de la antigüedad de saldos del proveedor.
/// <para>
/// Todo el acceso a datos va por Dapper contra <c>fn_prv_antiguedad_saldos</c>
/// (script <c>Database/2026-08-14_prv_antiguedad_saldos.sql</c>). Las reglas de vigencia —CxP
/// anulada, compromiso anulado y la compat legacy del compromiso procesado sin abonos— viven en la
/// función base <c>fn_prv_estado_cuenta_documentos</c>, no aquí.
/// </para>
/// <para>
/// <b>Tenancy:</b> Dapper NO pasa por el filtro global de <see cref="SiadDbContext"/>, así que la
/// empresa se resuelve con <see cref="ICurrentCompanyService"/> y se pasa explícita como primer
/// parámetro de la función.
/// </para>
/// </summary>
public sealed class AntiguedadSaldosProveedorService : IAntiguedadSaldosProveedorService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _currentCompanyService;

    public AntiguedadSaldosProveedorService(
        SiadDbContext context,
        ICurrentCompanyService currentCompanyService)
    {
        _context = context;
        _currentCompanyService = currentCompanyService;

        // Dapper no sabe pasar DateOnly como parámetro sin este handler. Idempotente.
        DapperTypeHandlers.EnsureRegistered();
    }

    public async Task<AntiguedadSaldosProveedorDto> GetAsync(
        DateOnly? corte = null,
        bool incluirPorVencer = true,
        int origen = 0,
        int? codTipoProveedor = null,
        string? codProveedor = null,
        CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        // Se acota a los tres valores válidos; cualquier otro se trata como "ambos".
        var origenNorm = origen is 1 or 2 ? origen : 0;

        var connection = await AbrirConexionAsync(cancellationToken);

        const string sql = @"
            SELECT cod_proveedor         AS CodProveedor,
                   proveedor_nombre      AS Nombre,
                   rtn                   AS Rtn,
                   cod_tipoproveedor     AS CodTipoProveedor,
                   tipo_nombre           AS TipoNombre,
                   cuenta_contable       AS CuentaContable,
                   por_vencer            AS PorVencer,
                   tramo_1_30            AS Tramo30,
                   tramo_31_60           AS Tramo60,
                   tramo_61_90           AS Tramo90,
                   tramo_91_120          AS Tramo120,
                   tramo_mas_120         AS TramoMas120,
                   vencido               AS Vencido,
                   saldo_total           AS SaldoTotal,
                   documentos_pendientes AS DocumentosPendientes
            FROM public.fn_prv_antiguedad_saldos(@CompanyId, @Corte, @IncluirPorVencer, @Origen, @CodTipoProveedor)";

        var filas = await connection.QueryAsync<AntiguedadSaldosProveedorFilaDto>(
            new CommandDefinition(sql,
                new
                {
                    CompanyId = companyId,
                    Corte = corte,
                    IncluirPorVencer = incluirPorVencer,
                    Origen = origenNorm,
                    CodTipoProveedor = codTipoProveedor
                },
                cancellationToken: cancellationToken));

        // Filtro opcional por un proveedor puntual: se descarta en memoria (el universo del aging es
        // chico) en vez de re-consultar; así el reporte de un solo proveedor usa la misma ruta.
        var cod = codProveedor?.Trim();
        var lista = new List<AntiguedadSaldosProveedorFilaDto>();
        foreach (var f in filas)
        {
            if (string.IsNullOrEmpty(cod)
                || string.Equals(f.CodProveedor?.Trim(), cod, StringComparison.OrdinalIgnoreCase))
            {
                lista.Add(f);
            }
        }

        // Totales del pie: se acumulan en una sola pasada. Sin LINQ, por convención del proyecto.
        var totales = new AntiguedadSaldosTotalesDto { Proveedores = lista.Count };
        foreach (var f in lista)
        {
            totales.PorVencer += f.PorVencer;
            totales.Tramo30 += f.Tramo30;
            totales.Tramo60 += f.Tramo60;
            totales.Tramo90 += f.Tramo90;
            totales.Tramo120 += f.Tramo120;
            totales.TramoMas120 += f.TramoMas120;
            totales.Vencido += f.Vencido;
            totales.SaldoTotal += f.SaldoTotal;
            totales.DocumentosPendientes += f.DocumentosPendientes;
        }

        return new AntiguedadSaldosProveedorDto
        {
            Corte = corte ?? DateOnly.FromDateTime(DateTime.Today),
            IncluyePorVencer = incluirPorVencer,
            Filas = lista,
            Totales = totales
        };
    }

    public async Task<AntiguedadSaldosImpresionDto> GetDatosImpresionAsync(
        DateOnly? corte = null,
        bool incluirPorVencer = true,
        int origen = 0,
        int? codTipoProveedor = null,
        string? codProveedor = null,
        string? impresoPor = null,
        CancellationToken cancellationToken = default)
    {
        var dto = await GetAsync(corte, incluirPorVencer, origen, codTipoProveedor, codProveedor, cancellationToken);

        // Identidad de la empresa para el encabezado del reporte (mismo origen que el estado de cuenta).
        var companyId = EnsureCompanyId();
        var empresa = await _context.cfg_companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.company_id == companyId, cancellationToken);

        var items = new List<AntiguedadSaldosProveedorFilaDto>(dto.Filas);

        return new AntiguedadSaldosImpresionDto
        {
            EmpresaNombre = empresa?.commercial_name ?? string.Empty,
            EmpresaRazonSocial = empresa?.legal_name,
            EmpresaRtn = empresa?.tax_id,
            EmpresaDireccion = empresa?.address,
            EmpresaTelefono = empresa?.phone,
            EmpresaEmail = empresa?.email,
            EmpresaLogo = empresa?.logo,
            ImpresoPor = string.IsNullOrWhiteSpace(impresoPor) ? "sistema" : impresoPor.Trim(),
            Corte = dto.Corte,
            Items = items,
            Totales = dto.Totales,
            FiltroTexto = BuildFiltroTexto(dto.IncluyePorVencer, origen)
        };
    }

    private static string BuildFiltroTexto(bool incluyePorVencer, int origen)
    {
        var alcance = incluyePorVencer ? "Incluye por vencer y vencido" : "Solo saldos vencidos";
        var fuente = origen switch
        {
            1 => "solo facturas de compra",
            2 => "solo compromisos",
            _ => "compras y compromisos"
        };

        return $"{alcance} · {fuente}";
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
}
