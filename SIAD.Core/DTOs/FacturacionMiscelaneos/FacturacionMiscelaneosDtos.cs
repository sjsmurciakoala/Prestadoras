using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.FacturacionMiscelaneos;

public class ClienteLookupDto
{
    public string Clave { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Direccion { get; set; }
    public string? Rtn { get; set; }
}

public class ClienteMiscelaneoDto
{
    public string Clave { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Rtn { get; set; }
    public string? Direccion { get; set; }
}

public class MiscelaneoCatalogoDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public decimal ValorUnitario { get; set; }
}

public class MiscelaneoDetalleDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public decimal Unidad { get; set; }
    public decimal ValorUnitario { get; set; }
    public decimal ValorTotal { get; set; }
}

public class FacturaMiscelaneoCrearDto
{
    [Required]
    public string ClienteClave { get; set; } = string.Empty;

    public string? Rtn { get; set; }

    public string? Direccion { get; set; }

    [MinLength(1, ErrorMessage = "Debe elegir al menos un concepto misceláneo.")]
    public List<MiscelaneoDetalleDto> Detalles { get; set; } = new();

    public string? Periodo { get; set; }

    public string Usuario { get; set; } = string.Empty;
}

public class FacturaMiscelaneoResponseDto
{
    public int FacturaId { get; set; }
    public int NumeroRecibo { get; set; }
    public string NumFactura { get; set; } = string.Empty;
    public string ClienteClave { get; set; } = string.Empty;
    public DateTime FechaEmision { get; set; }
    public decimal Total { get; set; }
    public IReadOnlyList<MiscelaneoDetalleDto> Detalles { get; set; } = Array.Empty<MiscelaneoDetalleDto>();
}
