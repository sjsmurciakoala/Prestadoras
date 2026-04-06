using System;

namespace SIAD.Core.DTOs.Clientes;

public record ClienteFotoMedidorHeaderDto(
    string CodigoCliente,
    string NombreCliente,
    string? DireccionCliente,
    string? NumeroMedidor,
    string? Ciclo,
    string? Ruta,
    string? Secuencia);

public record ClienteFotoMedidorItemDto(
    int Ide,
    int Ano,
    int Mes,
    string? Usuario);
