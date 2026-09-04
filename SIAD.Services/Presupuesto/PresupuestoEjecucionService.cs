using System.Data.Common;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.DTOs.Presupuesto;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Infrastructure;

namespace SIAD.Services.Presupuesto;

/// <summary>
/// Implementación de <see cref="IPresupuestoEjecucionService"/>. Lectura por Dapper sobre las vistas
/// (regla <c>hodsoft-sin-linq</c>): la lógica de agregación vive en las vistas
/// <c>vw_pst_ejecucion_presupuestaria</c>, <c>vw_pst_compromiso_saldo</c> y
/// <c>vw_pst_movimiento_detalle</c>, no aquí.
/// </summary>
public sealed class PresupuestoEjecucionService : IPresupuestoEjecucionService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _company;

    public PresupuestoEjecucionService(SiadDbContext context, ICurrentCompanyService company)
    {
        _context = context;
        _company = company;

        // Dapper no mapea DateOnly ni al leer ni al escribir sin este handler. Idempotente.
        DapperTypeHandlers.EnsureRegistered();
    }

    private const string SqlEjecucion = @"
SELECT id_presupuesto        AS IdPresupuesto,
       fecha_inicia          AS FechaInicia,
       fecha_finaliza        AS FechaFinaliza,
       estado_aprobado       AS EstadoAprobado,
       con_cuenta_code       AS ConCuentaCode,
       cuenta_nombre         AS CuentaNombre,
       cuenta_tipo           AS CuentaTipo,
       cuenta_presupuestable AS CuentaPresupuestable,
       presupuesto           AS Presupuesto,
       comprometido          AS Comprometido,
       ejecutado             AS Ejecutado,
       pagado                AS Pagado,
       disponible            AS Disponible,
       pct_ejecucion         AS PctEjecucion,
       pct_compromiso        AS PctCompromiso,
       pct_utilizado         AS PctUtilizado
  FROM public.vw_pst_ejecucion_presupuestaria
 WHERE company_id = @companyId
   AND (@idPresupuesto::varchar IS NULL OR id_presupuesto = @idPresupuesto)
   AND (NOT @soloPresupuestables OR cuenta_presupuestable)
   AND (NOT @soloConMovimiento
        OR comprometido <> 0 OR ejecutado <> 0 OR pagado <> 0)
   AND (@search::varchar IS NULL
        OR con_cuenta_code ILIKE @search
        OR COALESCE(cuenta_nombre, '') ILIKE @search)
 ORDER BY id_presupuesto DESC, con_cuenta_code;";

    public async Task<IReadOnlyList<PresupuestoEjecucionItemDto>> ListarEjecucionAsync(
        PresupuestoEjecucionFilterDto? filtro, CancellationToken ct = default)
    {
        var f = filtro ?? new PresupuestoEjecucionFilterDto();
        var (conn, tx) = Conexion();

        var filas = await conn.QueryAsync<PresupuestoEjecucionItemDto>(
            new CommandDefinition(SqlEjecucion, new
            {
                companyId = EmpresaActual(),
                idPresupuesto = Vacio(f.IdPresupuesto),
                soloPresupuestables = f.SoloPresupuestables,
                soloConMovimiento = f.SoloConMovimiento,
                search = Like(f.Search)
            }, tx, cancellationToken: ct));

        return filas.AsList();
    }

    private const string SqlCompromisos = @"
