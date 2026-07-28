using SIAD.Core.Utilities;

namespace SIAD.Core.DTOs.Presupuesto;

public sealed class PresupuestoImpresionDto
{
    public string EmpresaNombre { get; set; } = string.Empty;

    public string? EmpresaRazonSocial { get; set; }

    public string? EmpresaRtn { get; set; }

    public string? EmpresaDireccion { get; set; }

    public string? EmpresaTelefono { get; set; }

    public string? EmpresaEmail { get; set; }

    public byte[]? EmpresaLogo { get; set; }

    public string IdPresupuesto { get; set; } = string.Empty;

    public int RangoPeriodo { get; set; }

    public DateOnly FechaInicia { get; set; }

    public DateOnly FechaFinaliza { get; set; }

    public bool EstadoAprobado { get; set; }

    public decimal ValorGlobal { get; set; }

    public decimal ValorDisponible { get; set; }

    public List<PresupuestoImpresionLineaDto> Detalles { get; set; } = new();

    public string ImpresoPor { get; set; } = string.Empty;

    public string FormatoCuentas { get; set; } = AccountCodeFormatter.DefaultMask;

    public string SeparadorCodigo { get; set; } = AccountCodeFormatter.DefaultSeparator;

    public decimal TotalProyeccion => Detalles.Sum(d => d.ValorProyeccion);

    public decimal TotalReal => Detalles.Sum(d => d.ValorReal);

    public decimal TotalDisponible => Detalles.Sum(d => d.ValorDisponible);

    /// <summary>Presupuesto del encabezado que aun no se ha distribuido en cuentas.</summary>
    public decimal SinDistribuir => ValorGlobal - TotalProyeccion;
}

public sealed class PresupuestoImpresionLineaDto
{
    public string CuentaContableCodigo { get; set; } = string.Empty;

    /// <summary>Cuenta ya formateada con la mascara de la empresa: "codigo - nombre".</summary>
    public string CuentaContable { get; set; } = string.Empty;

    public decimal ValorProyeccion { get; set; }

    public decimal ValorReal { get; set; }

    public decimal ValorDisponible { get; set; }

    public decimal PorcentajeEjecucion => ValorProyeccion == 0m
        ? 0m
        : Math.Round(ValorReal / ValorProyeccion * 100m, 2, MidpointRounding.AwayFromZero);
}
