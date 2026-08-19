namespace SIAD.Core.DTOs.TalentoHumano;

/// <summary>Resultado de importar el Excel de empleados: cuántos entraron y qué filas fallaron.</summary>
public sealed class EmpleadoImportResultDto
{
    public int Insertados { get; set; }
    public int Actualizados { get; set; }
    public List<EmpleadoImportErrorDto> Errores { get; set; } = new();
}

public sealed class EmpleadoImportErrorDto
{
    public int Fila { get; set; }
    public string Mensaje { get; set; } = string.Empty;
}