SELECT id                    AS Id,
       id_presupuesto        AS IdPresupuesto,
       con_cuenta_code       AS ConCuentaCode,
       cuenta_nombre         AS CuentaNombre,
       centro_costo_codigo   AS CentroCostoCodigo,
       centro_costo_nombre   AS CentroCostoNombre,
       documento_tipo        AS DocumentoTipo,
       documento_id          AS DocumentoId,
       documento_numero      AS DocumentoNumero,
       fecha                 AS Fecha,
       cod_proveedor         AS CodProveedor,
       proveedor             AS Proveedor,
       orden_estado          AS OrdenEstado,
       monto_comprometido    AS MontoComprometido,
       monto_devengado       AS MontoDevengado,
       monto_liberado        AS MontoLiberado,
       saldo_comprometido    AS SaldoComprometido,
       dias_antiguedad       AS DiasAntiguedad
  FROM public.vw_pst_compromiso_saldo
 WHERE company_id = @companyId
   AND estado = 1
   AND saldo_comprometido > 0
   AND (@idPresupuesto::varchar IS NULL OR id_presupuesto = @idPresupuesto)
   AND (@cuenta::varchar        IS NULL OR con_cuenta_code = @cuenta)
   AND (@codProveedor::varchar  IS NULL OR cod_proveedor = @codProveedor)
   AND (@diasMinimos::int       IS NULL OR dias_antiguedad >= @diasMinimos)
   AND (@search::varchar IS NULL
        OR COALESCE(documento_numero, '') ILIKE @search
        OR COALESCE(proveedor, '')        ILIKE @search
        OR con_cuenta_code                ILIKE @search)
 ORDER BY dias_antiguedad DESC, saldo_comprometido DESC;";

    public async Task<IReadOnlyList<PresupuestoCompromisoPendienteDto>> ListarCompromisosPendientesAsync(
        PresupuestoCompromisoFilterDto? filtro, CancellationToken ct = default)
    {
        var f = filtro ?? new PresupuestoCompromisoFilterDto();
        var (conn, tx) = Conexion();

        var filas = await conn.QueryAsync<PresupuestoCompromisoPendienteDto>(
            new CommandDefinition(SqlCompromisos, new
            {
                companyId = EmpresaActual(),
                idPresupuesto = Vacio(f.IdPresupuesto),
                cuenta = Vacio(f.ConCuentaCode),
                codProveedor = Vacio(f.CodProveedor),
                diasMinimos = f.DiasMinimos,
                search = Like(f.Search)
            }, tx, cancellationToken: ct));

        return filas.AsList();
    }

    private const string SqlMovimientos = @"
