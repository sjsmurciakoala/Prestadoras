namespace SIAD.Core.DTOs.Bancos;

public sealed class BancoConfiguracionTransaccionListDto
{
    public string TipoTransaccionId { get; set; } = string.Empty;
    public string DescripcionTransaccion { get; set; } = string.Empty;
    public string Correlativo { get; set; } = string.Empty;
    public string EntraSale { get; set; } = string.Empty;
    public bool UsaCentroCosto { get; set; }
    public long? CentroCostoId { get; set; }
    public string? CentroCostoCodigo { get; set; }
    public string? CentroCostoNombre { get; set; }
    public string? CuentaContable { get; set; }
    public string? TipoPartida { get; set; }
    public bool EmiteCheque { get; set; }
    public bool DelSistema { get; set; }
    public bool Pad { get; set; }
    public bool Pda { get; set; }
    public bool DetalleContable { get; set; }
    public bool CuentaAlterna { get; set; }
}
