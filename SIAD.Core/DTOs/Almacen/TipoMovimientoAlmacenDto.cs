using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Almacen;

/// <summary>
/// Tipo de movimiento de almacén (<c>alm_tipo_movimiento</c>) para alta/edición. La
/// <see cref="Clase"/> gobierna qué hace el motor; el resto es metadato de negocio configurable.
/// <para>
/// Distinto de <see cref="TipoMovimientoDto"/> (lookup de códigos de transacción del kardex).
/// </para>
/// </summary>
public sealed class TipoMovimientoAlmacenDto
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(20, ErrorMessage = "El código no puede superar los 20 caracteres.")]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(80, ErrorMessage = "El nombre no puede superar los 80 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    /// <summary>ENTRADA · SALIDA · VALOR. Se valida contra <see cref="SIAD.Core.Constants.ClaseAjusteInventario"/>.</summary>
    [Required(ErrorMessage = "La clase es obligatoria.")]
    [StringLength(10)]
    public string Clase { get; set; } = string.Empty;

    public bool RequiereAutorizacion { get; set; }

    /// <summary>Solo aplica a clase SALIDA: envía aviso por correo al cruzar bajo mínimo.</summary>
    public bool NotificaCorreo { get; set; }

    [StringLength(20, ErrorMessage = "La cuenta contable no puede superar los 20 caracteres.")]
    public string? CuentaContable { get; set; }

    public bool Activo { get; set; } = true;

    public short Orden { get; set; }
}
