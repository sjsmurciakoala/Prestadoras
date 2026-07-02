using System;
using System.Collections.Generic;

namespace SIAD.Core.DTOs.Cobranza;

public record GenerarCorteMasivoRequest(
    int PeriodoAnio,        // año del período de facturación
    int PeriodoMes,         // mes del período (1-12)
    int? CicloId,           // filtro opcional por ciclo
    string? BarrioCodigo,   // filtro opcional por barrio
    int? CategoriaId,       // filtro opcional por categoría
    decimal ValorMinimo,    // saldo mínimo adeudado (0 = sin límite inferior)
    int DiasCorte);         // días a sumar a la fecha actual para la OT

public record CorteMasivoHdrDto(
    int Id,
    string Correlativo,
    DateOnly FechaGeneracion,
    string? Criterio,
    int? PeriodoAnio,
    int? PeriodoMes,
    int TotalClientes,
    string Estado);

public record CorteMasivoDtlDto(
    string ClienteClave,
    string? NombreCliente,
    decimal? SaldoAdeudado,
    int? DiasSinPago,
    bool Pagado,
    int? OrdenId,
    int? OrdenNumero);

public record CorteMasivoDetalleDto(
    CorteMasivoHdrDto Encabezado,
    IReadOnlyList<CorteMasivoDtlDto> Clientes);
