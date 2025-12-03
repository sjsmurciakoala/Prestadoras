using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SIAD.Core.DTOs.Common;

namespace SIAD.Core.DTOs.CaptacionPagos;

public class CaptacionHeaderDto
{
    public string NumFactura { get; set; } = string.Empty;
    public string ClienteClave { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public decimal Total { get; set; }
    public string Estado { get; set; } = string.Empty;
    public int? CajaId { get; set; }
    public string? CajaNombre { get; set; }
    public string? Banco { get; set; }
    public string? Usuario { get; set; }
}

public class CaptacionDetailDto
{
    public int Linea { get; set; }
    public string Servicio { get; set; } = string.Empty;
    public decimal MontoValor { get; set; }
}

public class CaptacionPagoResponseDto
{
    public CaptacionHeaderDto Header { get; set; } = new();
    public IReadOnlyList<CaptacionDetailDto> Detalles { get; set; } = Array.Empty<CaptacionDetailDto>();
}

public class PagoCrearDetalleDto
{
    [Required]
    public string Servicio { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal MontoValor { get; set; }
}

public class PagoCrearDto
{
    [Required]
    public string NumFactura { get; set; } = string.Empty;

    [Required]
    public string ClienteClave { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal Monto { get; set; }

    [Required]
    public int CajaId { get; set; }

    public string? Banco { get; set; }

    public string Usuario { get; set; } = string.Empty;

    public List<PagoCrearDetalleDto> Detalles { get; set; } = new();
}

public class CajaDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public DateTime? FechaApertura { get; set; }
    public string? Usuario { get; set; }
}

public class ArqueoDto
{
    public int CajaId { get; set; }
    public string CajaNombre { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public decimal TotalPagos { get; set; }
    public int ConteoPagos { get; set; }

    [JsonIgnore]
    public string RowKey => $"{CajaId}-{Fecha:yyyyMMdd}";
}

public class ReciboMiscelaneoDto
{
    public long Recibo { get; set; }
    public string Cliente { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public decimal Total { get; set; }
    public string Estado { get; set; } = string.Empty;
}

public class ReversoRequestDto
{
    [Required]
    public string NumFactura { get; set; } = string.Empty;

    [Required]
    public string ClienteClave { get; set; } = string.Empty;

    public string Usuario { get; set; } = string.Empty;
}

public class CaptacionArqueoFilterDto
{
    public int? CajaId { get; set; }
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
}
