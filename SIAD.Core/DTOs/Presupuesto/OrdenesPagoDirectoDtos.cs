using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Presupuesto;

public sealed class OrdenPagoDirectoFilterDto
{
    [Range(1, int.MaxValue, ErrorMessage = "El numero de orden debe ser mayor a cero.")]
    public int? NumeroOrden { get; set; }

    [StringLength(20, ErrorMessage = "El codigo del proveedor no puede superar 20 caracteres.")]
    public string? CodigoProveedor { get; set; }

    [StringLength(150, ErrorMessage = "La busqueda no puede superar 150 caracteres.")]
    public string? Search { get; set; }

    public bool IncludeProcessed { get; set; }

    public bool IncludeAnuladas { get; set; }
}

public sealed class OrdenPagoDirectoListItemDto
{
    public int NumeroOrden { get; set; }

    public int? CorrelativoProveedor { get; set; }

    public DateTime FechaCompromiso { get; set; }

    public string Proveedor { get; set; } = string.Empty;

    public string? Rtn { get; set; }

    public string Concepto { get; set; } = string.Empty;

    public decimal Monto { get; set; }

    public string? CuentaContable { get; set; }

    public string? CodigoProveedor { get; set; }

    public string? PagarA { get; set; }

    public bool Procesada { get; set; }

    public bool Anulada { get; set; }

    public int TotalDetalles { get; set; }
}

public sealed class OrdenPagoDirectoDetalleLineaDto
{
    public string? CodigoPresupuestario { get; set; }

    public string? Actividad { get; set; }

    public string? Programa { get; set; }

    public string? ObjetoGasto { get; set; }

    public string? CuentaContable { get; set; }

    public decimal Monto { get; set; }

    public string? Descripcion { get; set; }

    public string? ConceptoDetalle { get; set; }
}

public sealed class OrdenPagoDirectoPartidaContableLineaDto
{
    public string CuentaContable { get; set; } = string.Empty;

    public string? NombreCuenta { get; set; }

    public string? CentroCosto { get; set; }

    public string? Descripcion { get; set; }

    public decimal Debito { get; set; }

    public decimal Credito { get; set; }
}

public sealed class OrdenPagoDirectoPartidaContableDto
{
    public string NumeroPartida { get; set; } = string.Empty;

    public DateTime FechaPartida { get; set; }

    public string? Descripcion { get; set; }

    public IReadOnlyList<OrdenPagoDirectoPartidaContableLineaDto> Lineas { get; set; } = Array.Empty<OrdenPagoDirectoPartidaContableLineaDto>();
}

public sealed class OrdenPagoDirectoDetalleDto
{
    public int NumeroOrden { get; set; }

    public int? CorrelativoProveedor { get; set; }

    public string Proveedor { get; set; } = string.Empty;

    public string? Rtn { get; set; }

    public string Concepto { get; set; } = string.Empty;

    public decimal Monto { get; set; }

    public string? CuentaContable { get; set; }

    public string? CuentaContableProveedor { get; set; }

    public string? CodigoProveedor { get; set; }

    public DateTime? FechaCompromiso { get; set; }

    public string? PagarA { get; set; }

    public bool Procesada { get; set; }

    public bool Anulada { get; set; }

    public IReadOnlyList<OrdenPagoDirectoDetalleLineaDto> Detalles { get; set; } = Array.Empty<OrdenPagoDirectoDetalleLineaDto>();

    public OrdenPagoDirectoPartidaContableDto? PartidaContable { get; set; }
}

public sealed class OrdenPagoDirectoUpsertDto
{
    [Required(ErrorMessage = "La fecha es requerida.")]
    public DateTime? FechaCompromiso { get; set; } = DateTime.Today;

    [StringLength(20, ErrorMessage = "El codigo del proveedor no puede superar 20 caracteres.")]
    public string? CodigoProveedor { get; set; }

    [StringLength(20, ErrorMessage = "El RTN no puede superar 20 caracteres.")]
    public string? Rtn { get; set; }

    [Required(ErrorMessage = "El concepto es requerido.")]
    [StringLength(150, ErrorMessage = "El concepto no puede superar 150 caracteres.")]
    public string Concepto { get; set; } = string.Empty;

    [StringLength(20, ErrorMessage = "La cuenta contable no puede superar 20 caracteres.")]
    public string CuentaContable { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "El campo pagar a no puede superar 100 caracteres.")]
    public string? PagarA { get; set; }

    /// <summary>
    /// Cuando es true (default) se genera la partida contable GEN al crear el compromiso.
    /// Cuando es false el compromiso se crea sin partida, para generarla despues.
    /// </summary>
    public bool GenerarPartida { get; set; } = true;

    public List<OrdenPagoDirectoUpsertLineaDto> Detalles { get; set; } = new();
}

public sealed class OrdenPagoDirectoUpsertLineaDto
{
    [Required(ErrorMessage = "El codigo presupuestario es requerido.")]
    [StringLength(20, ErrorMessage = "El codigo presupuestario no puede superar 20 caracteres.")]
    public string CodigoPresupuestario { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripcion es requerida.")]
    [StringLength(150, ErrorMessage = "La descripcion no puede superar 150 caracteres.")]
    public string Descripcion { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "El concepto detalle no puede superar 100 caracteres.")]
    public string? ConceptoDetalle { get; set; }

    [Range(typeof(decimal), "0.01", "99999999999999.99", ErrorMessage = "El monto debe ser mayor a cero.")]
    public decimal Monto { get; set; }

    public string? Programa { get; set; }

    public string? Actividad { get; set; }

    public string? ObjetoGasto { get; set; }

    public string? CuentaContable { get; set; }
}

public sealed class OrdenPagoDirectoCentroCostoLookupDto
{
    public string CodigoPresupuestario { get; set; } = string.Empty;

    public string? Programa { get; set; }

    public string? Actividad { get; set; }

    public string? ObjetoGasto { get; set; }

    public string? CuentaContable { get; set; }

    public string Display =>
        string.IsNullOrWhiteSpace(ObjetoGasto)
            ? CodigoPresupuestario
            : $"{CodigoPresupuestario} - {ObjetoGasto}";
}

public sealed class OrdenPagoDirectoOperacionResultadoDto
{
    public bool Success { get; set; }

    public int NumeroOrden { get; set; }

    public int? CorrelativoProveedor { get; set; }

    public string Message { get; set; } = string.Empty;
}

public static class OrdenPagoDirectoMetodoPago
{
    public const string Contable = "CONTABLE";
    public const string Deposito = "DEPOSITO";
    public const string Cheque = "CHEQUE";
    public const string Transferencia = "TRANSFERENCIA";

    public static bool EsBancario(string? metodoPago)
    {
        return string.Equals(metodoPago, Deposito, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(metodoPago, Cheque, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(metodoPago, Transferencia, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class ProcesarOrdenPagoDirectoDto
{
    public long? CuentaContraId { get; set; }

    [StringLength(20, ErrorMessage = "El metodo de pago no puede superar 20 caracteres.")]
    public string MetodoPago { get; set; } = string.Empty;

    public List<PartidaLineaOrdenPagoDto> Lineas { get; set; } = new();

    public string Usuario { get; set; } = string.Empty;
}

public sealed class PartidaLineaOrdenPagoDto
{
    public long CuentaId { get; set; }

    public long? BancoCuentaId { get; set; }

    public long? CentroCostoId { get; set; }

    public string Descripcion { get; set; } = string.Empty;

    public decimal Debito { get; set; }

    public decimal Credito { get; set; }
}

public sealed class AnularOrdenPagoDirectoDto
{
    public string? Motivo { get; set; }
}
