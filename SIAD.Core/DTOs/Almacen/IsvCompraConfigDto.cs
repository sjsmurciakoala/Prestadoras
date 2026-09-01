using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Almacen;

/// <summary>
/// Configuración del ISV en compras a nivel de empresa: el <c>Tratamiento</c> (al costo /
/// impuesto fiscal) que se aplica a todo lo que compra la empresa.
/// </summary>
public sealed class IsvCompraConfigDto
{
    [Required(ErrorMessage = "El tratamiento es obligatorio.")]
    public string Tratamiento { get; set; } = "COSTO";
}
