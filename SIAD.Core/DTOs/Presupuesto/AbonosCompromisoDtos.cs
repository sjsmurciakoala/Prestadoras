using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Presupuesto;

/// <summary>
/// Entrada para registrar un abono (pago parcial o total) contra un compromiso /
/// orden de pago directo. El monto se valida en el servicio contra el saldo pendiente
/// (0 &lt; monto &lt;= saldo). El company_id NO se toma de aqui: lo resuelve
/// ICurrentCompanyService y lo estampa SiadDbContext.
/// </summary>
public sealed class AbonoCompromisoUpsertDto
{
    [Range(typeof(decimal), "0.01", "99999999999999.99", ErrorMessage = "El monto del abono debe ser mayor a cero.")]
    public decimal Monto { get; set; }

    /// <summary>Uno de <see cref="OrdenPagoDirectoMetodoPago"/> (CONTABLE/DEPOSITO/CHEQUE/TRANSFERENCIA).</summary>
    [Required(ErrorMessage = "El metodo de pago es requerido.")]
    [StringLength(20, ErrorMessage = "El metodo de pago no puede superar 20 caracteres.")]
    public string MetodoPago { get; set; } = string.Empty;

    /// <summary>Cuenta contable de contrapartida (origen del pago). AccountId de con_plan_cuentas.</summary>
    public long? CuentaContraId { get; set; }

    /// <summary>Cuenta bancaria (ban_cuenta) origen cuando el metodo es bancario; el servicio la resuelve si va null.</summary>
    public long? BancoCuentaId { get; set; }

    /// <summary>Cuenta destino del proveedor (prv_proveedor_cuenta_bancaria). Informativo para el pago bancario.</summary>
    public long? CuentaDestinoProveedorId { get; set; }

    /// <summary>Distribucion de contracuentas del abono; si va vacia el servicio arma una linea con CuentaContraId.</summary>
    public List<PartidaLineaOrdenPagoDto> Lineas { get; set; } = new();

    public DateTime? Fecha { get; set; }

    [StringLength(60, ErrorMessage = "El usuario no puede superar 60 caracteres.")]
    public string Usuario { get; set; } = string.Empty;
}

/// <summary>Fila de un abono ya registrado, para listarlo en la vista de saldo.</summary>
public sealed class AbonoCompromisoListItemDto
{
    public long AbonoId { get; set; }

    public int NumeroAbono { get; set; }

    public DateTime Fecha { get; set; }

    public decimal Monto { get; set; }

    public string MetodoPago { get; set; } = string.Empty;

    /// <summary>'V' vigente, 'A' anulado.</summary>
    public string Estado { get; set; } = string.Empty;

    /// <summary>Numero de partida (con_partida_hdr.poliza_number) derivado por JOIN por partida_id.</summary>
    public string? NumeroPartida { get; set; }

    public long? PartidaId { get; set; }

    /// <summary>True solo para el ultimo abono vigente (mayor numero_abono con 'V'): unico anulable. Lo calcula el servicio.</summary>
    public bool PuedeAnular { get; set; }
}

/// <summary>
/// Saldo de un compromiso con el desglose de sus abonos. Saldo y Abonado se calculan al
/// vuelo (no se persisten). Pagado = Saldo &lt;= 0 y no anulado.
/// </summary>
public sealed class CompromisoSaldoDto
{
    public int NumeroOrden { get; set; }

    public decimal Monto { get; set; }

    public decimal Abonado { get; set; }

    public decimal Saldo { get; set; }

    public bool Pagado { get; set; }

    public bool Anulado { get; set; }

    /// <summary>Texto para UI: "Anulado" / "Pagado" / "Abonado parcial" / "Pendiente".</summary>
    public string EstadoTexto { get; set; } = string.Empty;

    public IReadOnlyList<AbonoCompromisoListItemDto> Abonos { get; set; } = Array.Empty<AbonoCompromisoListItemDto>();
}

/// <summary>
/// Resultado de RegistrarAbonoAsync y AnularAbonoAsync (servicio, controller, cliente, tests).
/// NO se reutiliza OrdenPagoDirectoOperacionResultadoDto para abonos.
/// </summary>
public sealed class AbonoCompromisoResultadoDto
{
    public bool Success { get; set; }

    public int NumeroOrden { get; set; }

    public int NumeroAbono { get; set; }

    public string? NumeroPartida { get; set; }

    public decimal Saldo { get; set; }

    public bool Pagado { get; set; }

    public string Message { get; set; } = string.Empty;
}

/// <summary>Datos de impresion del comprobante de un abono (compone el encabezado del compromiso).</summary>
public sealed class CompromisoAbonoImpresionDto
{
    /// <summary>Encabezado empresa + compromiso, reutilizado de GetDatosImpresionAsync.</summary>
    public OrdenPagoDirectoImpresionDto Base { get; set; } = new();

    public int NumeroAbono { get; set; }

    public DateTime FechaAbono { get; set; }

    public decimal MontoAbono { get; set; }

    public decimal SaldoAnterior { get; set; }

    public decimal SaldoRestante { get; set; }

    public string MetodoPago { get; set; } = string.Empty;

    public string? NumeroPartida { get; set; }

    /// <summary>'V' vigente, 'A' anulado.</summary>
    public string Estado { get; set; } = "V";
}
