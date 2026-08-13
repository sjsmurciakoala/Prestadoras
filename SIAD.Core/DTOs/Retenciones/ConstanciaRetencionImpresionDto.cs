namespace SIAD.Core.DTOs.Retenciones;

/// <summary>
/// Datos de impresión de la CONSTANCIA de retención (F5): el documento que el agente retenedor
/// entrega al proveedor por la suma retenida (obligación del Régimen de Facturación, Acuerdo
/// 481-2017). Se arma desde el libro fiscal F4 (prv_retencion_hdr/dtl) + la empresa (cfg_company) +
/// el compromiso origen (prv_compromiso_hdr). Lo ensambla <c>ConstanciaRetencionBuilder</c> (puro).
/// </summary>
public sealed class ConstanciaRetencionImpresionDto
{
    // ── Empresa (agente retenedor) ──
    public string EmpresaNombre { get; init; } = string.Empty;
    public string? EmpresaRazonSocial { get; init; }
    public string? EmpresaRtn { get; init; }
    public string? EmpresaDireccion { get; init; }
    public string? EmpresaTelefono { get; init; }
    public string? EmpresaEmail { get; init; }
    public byte[]? EmpresaLogo { get; init; }

    // ── Proveedor (sujeto retenido) ──
    public string ProveedorNombre { get; init; } = string.Empty;
    public string? ProveedorCodigo { get; init; }
    public string? ProveedorRtn { get; init; }

    // ── Documento origen + folio interno ──
    public int NumeroOrden { get; init; }
    public int NumeroAbono { get; init; }
    public string? Concepto { get; init; }
    public int Folio { get; init; }
    public DateOnly FechaEmision { get; init; }
    public string? PolizaNumber { get; init; }

    // ── Montos ──
    /// <summary>Bruto del pago sujeto a retención (hdr.base_total).</summary>
    public decimal BaseTotal { get; init; }

    /// <summary>Total retenido (= Σ líneas). El "SON" en letras se calcula sobre este monto.</summary>
    public decimal TotalRetenido { get; init; }

    /// <summary>Monto en letras (string ya formateado con " LEMPIRAS"; no calculado en el reporte).</summary>
    public string MontoEnLetras { get; init; } = string.Empty;

    // ── Estado ──
    public short EstadoId { get; init; }

    /// <summary>Derivado de estado_id == Anulada; dispara la marca de agua "ANULADA".</summary>
    public bool Anulada { get; init; }

    public string? MotivoAnulacion { get; init; }

    // ── Detalle (una fila por retención aplicada) ──
    public IReadOnlyList<RetencionRegistroLineaDto> Lineas { get; init; } = Array.Empty<RetencionRegistroLineaDto>();

    /// <summary>Usuario que imprime (pie del documento). Se resuelve en el controller.</summary>
    public string ImpresoPor { get; init; } = "sistema";

    // ── Hooks CAI (F5b) — capa fiscal condicional; HOY NULL, NO se imprime numeración autorizada ──
    // Cuando se confirme D1 (constancia formal con CAI del Acuerdo 481-2017), estos campos se pueblan
    // desde prv_retencion_hdr.cai_id/cai_proveedor + un talonario propio (patrón adm_cai_facturacion /
    // CaiTarifarioService) y el reporte imprime el correlativo de 16 dígitos (NNN-NNN-NN-NNNNNNNN) y la
    // leyenda del acuerdo. NO inventar numeración: mientras estén en NULL, la constancia usa el folio
    // interno.
    /// <summary>F5b: CAI del talonario de constancias (hoy NULL). Ver <c>cai_id</c>.</summary>
    public string? CaiProveedor { get; init; }

    /// <summary>F5b: correlativo fiscal de 16 dígitos NNN-NNN-NN-NNNNNNNN (hoy NULL).</summary>
    public string? CaiCorrelativo { get; init; }

    /// <summary>F5b: leyenda del régimen (p. ej. "Acuerdo 481-2017") (hoy NULL).</summary>
    public string? CaiLeyenda { get; init; }
}
