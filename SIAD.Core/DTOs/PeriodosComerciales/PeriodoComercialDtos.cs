using System;
using System.Collections.Generic;

namespace SIAD.Core.DTOs.PeriodosComerciales;

/// <summary>Período comercial (empresa × año-mes) con sus ciclos (F7).</summary>
public sealed class PeriodoComercialDto
{
    public long PeriodoComercialId { get; set; }
    public int Anio { get; set; }
    public short Mes { get; set; }
    public short StatusId { get; set; }
    public DateTime FechaApertura { get; set; }
    public string? AbiertoPor { get; set; }
    public DateTime? FechaCierre { get; set; }
    public string? CerradoPor { get; set; }
    public IReadOnlyList<PeriodoComercialCicloDto> Ciclos { get; set; } = Array.Empty<PeriodoComercialCicloDto>();
}

public sealed class PeriodoComercialCicloDto
{
    public long PeriodoCicloId { get; set; }
    public string CicloCodigo { get; set; } = string.Empty;
    public short StatusId { get; set; }
    public DateTime FechaApertura { get; set; }
    public DateOnly? FechaLimite { get; set; }
    public DateTime? FechaCierre { get; set; }
    public string? CerradoPor { get; set; }
}

/// <summary>
/// Avance de facturación de una ruta del ciclo. Pendiente = ruta activa con
/// clientes activos y cero facturas de lectura emitidas en el mes.
/// </summary>
public sealed class RutaCicloDto
{
    public string CodRuta { get; set; } = string.Empty;
    public long ClientesActivos { get; set; }
    public long FacturasMes { get; set; }
    public bool Pendiente { get; set; }
}

/// <summary>Ítem de checklist de cierre (comercial o contable).</summary>
public sealed class ChecklistCierreItemDto
{
    public string Item { get; set; } = string.Empty;
    public bool Ok { get; set; }
    public decimal Cantidad { get; set; }
    public string? Detalle { get; set; }
}

public sealed record AbrirPeriodoComercialRequest(int Anio, int Mes, string? Ciclo);

public sealed record CerrarCicloRequest(bool Forzar);

/// <summary>Aviso de períodos para el banner del portal (F7).</summary>
public sealed class AvisoPeriodoDto
{
    public string Tipo { get; set; } = string.Empty;
    public string Severidad { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public long Cantidad { get; set; }
}
