using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Presupuesto;

public sealed class ConfiguracionPresupuestoDetalleUpdateDto
{
    [Range(typeof(decimal), "0", "99999999999999.9999", ErrorMessage = "El valor de proyeccion no es valido.")]
    public decimal ValorProyeccion { get; set; }
}
