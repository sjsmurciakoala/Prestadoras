using System;

namespace SIAD.Core.DTOs.Ordenes;

public sealed record OrdenTrabajoAdjuntoDto(
    int Id,
    string? Nombre,
    string? Tipo,
    string? Latitud,
    string? Longitud,
    DateTime? FechaInicio,
    DateTime? FechaFin,
    DateTime? FechaSincronizacion,
    bool TieneContenido,
    int TamanoBytes);

/// <summary>
/// Contenido binario de un adjunto. Se sirve por un endpoint aparte y no dentro del
/// detalle de la orden: las fotos pesan decenas de KB cada una y meterlas en el JSON
/// del detalle multiplicaria el peso de una pantalla que casi siempre se abre solo
/// para ver los datos de la orden.
/// </summary>
public sealed record OrdenTrabajoAdjuntoContenidoDto(
    byte[] Contenido,
    string ContentType,
    string NombreArchivo);
