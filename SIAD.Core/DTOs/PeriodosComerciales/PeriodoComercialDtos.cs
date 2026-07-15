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

/// <summary>
/// Resumen de la apertura integral de ciclo (Fase B apertura-ciclo-único):
/// mapea el jsonb de sp_adm_periodo_ciclo_abrir / fn_adm_periodo_ciclo_preview
/// (propiedades snake_case en el JSON).
/// </summary>
public sealed class AperturaCicloResumenDto
{
    public long? PeriodoComercialId { get; set; }
    public long? PeriodoCicloId { get; set; }
    public int Anio { get; set; }
    public int Mes { get; set; }
    public string Ciclo { get; set; } = string.Empty;

    /// <summary>Solo en preview: motivo que impediría abrir (PERIODO_ANTERIOR_ABIERTO, PERIODO_CERRADO, CICLO_CERRADO).</summary>
    public string? Bloqueo { get; set; }

    public DateOnly? FechaLimite { get; set; }

    /// <summary>EXISTENTE | ROLL_OVER | DESDE_CLIENTES | VACIA.</summary>
    public string OrigenPlanilla { get; set; } = string.Empty;

    public long ClientesPlanilla { get; set; }
    public IReadOnlyList<RutaLectorDto> Rutas { get; set; } = Array.Empty<RutaLectorDto>();
    public long RutasSinLector { get; set; }
    public IReadOnlyList<CicloAbiertoRefDto> OtrosCiclosAbiertos { get; set; } = Array.Empty<CicloAbiertoRefDto>();
    public IReadOnlyList<string> Avisos { get; set; } = Array.Empty<string>();
}

/// <summary>Ruta del ciclo con su lector asignado (null = sin lector).</summary>
public sealed class RutaLectorDto
{
    public string Ruta { get; set; } = string.Empty;
    public string? Lector { get; set; }
}

/// <summary>Referencia corta a otro ciclo abierto (aviso OTRO_CICLO_ABIERTO).</summary>
public sealed class CicloAbiertoRefDto
{
    public int Anio { get; set; }
    public int Mes { get; set; }
    public string Ciclo { get; set; } = string.Empty;
}

/// <summary>Próximo ciclo a abrir según el calendario de facturación.</summary>
public sealed class SugerenciaAperturaDto
{
    public int Anio { get; set; }
    public int Mes { get; set; }
    public string Ciclo { get; set; } = string.Empty;
    public DateOnly? FechaLectura { get; set; }
}

/// <summary>Resultado de deshacer una apertura de ciclo.</summary>
public sealed class DeshacerAperturaResultadoDto
{
    public long PlanillaEliminada { get; set; }
    public bool CicloEliminado { get; set; }
    public bool PeriodoEliminado { get; set; }
}

/// <summary>
/// Fila de la planilla de lectura de un ciclo (historicomedicion). Fase C:
/// reemplaza la consulta de la pantalla Auxiliar de Lectura eliminada.
/// Pendiente = sin usuario (la app aún no tomó la lectura).
/// </summary>
public sealed class PlanillaCicloFilaDto
{
    public string Clave { get; set; } = string.Empty;
    public string? Cliente { get; set; }
    public string? Ruta { get; set; }
    public string? Contador { get; set; }
    public string? Secuencia { get; set; }
    public decimal? LecturaAnterior { get; set; }
    public decimal? LecturaActual { get; set; }
    public decimal? Consumo { get; set; }
    public string? Condicion { get; set; }
    public DateTime? FechaLectura { get; set; }
    public string? Usuario { get; set; }
    public string? NumeroFactura { get; set; }
    public bool Pendiente => string.IsNullOrWhiteSpace(Usuario);
}

/// <summary>Aviso de períodos para el banner del portal (F7).</summary>
public sealed class AvisoPeriodoDto
{
    public string Tipo { get; set; } = string.Empty;
    public string Severidad { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public long Cantidad { get; set; }
}
