using System;
using System.Collections.Generic;

namespace SIAD.Core.DTOs.Aprobaciones;

/// <summary>
/// Un nivel que la escalera exige para un monto dado (D1, acumulativa: se exigen TODOS los
/// niveles activos cuyo <c>monto_desde</c> no supere el total del documento).
/// </summary>
public sealed class NivelExigidoDto
{
    public short Nivel { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public decimal MontoDesde { get; set; }

    /// <summary>
    /// Si el nivel tiene al menos un aprobador activo. Un nivel exigido y sin aprobadores
    /// deja el documento sin poder avanzar: el motor lo rechaza al enviar, con nombre y apellido.
    /// </summary>
    public bool TieneAprobadores { get; set; }
}

/// <summary>Un escalón del flujo vivo de un documento concreto.</summary>
public sealed class FlujoNivelDto
{
    public short Nivel { get; set; }
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>Ver <c>EstadoAprobacionNivel</c>: 1 Bloqueado · 2 Pendiente · 3 Aprobado · 4 Rechazado.</summary>
    public short Estado { get; set; }

    /// <summary>Etiqueta legible del estado. Los códigos nunca se muestran al usuario.</summary>
    public string EstadoDescripcion { get; set; } = string.Empty;

    public string? UsuarioFirma { get; set; }
    public DateTime? FechaFirma { get; set; }
    public string? Comentario { get; set; }

    /// <summary>Monto que se firmó (snapshot). Deja evidencia de QUÉ se aprobó.</summary>
    public decimal TotalDocumento { get; set; }

    /// <summary>
    /// Si el usuario de la sesión puede firmar ESTE nivel ahora mismo. Contempla las cuatro
    /// reglas: nivel pendiente, elegibilidad, autoaprobación y una sola firma por documento.
    /// </summary>
    public bool PuedoFirmar { get; set; }
}

/// <summary>Qué pasó al firmar. Lo consume el enganche del documento para decidir sus efectos.</summary>
public sealed class FirmaResultadoDto
{
    public short NivelFirmado { get; set; }

    /// <summary>
    /// La firma que estrena el flujo. <b>Es la que compromete presupuesto</b> (decisión D2):
    /// el enganche de la orden de compra la usa para llamar a <c>ComprometerOrdenCompraAsync</c>.
    /// </summary>
    public bool EsPrimeraFirma { get; set; }

    /// <summary>No queda ningún nivel por firmar: el documento pasa a Aprobado.</summary>
    public bool FlujoCompleto { get; set; }

    /// <summary>Nivel que queda pendiente, o null si el flujo se completó.</summary>
    public short? NivelPendiente { get; set; }

    public string? DescripcionPendiente { get; set; }
}

/// <summary>Un documento esperando la firma del usuario de la sesión (bandeja "Mis aprobaciones").</summary>
public sealed class PendienteAprobacionDto
{
    public long DocumentoId { get; set; }
    public string Documento { get; set; } = string.Empty;
    public string Numero { get; set; } = string.Empty;
    public DateOnly? Fecha { get; set; }

    /// <summary>Contraparte del documento (el proveedor, en la orden de compra).</summary>
    public string? Contraparte { get; set; }

    public decimal Total { get; set; }
    public short Nivel { get; set; }
    public string DescripcionNivel { get; set; } = string.Empty;
    public string? CreadoPor { get; set; }

    /// <summary>Días que el documento lleva detenido esperando esta firma. Ordena la bandeja.</summary>
    public int DiasEnEspera { get; set; }
}

/// <summary>
/// Progreso de un documento dentro de la escalera, para pintar «En aprobación (2 de 3)» en un
/// listado sin consultar fila por fila.
/// </summary>
public sealed class ProgresoAprobacionDto
{
    public long DocumentoId { get; set; }
    public int Firmados { get; set; }
    public int Total { get; set; }

    /// <summary>Nivel que está esperando firma, o null si ya no queda ninguno.</summary>
    public short? NivelPendiente { get; set; }

    public string? DescripcionPendiente { get; set; }
}

/// <summary>Configuración vigente del control para un documento de la empresa actual.</summary>
public sealed class AprobacionControlDto
{
    public string Documento { get; set; } = string.Empty;

    /// <summary>Ver <c>ModoAprobacion</c>: 0 Apagado · 1 Encendido.</summary>
    public short Modo { get; set; }

    /// <summary>D5. Falso (defecto): quien crea el documento no puede firmarlo.</summary>
    public bool PermiteAutoaprobacion { get; set; }

    public bool Encendido => Modo == 1;
}

/// <summary>Estado completo de la aprobación de un documento, para la pantalla.</summary>
public sealed class AprobacionEstadoDto
{
    public bool ControlEncendido { get; set; }
    public IReadOnlyList<FlujoNivelDto> Niveles { get; set; } = Array.Empty<FlujoNivelDto>();

    /// <summary>Nivel pendiente de firma, o null si no hay flujo abierto.</summary>
    public short? NivelPendiente { get; set; }

    /// <summary>Cuántos niveles ya firmaron, para el badge "En aprobación (2 de 3)".</summary>
    public int Firmados { get; set; }

    public int Total { get; set; }
}
