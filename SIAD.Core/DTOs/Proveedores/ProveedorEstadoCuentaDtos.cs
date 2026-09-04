using SIAD.Core.Constants;

namespace SIAD.Core.DTOs.Proveedores;

/// <summary>
/// Resumen del estado de cuenta de un proveedor: lo que se le debe, cuánto está vencido
/// y cómo se reparte por antigüedad. Mapea 1:1 con <c>fn_prv_estado_cuenta_resumen</c>.
/// </summary>
public sealed class ProveedorEstadoCuentaResumenDto
{
    /// <summary>Σ saldos de los documentos pendientes (compras + compromisos).</summary>
    public decimal SaldoTotal { get; set; }

    /// <summary>Parte del saldo cuya fecha de vencimiento ya pasó.</summary>
    public decimal SaldoVencido { get; set; }

    /// <summary>Parte del saldo aún dentro de plazo.</summary>
    public decimal SaldoPorVencer { get; set; }

    /// <summary>Subconjunto de <see cref="SaldoPorVencer"/> que vence dentro de 7 días.</summary>
    public decimal SaldoVence7Dias { get; set; }

    public int DocumentosPendientes { get; set; }

    /// <summary>Fecha del documento pendiente más antiguo (null si no debe nada).</summary>
    public DateOnly? DocumentoMasAntiguo { get; set; }

    public decimal? UltimoPagoMonto { get; set; }
    public DateOnly? UltimoPagoFecha { get; set; }

    // Tramos de antigüedad. Corriente = aún no vence.
    public decimal AntiguedadCorriente { get; set; }
    public decimal Antiguedad30 { get; set; }
    public decimal Antiguedad60 { get; set; }
    public decimal Antiguedad90 { get; set; }
    public decimal AntiguedadMas90 { get; set; }
}

/// <summary>
/// Un documento por pagar del proveedor. Mapea 1:1 con <c>fn_prv_estado_cuenta_documentos</c>.
/// </summary>
public sealed class ProveedorEstadoCuentaDocumentoDto
{
    /// <summary>Módulo del que nace: ver <see cref="OrigenDocumentoProveedor"/>.</summary>
    public short Origen { get; set; }

    /// <summary>Id dentro de su módulo: <c>alm_compra_cxp.id</c> o <c>numero_orden</c>.</summary>
    public long DocumentoId { get; set; }

    /// <summary>Número visible: factura SAR o <c>OPD-00412</c>.</summary>
    public string NumeroDocumento { get; set; } = string.Empty;

    public DateOnly Fecha { get; set; }
    public DateOnly FechaVencimiento { get; set; }
    public string Concepto { get; set; } = string.Empty;

    public decimal Monto { get; set; }
    public decimal Abonado { get; set; }
    public decimal Saldo { get; set; }

    /// <summary>Días transcurridos desde el vencimiento. Negativo = aún no vence.</summary>
    public int DiasVencido { get; set; }

    /// <summary>Escala de <see cref="EstadoCompraCxp"/>: 1 Pendiente, 2 Parcial, 3 Pagada.</summary>
    public short EstadoId { get; set; }

    public string OrigenDescripcion => OrigenDocumentoProveedor.Describir(Origen);
    public string EstadoDescripcion => EstadoCompraCxp.Describir(EstadoId);

    /// <summary>Vencido de verdad, es decir con saldo y fuera de plazo.</summary>
    public bool Vencido => DiasVencido > 0 && Saldo > 0;

    /// <summary>
    /// Clave única para el grid: <see cref="DocumentoId"/> solo no basta porque las dos ramas
    /// numeran por separado (una CxP y un compromiso pueden compartir id).
    /// </summary>
    public string ClaveGrid => $"{Origen}-{DocumentoId}";

    // El motor de expresiones de DevExpress no formatea DateOnly, así que el reporte bindea
    // estas dos y no las fechas crudas.
    public string FechaTexto => Fecha.ToString("dd/MM/yyyy");
    public string VencimientoTexto => FechaVencimiento.ToString("dd/MM/yyyy");

    /// <summary>Días de vencimiento en texto, ya legible en el PDF.</summary>
    public string DiasTexto => DiasVencido > 0
        ? $"{DiasVencido}"
        : DiasVencido == 0 ? "hoy" : $"({-DiasVencido})";
}

/// <summary>
/// Una línea del libro de movimientos. Mapea 1:1 con <c>fn_prv_estado_cuenta_movimientos</c>.
/// <para>
/// El saldo corrido es el acumulado histórico real del proveedor, no el del rango
/// consultado: filtrar por fechas cambia qué filas se ven, no el saldo de cada una.
/// </para>
/// </summary>
public sealed class ProveedorEstadoCuentaMovimientoDto
{
    public DateOnly Fecha { get; set; }

    /// <summary>Ver <see cref="OrigenDocumentoProveedor"/>.</summary>
    public short Origen { get; set; }

    /// <summary>Ver <see cref="TipoMovimientoProveedor"/>: 1 cargo, 2 abono.</summary>
    public short Tipo { get; set; }

    public string NumeroDocumento { get; set; } = string.Empty;
    public string? Referencia { get; set; }

    public decimal Cargo { get; set; }
    public decimal Abono { get; set; }
    public decimal SaldoCorrido { get; set; }

    public string OrigenDescripcion => OrigenDocumentoProveedor.Describir(Origen);
    public string TipoDescripcion => TipoMovimientoProveedor.Describir(Tipo);
}

/// <summary>
/// Cabecera de la pantalla: identidad del proveedor + su resumen. Los documentos y los
/// movimientos se piden aparte para poder filtrarlos sin recargar esto.
/// </summary>
public sealed class ProveedorEstadoCuentaDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Rtn { get; set; }
    public string? TipoNombre { get; set; }
    public string? CuentaContable { get; set; }
    public bool Activo { get; set; }

    /// <summary>Fecha de corte aplicada (hoy si no se pidió otra).</summary>
    public DateOnly Corte { get; set; }

    public ProveedorEstadoCuentaResumenDto Resumen { get; set; } = new();
}
