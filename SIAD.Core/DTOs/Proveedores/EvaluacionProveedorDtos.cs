using System.ComponentModel.DataAnnotations;
using SIAD.Core.Constants;

namespace SIAD.Core.DTOs.Proveedores;

/// <summary>
/// Período de evaluación: un rango de fechas con nombre. La periodicidad (trimestral, mensual,
/// anual) es un dato, no estructura — ver <c>Database/2026-08-14_prv_evaluacion.sql</c>.
/// </summary>
public sealed class EvaluacionPeriodoDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public DateOnly FechaDesde { get; set; }
    public DateOnly FechaHasta { get; set; }
    public short Estado { get; set; } = EstadoEvaluacionPeriodo.Abierto;
    public DateTime? FechaCalculo { get; set; }
    public string? UsuarioCalculo { get; set; }
    public DateTime? FechaCierre { get; set; }
    public string? UsuarioCierre { get; set; }

    /// <summary>Cuántos proveedores tiene evaluados. 0 = nunca se calculó.</summary>
    public int Evaluaciones { get; set; }

    public string EstadoDescripcion => EstadoEvaluacionPeriodo.Descripcion(Estado);
    public bool Cerrado => Estado == EstadoEvaluacionPeriodo.Cerrado;

    /// <summary>Texto para combos: "2026-T2 — Trimestre II 2026 (01/04/2026 al 30/06/2026)".</summary>
    public string Display => $"{Codigo} — {Nombre} ({FechaDesde:dd/MM/yyyy} al {FechaHasta:dd/MM/yyyy})";
}

/// <summary>Alta o edición de un período.</summary>
public sealed class EvaluacionPeriodoUpsertDto
{
    [Required(ErrorMessage = "El código del período es obligatorio.")]
    [StringLength(20, ErrorMessage = "El código no puede superar 20 caracteres.")]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre del período es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede superar 100 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    public DateOnly FechaDesde { get; set; }
    public DateOnly FechaHasta { get; set; }
}

/// <summary>Criterio del catálogo (lectura). El mantenimiento es F3.</summary>
public sealed class EvaluacionCriterioDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Peso { get; set; }
    public short Origen { get; set; } = OrigenCriterioEvaluacion.Automatico;

    /// <summary>Métrica que lo alimenta (ver <see cref="MetricaEvaluacion"/>). NULL = manual.</summary>
    public string? Metrica { get; set; }

    public decimal? Meta { get; set; }
    public decimal? Parametro { get; set; }
    public short Orden { get; set; }
    public bool Activo { get; set; } = true;

    public string OrigenDescripcion => OrigenCriterioEvaluacion.Descripcion(Origen);
    public bool EsManual => Origen == OrigenCriterioEvaluacion.Manual;
}

/// <summary>Alta o edición de un criterio del catálogo (F3).</summary>
public sealed class EvaluacionCriterioUpsertDto
{
    [Required(ErrorMessage = "El código del criterio es obligatorio.")]
    [StringLength(20, ErrorMessage = "El código no puede superar 20 caracteres.")]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre del criterio es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede superar 100 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(300, ErrorMessage = "La descripción no puede superar 300 caracteres.")]
    public string? Descripcion { get; set; }

    [Range(0, 100, ErrorMessage = "El peso debe estar entre 0 y 100.")]
    public decimal Peso { get; set; }

    public short Origen { get; set; } = OrigenCriterioEvaluacion.Automatico;

    /// <summary>Métrica que lo alimenta. Obligatoria si es automático; ignorada si es manual.</summary>
    public string? Metrica { get; set; }

    [Range(0, 100, ErrorMessage = "La meta debe estar entre 0 y 100.")]
    public decimal? Meta { get; set; }

    /// <summary>Parámetro de la métrica (hoy: tolerancia de precio en %).</summary>
    public decimal? Parametro { get; set; }

    public short Orden { get; set; }
    public bool Activo { get; set; } = true;
}

