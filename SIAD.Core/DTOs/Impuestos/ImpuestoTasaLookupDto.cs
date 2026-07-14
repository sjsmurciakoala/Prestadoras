namespace SIAD.Core.DTOs.Impuestos;

/// <summary>
/// Tasa vigente a una fecha, para desplegables y para el motor de cálculo
/// (p. ej. al asignarle la tasa fiscal a un artículo, o al facturar con la tasa
/// que regía en la fecha del documento).
/// </summary>
public sealed class ImpuestoTasaLookupDto
{
    public int Id { get; init; }
    public int ImpuestoId { get; init; }
    public string ImpuestoCodigo { get; init; } = string.Empty;

    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string Tipo { get; init; } = string.Empty;
    public decimal Porcentaje { get; init; }

    public DateOnly VigenciaDesde { get; init; }
    public DateOnly? VigenciaHasta { get; init; }

    public string Display => $"{Codigo} - {Nombre} ({Porcentaje:0.##}%)";
}
