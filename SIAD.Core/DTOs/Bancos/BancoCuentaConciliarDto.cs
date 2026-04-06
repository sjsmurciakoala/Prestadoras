namespace SIAD.Core.DTOs.Bancos;

public sealed class BancoCuentaConciliarDto
{
    public long BancoCuentaId { get; set; }
    public DateOnly? FechaConciliacion { get; set; }
    public List<BancoCuentaConciliacionDto> Movimientos { get; set; } = new();
}
