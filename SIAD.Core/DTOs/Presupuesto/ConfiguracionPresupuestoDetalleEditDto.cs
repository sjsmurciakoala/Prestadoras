using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Presupuesto;

public sealed class ConfiguracionPresupuestoDetalleEditDto
{
    [Required(ErrorMessage = "La cuenta contable es obligatoria.")]
    [StringLength(20, ErrorMessage = "La cuenta contable no puede exceder 20 caracteres.")]
    public string CuentaContable { get; set; } = string.Empty;

    [Range(typeof(decimal), "0", "99999999999999.9999", ErrorMessage = "El valor de proyeccion no es valido.")]
    public decimal ValorProyeccion { get; set; }
}
