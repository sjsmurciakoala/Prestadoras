using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Bancos;

public sealed class BanTransaccionAnularDto
{
    [Required(ErrorMessage = "La cuenta bancaria es obligatoria.")]
    [Range(typeof(long), "1", "9223372036854775807", ErrorMessage = "Seleccione una cuenta bancaria válida.")]
    public long BancoCuentaId { get; set; }

    [Required(ErrorMessage = "La transacción original es obligatoria.")]
    [Range(typeof(long), "1", "9223372036854775807", ErrorMessage = "Seleccione una transacción válida.")]
    public long BanKardexIdOriginal { get; set; }

    [Required(ErrorMessage = "El motivo es obligatorio.")]
    [StringLength(500, ErrorMessage = "El motivo no puede superar 500 caracteres.")]
    public string Motivo { get; set; } = string.Empty;
}
