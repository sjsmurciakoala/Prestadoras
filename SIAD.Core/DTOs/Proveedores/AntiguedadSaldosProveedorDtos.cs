namespace SIAD.Core.DTOs.Proveedores;

/// <summary>
/// Una fila del reporte de antigüedad de saldos: un proveedor con su deuda por pagar repartida
/// en seis tramos a una fecha de corte. Mapea 1:1 con <c>fn_prv_antiguedad_saldos</c>.
/// <para>
/// Los tramos NO se solapan y suman <see cref="SaldoTotal"/>. <see cref="Vencido"/> es la suma de
/// los cinco tramos vencidos (de <see cref="Tramo30"/> en adelante); <see cref="PorVencer"/> queda
/// fuera de él. Cuando se consulta «solo lo vencido», <see cref="PorVencer"/> llega en 0.
/// </para>
/// </summary>
public sealed class AntiguedadSaldosProveedorFilaDto
{
    public string CodProveedor { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Rtn { get; set; }

    /// <summary>Código numérico del tipo de proveedor (para agrupar/filtrar). Nunca se muestra crudo.</summary>
    public int? CodTipoProveedor { get; set; }
    public string? TipoNombre { get; set; }

    /// <summary>Cuenta por pagar del proveedor, sin formatear (el formato de empresa se aplica al mostrar).</summary>
    public string? CuentaContable { get; set; }

    /// <summary>Saldo aún dentro de plazo (días vencidos ≤ 0).</summary>
    public decimal PorVencer { get; set; }

    /// <summary>1 – 30 días vencidos.</summary>
    public decimal Tramo30 { get; set; }

    /// <summary>31 – 60 días.</summary>
    public decimal Tramo60 { get; set; }

    /// <summary>61 – 90 días.</summary>
    public decimal Tramo90 { get; set; }

    /// <summary>91 – 120 días.</summary>
    public decimal Tramo120 { get; set; }

    /// <summary>Más de 120 días.</summary>
    public decimal TramoMas120 { get; set; }

    /// <summary>Σ de los cinco tramos vencidos (días &gt; 0).</summary>
    public decimal Vencido { get; set; }

    /// <summary>Total del proveedor: vencido + por vencer (o solo vencido, según el filtro).</summary>
    public decimal SaldoTotal { get; set; }

    public int DocumentosPendientes { get; set; }
}

/// <summary>
/// Totales por tramo del reporte: la fila de pie de la matriz. Se calculan sumando las filas, no
/// con otra consulta, para no re-ejecutar la función.
/// </summary>
public sealed class AntiguedadSaldosTotalesDto
{
    public int Proveedores { get; set; }
    public decimal PorVencer { get; set; }
    public decimal Tramo30 { get; set; }
    public decimal Tramo60 { get; set; }
    public decimal Tramo90 { get; set; }
    public decimal Tramo120 { get; set; }
    public decimal TramoMas120 { get; set; }
    public decimal Vencido { get; set; }
    public decimal SaldoTotal { get; set; }
    public int DocumentosPendientes { get; set; }
}

/// <summary>
/// Respuesta del reporte de antigüedad de saldos: las filas por proveedor, sus totales y el
/// contexto del corte. Todo lo que la pantalla matriz necesita en una sola llamada.
/// </summary>
public sealed class AntiguedadSaldosProveedorDto
{
    /// <summary>Fecha de corte aplicada (hoy si no se pidió otra).</summary>
    public DateOnly Corte { get; set; }

    /// <summary>false = la consulta trajo solo lo vencido (la columna «por vencer» va en 0).</summary>
    public bool IncluyePorVencer { get; set; } = true;

    public IReadOnlyList<AntiguedadSaldosProveedorFilaDto> Filas { get; set; }
        = Array.Empty<AntiguedadSaldosProveedorFilaDto>();

    public AntiguedadSaldosTotalesDto Totales { get; set; } = new();
}
