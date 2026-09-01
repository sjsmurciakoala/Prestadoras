using System.Data.Common;
using Dapper;
using Npgsql;

namespace SIAD.Services.Presupuesto;

/// <summary>
/// Registro presupuestario del PAGO, sobre una conexión y transacción que da el llamador.
/// <para>
/// Existe aparte de <see cref="IPresupuestoCompromisoService"/> porque <c>CompraCxpService</c> no
/// usa la transacción del <c>SiadDbContext</c>: abre y maneja la suya con <see cref="NpgsqlConnection"/>.
/// Inyectarle el servicio dejaría el movimiento presupuestario FUERA de la transacción del abono, y
/// un fallo posterior dejaría un pago registrado en el kardex sin abono que lo respalde.
/// Es el mismo patrón que ese archivo ya usa para el asiento contable del pago
/// (<c>IntegracionContableConfigSql</c>).
/// </para>
/// <para>
/// El pago <b>no altera el disponible</b>: es tesorería, no presupuesto. Se registra para el reporte
/// de ejecución y para poder conciliar contra bancos.
/// </para>
/// </summary>
internal static class PresupuestoPagoSql
{
    private const string ErrorDeNegocio = "P0001";

    internal const string SqlRegistrar = @"
SELECT public.sp_pst_registrar_pago(
       @companyId::bigint, @documentoId::bigint, @numero::varchar, @compraHdrId::bigint,
       @fecha::date, @monto::numeric, @usuario::varchar, @ip::varchar);";

    internal const string SqlRevertir = @"
SELECT public.sp_pst_revertir_pago(
       @companyId::bigint, @documentoId::bigint, @motivo::varchar, @usuario::varchar, @ip::varchar);";

    /// <summary>
    /// Suma el abono a <c>valor_pagado</c>, prorrateado entre las partidas que devengó la factura.
    /// No-op si el control está apagado o si la factura nunca devengó.
    /// </summary>
    internal static Task<decimal> RegistrarAsync(
        DbConnection conn, DbTransaction? tx, long companyId, long abonoId, string numero,
        int compraHdrId, DateOnly fecha, decimal monto, string usuario, CancellationToken ct)
    {
        if (monto <= 0m) return Task.FromResult(0m);

        return EjecutarAsync(conn, tx, SqlRegistrar, new
        {
            companyId,
            documentoId = abonoId,
            numero,
            compraHdrId = (long)compraHdrId,
            // Dapper/Npgsql no mapea DateOnly.
            fecha = fecha.ToDateTime(TimeOnly.MinValue),
            monto,
            usuario,
            ip = (string?)null
        }, ct);
    }

    /// <summary>Resta del <c>valor_pagado</c> lo que había sumado el abono anulado.</summary>
    internal static Task<decimal> RevertirAsync(
        DbConnection conn, DbTransaction? tx, long companyId, long abonoId, string motivo,
        string usuario, CancellationToken ct)
        => EjecutarAsync(conn, tx, SqlRevertir, new
        {
            companyId,
            documentoId = abonoId,
            motivo,
            usuario,
            ip = (string?)null
        }, ct);

    private static async Task<decimal> EjecutarAsync(
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
}
