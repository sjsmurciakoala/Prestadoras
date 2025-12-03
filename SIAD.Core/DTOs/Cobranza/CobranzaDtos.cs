using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Cobranza;

public class CobranzaSaldoDetalleDto
{
    public string ClienteClave { get; set; } = string.Empty;
    public string ClienteNombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public decimal SaldoDetalle { get; set; }
    public decimal? Recibo { get; set; }
    public string? Direccion { get; set; }
    public int? CicloId { get; set; }
    public string? CicloDescripcion { get; set; }
    public bool Bloqueado { get; set; }
    public string? Periodo { get; set; }
    public string? TipoServicio { get; set; }
}

public class CobranzaPlanPreviewRequestDto
{
    [Range(1, 120)]
    public int Meses { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal MontoFinanciar { get; set; }

    [Range(typeof(decimal), "0", "100")]
    public decimal PorcentajePrima { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal Total { get; set; }

    [Required]
    public DateTime FechaPrimerPago { get; set; }
}

public class CobranzaCuotaDto
{
    public int Numero { get; set; }
    public DateTime Fecha { get; set; }
    public decimal Valor { get; set; }
}

public class CobranzaPlanPreviewDto
{
    public decimal ValorCuota { get; set; }
    public IReadOnlyList<CobranzaCuotaDto> Cuotas { get; set; } = Array.Empty<CobranzaCuotaDto>();
}

public class CobranzaPlanGuardarDto
{
    [Required]
    public string ClienteClave { get; set; } = string.Empty;

    [Range(1, 120)]
    public int Meses { get; set; }

    [Range(typeof(decimal), "0", "100")]
    public decimal PorcentajePrima { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal Total { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal ValorPrima { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal MontoFinanciar { get; set; }

    public DateTime Fecha { get; set; }

    public DateTime FechaPrimerPago { get; set; }

    public string Direccion { get; set; } = string.Empty;

    public string ClienteNombre { get; set; } = string.Empty;

    public string? Comentario { get; set; }

    public string? NomRepresentante { get; set; }

    public string? DocRepresentante { get; set; }

    public string? NumRepresentante { get; set; }

    public decimal? Recibo { get; set; }

    public int? CicloId { get; set; }

    public string Usuario { get; set; } = string.Empty;
}

public class CobranzaPlanResumenDto
{
    public string Correlativo { get; set; } = string.Empty;
    public string Cliente { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public DateTime? Fecha { get; set; }
    public int? EncabezadoId { get; set; }
    public string ClienteClave { get; set; } = string.Empty;
}

public class CobranzaPlanDetalleDto
{
    public string Correlativo { get; set; } = string.Empty;
    public string ClienteClave { get; set; } = string.Empty;
    public string ClienteNombre { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public decimal Prima { get; set; }
    public decimal MontoFinanciar { get; set; }
    public int Meses { get; set; }
    public DateTime? Fecha { get; set; }
    public DateTime? FechaPrimerPago { get; set; }
    public string? Direccion { get; set; }
    public string? Comentario { get; set; }
    public string? Representante { get; set; }
    public string? DocRepresentante { get; set; }
    public string? NumRepresentante { get; set; }
    public decimal? Recibo { get; set; }
    public string Estado { get; set; } = string.Empty;
    public IReadOnlyList<CobranzaPlanDetalleCuotaDto> Cuotas { get; set; } = Array.Empty<CobranzaPlanDetalleCuotaDto>();
}

public class CobranzaPlanDetalleCuotaDto
{
    public int Numero { get; set; }
    public DateTime? Fecha { get; set; }
    public decimal Valor { get; set; }
    public string Estado { get; set; } = "Pendiente";
}
