namespace SIAD.Core.DTOs.Tarifario;

/// <summary>
/// Ítem del desglose por servicio del estado de cuenta con su porcentaje de
/// distribución de abonos (adm_desglose_abono_porcentaje).
/// </summary>
public sealed class DesgloseAbonoItemDto
{
    public string ItemCodigo { get; set; } = string.Empty;
    public string ItemNombre { get; set; } = string.Empty;
    public int Orden { get; set; }
    /// <summary>false para ítems especiales como SALDO_ANTERIOR.</summary>
    public bool EsServicioCatalogo { get; set; }
    public decimal Porcentaje { get; set; }
}

public sealed class DesgloseAbonoGuardarDto
{
    public List<DesgloseAbonoItemGuardarDto> Items { get; set; } = [];
}

public sealed class DesgloseAbonoItemGuardarDto
{
    public string ItemCodigo { get; set; } = string.Empty;
    public decimal Porcentaje { get; set; }
}
