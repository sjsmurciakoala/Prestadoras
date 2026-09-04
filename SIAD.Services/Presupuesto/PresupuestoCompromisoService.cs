using System.Data.Common;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using SIAD.Core.DTOs.Presupuesto;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Infrastructure;

namespace SIAD.Services.Presupuesto;

/// <summary>
/// Implementación de <see cref="IPresupuestoCompromisoService"/>. Ver la interfaz para el contrato.
/// <para>
/// <b>Por qué toma la conexión del <see cref="SiadDbContext"/> y no abre la suya:</b> el compromiso
/// tiene que ser atómico con la aprobación de la orden. Si abriera su propia conexión, la validación
/// vería un estado distinto y un fallo posterior dejaría presupuesto comprometido contra una orden
/// que se quedó en Borrador. Es el mismo patrón de <c>CompraContabilidad</c>.
/// </para>
/// <para>
/// <b>Sin LINQ y sin lógica de negocio</b> (regla <c>hodsoft-sin-linq</c>): el disponible, los locks,
/// la idempotencia y el kardex viven en los <c>sp_pst_*</c>. Aquí solo se arman parámetros y se
/// traduce el error.
/// </para>
/// </summary>
public sealed class PresupuestoCompromisoService : IPresupuestoCompromisoService
{
    private const string ModuloCompras = "COMPRAS";
    private const string DocumentoOrdenCompra = "ORDEN_COMPRA";
    private const string DocumentoFacturaCompra = "FACTURA_COMPRA";

    /// <summary>Código de error de PostgreSQL con el que los sp_pst_* lanzan sus errores de negocio.</summary>
    private const string ErrorDeNegocio = "P0001";

    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _company;

    public PresupuestoCompromisoService(SiadDbContext context, ICurrentCompanyService company)
    {
        _context = context;
        _company = company;

        // Dapper no mapea DateOnly ni al leer ni al escribir sin este handler. Idempotente.
        DapperTypeHandlers.EnsureRegistered();
    }

    // Los casts ::bigint/::varchar/::date NO son adorno: sin ellos Npgsql infiere text y timestamp,
    // y Postgres no resuelve la firma del procedimiento (p_fecha es DATE) -> 42883.
    //
    // El array de renglones NO viaja por el cable: se arma en la propia consulta a partir de
    // fn_alm_oc_distribucion_partidas. Así la distribución se calcula una sola vez, en la base, con
    // la misma regla que el asiento contable — y no hay que mapear el tipo compuesto en Npgsql.
    private const string SqlComprometer = @"
SELECT con_cuenta_code AS CuentaCode,
       disponible      AS Disponible,
       requerido       AS Requerido,
       exceso          AS Exceso,
       excedio         AS Excedio
  FROM public.sp_pst_comprometer_documento(
       @companyId::bigint, @modulo::varchar, @documentoTipo::varchar, @documentoId::bigint,
       @numero::varchar, @fecha::date, @usuario::varchar, @usuarioAprobo::varchar, @ip::varchar,
       ARRAY(SELECT ROW(l.*)::public.pst_linea_afectacion
               FROM public.fn_alm_oc_distribucion_partidas(@companyId::bigint, @documentoId::bigint) l));";

    private const string SqlLiberar = @"
SELECT public.sp_pst_liberar_compromiso(
       @companyId::bigint, @modulo::varchar, @documentoTipo::varchar, @documentoId::bigint,
       @motivo::varchar, @usuario::varchar, @ip::varchar);";

    private const string SqlAjustar = @"
SELECT con_cuenta_code AS CuentaCode,
       disponible      AS Disponible,
       requerido       AS Requerido,
       exceso          AS Exceso,
       excedio         AS Excedio
  FROM public.sp_pst_ajustar_compromiso(
       @companyId::bigint, @modulo::varchar, @documentoTipo::varchar, @documentoId::bigint,
       @numero::varchar, @motivo::varchar, @usuario::varchar, @ip::varchar,
       ARRAY(SELECT ROW(l.*)::public.pst_linea_afectacion
               FROM public.fn_alm_oc_distribucion_partidas(@companyId::bigint, @documentoId::bigint) l));";

