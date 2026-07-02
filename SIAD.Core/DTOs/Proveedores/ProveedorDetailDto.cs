using System;
using System.Collections.Generic;

namespace SIAD.Core.DTOs.Proveedores;

public record ProveedorDetailDto(
    string Codigo,
    string Nombre,
    string? RazonSocial,
    string? Rtn,
    string? Direccion,
    string? NombreContacto,
    string? Telefono,
    string? Fax,
    string? Email,
    string? PaginaWeb,
    string? CuentaContable,
    IReadOnlyList<ProveedorCuentaBancariaDto> CuentasBancarias,
    int CodTipoProveedor,
    string? TipoProveedor,
    double? ComprasAcum,
    double? ComprasDolares,
    double? SaldoAnterior,
    double? SaldoAnteriorDolares,
    double? SaldoActual,
    double? SaldoActualDolares,
    DateTime FechaCreacion,
    DateTime? FechaModificacion,
    bool Activo);
