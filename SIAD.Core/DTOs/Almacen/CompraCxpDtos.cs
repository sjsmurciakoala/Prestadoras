using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Retenciones;

namespace SIAD.Core.DTOs.Almacen;

/// <summary>Filtro de la vista de pagos a proveedores (cuentas por pagar de compra).</summary>
public sealed class CompraCxpFilterDto
{
    public string? CodProveedor { get; set; }
    public short? EstadoId { get; set; }
    public bool SoloVencidas { get; set; }
    public DateOnly? VenceDesde { get; set; }
    public DateOnly? VenceHasta { get; set; }
    public string? Search { get; set; }
}

/// <summary>Fila de la vista de pagos: una cuenta por pagar con su saldo.</summary>
public sealed class CompraCxpListItemDto
{
    public int Id { get; set; }
    public int CompraHdrId { get; set; }

    /// <summary>Correlativo de la factura (alm_compra_hdr.numero).</summary>
    public int Numero { get; set; }

    public string CodProveedor { get; set; } = string.Empty;
    public string? Proveedor { get; set; }
    public string? NumeroFacturaSar { get; set; }
    public DateOnly Fecha { get; set; }
    public DateOnly FechaVencimiento { get; set; }

    public short CondicionPago { get; set; }
    public string CondicionPagoDescripcion => CondicionPagoCompra.Describir(CondicionPago);

    public decimal Monto { get; set; }
    public decimal Abonado { get; set; }
    public decimal Saldo { get; set; }

    public short EstadoId { get; set; }
    public string EstadoDescripcion => EstadoCompraCxp.Describir(EstadoId);
}

/// <summary>Datos de un pago/abono a una CxP.</summary>
public sealed class CompraCxpAbonoUpsertDto
{
    [Range(0.01, 9_999_999_999d, ErrorMessage = "El monto del pago debe ser mayor que cero.")]
    public decimal Monto { get; set; }

    [Required(ErrorMessage = "El método de pago es obligatorio.")]
    [StringLength(20)]
    public string MetodoPago { get; set; } = MetodoPagoCompra.Efectivo;

    /// <summary>Cuenta bancaria de origen. Obligatoria si el método es bancario (cheque/transferencia).</summary>
    public long? BancoCuentaId { get; set; }

    /// <summary>Cuenta contable de contrapartida para pagos en EFECTIVO (la caja de donde sale el
    /// dinero). Se usa como HABER del asiento del pago cuando la contabilidad de compras está activa.</summary>
    public long? CuentaContableId { get; set; }

    public DateOnly? Fecha { get; set; }

    [StringLength(300)]
    public string? Observaciones { get; set; }

    /// <summary>
    /// Retenciones aplicadas en este pago (en paralelo, patrón del flujo de compromisos). El
    /// <see cref="Monto"/> es el BRUTO aplicado a la deuda; el banco/caja paga el NETO = Monto −
    /// Σ Retenciones.Monto. El backend arma la partida (retención al HABER) y el registro fiscal, y
    /// valida Σ Monto == Σ crédito de esas cuentas. Vacía = pago sin retención (comportamiento previo).
    /// </summary>
    public List<RetencionAplicadaDto> Retenciones { get; set; } = new();
}

/// <summary>Resultado de registrar o anular un pago.</summary>
public sealed class CompraCxpAbonoResultadoDto
{
    public bool Success { get; set; }
    public int CxpId { get; set; }
    public int NumeroAbono { get; set; }
    public decimal Saldo { get; set; }
    public short EstadoId { get; set; }
    public string EstadoDescripcion => EstadoCompraCxp.Describir(EstadoId);
    public decimal? NumeroCheque { get; set; }
    public long? ChequeId { get; set; }
    public string? Message { get; set; }

    /// <summary>Suma retenida en este pago (0 si no hubo). El neto pagado al banco/caja = Monto − Retenido.</summary>
    public decimal Retenido { get; set; }
}

/// <summary>Cuenta bancaria de origen para el combo de pago.</summary>
public sealed class CuentaBancariaLookupDto
{
    public long BancoCuentaId { get; init; }
    public long? BancoId { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string? Banco { get; init; }
    public string? NumeroCuenta { get; init; }
    public string? Moneda { get; init; }
    public string Display => string.IsNullOrWhiteSpace(NumeroCuenta) ? Nombre : $"{Nombre} · {NumeroCuenta}";
}

/// <summary>Un abono ya aplicado, para el historial de la CxP.</summary>
public sealed class CompraCxpAbonoListItemDto
{
    public int NumeroAbono { get; set; }
    public DateOnly Fecha { get; set; }
    public decimal Monto { get; set; }

    /// <summary>Suma retenida en este pago (0 si no hubo). El neto pagado al banco/caja = Monto − Retenido.</summary>
    public decimal Retenido { get; set; }

    public string? MetodoPago { get; set; }
    public string? NumCheque { get; set; }
    public string Estado { get; set; } = "V";
    public string EstadoDescripcion => Estado == "A" ? "Anulado" : "Vigente";

    /// <summary>Id de la partida contable del pago; null si el pago no generó asiento.</summary>
    public long? PartidaId { get; set; }
}

/// <summary>Cuenta del plan que permite movimiento, para el combo de contrapartida en efectivo.</summary>
public sealed class CompraCuentaContableLookupDto
{
    public long AccountId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Display => $"{Code} · {Name}";
}

/// <summary>Estado de la contabilidad de compras (activo_compras) de la empresa, para la UI de pago.</summary>
public sealed class CompraContabilidadEstadoDto
{
    public bool ContabilidadActiva { get; set; }
}

/// <summary>La partida contable de un pago (cabecera + líneas Debe/Haber), para mostrarla en pantalla.</summary>
public sealed class CompraCxpPartidaDto
{
    public long PolizaId { get; set; }
    public string? PolizaNumero { get; set; }
    public DateOnly Fecha { get; set; }
    public string? Descripcion { get; set; }
    public decimal TotalDebe { get; set; }
    public decimal TotalHaber { get; set; }
    public List<CompraCxpPartidaLineaDto> Lineas { get; set; } = new();
}

public sealed class CompraCxpPartidaLineaDto
{
    public string Cuenta { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public decimal Debe { get; set; }
    public decimal Haber { get; set; }
}
