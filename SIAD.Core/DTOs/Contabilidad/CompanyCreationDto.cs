using System;
using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Contabilidad;

public sealed class CompanyCreationDto
{
    public long CompanyId { get; set; }

    public string? Advertencia { get; set; }

    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(20, ErrorMessage = "El código no puede superar los 20 caracteres.")]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripción es obligatoria.")]
    [StringLength(200, ErrorMessage = "La descripción no puede superar los 200 caracteres.")]
    public string Descripcion { get; set; } = string.Empty;

    [Required(ErrorMessage = "El tipo de empresa es obligatorio.")]
    [StringLength(60, ErrorMessage = "El tipo de empresa no puede superar los 60 caracteres.")]
    public string TipoEmpresa { get; set; } = string.Empty;

    [Required(ErrorMessage = "Las siglas del ID fiscal son obligatorias.")]
    [StringLength(10, ErrorMessage = "Las siglas del ID fiscal no pueden superar los 10 caracteres.")]
    public string IdFiscalSiglas { get; set; } = string.Empty;

    [Required(ErrorMessage = "El valor del ID fiscal es obligatorio.")]
    [StringLength(40, ErrorMessage = "El valor del ID fiscal no puede superar los 40 caracteres.")]
    public string IdFiscalValor { get; set; } = string.Empty;

    [Required(ErrorMessage = "Debes seleccionar un tamaño.")]
    public CompanySizeType? Tamano { get; set; }

    [Required(ErrorMessage = "Debes seleccionar un tipo de capital.")]
    public CompanyCapitalType? Capital { get; set; }

    [Required(ErrorMessage = "La fecha de constitución es obligatoria.")]
    public DateTime? FechaConstitucion { get; set; }

    public bool Activa { get; set; } = true;

    [StringLength(160, ErrorMessage = "El contacto no puede superar los 160 caracteres.")]
    public string? Contacto { get; set; }

    [Required(ErrorMessage = "La dirección es obligatoria.")]
    [StringLength(500, ErrorMessage = "La dirección no puede superar los 500 caracteres.")]
    public string Direccion { get; set; } = string.Empty;

    [StringLength(120, ErrorMessage = "Los teléfonos no pueden superar los 120 caracteres.")]
    public string? Telefonos { get; set; }

    [Required(ErrorMessage = "El país es obligatorio.")]
    [StringLength(120, ErrorMessage = "El país no puede superar los 120 caracteres.")]
    public string Pais { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    [StringLength(160, ErrorMessage = "El correo no puede superar los 160 caracteres.")]
    public string? Email { get; set; }

    [Url(ErrorMessage = "La página web no tiene un formato válido.")]
    [StringLength(200, ErrorMessage = "La página web no puede superar los 200 caracteres.")]
    public string? PaginaWeb { get; set; }
}

public enum CompanySizeType
{
    Pequena,
    Mediana,
    GranContribuyente
}

public enum CompanyCapitalType
{
    Privado,
    Oficial,
    Mixto
}
