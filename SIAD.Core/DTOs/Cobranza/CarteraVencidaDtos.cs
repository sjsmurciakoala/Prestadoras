namespace SIAD.Core.DTOs.Cobranza;

public class CarteraVencidaFiltroDto
{
    /// <summary>Fecha de corte (as-of). Si es null, el servicio usa la fecha actual.</summary>
    public DateOnly? FechaCorte { get; set; }
    public string? Busqueda { get; set; }
    /// <summary>Tramo de antigüedad: 1=0–30, 2=31–60, 3=61–120, 4=+120. Null = todos.</summary>
    public int? Tramo { get; set; }
    public int? CicloId { get; set; }
}

public record CarteraVencidaClienteDto(
    string Clave,
    string? Nombre,
    int? CicloId,
    string? Ruta,
    decimal B0_30,
    decimal B31_60,
    decimal B61_120,
    decimal BMas120,
    decimal TotalVencido,
    int FacturasVencidas,
    int? DiasMaxVencido,
    bool Bloqueado,
    bool NoCortable,
    int? AbogadoId);
