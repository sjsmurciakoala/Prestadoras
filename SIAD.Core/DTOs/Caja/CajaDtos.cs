using System;
using System.Collections.Generic;

namespace SIAD.Core.DTOs.Caja;

// ------- Sesión activa -------

public record SesionCajaDto(
    int Id,
    string UsuarioApertura,
    DateTime FechaApertura,
    string? UsuarioCierre,
    DateTime? FechaCierre,
    string Estado,
    decimal? TotalCobrado
);

// ------- Apertura / Cierre -------

public record AbrirCajaRequestDto(string UsuarioApertura);

public record CerrarCajaRequestDto(int SesionId, string UsuarioCierre, string? Observacion);

// ------- Resumen del día -------

public record ResumenCajaDto(
    decimal TotalCreditos,
    decimal TotalDebitos,
    int CantidadTransacciones,
    IReadOnlyList<ResumenPorTipoDto> PorTipo
);

// Agrupa transacciones por tipotransaccion de transaccion_abonado
public record ResumenPorTipoDto(string Tipo, decimal Creditos, decimal Debitos, int Cantidad);

// ------- Historial -------

public record HistorialCierreDto(
    int SesionId,
    DateTime FechaApertura,
    DateTime? FechaCierre,
    string UsuarioApertura,
    string? UsuarioCierre,
    decimal? TotalCobrado
);

// ------- Response genérico -------

public record CajaResponseDto(bool Success, string Message, object? Data = null);
