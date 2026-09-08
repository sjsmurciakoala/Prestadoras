using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.ActivosFijos;

public sealed class ActivoFijoFilterDto
{
    public string? Search { get; set; }
    public int? TipoActivoId { get; set; }
    public short? EstadoActivoId { get; set; }
    public int? UbicacionId { get; set; }
    public int? EmpleadoId { get; set; }

    /// <summary>
    /// true = solo los que están incompletos (sin tipo o sin estado). Son los registros
    /// que vienen del histórico de SIMAFI y todavía nadie normalizó.
    /// </summary>
    public bool? SoloPendientes { get; set; }
}

public sealed class ActivoFijoListItemDto
{
    public int Id { get; set; }
    public string CodigoActivo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;

    public int? TipoActivoId { get; set; }
    public string? TipoActivo { get; set; }

    public short? EstadoActivoId { get; set; }
    public string? EstadoActivo { get; set; }
    public bool EstadoEsFinal { get; set; }

    public int? UbicacionId { get; set; }
    public string? Ubicacion { get; set; }

    public int? EmpleadoId { get; set; }
    public string? Responsable { get; set; }

    public string? Marca { get; set; }
    public string? Modelo { get; set; }
    public string? Serie { get; set; }
    public string? Placa { get; set; }

    public DateOnly? FechaCompra { get; set; }
    public decimal ValorCompra { get; set; }
    public decimal ValorRescate { get; set; }
    public decimal DepreciacionAcumulada { get; set; }
    public decimal ValorLibros { get; set; }
    public bool Depreciar { get; set; }

    /// <summary>true = le falta tipo o estado; la ficha lo marca como pendiente de completar.</summary>
    public bool PendienteCompletar { get; set; }
}

/// <summary>Tarjetas de resumen del listado de activos.</summary>
public sealed class ActivoFijoResumenDto
{
    public long TotalActivos { get; set; }
    public long EnPatrimonio { get; set; }
    public long PendientesCompletar { get; set; }
    public long SinResponsable { get; set; }
    public decimal ValorCompra { get; set; }
    public decimal DepreciacionAcumulada { get; set; }
    public decimal ValorLibros { get; set; }
}

public sealed class ActivoFijoEditDto
{
    public int? Id { get; set; }

    /// <summary>Vacío en un registro nuevo: el correlativo lo genera la base de datos.</summary>
    [StringLength(50)]
    public string? CodigoActivo { get; set; }

    [Required(ErrorMessage = "La descripción es obligatoria."), StringLength(254)]
    public string Descripcion { get; set; } = string.Empty;

    [StringLength(55)] public string? Clase { get; set; }

    [Required(ErrorMessage = "Seleccione el tipo de activo.")]
    public int? TipoActivoId { get; set; }

    [Required(ErrorMessage = "Seleccione el estado del activo.")]
    public short? EstadoActivoId { get; set; }

    public int? UbicacionId { get; set; }
    public int? EmpleadoId { get; set; }

    /// <summary>Responsable escrito a mano cuando no está en el catálogo de empleados.</summary>
    [StringLength(80)] public string? Responsable { get; set; }
    [StringLength(50)] public string? CargoResponsable { get; set; }

    [StringLength(20)] public string? CodProveedor { get; set; }
    public long? CentroCostoId { get; set; }

    [StringLength(60)] public string? Marca { get; set; }
    [StringLength(30)] public string? Modelo { get; set; }
    [StringLength(30)] public string? Serie { get; set; }
    [StringLength(30)] public string? Placa { get; set; }
    [StringLength(50)] public string? CodigoBarra { get; set; }
    [StringLength(20)] public string? NumeroFactura { get; set; }

    [Required(ErrorMessage = "La fecha de compra es obligatoria.")]
    public DateOnly? FechaCompra { get; set; }

    public DateOnly? FechaInicioDepreciacion { get; set; }

    /// <summary>Solo lectura: la calcula el procedimiento de guardado.</summary>
    public DateOnly? FechaFinDepreciacion { get; set; }

    [Range(0.01, 9999999999.99, ErrorMessage = "El valor de compra debe ser mayor que cero.")]
    public decimal ValorCompra { get; set; }

    /// <summary>Nulo = se calcula con el porcentaje residual del tipo de activo.</summary>
    [Range(0, 99999999999.99, ErrorMessage = "El valor residual no puede ser negativo.")]
    public decimal? ValorRescate { get; set; }

    /// <summary>Nula = se hereda la del tipo de activo.</summary>
    [Range(0.1, 999, ErrorMessage = "La vida útil debe ser mayor que cero.")]
    public decimal? VidaUtilAnios { get; set; }

    public short? MetodoDepreciacionId { get; set; }
    public bool Depreciar { get; set; } = true;

    /// <summary>Saldo de depreciación con el que entra un activo que ya venía depreciándose.</summary>
    [Range(0, 99999999999.99, ErrorMessage = "La depreciación acumulada no puede ser negativa.")]
    public decimal DepreciacionAcumulada { get; set; }

    // Derivados, solo lectura en el formulario.
    public decimal DepreciacionMensual { get; set; }
    public decimal DepreciacionDiaria { get; set; }
    public decimal ValorLibros { get; set; }

    /// <summary>Cuentas del activo. Vacías = hereda las del tipo.</summary>
    [StringLength(25)] public string? CuentaContable { get; set; }
    [StringLength(25)] public string? CuentaDepreciacion { get; set; }
    [StringLength(25)] public string? CuentaGasto { get; set; }

    [StringLength(60)] public string? PolizaSeguro { get; set; }
    public DateOnly? PolizaVence { get; set; }
    public DateOnly? GarantiaVence { get; set; }

    [StringLength(254)] public string? PropiedadesEspeciales { get; set; }
    [StringLength(254)] public string? Observacion { get; set; }

    // Descripciones para mostrar en la ficha (no se envían al guardar).
    public string? TipoActivo { get; set; }
    public string? EstadoActivo { get; set; }
    public string? Ubicacion { get; set; }
    public string? ProveedorNombre { get; set; }
    public string? CentroCosto { get; set; }
}

public sealed class ActivoAsignacionDto
{
    public int Id { get; set; }
    public DateOnly FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }
    public bool Vigente { get; set; }
    public int? EmpleadoId { get; set; }
    public string? Responsable { get; set; }
    public string? CargoResponsable { get; set; }
    public int? UbicacionId { get; set; }
    public string? Ubicacion { get; set; }
    public string? CentroCosto { get; set; }
    public string? Motivo { get; set; }
    public string? UsuarioCreacion { get; set; }
}

public sealed class ActivoAsignacionRequestDto
{
    [Required(ErrorMessage = "La fecha de la asignación es obligatoria.")]
    public DateOnly? Fecha { get; set; }

    public int? EmpleadoId { get; set; }
    public int? UbicacionId { get; set; }
    public long? CentroCostoId { get; set; }

    [StringLength(254)] public string? Motivo { get; set; }
}

public sealed class ActivoComponenteDto
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "La descripción del componente es obligatoria."), StringLength(200)]
    public string Descripcion { get; set; } = string.Empty;

    [StringLength(60)] public string? Marca { get; set; }
    [StringLength(60)] public string? Modelo { get; set; }
    [StringLength(60)] public string? Serie { get; set; }

    [Range(0.01, 99999999.99, ErrorMessage = "La cantidad debe ser mayor que cero.")]
    public decimal Cantidad { get; set; } = 1;

    [Range(0, 999999999999.99, ErrorMessage = "El valor no puede ser negativo.")]
    public decimal Valor { get; set; }

    [StringLength(254)] public string? Observacion { get; set; }
}