    // La distribución de la FACTURA sale de fn_alm_compra_distribucion_partidas, que replica el DEBE
    // del asiento contable. Así el presupuesto muerde exactamente lo mismo que la contabilidad.
    private const string SqlDevengar = @"
SELECT con_cuenta_code AS CuentaCode,
       disponible      AS Disponible,
       requerido       AS Requerido,
       exceso          AS Exceso,
       excedio         AS Excedio
  FROM public.sp_pst_devengar_documento(
       @companyId::bigint, @documentoTipo::varchar, @documentoId::bigint, @numero::varchar,
       @ordenCompraId::bigint, @fecha::date, @usuario::varchar, @ip::varchar,
       ARRAY(SELECT ROW(l.*)::public.pst_linea_afectacion
               FROM public.fn_alm_compra_distribucion_partidas(@companyId::bigint, @documentoId::bigint) l));";

    private const string SqlRevertirDevengo = @"
SELECT public.sp_pst_revertir_devengo(
       @companyId::bigint, @documentoTipo::varchar, @documentoId::bigint,
       @motivo::varchar, @usuario::varchar, @ip::varchar);";

    private const string SqlRegistrarPago = @"
SELECT public.sp_pst_registrar_pago(
       @companyId::bigint, @documentoId::bigint, @numero::varchar, @compraHdrId::bigint,
       @fecha::date, @monto::numeric, @usuario::varchar, @ip::varchar);";

    private const string SqlRevertirPago = @"
SELECT public.sp_pst_revertir_pago(
       @companyId::bigint, @documentoId::bigint, @motivo::varchar, @usuario::varchar, @ip::varchar);";

    public async Task<IReadOnlyList<PresupuestoAvisoDto>> ComprometerOrdenCompraAsync(
        int ordenCompraId, string numero, DateOnly fecha, string usuario, string? usuarioAprobo,
        CancellationToken ct = default)
    {
        if (ordenCompraId <= 0) throw new ArgumentOutOfRangeException(nameof(ordenCompraId));

        var (conn, tx) = Conexion();

        var parametros = new
        {
            companyId = EmpresaActual(),
            modulo = ModuloCompras,
            documentoTipo = DocumentoOrdenCompra,
            documentoId = (long)ordenCompraId,
            numero,
            // Dapper/Npgsql no mapea DateOnly: se convierte a DateTime (lección del estado de
            // cuenta de proveedor). La hora es irrelevante — el parámetro del SP es DATE.
            fecha = fecha.ToDateTime(TimeOnly.MinValue),
            usuario,
            usuarioAprobo,
            ip = (string?)null   // ver decisión D7: exige pasar el HttpContext hasta acá
        };

        try
        {
            var avisos = await conn.QueryAsync<PresupuestoAvisoDto>(
                new CommandDefinition(SqlComprometer, parametros, tx, cancellationToken: ct));
            return avisos.AsList();
        }
        catch (PostgresException ex) when (ex.SqlState == ErrorDeNegocio)
        {
            throw new InvalidOperationException(ex.MessageText, ex);
        }
    }

    public async Task<decimal> LiberarOrdenCompraAsync(
        int ordenCompraId, string motivo, string usuario, CancellationToken ct = default)
    {
        if (ordenCompraId <= 0) throw new ArgumentOutOfRangeException(nameof(ordenCompraId));

        var (conn, tx) = Conexion();

        var parametros = new
        {
            companyId = EmpresaActual(),
            modulo = ModuloCompras,
            documentoTipo = DocumentoOrdenCompra,
            documentoId = (long)ordenCompraId,
            motivo,
            usuario,
            ip = (string?)null
        };

        try
        {
            return await conn.ExecuteScalarAsync<decimal?>(
                new CommandDefinition(SqlLiberar, parametros, tx, cancellationToken: ct)) ?? 0m;
        }
        catch (PostgresException ex) when (ex.SqlState == ErrorDeNegocio)
        {
            throw new InvalidOperationException(ex.MessageText, ex);
        }
    }

    public Task<IReadOnlyList<PresupuestoAvisoDto>> AjustarCompromisoOrdenCompraAsync(
        int ordenCompraId, string numero, string motivo, string usuario, CancellationToken ct = default)
    {
        if (ordenCompraId <= 0) throw new ArgumentOutOfRangeException(nameof(ordenCompraId));

        var (conn, tx) = Conexion();
        return AvisosAsync(conn, tx, SqlAjustar, new
        {
            companyId = EmpresaActual(),
            modulo = ModuloCompras,
            documentoTipo = DocumentoOrdenCompra,
            documentoId = (long)ordenCompraId,
            numero,
            motivo,
            usuario,
            ip = (string?)null
        }, ct);
    }

