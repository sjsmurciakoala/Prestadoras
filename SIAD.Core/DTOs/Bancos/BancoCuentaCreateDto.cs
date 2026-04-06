using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Bancos;

public class BancoCuentaCreateDto
{
    [Range(typeof(long), "1", "9223372036854775807", ErrorMessage = "Seleccione un banco valido.")]
    public long BancoId { get; set; }

    [Required(ErrorMessage = "El numero de cuenta es obligatorio.")]
    [StringLength(50, ErrorMessage = "El numero de cuenta no puede superar 50 caracteres.")]
    [RegularExpression(@"^[0-9A-Za-z\- ]+$", ErrorMessage = "El numero de cuenta solo permite letras, numeros, espacios y guiones.")]
    public string NumeroCuenta { get; set; } = string.Empty;

    [Required(ErrorMessage = "El tipo de cuenta es obligatorio.")]
    [StringLength(20, ErrorMessage = "El tipo de cuenta no puede superar 20 caracteres.")]
    public string TipoCuenta { get; set; } = string.Empty;

    [StringLength(3, ErrorMessage = "La moneda debe tener 3 caracteres.")]
    public string? Moneda { get; set; }

    [Required(ErrorMessage = "El saldo actual es obligatorio.")]
    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "El saldo actual no puede ser negativo.")]
    public decimal? SaldoActual { get; set; }

    [StringLength(150, ErrorMessage = "El titular no puede superar 150 caracteres.")]
    public string? Titular { get; set; }

    [StringLength(300, ErrorMessage = "Las observaciones no pueden superar 300 caracteres.")]
    public string? Observaciones { get; set; }

    public bool Activo { get; set; } = true;

    [Range(typeof(long), "1", "9223372036854775807", ErrorMessage = "Seleccione una cuenta contable valida.")]
    public long? ContAccountId { get; set; }

    [StringLength(255, ErrorMessage = "cta_conc no puede superar 255 caracteres.")]
    public string? CtaConc { get; set; }
}
