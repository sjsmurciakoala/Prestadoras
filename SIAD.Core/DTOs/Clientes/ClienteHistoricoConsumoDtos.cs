using System;

namespace SIAD.Core.DTOs.Clientes;

public record ClienteHistoricoConsumoHeaderDto(
    DateTime Desde,
    DateTime Hasta,
    string CodigoCliente,
    string NombreCliente,
    string? DireccionCliente,
    string? NumeroMedidor,
    string? Catastro,
    string? Diametro,
    string? Categoria,
    string? TipoTarifa,
    string? Ciclo,
    string? Ruta,
    string? Secuencia);

public record ClienteHistoricoConsumoItemDto(
    string Periodo,
    string? DisplayFechaLectAnterior,
    string? DisplayFechaLectActual,
    decimal LecturaAnterior,
    decimal LecturaActual,
    decimal Consumo,
    decimal Total,
    string? Condicion,
    string? TipoLectura,
    string? Ajuste,
    string? Observacion);

public record ClienteHistoricoConsumoResponseDto(
    ClienteHistoricoConsumoHeaderDto? Header,
    IReadOnlyList<ClienteHistoricoConsumoItemDto> Items);

// NOTE: Added paged response to support server-side paging + remote summary for historico consumo.
public record ClienteHistoricoConsumoPagedResponseDto(
    ClienteHistoricoConsumoHeaderDto? Header,
    IReadOnlyList<ClienteHistoricoConsumoItemDto> Items,
    int TotalCount,
    decimal TotalConsumo);
