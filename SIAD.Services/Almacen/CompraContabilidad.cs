using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Data;
using SIAD.Services.Contabilidad;

namespace SIAD.Services.Almacen;

/// <summary>
/// Fase 2 — asiento contable de la FACTURA de compra (module <c>COMPRAS</c>), por el mismo motor
/// que Ventas/Caja/Bancos (<see cref="IntegracionContableConfigSql"/> →
/// <c>sp_con_generar_comprobante_config</c>). Calca <see cref="AlmacenContabilidad"/>: toma la
/// conn/tx del <see cref="SiadDbContext"/>, corre dentro de la transacción del alta, y sólo postea
/// si el módulo COMPRAS está activo (<c>activo_compras</c>) — Compras es su PROPIO módulo contable,
/// separado de inventario (ALMACEN) y de proveedores (PROV). Apagado = no-op, el flujo como hoy.
/// <para>
/// Asiento por defecto (D3 = ISV al costo): <b>DEBE cuenta_inventario</b> (del tipo de cada renglón)
/// / <b>HABER cuenta del proveedor</b> por el total. Los conceptos que no entraron al kardex (flete,
/// otros gastos, descuento, ISV no prorrateado) se <b>capitalizan al costo</b> en la primera cuenta
/// de inventario, de modo que el asiento cuadra contra el total de la CxP. Separar el ISV a crédito
/// fiscal es un afinamiento posterior del contador (uso nuevo en la matriz de cuentas).
/// </para>
/// </summary>
internal static class CompraContabilidad
{
    internal const string DocTypeFactura = "FACTURA";

    /// <summary>
    /// Contabiliza la factura de compra si el módulo COMPRAS está activo (<c>activo_compras</c>).
    /// Devuelve el <c>poliza_id</c>, o <c>null</c> si el módulo está inactivo, la partida quedó
    /// encolada (sin período) o la factura no tiene valor.
    /// </summary>
    internal static async Task<long?> ContabilizarFacturaAsync(
        SiadDbContext context, long companyId, string codProveedor, decimal total,
        IReadOnlyList<(int ArticuloId, decimal Valor)> renglones,
        long documentoId, string documentoNumero, DateOnly fecha, string descripcion, string usuario, CancellationToken ct)
    {
        var conn = context.Database.GetDbConnection();
        var tx = context.Database.CurrentTransaction?.GetDbTransaction();

        var config = await IntegracionContableConfigSql.ObtenerConfigAsync(conn, companyId, tx, ct);
        if (config is null || !config.ActivoCompras || total <= 0m)
        {
            return null;
        }

        // HABER: la cuenta por pagar del proveedor (código del plan → account_id).
        var codCuentaProveedor = await context.prv_proveedores.AsNoTracking()
            .Where(p => p.company_id == companyId && p.cod_proveedor == codProveedor)
            .Select(p => p.cuenta_contable)
            .FirstOrDefaultAsync(ct);
        var ctaProveedor = await ResolverCuentaAsync(context, companyId, codCuentaProveedor, "cuenta por pagar del proveedor", ct);

        // DEBE: cuenta de inventario del tipo de cada renglón, acumulada por cuenta.
        var porCuenta = new Dictionary<long, decimal>();
        foreach (var (articuloId, valor) in renglones)
        {
            if (valor <= 0m) continue;

            var codInventario = await (
                from a in context.alm_articulos.AsNoTracking()
                join t in context.alm_tipo_articulos.AsNoTracking() on a.tipo_articulo_id equals t.id
                where a.id == articuloId
                select t.cuenta_inventario).FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException(
                    "El artículo no tiene un tipo con cuenta de inventario: configúrelo antes de contabilizar la compra.");

            var ctaInventario = await ResolverCuentaAsync(context, companyId, codInventario, "inventario", ct);
            porCuenta[ctaInventario] = porCuenta.GetValueOrDefault(ctaInventario) + valor;
        }
        if (porCuenta.Count == 0)
        {
            return null;
        }

        // El DEBE cuadra contra el total (HABER proveedor). El remanente (flete/otros/descuento/ISV
        // no prorrateado) se capitaliza al costo en la cuenta de mayor valor.
        var baseInventario = porCuenta.Values.Sum();
        var remanente = Math.Round(total - baseInventario, 2, MidpointRounding.AwayFromZero);
        if (remanente != 0m)
        {
            var cuentaMayor = porCuenta.OrderByDescending(k => k.Value).First().Key;
            porCuenta[cuentaMayor] = porCuenta[cuentaMayor] + remanente;
        }

        var lineas = new List<IntegracionContableConfigSql.ComprobanteLinea>();
        foreach (var kv in porCuenta.OrderBy(k => k.Key))
        {
            var debe = Math.Round(kv.Value, 2, MidpointRounding.AwayFromZero);
            if (debe <= 0m) continue;
            lineas.Add(new IntegracionContableConfigSql.ComprobanteLinea(kv.Key, debe, 0m, descripcion));
        }
        lineas.Add(new IntegracionContableConfigSql.ComprobanteLinea(ctaProveedor, 0m, total, descripcion));

        return await IntegracionContableConfigSql.GenerarComprobanteAsync(
            conn, companyId, IntegracionContableModulos.Compras, DocTypeFactura, documentoId,
            documentoNumero, fecha, descripcion, usuario, lineas, tx, ct);
    }

    /// <summary>Revierte el asiento de la factura (module ALMACEN + docType FACTURA + id). No-op si no había.</summary>
    internal static Task<long?> RevertirFacturaAsync(
        SiadDbContext context, long companyId, long documentoId, string usuario, CancellationToken ct)
    {
        var conn = context.Database.GetDbConnection();
        var tx = context.Database.CurrentTransaction?.GetDbTransaction();
        return IntegracionContableConfigSql.RevertirComprobanteAsync(
            conn, companyId, IntegracionContableModulos.Compras, new[] { DocTypeFactura }, documentoId, usuario, tx, ct);
    }

    private static async Task<long> ResolverCuentaAsync(
        SiadDbContext context, long companyId, string? code, string rol, CancellationToken ct)
    {
        var codigo = (code ?? string.Empty).Trim();
        if (codigo.Length == 0)
        {
            throw new InvalidOperationException($"No está configurada la cuenta de {rol}.");
        }

        var accountId = await context.con_plan_cuentas.AsNoTracking()
            .Where(c => c.company_id == companyId && c.code == codigo && c.allows_posting)
            .Select(c => (long?)c.account_id)
            .FirstOrDefaultAsync(ct);

        return accountId ?? throw new InvalidOperationException(
            $"La cuenta de {rol} '{codigo}' no existe en el plan de cuentas o no permite movimiento.");
    }
}
