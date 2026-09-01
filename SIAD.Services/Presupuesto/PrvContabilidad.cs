using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Services.Contabilidad;

namespace SIAD.Services.Presupuesto;

/// <summary>
/// Puente de contabilización del asiento de PAGO de compromisos/OPD (module <c>PROV</c>) por el
/// motor config-driven <c>sp_con_generar_comprobante_config</c> a través de
/// <see cref="IntegracionContableConfigSql"/>, calcando
/// <c>SIAD.Services/Almacen/AlmacenContabilidad.cs</c>. El SP resuelve solo el período, el encolado en
/// <c>con_partida_pendiente</c> y la idempotencia por documento; aquí solo se convierten las líneas a
/// <see cref="IntegracionContableConfigSql.ComprobanteLinea"/> y se invoca el motor.
/// <para>
/// A diferencia de <c>AlmacenContabilidad</c> (que toma conn+tx del <c>SiadDbContext</c>), este helper
/// recibe la <see cref="NpgsqlConnection"/> y la <see cref="NpgsqlTransaction"/> EXPLÍCITAS porque
/// <c>OrdenesPagoDirectoService</c> maneja su transacción con ADO.NET crudo (la ambiente en tests, o una
/// propia en producción), que no siempre está registrada en <c>context.Database.CurrentTransaction</c>.
/// </para>
/// <para>
/// Solo cubre el asiento de PAGO (procesar + abonos) y su reverso. El asiento de creación (GEN) NO pasa
/// por aquí: sigue en borrador por el camino viejo (<c>sp_registrar_partida_contable</c>). El tercero
/// (third_party) queda fuera (es F4): <see cref="IntegracionContableConfigSql.ComprobanteLinea"/> no lo
/// lleva.
/// </para>
/// </summary>
internal static class PrvContabilidad
{
    /// <summary>
    /// Postea el asiento de pago por el motor (POSTED). Devuelve el <c>poliza_id</c>, o <c>null</c> si la
    /// partida quedó encolada en <c>con_partida_pendiente</c> (sin período abierto y
    /// <c>encolar_sin_periodo=true</c>). Idempotente por (module=PROV, <paramref name="docType"/>,
    /// <paramref name="documentId"/>) con status=1: reintentar con el mismo documento devuelve el mismo
    /// <c>poliza_id</c> sin duplicar.
    /// </summary>
    internal static async Task<long?> PostearPagoAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long companyId,
        string docType,
        long documentId,
        string documentNumber,
        DateOnly fecha,
        string descripcion,
        string usuario,
        IReadOnlyList<(long AccountId, decimal Debe, decimal Haber, string? Descripcion)> lineas,
        CancellationToken ct)
    {
        var comprobante = lineas
            .Select(l => new IntegracionContableConfigSql.ComprobanteLinea(l.AccountId, l.Debe, l.Haber, l.Descripcion))
            .ToList();

        return await IntegracionContableConfigSql.GenerarComprobanteAsync(
            connection,
            companyId,
            IntegracionContableModulos.Proveedores,
            docType,
            documentId,
            documentNumber,
            fecha,
            descripcion,
            usuario,
            comprobante,
            transaction,
            ct);
    }

    /// <summary>
    /// Revierte el asiento de pago del documento (por module=PROV + <paramref name="docTypes"/> +
    /// <paramref name="documentId"/>) y descarta su pendiente viva si estaba encolada. Devuelve el
    /// <c>poliza_id</c> revertido, o <c>null</c> si el documento no tenía comprobante del motor (p.ej. un
    /// abono pre-F0 posteado por el camino viejo, o uno encolado que solo se descarta).
    /// </summary>
    internal static async Task<long?> RevertirPagoAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long companyId,
        string[] docTypes,
        long documentId,
        string usuario,
        CancellationToken ct)
    {
        return await IntegracionContableConfigSql.RevertirComprobanteAsync(
            connection,
            companyId,
            IntegracionContableModulos.Proveedores,
            docTypes,
            documentId,
            usuario,
            transaction,
            ct);
    }
}
