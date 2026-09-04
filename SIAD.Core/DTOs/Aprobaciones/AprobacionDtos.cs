using System;
using System.Collections.Generic;

namespace SIAD.Core.DTOs.Aprobaciones;

/// <summary>
/// Un tramo de autorización capaz de aprobar un monto.
/// <para>
/// <b>La aprobación NO es en cascada</b> (regla del 2026-09-01): un tramo no es un escalón que
/// haya que subir, es una capacidad. Quien pertenece a un tramo cuyo límite cubre el total del
/// documento lo aprueba <b>directamente</b>, sin que los tramos inferiores hayan firmado antes.
/// </para>
/// </summary>
public sealed class TramoAutorizacionDto
{
    /// <summary>Orden del tramo (1..9), de menor a mayor capacidad. No impone secuencia de firmas.</summary>
    public short Nivel { get; set; }

    public string Descripcion { get; set; } = string.Empty;

    /// <summary>Monto máximo que este tramo autoriza. <c>null</c> = sin tope.</summary>
    public decimal? MontoHasta { get; set; }

    /// <summary>
    /// Si el tramo tiene al menos un aprobador activo. Un tramo capaz pero vacío no sirve de nada:
    /// es lo que produce el aviso «no hay aprobador con límite suficiente».
    /// </summary>
    public bool TieneAprobadores { get; set; }

    /// <summary>Etiqueta del límite para la pantalla: el monto, o «sin tope».</summary>
    public string LimiteDescripcion => MontoHasta.HasValue ? MontoHasta.Value.ToString("N2") : "Sin tope";
}

/// <summary>La autorización de un documento: quién la dio, con qué capacidad y sobre qué monto.</summary>
public sealed class FirmaAprobacionDto
{
    /// <summary>Tramo con el que se autorizó.</summary>
    public short Nivel { get; set; }

    public string Descripcion { get; set; } = string.Empty;

    /// <summary>Ver <c>EstadoAprobacionNivel</c>: 3 Aprobado · 4 Rechazado.</summary>
    public short Estado { get; set; }

    public string EstadoDescripcion { get; set; } = string.Empty;

    public string? UsuarioFirma { get; set; }
    public DateTime? FechaFirma { get; set; }
    public string? Comentario { get; set; }

    /// <summary>Monto aprobado (snapshot del total del documento).</summary>
    public decimal TotalDocumento { get; set; }

    /// <summary>Límite del tramo usado. <c>null</c> = tramo sin tope.</summary>
    public decimal? LimiteUtilizado { get; set; }
}

/// <summary>Qué pasó al autorizar. Con una sola firma, autorizar cierra el trámite.</summary>
public sealed class FirmaResultadoDto
{
    public short Nivel { get; set; }
    public string DescripcionNivel { get; set; } = string.Empty;

    /// <summary>Límite con el que se autorizó. <c>null</c> = sin tope.</summary>
    public decimal? LimiteUtilizado { get; set; }

    public decimal MontoAprobado { get; set; }

    public short EstadoAnterior { get; set; }
    public short EstadoNuevo { get; set; }
}

/// <summary>Un documento esperando autorización que el usuario de la sesión puede dar.</summary>
public sealed class PendienteAprobacionDto
{
    public long DocumentoId { get; set; }
    public string Documento { get; set; } = string.Empty;
    public string Numero { get; set; } = string.Empty;
    public DateOnly? Fecha { get; set; }

    /// <summary>Contraparte del documento (el proveedor, en la orden de compra).</summary>
    public string? Contraparte { get; set; }

    public decimal Total { get; set; }

    /// <summary>Tramo con el que ESTE usuario lo autorizaría: el más bajo que le alcanza.</summary>
    public short Nivel { get; set; }

    public string DescripcionNivel { get; set; } = string.Empty;
    public string? CreadoPor { get; set; }

    /// <summary>Días que el documento lleva esperando. Ordena la bandeja.</summary>
    public int DiasEnEspera { get; set; }
}

/// <summary>
/// Si un documento en aprobación tiene quién lo autorice. Alimenta el listado sin consultar
/// documento por documento.
/// </summary>
public sealed class CapacidadAprobacionDto
{
    public long DocumentoId { get; set; }

    /// <summary>
    /// Falso = ningún tramo con aprobadores cubre su total. El documento se queda esperando a que
    /// se configure a alguien; no es un error, es un estado que hay que mostrar.
    /// </summary>
    public bool HayAprobadorCapaz { get; set; }

    /// <summary>Límite del tramo más bajo que lo cubre. <c>null</c> con tramo sin tope o sin capacidad.</summary>
    public decimal? LimiteMinimoSuficiente { get; set; }

    public string? TramoMinimo { get; set; }
}

/// <summary>Configuración vigente del control para un documento de la empresa actual.</summary>
public sealed class AprobacionControlDto
{
    public string Documento { get; set; } = string.Empty;

    /// <summary>Ver <c>ModoAprobacion</c>: 0 Apagado · 1 Encendido.</summary>
    public short Modo { get; set; }

    /// <summary>D5. Falso (defecto): quien crea el documento no puede autorizarlo.</summary>
    public bool PermiteAutoaprobacion { get; set; }

    public bool Encendido => Modo == 1;
}

/// <summary>Estado de la autorización de un documento, para la pantalla.</summary>
public sealed class AprobacionEstadoDto
{
    public bool ControlEncendido { get; set; }

    /// <summary>La autorización, si ya la dieron. Null mientras el documento espera.</summary>
    public FirmaAprobacionDto? Firma { get; set; }

    /// <summary>El usuario de la sesión puede autorizarlo ahora mismo.</summary>
    public bool PuedoAutorizar { get; set; }

    /// <summary>Existe algún tramo con aprobadores que cubra el monto.</summary>
    public bool HayAprobadorCapaz { get; set; }

    /// <summary>Tramo más bajo que lo cubre, para decir quién debería autorizarlo.</summary>
    public string? TramoMinimo { get; set; }

    /// <summary>Monto que hay que autorizar.</summary>
    public decimal MontoRequerido { get; set; }
}
