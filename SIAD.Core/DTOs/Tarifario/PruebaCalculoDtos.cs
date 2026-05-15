using System.Text.Json.Serialization;

namespace SIAD.Core.DTOs.Tarifario;

/// <summary>
/// Request para invocar sp_adm_calcular_factura_lectura desde el portal.
/// </summary>
public record PruebaCalculoRequest(
    int Anio,
    int Mes,
    long ClienteId,
    decimal? LecturaActual,
    string CondicionLectura = "N",
    DateTime? FechaLectura = null,
    decimal? LecturaPromedio = null);

/// <summary>
/// Resultado principal del cálculo (1 fila del SP).
/// </summary>
public class PruebaCalculoResultDto
{
    public long CompanyId { get; set; }
    public long ClienteId { get; set; }
    public string? ClienteClave { get; set; }
    public string? ClienteNombre { get; set; }
    public int Anio { get; set; }
    public int Mes { get; set; }
    public string? Contador { get; set; }
    public string? Ciclo { get; set; }
    public string? Ruta { get; set; }
    public string? Secuencia { get; set; }
    public bool TieneMedidor { get; set; }
    public string? CondicionLecturaAplicada { get; set; }
    public decimal LecturaAnterior { get; set; }
    public decimal LecturaActualEfectiva { get; set; }
    public decimal ConsumoFacturable { get; set; }
    public string? NumeroFactura { get; set; }
    public int? IdCai { get; set; }
    public int? CorrelativoCai { get; set; }
    public DateTime? FechaFactura { get; set; }
    public DateTime? FechaVencimiento { get; set; }
    public decimal SubtotalServicios { get; set; }
    public decimal SubtotalAjustes { get; set; }
    public decimal SaldosAnteriores { get; set; }
    public decimal Recargos { get; set; }
    public decimal TotalFactura { get; set; }
    public decimal Taservi1 { get; set; }
    public decimal Taservi2 { get; set; }
    public decimal Taservi3 { get; set; }
    public decimal Taservi4 { get; set; }
    public string? DetalleServiciosJson { get; set; }
    public string? WarningsJson { get; set; }
    public string? SnapshotContractVersion { get; set; }
}

/// <summary>
/// Un servicio desglosado dentro del detalle JSON.
/// </summary>
public class PruebaCalculoServicioDetalleDto
{
    [JsonPropertyName("servicio_codigo")]
    public string? ServicioCodigo { get; set; }

    [JsonPropertyName("servicio_nombre")]
    public string? ServicioNombre { get; set; }

    [JsonPropertyName("tipo_servicio")]
    public string? TipoServicio { get; set; }

    [JsonPropertyName("origen_calculo")]
    public string? OrigenCalculo { get; set; }

    [JsonPropertyName("cuadro_codigo")]
    public string? CuadroCodigo { get; set; }

    [JsonPropertyName("cuadro_nombre")]
    public string? CuadroNombre { get; set; }

    [JsonPropertyName("cantidad")]
    public decimal? Cantidad { get; set; }

    [JsonPropertyName("monto")]
    public decimal Monto { get; set; }

    [JsonPropertyName("aplica_descuento")]
    public bool AplicaDescuento { get; set; }

    [JsonPropertyName("monto_descuento")]
    public decimal MontoDescuento { get; set; }

    [JsonPropertyName("monto_final")]
    public decimal MontoFinal { get; set; }

    [JsonPropertyName("componentes")]
    public List<PruebaCalculoComponenteDto>? Componentes { get; set; }
}

/// <summary>
/// Componente individual de una regla tarifaria.
/// </summary>
public class PruebaCalculoComponenteDto
{
    [JsonPropertyName("regla_tarifaria_id")]
    public long? ReglaTarifariaId { get; set; }

    [JsonPropertyName("regla_orden")]
    public int? ReglaOrden { get; set; }

    [JsonPropertyName("tipo_regla_codigo")]
    public string? TipoReglaCodigo { get; set; }

    [JsonPropertyName("tipo_regla_nombre")]
    public string? TipoReglaNombre { get; set; }

    [JsonPropertyName("modo_calculo")]
    public string? ModoCalculo { get; set; }

    [JsonPropertyName("consumo")]
    public decimal? Consumo { get; set; }

    [JsonPropertyName("consumo_minimo")]
    public decimal? ConsumoMinimo { get; set; }

    [JsonPropertyName("consumo_maximo")]
    public decimal? ConsumoMaximo { get; set; }

    [JsonPropertyName("monto_fijo")]
    public decimal? MontoFijo { get; set; }

    [JsonPropertyName("monto_unitario")]
    public decimal? MontoUnitario { get; set; }

    [JsonPropertyName("porcentaje")]
    public decimal? Porcentaje { get; set; }

    [JsonPropertyName("unidades_aplicadas")]
    public decimal? UnidadesAplicadas { get; set; }

    [JsonPropertyName("alquiler_aplicado")]
    public decimal? AlquilerAplicado { get; set; }

    [JsonPropertyName("monto_calculado")]
    public decimal? MontoCalculado { get; set; }

    [JsonPropertyName("detalle_calculo")]
    public string? DetalleCalculo { get; set; }

    [JsonPropertyName("monto_referencia")]
    public decimal? MontoReferencia { get; set; }

    [JsonPropertyName("servicio_referencia_codigo")]
    public string? ServicioReferenciaCodigo { get; set; }
}
