using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Bancos;

public sealed class BanTransaccionContraLineaDto
{
    [Required(ErrorMessage = "La cuenta contable es obligatoria.")]
    [Range(typeof(long), "1", "9223372036854775807", ErrorMessage = "Seleccione una cuenta contable válida.")]
    public long CuentaId { get; set; }

    [Required(ErrorMessage = "El monto es obligatorio.")]
    [Range(typeof(decimal), "0.01", "999999999.99", ErrorMessage = "El monto debe ser un número positivo.")]
    public decimal Monto { get; set; }

    [StringLength(500, ErrorMessage = "La descripción no puede superar 500 caracteres.")]
    [Required(ErrorMessage = "La descripción es obligatoria.")]
    public string? Descripcion { get; set; }

    [StringLength(120, ErrorMessage = "La referencia no puede superar 120 caracteres.")]
    [Required(ErrorMessage = "La referencia es obligatoria.")]
    public string? SourceDocument { get; set; }
}
