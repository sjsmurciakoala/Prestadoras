namespace SIAD.Core.DTOs.Bancos;

public sealed class BanTipoTransaccionListDto
{
    public string TipoTransaccion { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public string? EntraSale { get; set; }
}
