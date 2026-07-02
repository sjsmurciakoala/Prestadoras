using System;
using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Clientes;

public sealed class ClienteCreateDto
{
    public string Clave { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    public string Nombre { get; set; } = null!;

    public string? Apellidos { get; set; }

    [Required(ErrorMessage = "El DNI es obligatorio.")]
    public string Dni { get; set; } = null!;

    public string? Rtn { get; set; }

    public DateTime? FechaNacimiento { get; set; }

    [StringLength(7, ErrorMessage = "El barrio debe tener maximo 7 caracteres.")]
    public string? BarrioCodigo { get; set; }

    public bool? TerceraEdad { get; set; }

    public bool? BloqueadoCobranza { get; set; }

    public bool? NoCortable { get; set; }

    public int? AbogadoId { get; set; }

    public string? Telefono { get; set; }

    public string? TelefonoMovil { get; set; }

    public string? Email { get; set; }

    public string? Direccion { get; set; }

    public string? ColorCasa { get; set; }

    public string? ClaveCatastral { get; set; }

    public bool? TieneMedidor { get; set; }

    public int? MedidorId { get; set; }

    public string? EmpresaNombre { get; set; }

    public string? EmpresaRtn { get; set; }

    public string? EmpresaTelefono { get; set; }

    public string? EmpresaDireccion { get; set; }

    [StringLength(2, ErrorMessage = "El tipo de uso debe tener maximo 2 caracteres.")]
    public string? TipoUsoCodigo { get; set; }

    public int? CategoriaServicioId { get; set; }

    public int? ServicioId { get; set; }

    public string? LetraCodigo { get; set; }

    public int? CicloId { get; set; }

    [RegularExpression("^[0-9]{1,5}$", ErrorMessage = "Acepta libreta de 1-3 digitos o ruta completa de 5 digitos. Una ruta valida es CC+LLL donde CC=ciclo (2d) y LLL=libreta (3d).")]
    public string? Libreta { get; set; }

    public string? Secuencia { get; set; }

    public string? Contador { get; set; }

    public string? ClaveSure { get; set; }

    public bool? TieneConvenio { get; set; }

    public string? NumeroConvenio { get; set; }

    public bool? TieneContrato { get; set; }

    public string? NumeroContrato { get; set; }

    public string? Observaciones { get; set; }

    public bool Activo { get; set; } = true;
}

