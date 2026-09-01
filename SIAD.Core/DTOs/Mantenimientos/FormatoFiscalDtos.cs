using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Mantenimientos;

/// <summary>
/// Modos de validación de un formato fiscal. El código es de almacenamiento:
/// las pantallas muestran siempre la descripción, nunca el número.
/// </summary>
public static class ModoValidacionFormatoFiscal
{
    /// <summary>La máscara guía al teclear pero nunca se valida ni se avisa.</summary>
    public const short Libre = 1;

    /// <summary>Avisa que el valor no cumple pero deja guardar.</summary>
    public const short Advierte = 2;

    /// <summary>No deja guardar el documento hasta que el valor cumpla.</summary>
    public const short Bloquea = 3;

    public static string Descripcion(short modo) => modo switch
    {
        Libre => "No valida",
        Advierte => "Advierte",
        Bloquea => "Bloquea",
        _ => "Desconocido"
    };

    public static bool EsValido(short modo) => modo is Libre or Advierte or Bloquea;
}

/// <summary>Filtro de la lista del mantenimiento.</summary>
public sealed class FormatoFiscalFilterDto
{
    public string? Search { get; set; }
    public bool? Activo { get; set; }
}

/// <summary>Fila de la grilla del mantenimiento.</summary>
public sealed class FormatoFiscalListItemDto
{
    public int Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string Mascara { get; init; } = string.Empty;
    public string Ejemplo { get; init; } = string.Empty;
    public short ModoValidacion { get; init; }
    public string ModoValidacionDescripcion => ModoValidacionFormatoFiscal.Descripcion(ModoValidacion);
    public bool Obligatorio { get; init; }
    public bool Activo { get; init; }
}

/// <summary>Alta y edición del formato.</summary>
public sealed class FormatoFiscalEditDto
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "El código del campo es obligatorio.")]
    [StringLength(30, ErrorMessage = "El código no puede superar los 30 caracteres.")]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre visible es obligatorio.")]
    [StringLength(60, ErrorMessage = "El nombre no puede superar los 60 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "La máscara es obligatoria.")]
    [StringLength(80, ErrorMessage = "La máscara no puede superar los 80 caracteres.")]
    public string Mascara { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "El patrón no puede superar los 200 caracteres.")]
    public string? Patron { get; set; }

    [Range(1, 3, ErrorMessage = "Elija cómo debe validarse el campo.")]
    public short ModoValidacion { get; set; } = ModoValidacionFormatoFiscal.Bloquea;

    public bool Obligatorio { get; set; }
    public bool Normalizar { get; set; } = true;
    public bool Mayusculas { get; set; } = true;
    public bool Activo { get; set; } = true;
}

/// <summary>
/// Lo que consumen las vistas que capturan el dato. Trae ya derivadas la máscara de
/// DevExpress, el patrón efectivo y el ejemplo, para que la página no repita esa lógica.
/// </summary>
public sealed class FormatoFiscalLookupDto
{
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string Mascara { get; init; } = string.Empty;
    public string MascaraDevExpress { get; init; } = string.Empty;
    public string Patron { get; init; } = string.Empty;
    public string Ejemplo { get; init; } = string.Empty;
    public short ModoValidacion { get; init; }
    public bool Obligatorio { get; init; }
    public bool Normalizar { get; init; }
    public bool Mayusculas { get; init; }

    public bool Bloquea => ModoValidacion == ModoValidacionFormatoFiscal.Bloquea;
    public bool Advierte => ModoValidacion == ModoValidacionFormatoFiscal.Advierte;
}
