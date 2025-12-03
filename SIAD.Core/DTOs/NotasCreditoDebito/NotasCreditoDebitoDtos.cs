using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.NotasCreditoDebito;

public enum NotaTipoDto
{
    Debito = 1,
    Credito = 2
}

public class NotaClienteLookupDto
{
    public string Clave { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Direccion { get; set; }
    public string? Rtn { get; set; }
    public string? Categoria { get; set; }
    public string? CicloCodigo { get; set; }
    public string? CicloDescripcion { get; set; }
}

public class NotaMotivoDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string? Tipo { get; set; }
}

public class NotaTarifaDto
{
    public int ServiciosId { get; set; }
    public string ServicioCodigo { get; set; } = string.Empty;
    public string ServicioDescripcion { get; set; } = string.Empty;
    public decimal MontoSugerido { get; set; }
}

public class NotaClienteConfiguracionDto
{
    public NotaClienteLookupDto Cliente { get; set; } = new();
    public IReadOnlyList<NotaTarifaDto> Tarifas { get; set; } = Array.Empty<NotaTarifaDto>();
    public int ProximoDocumento { get; set; }
}

public class NotaDetalleRequestDto
{
    [Required]
    public string ServicioCodigo { get; set; } = string.Empty;

    public string ServicioDescripcion { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal Monto { get; set; }
}

public class NotaCrearRequestDto
{
    [Required]
    public string ClienteClave { get; set; } = string.Empty;

    [Required]
    public NotaTipoDto TipoNota { get; set; } = NotaTipoDto.Debito;

    [Required]
    public int MotivoId { get; set; }

    public string? Descripcion { get; set; }

    public string? Periodo { get; set; }

    public string? NumeroDocumento { get; set; }

    public decimal? Lectura { get; set; }

    [MinLength(1, ErrorMessage = "Debe agregar al menos un ajuste.")]
    public List<NotaDetalleRequestDto> Detalles { get; set; } = new();

    public string Usuario { get; set; } = string.Empty;
}

public class NotaResponseDto
{
    public int DocumentoId { get; set; }
    public decimal Total { get; set; }
    public decimal SaldoAjuste { get; set; }
    public decimal SaldoCuenta { get; set; }
}
