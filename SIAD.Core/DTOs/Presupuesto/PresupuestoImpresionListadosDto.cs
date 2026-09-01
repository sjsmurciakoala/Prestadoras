using SIAD.Core.DTOs.Almacen;

namespace SIAD.Core.DTOs.Presupuesto;

/// <summary>
/// Datos de impresión del reporte de <b>ejecución presupuestaria</b>: una fila por partida con sus
/// cuatro montos y el disponible, más los totales del pie. Reutiliza
/// <see cref="ComprobanteAlmacenImpresionBase"/> para el encabezado de empresa y el pie, como el
/// resto de los reportes programáticos del repositorio.
/// </summary>
public sealed class PresupuestoEjecucionImpresionDto : ComprobanteAlmacenImpresionBase
{
    public string Titulo { get; set; } = "EJECUCIÓN PRESUPUESTARIA";

    /// <summary>Fecha en que se generó el reporte (va bajo el título).</summary>
    public DateOnly Corte { get; set; }

    /// <summary>Descripción legible de los filtros aplicados. Vacío si no se filtró nada.</summary>
    public string FiltroTexto { get; set; } = string.Empty;

    /// <summary>Filas del cuadro, en el mismo orden que la pantalla.</summary>
    public List<PresupuestoEjecucionItemDto> Items { get; set; } = new();

    public decimal TotalPresupuesto { get; set; }
    public decimal TotalComprometido { get; set; }
    public decimal TotalEjecutado { get; set; }
    public decimal TotalPagado { get; set; }
    public decimal TotalDisponible { get; set; }
}

/// <summary>
/// Datos de impresión del reporte de <b>compromisos pendientes</b>: las órdenes aprobadas que
/// todavía retienen presupuesto sin ejecutar.
/// </summary>
public sealed class PresupuestoCompromisosImpresionDto : ComprobanteAlmacenImpresionBase
{
    public string Titulo { get; set; } = "COMPROMISOS PRESUPUESTARIOS PENDIENTES";

    public DateOnly Corte { get; set; }

    public string FiltroTexto { get; set; } = string.Empty;

    public List<PresupuestoCompromisoPendienteDto> Items { get; set; } = new();

    public decimal TotalComprometido { get; set; }
    public decimal TotalDevengado { get; set; }
    public decimal TotalSaldo { get; set; }
}
