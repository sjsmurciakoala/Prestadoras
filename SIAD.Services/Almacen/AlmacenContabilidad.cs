using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Data;
using SIAD.Services.Contabilidad;

namespace SIAD.Services.Almacen;

/// <summary>
/// Puente de contabilización de los movimientos de almacén (integración contable, F1+).
/// <para>
/// Reutiliza el MISMO motor que Ventas/Caja/Bancos: el SP
/// <c>sp_con_generar_comprobante_config</c> a través de
/// <see cref="IntegracionContableConfigSql"/> (module <c>ALMACEN</c>). El SP resuelve solo
/// el período, el encolado en <c>con_partida_pendiente</c> y la idempotencia por documento;
/// aquí solo se decide si el módulo está activo, se resuelven las cuentas y se arman las
/// líneas Debe/Haber. Todo corre dentro de la transacción del servicio llamador (la del
/// kardex): un movimiento y su asiento se confirman o se revierten juntos.
/// </para>
/// <para>Las cuentas salen del <b>tipo de artículo</b> (<c>alm_tipo_articulo</c>), que las
/// guarda como código del plan; aquí se resuelven a <c>account_id</c> contra
/// <c>con_plan_cuentas</c>.</para>
/// </summary>
internal static class AlmacenContabilidad
{
    /// <summary>
    /// Contabiliza un ajuste de EXISTENCIA (ENTRADA / SALIDA) si el módulo ALMACEN está activo.
    /// ENTRADA sube el inventario (Debe inventario / Haber ajustes); SALIDA lo baja (al revés).
    /// Devuelve el <c>poliza_id</c>, o <c>null</c> si el módulo está inactivo, la partida quedó
    /// encolada (sin período) o el movimiento no tiene valor contable.
    /// </summary>
    internal static async Task<long?> ContabilizarAjusteAsync(
        SiadDbContext context, long companyId, int articuloId, string clase, decimal monto,
        long documentoId, string documentoNumero, DateOnly fecha, string descripcion, string usuario, CancellationToken ct)
    {
        // Un ajuste sin valor (par sin costo promedio) no deja asiento: no hay nada que cuadrar.
        if (monto <= 0m)
        {
            return null;
        }

        var inventarioSube = string.Equals(clase, ClaseAjusteInventario.Entrada, StringComparison.OrdinalIgnoreCase);
        return await PostearInventarioVsAjustesAsync(
            context, companyId, articuloId, inventarioSube, monto, documentoId, documentoNumero, fecha, descripcion, usuario, ct);
    }

    /// <summary>
    /// Contabiliza un ajuste de VALOR (corrección de costo, sin mover existencia). El asiento vale
    /// <paramref name="delta"/> = existencia × (costo_nuevo − costo_anterior): si es positivo el
    /// inventario vale más (Debe inventario / Haber ajustes); si es negativo, al revés. delta 0 no
    /// deja asiento. Devuelve el <c>poliza_id</c> o <c>null</c>.
    /// </summary>
    internal static async Task<long?> ContabilizarAjusteValorAsync(
        SiadDbContext context, long companyId, int articuloId, decimal delta,
        long documentoId, string documentoNumero, DateOnly fecha, string descripcion, string usuario, CancellationToken ct)
    {
        if (delta == 0m)
        {
            return null;
        }

        return await PostearInventarioVsAjustesAsync(
            context, companyId, articuloId, delta > 0m, Math.Abs(delta), documentoId, documentoNumero, fecha, descripcion, usuario, ct);
    }

    /// <summary>
    /// Revierte la partida del documento de almacén (por <c>module</c> + <c>docType</c> + id).
    /// Descarta la pendiente viva si estaba encolada. Devuelve el <c>poliza_id</c> revertido o null.
    /// </summary>
    internal static async Task<long?> RevertirAsync(
        SiadDbContext context, long companyId, string docType, long documentoId, string usuario, CancellationToken ct)
    {
        var conn = context.Database.GetDbConnection();
        var tx = context.Database.CurrentTransaction?.GetDbTransaction();
        return await IntegracionContableConfigSql.RevertirComprobanteAsync(
            conn, companyId, IntegracionContableModulos.Almacen, new[] { docType }, documentoId, usuario, tx, ct);
    }

    /// <summary>
    /// Núcleo del asiento de ajuste: dos líneas entre la cuenta de inventario y la de ajustes del
    /// tipo de artículo. <paramref name="inventarioSube"/> decide la dirección (Debe/Haber). Solo
    /// postea si el módulo ALMACEN está activo para la empresa.
    /// </summary>
    private static async Task<long?> PostearInventarioVsAjustesAsync(
        SiadDbContext context, long companyId, int articuloId, bool inventarioSube, decimal monto,
        long documentoId, string documentoNumero, DateOnly fecha, string descripcion, string usuario, CancellationToken ct)
    {
        var conn = context.Database.GetDbConnection();
        var tx = context.Database.CurrentTransaction?.GetDbTransaction();

        var config = await IntegracionContableConfigSql.ObtenerConfigAsync(conn, companyId, tx, ct);
        if (config is null || !config.ActivoAlmacen)
        {
            return null;
        }

        var cuentas = await (
            from a in context.alm_articulos.AsNoTracking()
            join t in context.alm_tipo_articulos.AsNoTracking() on a.tipo_articulo_id equals t.id
            where a.id == articuloId
            select new { t.cuenta_inventario, t.cuenta_ajustes }).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException(
                "El artículo no tiene un tipo con cuentas contables: configure el tipo antes de contabilizar el ajuste.");

        var ctaInventario = await ResolverCuentaAsync(context, companyId, cuentas.cuenta_inventario, "inventario", ct);
        var ctaAjustes = await ResolverCuentaAsync(context, companyId, cuentas.cuenta_ajustes, "ajustes de inventario", ct);

        // Sube el valor del inventario → Debe inventario / Haber ajustes; baja → al revés.
        var (cuentaDebe, cuentaHaber) = inventarioSube ? (ctaInventario, ctaAjustes) : (ctaAjustes, ctaInventario);

        var lineas = new List<IntegracionContableConfigSql.ComprobanteLinea>
        {
            new(cuentaDebe, monto, 0m, descripcion),
            new(cuentaHaber, 0m, monto, descripcion)
        };

        return await IntegracionContableConfigSql.GenerarComprobanteAsync(
            conn, companyId, IntegracionContableModulos.Almacen, "AJUSTE", documentoId,
            documentoNumero, fecha, descripcion, usuario, lineas, tx, ct);
    }

    /// <summary>Resuelve un código de cuenta del tipo de artículo a <c>account_id</c> posteable, o lanza.</summary>
    private static async Task<long> ResolverCuentaAsync(
        SiadDbContext context, long companyId, string? code, string rol, CancellationToken ct)
    {
        var codigo = (code ?? string.Empty).Trim();
        if (codigo.Length == 0)
        {
            throw new InvalidOperationException($"El tipo de artículo no tiene configurada la cuenta de {rol}.");
        }

        var accountId = await context.con_plan_cuentas.AsNoTracking()
            .Where(c => c.company_id == companyId && c.code == codigo && c.allows_posting)
            .Select(c => (long?)c.account_id)
            .FirstOrDefaultAsync(ct);

        return accountId ?? throw new InvalidOperationException(
            $"La cuenta de {rol} '{codigo}' no existe en el plan de cuentas o no permite movimiento.");
    }
}