/// <summary>Alta o edición de una clase de la escala (F3).</summary>
public sealed class EvaluacionClaseUpsertDto
{
    [Required(ErrorMessage = "El código de la clase es obligatorio.")]
    [StringLength(10, ErrorMessage = "El código no puede superar 10 caracteres.")]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre de la clase es obligatorio.")]
    [StringLength(60, ErrorMessage = "El nombre no puede superar 60 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(300, ErrorMessage = "La descripción no puede superar 300 caracteres.")]
    public string? Descripcion { get; set; }

    [Range(0, 100, ErrorMessage = "El puntaje inicial debe estar entre 0 y 100.")]
    public decimal PuntajeDesde { get; set; }

    [Range(0, 100, ErrorMessage = "El puntaje final debe estar entre 0 y 100.")]
    public decimal PuntajeHasta { get; set; }

    public short Orden { get; set; }
    public bool Activo { get; set; } = true;
}

/// <summary>Clase de la escala (A/B/C/D).</summary>
public sealed class EvaluacionClaseDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal PuntajeDesde { get; set; }
    public decimal PuntajeHasta { get; set; }
    public short Orden { get; set; }

    /// <summary>"A · Confiable" para chips y combos.</summary>
    public string Display => $"{Codigo} · {Nombre}";
}

/// <summary>
/// Resultado de un criterio dentro de una evaluación. Trae numerador y denominador, no sólo el
/// porcentaje: la ficha muestra la evidencia ("12 de 16 órdenes a tiempo").
/// </summary>
public sealed class EvaluacionCriterioResultadoDto
{
    public string CriterioCodigo { get; set; } = string.Empty;
    public string CriterioNombre { get; set; } = string.Empty;

    /// <summary>Peso configurado (snapshot al calcular).</summary>
    public decimal Peso { get; set; }

    public short Origen { get; set; }
    public string? Metrica { get; set; }
    public decimal? Numerador { get; set; }
    public decimal? Denominador { get; set; }

    /// <summary>Porcentaje de logro 0–100. NULL = el criterio no tuvo datos en el período.</summary>
    public decimal? Logro { get; set; }

    /// <summary>Peso realmente aplicado tras repartir el de los criterios sin datos.</summary>
    public decimal? PesoEfectivo { get; set; }

    public decimal? Puntos { get; set; }
    public string? Detalle { get; set; }
    public string? UsuarioCaptura { get; set; }
    public DateTime? FechaCaptura { get; set; }

    public string OrigenDescripcion => OrigenCriterioEvaluacion.Descripcion(Origen);
    public bool EsManual => Origen == OrigenCriterioEvaluacion.Manual;

    /// <summary>Sin datos en el período: no puntúa y su peso se redistribuyó.</summary>
    public bool SinDatos => Logro is null;
}

/// <summary>Fila del ranking del período.</summary>
public sealed class EvaluacionRankingItemDto
{
    public int Id { get; set; }
    public string CodProveedor { get; set; } = string.Empty;
    public string? ProveedorNombre { get; set; }
    public decimal? Puntaje { get; set; }
    public string? ClaseCodigo { get; set; }
    public string? ClaseNombre { get; set; }
    public decimal ComprasPeriodo { get; set; }
    public int Recepciones { get; set; }
    public int Ordenes { get; set; }
    public short Estado { get; set; } = EstadoEvaluacionProveedor.Calculada;

    /// <summary>Puntaje del período anterior (por fecha), para la tendencia. NULL si no hay.</summary>
    public decimal? PuntajeAnterior { get; set; }

    /// <summary>Un resultado por criterio, en el orden del catálogo: son las columnas del grid.</summary>
    public List<EvaluacionCriterioResultadoDto> Criterios { get; set; } = new();

    public string EstadoDescripcion => EstadoEvaluacionProveedor.Descripcion(Estado);

    /// <summary>Diferencia contra el período anterior. NULL si falta alguno de los dos.</summary>
    public decimal? Variacion => Puntaje.HasValue && PuntajeAnterior.HasValue
        ? Math.Round(Puntaje.Value - PuntajeAnterior.Value, 2)
        : null;

    // El motor de expresiones de DevExpress no formatea nullables ni recorre listas, así que el
    // reporte comparativo bindea estos tres textos ya armados y no las propiedades crudas.

    /// <summary>"93.02" o "sin datos".</summary>
    public string PuntajeTexto => Puntaje.HasValue ? Puntaje.Value.ToString("N2") : "sin datos";

