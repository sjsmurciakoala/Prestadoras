namespace SIAD.Core.DTOs.Presupuesto;

/// <summary>
/// Una partida presupuestaria y su ejecución: cuánto se presupuestó, cuánto está comprometido en
/// órdenes aprobadas, cuánto se ejecutó y cuánto queda.
/// <para>Fuente: <c>vw_pst_ejecucion_presupuestaria</c>.</para>
/// </summary>
public sealed class PresupuestoEjecucionItemDto
{
    public string IdPresupuesto { get; set; } = string.Empty;
    public DateOnly FechaInicia { get; set; }
    public DateOnly FechaFinaliza { get; set; }
    public bool EstadoAprobado { get; set; }

    /// <summary>Código de la cuenta, sin formato. La pantalla lo formatea con <c>IAccountFormatService</c>.</summary>
    public string ConCuentaCode { get; set; } = string.Empty;
    public string? CuentaNombre { get; set; }
    public string? CuentaTipo { get; set; }

    /// <summary>false = la cuenta no participa del control aunque aparezca presupuestada.</summary>
    public bool CuentaPresupuestable { get; set; }

    public decimal Presupuesto { get; set; }
    public decimal Comprometido { get; set; }
    public decimal Ejecutado { get; set; }
    public decimal Pagado { get; set; }
    public decimal Disponible { get; set; }

    public decimal? PctEjecucion { get; set; }
    public decimal? PctCompromiso { get; set; }

    /// <summary>(Comprometido + ejecutado) sobre lo presupuestado. Es el porcentaje que importa mirar.</summary>
    public decimal? PctUtilizado { get; set; }

    /// <summary>Clave para el drill-down al kardex de esta partida.</summary>
    public string Clave => $"{IdPresupuesto}|{ConCuentaCode}";
}

/// <summary>Filtros del reporte de ejecución.</summary>
public sealed class PresupuestoEjecucionFilterDto
{
    public string? IdPresupuesto { get; set; }
    public string? Search { get; set; }

    /// <summary>true = solo las cuentas marcadas como presupuestables (las que el control mira).</summary>
    public bool SoloPresupuestables { get; set; }

    /// <summary>true = solo partidas con movimiento (comprometido, ejecutado o pagado distinto de cero).</summary>
    public bool SoloConMovimiento { get; set; }
}

/// <summary>
/// Orden de compra aprobada que todavía retiene presupuesto comprometido. Es la herramienta
/// operativa para depurar órdenes viejas que nadie cerró.
/// <para>Fuente: <c>vw_pst_compromiso_saldo</c> con estado vigente.</para>
/// </summary>
public sealed class PresupuestoCompromisoPendienteDto
{
    public long Id { get; set; }
    public string IdPresupuesto { get; set; } = string.Empty;
    public string ConCuentaCode { get; set; } = string.Empty;
    public string? CuentaNombre { get; set; }
    public string? CentroCostoCodigo { get; set; }
    public string? CentroCostoNombre { get; set; }

    public string DocumentoTipo { get; set; } = string.Empty;
    public long DocumentoId { get; set; }
    public string? DocumentoNumero { get; set; }
    public DateOnly Fecha { get; set; }

    public string? CodProveedor { get; set; }
    public string? Proveedor { get; set; }
    public short? OrdenEstado { get; set; }

    public decimal MontoComprometido { get; set; }
    public decimal MontoDevengado { get; set; }
    public decimal MontoLiberado { get; set; }
    public decimal SaldoComprometido { get; set; }

    /// <summary>Días desde la fecha del compromiso. Un saldo viejo es candidato a cerrarse.</summary>
    public int DiasAntiguedad { get; set; }
}

/// <summary>Filtros de compromisos pendientes.</summary>
public sealed class PresupuestoCompromisoFilterDto
{
    public string? IdPresupuesto { get; set; }
    public string? ConCuentaCode { get; set; }
    public string? CodProveedor { get; set; }
    public string? Search { get; set; }

    /// <summary>Solo compromisos con al menos estos días de antigüedad. Para depurar lo viejo.</summary>
    public int? DiasMinimos { get; set; }
}

/// <summary>
/// Un renglón del kardex presupuestario: qué documento movió la partida, cuánto, y cómo quedaron
/// los saldos antes y después.
/// <para>Fuente: <c>vw_pst_movimiento_detalle</c>.</para>
/// </summary>
public sealed class PresupuestoMovimientoDto
{
    public long Id { get; set; }
    public string IdPresupuesto { get; set; } = string.Empty;
    public string ConCuentaCode { get; set; } = string.Empty;
    public string? CuentaNombre { get; set; }
    public string? CentroCostoCodigo { get; set; }

    public short TipoMovimiento { get; set; }
    public string TipoMovimientoNombre { get; set; } = string.Empty;

