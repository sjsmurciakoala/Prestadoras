namespace SIAD.Core.DTOs.Presupuesto;

public sealed class ConfiguracionPresupuestoListItemDto
{
    public string IdPresupuesto { get; set; } = string.Empty;
    public string CuentaContableCodigo { get; set; } = string.Empty;
    public string CuentaContable { get; set; } = string.Empty;
    public decimal ValorGlobal { get; set; }
    public decimal ValorDisponible { get; set; }
    public decimal ValorProyeccion { get; set; }
    public decimal ValorReal { get; set; }
    public int RangoPeriodo { get; set; }
    public DateOnly FechaInicia { get; set; }
    public DateOnly FechaFinaliza { get; set; }
    public int AnioPresupuesto => FechaInicia.Year;
    public bool EstadoAprobado { get; set; }

    public string RegistroId => $"{IdPresupuesto?.Trim()}|{CuentaContableCodigo?.Trim()}";
    public decimal Variacion => ValorProyeccion - ValorReal;
}
