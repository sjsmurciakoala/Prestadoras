using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Aprobaciones;

/// <summary>
/// Configuración completa de la aprobación de un documento: el interruptor y su escalera.
/// Es lo que pinta la pantalla Configuración → Aprobaciones de una sola carga.
/// </summary>
public sealed class AprobacionConfiguracionDto
{
    public string Documento { get; set; } = string.Empty;

    /// <summary>Etiqueta legible del documento ("Orden de compra"). El código nunca se muestra.</summary>
    public string DocumentoDescripcion { get; set; } = string.Empty;

    /// <summary>0 Apagado · 1 Encendido. Ver <c>ModoAprobacion</c>.</summary>
    public short Modo { get; set; }

    /// <summary>D5. Falso: quien crea el documento no puede firmarlo.</summary>
    public bool PermiteAutoaprobacion { get; set; }

    public List<AprobacionNivelConfigDto> Niveles { get; set; } = new();

    /// <summary>
    /// Avisos de configuración que no impiden guardar pero sí operar: un nivel sin aprobadores,
    /// o el control encendido sin ningún nivel. La pantalla los muestra en amarillo.
    /// </summary>
    public List<string> Advertencias { get; set; } = new();
}

/// <summary>
/// Un tramo de autorización con sus aprobadores. La aprobación NO es en cascada: el tramo dice
/// CUÁNTO puede autorizar quien esté en él, no en qué orden hay que firmar.
/// </summary>
public sealed class AprobacionNivelConfigDto
{
    public int Id { get; set; }

    [Range(1, 9, ErrorMessage = "El nivel debe estar entre 1 y 9.")]
    public short Nivel { get; set; }

    [Required(ErrorMessage = "Escriba una descripción para el nivel.")]
    [StringLength(100)]
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>
    /// Monto MÁXIMO que este tramo puede autorizar. <c>null</c> = sin tope.
    /// <para>
    /// No es un umbral de entrada: quien pertenece a este tramo aprueba <b>directamente</b>
    /// cualquier documento cuyo total no lo supere, sin que los tramos inferiores firmen antes.
    /// </para>
    /// </summary>
    [Range(0, 999999999999.99, ErrorMessage = "El límite no puede ser negativo.")]
    public decimal? MontoHasta { get; set; }

    /// <summary>Etiqueta del límite para la pantalla: el monto, o «sin tope».</summary>
    public string LimiteDescripcion => MontoHasta.HasValue ? MontoHasta.Value.ToString("N2") : "Sin tope";

    public bool Activo { get; set; } = true;

    public List<AprobacionAprobadorConfigDto> Aprobadores { get; set; } = new();
}

/// <summary>Quién puede autorizar dentro de un tramo: una persona o un rol completo (D3).</summary>
public sealed class AprobacionAprobadorConfigDto
{
    public int Id { get; set; }
    public int NivelId { get; set; }

    /// <summary>1 Usuario · 2 Rol. Ver <c>TipoAprobador</c>.</summary>
    public short Tipo { get; set; }

    /// <summary>User name (email) o nombre del rol.</summary>
    [Required(ErrorMessage = "Elija un usuario o un rol.")]
    [StringLength(256)]
    public string Valor { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;

    /// <summary>Etiqueta legible del tipo, para el grid.</summary>
    public string TipoDescripcion { get; set; } = string.Empty;
}

/// <summary>
/// Usuario del portal para elegir aprobadores. No expone nada sensible: nombre de usuario y
/// roles, que es lo que hace falta para armar la escalera.
/// </summary>
public sealed class AprobadorUsuarioLookupDto
{
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public List<string> Roles { get; set; } = new();
}
