namespace SIAD.Core.DTOs.Bancos;

public class BanTransaccionFilterDto
{
    public long? BancoCuentaId { get; set; }

    public DateOnly? FechaDesde { get; set; }

    public DateOnly? FechaHasta { get; set; }

    public string? IdTipoTransaccion { get; set; }

    public string? Descripcion { get; set; }
}
