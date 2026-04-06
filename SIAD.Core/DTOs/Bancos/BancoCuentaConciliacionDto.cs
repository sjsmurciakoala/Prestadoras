namespace SIAD.Core.DTOs.Bancos;

public sealed class BancoCuentaConciliacionDto
{
    public long CompanyId { get; set; }
    public long BancoCuentaId { get; set; }
    public string NumeroTransaccion { get; set; } = string.Empty;
    public DateOnly Fecha { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Referencia { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public string? EstadoConciliacion { get; set; }
}
