namespace SIAD.Core.DTOs.Presupuesto;

public sealed class ConfiguracionPresupuestoDetalleListItemDto
{
    public string IdPresupuesto { get; set; } = string.Empty;
    public string CuentaContableCodigo { get; set; } = string.Empty;
    public string CuentaContable { get; set; } = string.Empty;
    public decimal ValorProyeccion { get; set; }
    public decimal ValorReal { get; set; }
    public decimal ValorDisponible { get; set; }

    public string DetalleId => $"{IdPresupuesto?.Trim()}|{CuentaContableCodigo?.Trim()}";
    public decimal Variacion => ValorDisponible;
}