SELECT id                      AS Id,
       id_presupuesto          AS IdPresupuesto,
       con_cuenta_code         AS ConCuentaCode,
       cuenta_nombre           AS CuentaNombre,
       centro_costo_codigo     AS CentroCostoCodigo,
       tipo_movimiento         AS TipoMovimiento,
       tipo_movimiento_nombre  AS TipoMovimientoNombre,
       efecto_comprometido     AS EfectoComprometido,
       efecto_ejecutado        AS EfectoEjecutado,
       efecto_pagado           AS EfectoPagado,
       modulo                  AS Modulo,
       documento_tipo          AS DocumentoTipo,
       documento_id            AS DocumentoId,
       documento_numero        AS DocumentoNumero,
       orden_compra_id         AS OrdenCompraId,
       orden_compra_numero     AS OrdenCompraNumero,
       proveedor               AS Proveedor,
       fecha                   AS Fecha,
       monto                   AS Monto,
       comprometido_anterior   AS ComprometidoAnterior,
       comprometido_posterior  AS ComprometidoPosterior,
       ejecutado_anterior      AS EjecutadoAnterior,
       ejecutado_posterior     AS EjecutadoPosterior,
       disponible_anterior     AS DisponibleAnterior,
       disponible_posterior    AS DisponiblePosterior,
       excedio                 AS Excedio,
       estado                  AS Estado,
       observacion             AS Observacion,
       usuario                 AS Usuario,
       usuario_aprobo          AS UsuarioAprobo,
       fecha_registro          AS FechaRegistro
  FROM public.vw_pst_movimiento_detalle
 WHERE company_id = @companyId
   AND id_presupuesto = @idPresupuesto
   AND upper(btrim(con_cuenta_code)) = upper(btrim(@cuenta))
 ORDER BY id;";

    public async Task<IReadOnlyList<PresupuestoMovimientoDto>> ListarMovimientosAsync(
        string idPresupuesto, string conCuentaCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idPresupuesto) || string.IsNullOrWhiteSpace(conCuentaCode))
        {
            return Array.Empty<PresupuestoMovimientoDto>();
        }

        var (conn, tx) = Conexion();
        var filas = await conn.QueryAsync<PresupuestoMovimientoDto>(
            new CommandDefinition(SqlMovimientos, new
            {
                companyId = EmpresaActual(),
                idPresupuesto = idPresupuesto.Trim(),
                cuenta = conCuentaCode.Trim()
            }, tx, cancellationToken: ct));

        return filas.AsList();
    }

    // ── Datos de impresión ───────────────────────────────────────────────────
    // Los totales se calculan aquí y NO por expresión en el reporte: el disponible es derivado
    // (proyección − comprometido − ejecutado), y sumarlo con sumSum daría una cifra distinta a la
    // que muestra la pantalla.

    public async Task<PresupuestoEjecucionImpresionDto> GetDatosImpresionEjecucionAsync(
        PresupuestoEjecucionFilterDto? filtro, string? impresoPor, CancellationToken ct = default)
    {
        var items = await ListarEjecucionAsync(filtro, ct);
        var dto = await NuevaImpresionAsync(new PresupuestoEjecucionImpresionDto(), impresoPor, ct);

        dto.Items = items.AsList();
        foreach (var i in items)
        {
            dto.TotalPresupuesto += i.Presupuesto;
            dto.TotalComprometido += i.Comprometido;
            dto.TotalEjecutado += i.Ejecutado;
            dto.TotalPagado += i.Pagado;
            dto.TotalDisponible += i.Disponible;
        }
        dto.FiltroTexto = DescribirFiltro(filtro);
        return dto;
    }

    public async Task<PresupuestoCompromisosImpresionDto> GetDatosImpresionCompromisosAsync(
        PresupuestoCompromisoFilterDto? filtro, string? impresoPor, CancellationToken ct = default)
    {
        var items = await ListarCompromisosPendientesAsync(filtro, ct);
        var dto = await NuevaImpresionAsync(new PresupuestoCompromisosImpresionDto(), impresoPor, ct);

        dto.Items = items.AsList();
        foreach (var i in items)
        {
            dto.TotalComprometido += i.MontoComprometido;
            dto.TotalDevengado += i.MontoDevengado;
            dto.TotalSaldo += i.SaldoComprometido;
        }
        dto.FiltroTexto = DescribirFiltro(filtro);
        return dto;
    }

    /// <summary>Rellena el encabezado de empresa y el pie, igual que el resto de los comprobantes.</summary>
    private async Task<T> NuevaImpresionAsync<T>(T dto, string? impresoPor, CancellationToken ct)
        where T : ComprobanteAlmacenImpresionBase
    {
        var companyId = EmpresaActual();
        var empresa = await _context.cfg_companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.company_id == companyId, ct);

        dto.EmpresaNombre = empresa?.commercial_name ?? string.Empty;
        dto.EmpresaRazonSocial = empresa?.legal_name;
        dto.EmpresaRtn = empresa?.tax_id;
        dto.EmpresaDireccion = empresa?.address;
        dto.EmpresaTelefono = empresa?.phone;
        dto.EmpresaEmail = empresa?.email;
        dto.EmpresaLogo = empresa?.logo;
        dto.ImpresoPor = string.IsNullOrWhiteSpace(impresoPor) ? "sistema" : impresoPor.Trim();

        if (dto is PresupuestoEjecucionImpresionDto e) e.Corte = DateOnly.FromDateTime(DateTime.Today);
        if (dto is PresupuestoCompromisosImpresionDto c) c.Corte = DateOnly.FromDateTime(DateTime.Today);

        return dto;
    }

    /// <summary>Descripción legible de los filtros, para que el PDF diga qué se imprimió.</summary>
    private static string DescribirFiltro(PresupuestoEjecucionFilterDto? f)
    {
        if (f is null) return string.Empty;
        var partes = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.IdPresupuesto)) partes.Add($"Presupuesto {f.IdPresupuesto.Trim()}");
        if (!string.IsNullOrWhiteSpace(f.Search)) partes.Add($"Búsqueda: {f.Search.Trim()}");
        if (f.SoloPresupuestables) partes.Add("Solo cuentas controladas");
        if (f.SoloConMovimiento) partes.Add("Solo con movimiento");
        return string.Join(" · ", partes);
    }

    private static string DescribirFiltro(PresupuestoCompromisoFilterDto? f)
    {
        if (f is null) return string.Empty;
        var partes = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.IdPresupuesto)) partes.Add($"Presupuesto {f.IdPresupuesto.Trim()}");
        if (!string.IsNullOrWhiteSpace(f.ConCuentaCode)) partes.Add($"Partida {f.ConCuentaCode.Trim()}");
        if (!string.IsNullOrWhiteSpace(f.CodProveedor)) partes.Add($"Proveedor {f.CodProveedor.Trim()}");
        if (!string.IsNullOrWhiteSpace(f.Search)) partes.Add($"Búsqueda: {f.Search.Trim()}");
        if (f.DiasMinimos.HasValue) partes.Add($"Con {f.DiasMinimos.Value} días o más");
        return string.Join(" · ", partes);
    }

    private const string SqlConfig = @"
