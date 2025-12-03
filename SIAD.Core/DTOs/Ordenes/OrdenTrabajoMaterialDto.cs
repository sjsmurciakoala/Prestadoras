using System;

namespace SIAD.Core.DTOs.Ordenes;

public sealed record OrdenTrabajoMaterialDto(
    int Id,
    string? CuentaContable,
    string? CodigoProducto,
    string? Descripcion,
    int? Cantidad,
    DateTime? Fecha);