    /// <summary>Efecto con signo sobre cada eje. Los calcula la vista para no replicar la tabla de tipos.</summary>
    public decimal EfectoComprometido { get; set; }
    public decimal EfectoEjecutado { get; set; }
    public decimal EfectoPagado { get; set; }

    public string Modulo { get; set; } = string.Empty;
    public string DocumentoTipo { get; set; } = string.Empty;
    public long DocumentoId { get; set; }
    public string? DocumentoNumero { get; set; }
    public long? OrdenCompraId { get; set; }
    public int? OrdenCompraNumero { get; set; }
    public string? Proveedor { get; set; }

    public DateOnly Fecha { get; set; }
    public decimal Monto { get; set; }

    public decimal ComprometidoAnterior { get; set; }
    public decimal ComprometidoPosterior { get; set; }
    public decimal EjecutadoAnterior { get; set; }
    public decimal EjecutadoPosterior { get; set; }
    public decimal DisponibleAnterior { get; set; }
    public decimal DisponiblePosterior { get; set; }

    /// <summary>true = pasó excediendo el disponible, en modo Advertencia.</summary>
    public bool Excedio { get; set; }

    /// <summary>1 Vigente · 9 Reversado.</summary>
    public short Estado { get; set; }

    public string? Observacion { get; set; }
    public string Usuario { get; set; } = string.Empty;
    public string? UsuarioAprobo { get; set; }
    public DateTime FechaRegistro { get; set; }
}

/// <summary>
/// Modo del control presupuestario de un módulo. Es el interruptor: mientras esté en Apagado, el
/// portal se comporta como antes de que existiera el control.
/// </summary>
public sealed class PresupuestoControlConfigDto
{
    public string Modulo { get; set; } = string.Empty;

    /// <summary>0 Apagado · 1 Advertencia · 2 Bloqueo.</summary>
    public short Modo { get; set; }

    public bool ExigePresupuestoAprobado { get; set; } = true;

    /// <summary>Variación admitida entre lo comprometido en la orden y lo facturado.</summary>
    public decimal ToleranciaPct { get; set; }

    /// <summary>0 Prohíbe la compra sin orden · 1 Consume disponible · 2 Solo advierte.</summary>
    public short PermiteDevengoSinOc { get; set; } = 1;

    public string ModuloDescripcion => PresupuestoControlModulos.Describir(Modulo);
    public string ModoDescripcion => PresupuestoControlModos.Describir(Modo);
}

/// <summary>Nombres visibles de los módulos del control. Los códigos nunca llegan al usuario.</summary>
public static class PresupuestoControlModulos
{
    public const string ComprasOc = "COMPRAS_OC";
    public const string ComprasFactura = "COMPRAS_FACTURA";
    public const string Proveedores = "PROVEEDORES";
    public const string Bancos = "BANCOS";

    public static string Describir(string? modulo) => modulo switch
    {
        ComprasOc => "Aprobación de órdenes de compra",
        ComprasFactura => "Factura de compra (recepción)",
        Proveedores => "Compromisos a proveedores",
        Bancos => "Movimientos bancarios",
        _ => modulo ?? "—"
    };

    public static string Detalle(string? modulo) => modulo switch
    {
        ComprasOc => "Valida el disponible al aprobar la orden y compromete el presupuesto.",
        ComprasFactura => "Devenga la factura contra el compromiso de la orden, o contra el disponible si no hay orden.",
        Proveedores => "Reservado: los compromisos a proveedores todavía no pasan por este motor.",
        Bancos => "Reservado: los créditos bancarios todavía no pasan por este motor.",
        _ => string.Empty
    };

    /// <summary>
    /// Los módulos que hoy están efectivamente conectados al motor. Proveedores y Bancos existen en
    /// la tabla pero siguen usando su propio mecanismo (fase F8 del diseño).
    /// </summary>
    public static bool EstaConectado(string? modulo)
        => modulo is ComprasOc or ComprasFactura;
}

/// <summary>Nombres visibles de los modos.</summary>
public static class PresupuestoControlModos
{
    public const short Apagado = 0;
    public const short Advertencia = 1;
    public const short Bloqueo = 2;

    public static string Describir(short modo) => modo switch
    {
        Apagado => "Apagado",
        Advertencia => "Advertencia",
        Bloqueo => "Bloqueo",
        _ => "—"
    };

    public static string Detalle(short modo) => modo switch
    {
        Apagado => "No consulta presupuesto. El portal se comporta como si el control no existiera.",
        Advertencia => "Registra el consumo y deja pasar aunque exceda. Sirve para observar un ciclo antes de bloquear.",
        Bloqueo => "Rechaza la operación cuando no hay disponible.",
        _ => string.Empty
    };
}