    public Task<IReadOnlyList<PresupuestoAvisoDto>> DevengarFacturaAsync(
        int compraHdrId, string numero, int? ordenCompraId, DateOnly fecha, string usuario,
        CancellationToken ct = default)
    {
        if (compraHdrId <= 0) throw new ArgumentOutOfRangeException(nameof(compraHdrId));

        var (conn, tx) = Conexion();
        return AvisosAsync(conn, tx, SqlDevengar, new
        {
            companyId = EmpresaActual(),
            documentoTipo = DocumentoFacturaCompra,
            documentoId = (long)compraHdrId,
            numero,
            // NULL = compra directa. El SP decide qué hacer según permite_devengo_sin_oc.
            ordenCompraId = ordenCompraId.HasValue ? (long?)ordenCompraId.Value : null,
            fecha = fecha.ToDateTime(TimeOnly.MinValue),
            usuario,
            ip = (string?)null
        }, ct);
    }

    public Task<decimal> RevertirDevengoFacturaAsync(
        int compraHdrId, string motivo, string usuario, CancellationToken ct = default)
    {
        if (compraHdrId <= 0) throw new ArgumentOutOfRangeException(nameof(compraHdrId));

        var (conn, tx) = Conexion();
        return EscalarAsync(conn, tx, SqlRevertirDevengo, new
        {
            companyId = EmpresaActual(),
            documentoTipo = DocumentoFacturaCompra,
            documentoId = (long)compraHdrId,
            motivo,
            usuario,
            ip = (string?)null
        }, ct);
    }

    public Task<decimal> RegistrarPagoAsync(
        long abonoId, string numero, int compraHdrId, DateOnly fecha, decimal monto, string usuario,
        CancellationToken ct = default)
    {
        if (abonoId <= 0) throw new ArgumentOutOfRangeException(nameof(abonoId));
        if (monto <= 0m) return Task.FromResult(0m);

        var (conn, tx) = Conexion();
        return EscalarAsync(conn, tx, SqlRegistrarPago, new
        {
            companyId = EmpresaActual(),
            documentoId = abonoId,
            numero,
            compraHdrId = (long)compraHdrId,
            fecha = fecha.ToDateTime(TimeOnly.MinValue),
            monto,
            usuario,
            ip = (string?)null
        }, ct);
    }

    public Task<decimal> RevertirPagoAsync(
        long abonoId, string motivo, string usuario, CancellationToken ct = default)
    {
        if (abonoId <= 0) throw new ArgumentOutOfRangeException(nameof(abonoId));

        var (conn, tx) = Conexion();
        return EscalarAsync(conn, tx, SqlRevertirPago, new
        {
            companyId = EmpresaActual(),
            documentoId = abonoId,
            motivo,
            usuario,
            ip = (string?)null
        }, ct);
    }

    // Aquí el array SÍ viaja por el cable: las líneas no salen de una función de la base, las trae
    // el llamador. Se arma con unnest sobre dos arreglos paralelos para no tener que registrar el
    // tipo compuesto en Npgsql.
    private const string SqlAfectarEjecutado = @"
SELECT * FROM public.sp_pst_afectar_valor_real(
       @companyId::bigint, @modulo::varchar, @documentoTipo::varchar, @documentoId::bigint,
       @documentoNumero::varchar, @fecha::date, @usuario::varchar, @ip::varchar,
       @direccion::smallint, @exigeAprobado::boolean,
       ARRAY(SELECT ROW(c, NULL::bigint, NULL::bigint, m)::public.pst_linea_afectacion
               FROM unnest(@cuentas::varchar[], @montos::numeric[]) AS t(c, m)));";

    public async Task AfectarEjecutadoAsync(
        string modulo, string documentoTipo, long documentoId, string? documentoNumero,
        DateOnly fecha, string usuario, short direccion, bool exigeAprobado,
        IReadOnlyCollection<(string Cuenta, decimal Monto)> lineas,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(lineas);
        if (lineas.Count == 0)
        {
            return;
        }

        var (conn, tx) = Conexion();
        var cuentas = new string[lineas.Count];
        var montos = new decimal[lineas.Count];
        var i = 0;
        foreach (var (cuenta, monto) in lineas)
        {
            cuentas[i] = cuenta;
            montos[i] = monto;
            i++;
        }

        try
        {
            await conn.ExecuteAsync(new CommandDefinition(SqlAfectarEjecutado, new
            {
                companyId = EmpresaActual(),
                modulo,
                documentoTipo,
                documentoId,
                documentoNumero,
                fecha = fecha.ToDateTime(TimeOnly.MinValue),
                usuario,
                ip = (string?)null,
                direccion = (short)(direccion >= 0 ? 1 : -1),
                exigeAprobado,
                cuentas,
                montos
            }, tx, cancellationToken: ct));
        }
        catch (PostgresException ex) when (ex.SqlState == ErrorDeNegocio)
        {
            throw new InvalidOperationException(ex.MessageText, ex);
        }
    }