    /// <summary>"A · Confiable" o "—".</summary>
    public string ClaseTexto => string.IsNullOrWhiteSpace(ClaseCodigo)
        ? "—"
        : $"{ClaseCodigo} · {ClaseNombre}";

    /// <summary>Logro de cada criterio en una línea: "Cumpl. 75 · Compl. 94 · Calid. —".</summary>
    public string DesgloseTexto
    {
        get
        {
            if (Criterios.Count == 0) return "—";

            var partes = new List<string>(Criterios.Count);
            foreach (var c in Criterios)
            {
                var nombre = c.CriterioNombre.Length <= 6 ? c.CriterioNombre : c.CriterioNombre[..5] + ".";
                partes.Add($"{nombre} {(c.SinDatos ? "—" : c.Logro!.Value.ToString("N0"))}");
            }

            return string.Join(" · ", partes);
        }
    }
}

/// <summary>Un período del historial del proveedor (mini gráfico de la ficha).</summary>
public sealed class EvaluacionHistorialItemDto
{
    public int PeriodoId { get; set; }
    public string PeriodoCodigo { get; set; } = string.Empty;
    public DateOnly FechaDesde { get; set; }
    public decimal? Puntaje { get; set; }
    public string? ClaseCodigo { get; set; }
}

/// <summary>Ficha completa: identidad, desglose por criterio e historial.</summary>
public sealed class EvaluacionFichaDto
{
    public int Id { get; set; }
    public int PeriodoId { get; set; }
    public string PeriodoCodigo { get; set; } = string.Empty;
    public string PeriodoNombre { get; set; } = string.Empty;
    public DateOnly FechaDesde { get; set; }
    public DateOnly FechaHasta { get; set; }
    public bool PeriodoCerrado { get; set; }

    public string CodProveedor { get; set; } = string.Empty;
    public string? ProveedorNombre { get; set; }
    public string? Rtn { get; set; }
    public string? TipoNombre { get; set; }

    public decimal? Puntaje { get; set; }
    public string? ClaseCodigo { get; set; }
    public string? ClaseNombre { get; set; }
    public decimal ComprasPeriodo { get; set; }
    public int Recepciones { get; set; }
    public int Ordenes { get; set; }
    public short Estado { get; set; } = EstadoEvaluacionProveedor.Calculada;
    public string? Observaciones { get; set; }

    public List<EvaluacionCriterioResultadoDto> Criterios { get; set; } = new();
    public List<EvaluacionHistorialItemDto> Historial { get; set; } = new();

    public string EstadoDescripcion => EstadoEvaluacionProveedor.Descripcion(Estado);

    /// <summary>Criterios que no puntuaron por falta de datos: la ficha los señala.</summary>
    public int CriteriosSinDatos => Criterios.Count(c => c.SinDatos);
}

/// <summary>Captura de un criterio manual (y del plan de acción) desde la ficha.</summary>
public sealed class EvaluacionCapturaDto
{
    /// <summary>Criterio manual a calificar. Vacío = sólo se actualizan las observaciones.</summary>
    public string? CriterioCodigo { get; set; }

    /// <summary>Logro 0–100. NULL borra la calificación y devuelve el criterio a "sin datos".</summary>
    [Range(0, 100, ErrorMessage = "La calificación debe estar entre 0 y 100.")]
    public decimal? Logro { get; set; }

    [StringLength(1000, ErrorMessage = "Las observaciones no pueden superar 1000 caracteres.")]
    public string? Observaciones { get; set; }
}

/// <summary>Filtro del ranking.</summary>
public sealed class EvaluacionFilterDto
{
    public string? Search { get; set; }
    public string? ClaseCodigo { get; set; }

    /// <summary>Deja fuera a los proveedores con compras por debajo de este monto en el período.</summary>
    public decimal? ComprasMinimas { get; set; }
}

/// <summary>Resultado de recalcular un período.</summary>
public sealed class EvaluacionCalculoResultadoDto
{
    public int PeriodoId { get; set; }
    public int Evaluados { get; set; }
    public decimal? PromedioPuntaje { get; set; }
    public DateTime FechaCalculo { get; set; }

    /// <summary>Criterios que quedaron sin datos para TODOS los proveedores: la pantalla lo avisa.</summary>
    public List<string> CriteriosSinDatos { get; set; } = new();
}
