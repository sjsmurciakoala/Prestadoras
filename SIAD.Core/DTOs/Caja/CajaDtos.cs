using System;
using System.Collections.Generic;

namespace SIAD.Core.DTOs.Caja;

// ------- Sesión activa -------

// NOTA: los records de DTO deben tener UN solo constructor — System.Text.Json
// no deserializa tipos con varios constructores parametrizados sin
// [JsonConstructor]. No agregar sobrecargas "de compatibilidad" aquí; ante un
// cambio de firma, Rebuild Solution.
public record SesionCajaDto(
    int Id,
    string UsuarioApertura,
    DateTime FechaApertura,
    string? UsuarioCierre,
    DateTime? FechaCierre,
    string Estado,
    decimal? TotalCobrado,
    int? CajaFisicaId = null,
    decimal? MontoApertura = null
);

// ------- Apertura / Cierre -------

// F3: la caja NO se elige — la apertura resuelve la caja ASIGNADA al usuario
// (adm_caja_usuario). CajaFisicaId se conserva solo por compatibilidad de
// contrato y el servicio lo ignora. MontoApertura = fondo inicial del turno.
public record AbrirCajaRequestDto(string UsuarioApertura, int? CajaFisicaId = null, decimal? MontoApertura = null);

// ------- Cajas físicas (adm_caja) -------

public record CajaFisicaDto(int CajaId, string Codigo, string Nombre, bool Activo, bool Ocupada);

// La caja asignada al usuario (F3: la apertura la resuelve el sistema, no un combo)
public record MiCajaDto(int CajaId, string Codigo, string Nombre, bool Activo, bool Ocupada, string? OcupadaPor);

// Mantenimiento de cajas + asignación de cajeros
public record CajaAdminDto(int CajaId, string Codigo, string Nombre, bool Activo, bool Ocupada, IReadOnlyList<string> Asignados);

public record CajaGuardarDto(int? CajaId, string Codigo, string Nombre, bool Activo);

public record AsignarCajeroDto(int CajaId, string Usuario);

// MontoCierre = efectivo contado por el cajero en el arqueo del cierre.
public record CerrarCajaRequestDto(int SesionId, string UsuarioCierre, string? Observacion, decimal? MontoCierre = null);

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
    decimal? TotalCobrado,
    decimal? MontoApertura = null,
    decimal? MontoCierre = null,
    string? Observacion = null
);

// ------- Response genérico -------

public record CajaResponseDto(bool Success, string Message, object? Data = null);