SELECT modulo                     AS Modulo,
       modo                       AS Modo,
       exige_presupuesto_aprobado AS ExigePresupuestoAprobado,
       tolerancia_pct             AS ToleranciaPct,
       permite_devengo_sin_oc     AS PermiteDevengoSinOc
  FROM public.cfg_presupuesto_control
 WHERE company_id = @companyId
 ORDER BY modulo;";

    public async Task<IReadOnlyList<PresupuestoControlConfigDto>> ListarConfiguracionAsync(
        CancellationToken ct = default)
    {
        var (conn, tx) = Conexion();
        var filas = (await conn.QueryAsync<PresupuestoControlConfigDto>(
            new CommandDefinition(SqlConfig, new { companyId = EmpresaActual() }, tx, cancellationToken: ct)))
            .AsList();

        // La semilla crea los cuatro módulos, pero una empresa dada de alta después no los tendría.
        // Se completan en memoria (apagados) para que la pantalla nunca aparezca vacía.
        foreach (var modulo in new[]
                 {
                     PresupuestoControlModulos.ComprasOc,
                     PresupuestoControlModulos.ComprasFactura,
                     PresupuestoControlModulos.Proveedores,
                     PresupuestoControlModulos.Bancos
                 })
        {
            if (!filas.Exists(f => f.Modulo == modulo))
            {
                filas.Add(new PresupuestoControlConfigDto { Modulo = modulo, Modo = 0 });
            }
        }

        filas.Sort((a, b) => string.CompareOrdinal(a.Modulo, b.Modulo));
        return filas;
    }

    private const string SqlGuardarConfig = @"
INSERT INTO public.cfg_presupuesto_control
       (company_id, modulo, modo, exige_presupuesto_aprobado, tolerancia_pct,
        permite_devengo_sin_oc, usuariomodificacion, fechamodificacion)
VALUES (@companyId, @modulo, @modo, @exigeAprobado, @tolerancia,
        @permiteSinOc, @usuario, (now() AT TIME ZONE 'utc'))
ON CONFLICT (company_id, modulo) DO UPDATE
   SET modo                       = EXCLUDED.modo,
       exige_presupuesto_aprobado = EXCLUDED.exige_presupuesto_aprobado,
       tolerancia_pct             = EXCLUDED.tolerancia_pct,
       permite_devengo_sin_oc     = EXCLUDED.permite_devengo_sin_oc,
       usuariomodificacion        = EXCLUDED.usuariomodificacion,
       fechamodificacion          = EXCLUDED.fechamodificacion;";

    public async Task GuardarConfiguracionAsync(
        PresupuestoControlConfigDto dto, string usuario, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (!PresupuestoControlModulos.EstaConectado(dto.Modulo)
            && dto.Modulo is not (PresupuestoControlModulos.Proveedores or PresupuestoControlModulos.Bancos))
        {
            throw new InvalidOperationException("El módulo indicado no existe.");
        }
        if (dto.Modo is < 0 or > 2)
        {
            throw new InvalidOperationException("El modo debe ser Apagado, Advertencia o Bloqueo.");
        }
        if (dto.ToleranciaPct is < 0m or > 100m)
        {
            throw new InvalidOperationException("La tolerancia debe estar entre 0 y 100 por ciento.");
        }
        if (dto.PermiteDevengoSinOc is < 0 or > 2)
        {
            throw new InvalidOperationException("La opción de compra sin orden no es válida.");
        }

        var (conn, tx) = Conexion();
        await conn.ExecuteAsync(new CommandDefinition(SqlGuardarConfig, new
        {
            companyId = EmpresaActual(),
            modulo = dto.Modulo,
            modo = (int)dto.Modo,
            exigeAprobado = dto.ExigePresupuestoAprobado,
            tolerancia = dto.ToleranciaPct,
            permiteSinOc = (int)dto.PermiteDevengoSinOc,
            usuario
        }, tx, cancellationToken: ct));
    }

    private static string? Vacio(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static string? Like(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : $"%{valor.Trim()}%";

    private long EmpresaActual()
    {
        var companyId = _company.GetCompanyId();
        if (companyId <= 0)
        {
            throw new InvalidOperationException("No se pudo resolver la empresa actual.");
        }
        return companyId;
    }

    private (DbConnection Conn, DbTransaction? Tx) Conexion()
        => (_context.Database.GetDbConnection(), _context.Database.CurrentTransaction?.GetDbTransaction());
}
