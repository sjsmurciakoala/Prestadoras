using System;
using System.Collections.Generic;

namespace SIAD.Core.DTOs.Facturacion;

/// <summary>
/// Fila del calendario de facturación (calendariopro): fechas de un ciclo en un
/// año/mes. Fase A del plan apertura-ciclo-único (2026-07-14).
/// </summary>
public sealed class CalendarioCicloDto
{
    public int Ide { get; set; }
    public int Anio { get; set; }
    public int Mes { get; set; }

    /// <summary>Código del ciclo normalizado a 2 dígitos ('01'..'99').</summary>
    public string Ciclo { get; set; } = string.Empty;

    /// <summary>Fecha "al" (corte de carga del ciclo, legado SIMAFI).</summary>
    public DateTime? FechaAl { get; set; }

    /// <summary>Fecha planificada de lectura del ciclo.</summary>
    public DateTime? FechaLectura { get; set; }

    /// <summary>Fecha de facturación del ciclo.</summary>
    public DateTime? FechaFacturacion { get; set; }

    /// <summary>Fecha de refacturación (legado SIMAFI).</summary>
    public DateTime? FechaRefacturacion { get; set; }

    /// <summary>Fecha de vencimiento de las facturas del ciclo.</summary>
    public DateTime? FechaVencimiento { get; set; }

    /// <summary>Plazo en días (viaja a transaccion_abonado.plazo).</summary>
    public int? DiasVencimiento { get; set; }

    public DateTime? FechaFacturacion2 { get; set; }
    public DateTime? FechaVencimiento2 { get; set; }
}

/// <summary>Calendario completo de un año de la empresa.</summary>
public sealed class CalendarioAnioDto
{
    public int Anio { get; set; }
    public List<CalendarioCicloDto> Filas { get; set; } = [];
}

/// <summary>Petición de "copiar año": clona el calendario de un año a otro desplazando las fechas.</summary>
public sealed class CopiarCalendarioAnioRequest
{
    public int AnioOrigen { get; set; }
    public int AnioDestino { get; set; }
}
