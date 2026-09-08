using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.ActivosFijos;

/// <summary>
/// Entrada de un catálogo de sistema del módulo (método de depreciación, estado del
/// activo). La UI muestra <see cref="Nombre"/>, nunca el id: en este proyecto los
/// códigos internos no llegan al usuario.
/// </summary>
public sealed class CatalogoAfDto
{
    public short Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    /// <summary>Solo métodos de depreciación: false = el motor todavía no lo sabe calcular.</summary>
    public bool Implementado { get; set; }

    /// <summary>Solo estados: false = el activo no se deprecia mientras esté en este estado.</summary>
    public bool PermiteDepreciar { get; set; }

    /// <summary>Solo estados: true = el activo salió del patrimonio (baja, venta, extravío).</summary>
    public bool EsFinal { get; set; }
}

public sealed class TipoActivoListItemDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? PrefijoCodigo { get; set; }
    public decimal? VidaUtilAnios { get; set; }
    public short MetodoDepreciacionId { get; set; }
    public string? MetodoDepreciacion { get; set; }
    public decimal PorcentajeResidual { get; set; }
    public string? CuentaActivo { get; set; }
    public string? CuentaDepreciacionAcumulada { get; set; }
    public string? CuentaGastoDepreciacion { get; set; }
    public string? CuentaPerdidaBaja { get; set; }
    public bool Activo { get; set; }

    /// <summary>Cuántos activos usan este tipo. Avisa de lo que se rompe al desactivarlo.</summary>
    public long ActivosRegistrados { get; set; }
}

public sealed class TipoActivoEditDto
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "El código es obligatorio."), StringLength(20)]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio."), StringLength(120)]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(254)]
    public string? Descripcion { get; set; }

    [StringLength(6, ErrorMessage = "El prefijo admite hasta 6 caracteres.")]
    public string? PrefijoCodigo { get; set; }

    [Range(0.1, 999, ErrorMessage = "La vida útil debe ser mayor que cero.")]
    public decimal? VidaUtilAnios { get; set; }

    public short MetodoDepreciacionId { get; set; } = 1;

    [Range(0, 100, ErrorMessage = "El porcentaje residual va de 0 a 100.")]
    public decimal PorcentajeResidual { get; set; }

    [StringLength(30)] public string? CuentaActivo { get; set; }
    [StringLength(30)] public string? CuentaDepreciacionAcumulada { get; set; }
    [StringLength(30)] public string? CuentaGastoDepreciacion { get; set; }
    [StringLength(30)] public string? CuentaPerdidaBaja { get; set; }

    public bool Activo { get; set; } = true;
}

/// <summary>Tipo de activo para combos, con los valores que presta al activo nuevo.</summary>
public sealed class TipoActivoLookupDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public decimal? VidaUtilAnios { get; set; }
    public short MetodoDepreciacionId { get; set; }
    public decimal PorcentajeResidual { get; set; }
    public string? CuentaActivo { get; set; }
    public string? CuentaDepreciacionAcumulada { get; set; }
    public string? CuentaGastoDepreciacion { get; set; }
}

public sealed class UbicacionActivoListItemDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int? PadreId { get; set; }
    public string? PadreNombre { get; set; }

    /// <summary>Ruta completa desde la raíz, p. ej. "Oficina central / Edificio A / Piso 2".</summary>
    public string? Ruta { get; set; }

    public string? Direccion { get; set; }
    public string? Responsable { get; set; }
    public bool Activo { get; set; }
    public long ActivosRegistrados { get; set; }
}

public sealed class UbicacionActivoEditDto
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "El código es obligatorio."), StringLength(20)]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio."), StringLength(120)]
    public string Nombre { get; set; } = string.Empty;

    public int? PadreId { get; set; }

    [StringLength(254)] public string? Direccion { get; set; }
    [StringLength(120)] public string? Responsable { get; set; }

    public bool Activo { get; set; } = true;
}

public sealed class UbicacionActivoLookupDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Ruta { get; set; }
}
