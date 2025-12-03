namespace SIAD.Core.DTOs.AuxiliarLectura;

public class AuxiliarLecturaFilterDto
{
    public int? Anio { get; set; }
    public int? Mes { get; set; }
    public string? Ciclo { get; set; }
    public bool? SoloPendientes { get; set; }
    public int? Skip { get; set; }
    public int? Take { get; set; }
}