    // Lectura pura para el panel: agrupa la distribución de la orden por cuenta y le pega el
    // disponible actual. fn_pst_disponible NO toma lock — es informativa a propósito.
    private const string SqlPrevio = @"
SELECT upper(btrim(d.con_cuenta_code))              AS CuentaCode,
       max(pc.name)                                 AS CuentaNombre,
       sum(d.monto)                                 AS Requerido,
       max(public.fn_pst_disponible(@companyId::bigint, d.con_cuenta_code, @fecha::date)) AS Disponible,
       COALESCE(bool_or(pc.allows_budget), FALSE)   AS Presupuestable
  FROM public.fn_alm_oc_distribucion_partidas(@companyId::bigint, @documentoId::bigint) d
  LEFT JOIN public.con_plan_cuentas pc
         ON pc.company_id = @companyId::bigint
        AND upper(btrim(pc.code)) = upper(btrim(d.con_cuenta_code))
 GROUP BY upper(btrim(d.con_cuenta_code))
 ORDER BY 1 NULLS FIRST;";

    private const string SqlModo = @"
SELECT COALESCE(modo, 0) FROM public.cfg_presupuesto_control
 WHERE company_id = @companyId::bigint AND modulo = 'COMPRAS_OC';";

    public async Task<PresupuestoPrevioDto> ConsultarPrevioOrdenCompraAsync(
        int ordenCompraId, DateOnly fecha, CancellationToken ct = default)
    {
        if (ordenCompraId <= 0) throw new ArgumentOutOfRangeException(nameof(ordenCompraId));

        var (conn, tx) = Conexion();
        var companyId = EmpresaActual();

        var modo = await conn.ExecuteScalarAsync<short?>(
            new CommandDefinition(SqlModo, new { companyId }, tx, cancellationToken: ct)) ?? (short)0;

        var previo = new PresupuestoPrevioDto { Modo = modo };
        if (modo == 0)
        {
            return previo;   // apagado: la pantalla no muestra el panel
        }

        var filas = await conn.QueryAsync<PresupuestoPrevioPartidaDto>(
            new CommandDefinition(SqlPrevio, new
            {
                companyId,
                documentoId = (long)ordenCompraId,
                fecha = fecha.ToDateTime(TimeOnly.MinValue)
            }, tx, cancellationToken: ct));

        previo.Partidas = filas.AsList();
        return previo;
    }

    /// <summary>Ejecuta un sp_pst_* que devuelve avisos, y traduce el error de negocio.</summary>
    private static async Task<IReadOnlyList<PresupuestoAvisoDto>> AvisosAsync(
        DbConnection conn, DbTransaction? tx, string sql, object parametros, CancellationToken ct)
    {
        try
        {
            var avisos = await conn.QueryAsync<PresupuestoAvisoDto>(
                new CommandDefinition(sql, parametros, tx, cancellationToken: ct));
            return avisos.AsList();
        }
        catch (PostgresException ex) when (ex.SqlState == ErrorDeNegocio)
        {
            throw new InvalidOperationException(ex.MessageText, ex);
        }
    }

    /// <summary>Ejecuta un sp_pst_* que devuelve un importe, y traduce el error de negocio.</summary>
    private static async Task<decimal> EscalarAsync(
        DbConnection conn, DbTransaction? tx, string sql, object parametros, CancellationToken ct)
    {
        try
        {
            return await conn.ExecuteScalarAsync<decimal?>(
                new CommandDefinition(sql, parametros, tx, cancellationToken: ct)) ?? 0m;
        }
        catch (PostgresException ex) when (ex.SqlState == ErrorDeNegocio)
        {
            throw new InvalidOperationException(ex.MessageText, ex);
        }
    }

    private long EmpresaActual()
    {
        var companyId = _company.GetCompanyId();
        if (companyId <= 0)
        {
            throw new InvalidOperationException("No se pudo resolver la empresa actual.");
        }
        return companyId;
    }

    /// <summary>
    /// Conexión y transacción <b>del contexto</b>, para correr dentro de la transacción del
    /// documento. La transacción puede ser null (el llamador no abrió ninguna): el SP sigue siendo
    /// atómico por sí solo, pero entonces el compromiso no se revierte con el documento — por eso
    /// los enganches del módulo abren transacción antes de llamar.
    /// </summary>
    private (DbConnection Conn, DbTransaction? Tx) Conexion()
        => (_context.Database.GetDbConnection(), _context.Database.CurrentTransaction?.GetDbTransaction());
}
