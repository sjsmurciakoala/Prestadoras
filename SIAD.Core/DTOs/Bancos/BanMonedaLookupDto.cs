namespace SIAD.Core.DTOs.Bancos;

public sealed class BanMonedaLookupDto
{
    public long BanMonedaId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
}
