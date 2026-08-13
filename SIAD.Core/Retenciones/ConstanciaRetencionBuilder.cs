using System;
using System.Collections.Generic;
using System.Linq;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Retenciones;
using SIAD.Core.Entities;
using SIAD.Core.Utilities;

namespace SIAD.Core.Retenciones;

/// <summary>
/// Ensambla el DTO de impresión de la constancia de retención (F5) de forma PURA (sin EF/DB), a
/// partir de la cabecera fiscal F4, sus líneas, la empresa (agente retenedor) y el compromiso
/// origen (para el nombre del proveedor y el concepto). Testeable en unidad (patrón del calculador
/// de F2). La numeración en letras se toma de <see cref="NumerosALetras"/> sobre el total retenido.
/// </summary>
public static class ConstanciaRetencionBuilder
{
    public static ConstanciaRetencionImpresionDto Build(
        prv_retencion_hdr hdr,
        IReadOnlyList<RetencionRegistroLineaDto> lineas,
        cfg_company? company,
        string? nombreProveedor,
        string? concepto,
        string? impresoPor)
    {
        ArgumentNullException.ThrowIfNull(hdr);
        lineas ??= Array.Empty<RetencionRegistroLineaDto>();

        return new ConstanciaRetencionImpresionDto
        {
            // Empresa (agente retenedor).
            EmpresaNombre = FirstNonEmpty(company?.commercial_name, company?.legal_name, company?.code) ?? string.Empty,
            EmpresaRazonSocial = company?.legal_name,
            EmpresaRtn = company?.tax_id,
            EmpresaDireccion = company?.address,
            EmpresaTelefono = company?.phone,
            EmpresaEmail = company?.email,
            EmpresaLogo = company?.logo,

            // Proveedor (sujeto retenido): nombre desde el compromiso; RTN/código snapshot del hdr.
            ProveedorNombre = FirstNonEmpty(nombreProveedor, hdr.cod_proveedor) ?? string.Empty,
            ProveedorCodigo = hdr.cod_proveedor,
            ProveedorRtn = hdr.rtn_proveedor,

            // Documento origen + folio interno.
            NumeroOrden = hdr.numero_orden,
            NumeroAbono = hdr.numero_abono,
            Concepto = concepto,
            Folio = hdr.folio,
            FechaEmision = hdr.fecha_emision,
            PolizaNumber = hdr.poliza_number,

            // Montos.
            BaseTotal = hdr.base_total,
            TotalRetenido = hdr.total_retenido,
            MontoEnLetras = $"{NumerosALetras.Convertir(hdr.total_retenido)} LEMPIRAS",

            // Estado.
            EstadoId = hdr.estado_id,
            Anulada = hdr.estado_id == EstadoRetencion.Anulada,
            MotivoAnulacion = hdr.motivo_anulacion,

            Lineas = lineas,

            ImpresoPor = string.IsNullOrWhiteSpace(impresoPor) ? "sistema" : impresoPor!.Trim(),

            // Hooks CAI (F5b): hoy NULL. cai_proveedor viaja por si ya se pobló; el correlativo y la
            // leyenda quedan reservados hasta confirmar D1 (ver ConstanciaRetencionImpresionDto).
            CaiProveedor = hdr.cai_proveedor,
            CaiCorrelativo = null,
            CaiLeyenda = null,
        };
    }

    private static string? FirstNonEmpty(params string?[] valores)
        => valores.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
}
