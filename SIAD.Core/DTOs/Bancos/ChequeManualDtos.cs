using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Bancos;

/// <summary>
/// Entrada de la emision de un cheque MANUAL (suelto): no nace de un compromiso ni de
/// una orden de pago. Genera movimiento bancario + partida contable + cheque con el
/// siguiente numero de la cuenta (origen <see cref="ChequeOrigen.Manual"/>, estado 'E').
/// </summary>
public sealed class ChequeManualCreateDto : IValidatableObject
{
    [Range(typeof(long), "1", "9223372036854775807", ErrorMessage = "Seleccione una cuenta bancaria válida.")]
    public long BancoCuentaId { get; set; }

    [Required(ErrorMessage = "El tipo de transacción es obligatorio.")]
    [StringLength(3, ErrorMessage = "El tipo de transacción no puede superar 3 caracteres.")]
    public string IdTipoTransaccion { get; set; } = string.Empty;

    [Required(ErrorMessage = "La fecha de emisión es obligatoria.")]
    public DateOnly FechaEmision { get; set; }

    [Required(ErrorMessage = "El beneficiario es obligatorio.")]
    [StringLength(200, ErrorMessage = "El beneficiario no puede superar 200 caracteres.")]
    public string Beneficiario { get; set; } = string.Empty;

    [Required(ErrorMessage = "El concepto es obligatorio.")]
    [StringLength(250, ErrorMessage = "El concepto no puede superar 250 caracteres.")]
    public string Concepto { get; set; } = string.Empty;

    [Required(ErrorMessage = "La referencia es obligatoria.")]
    [StringLength(100, ErrorMessage = "La referencia no puede superar 100 caracteres.")]
    public string Referencia { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "999999999.99", ErrorMessage = "El monto debe ser un número positivo.")]
    public decimal Monto { get; set; }

    [Range(typeof(decimal), "0.0001", "999999999.9999", ErrorMessage = "La tasa de cambio debe ser mayor a cero.")]
    public decimal TasaCambio { get; set; } = 1m;

    /// <summary>Codigo del proveedor cuando el cheque se emite desde el modulo de proveedores.</summary>
    [StringLength(50, ErrorMessage = "El código del proveedor no puede superar 50 caracteres.")]
    public string? ProveedorCodigo { get; set; }

    /// <summary>
    /// Descripcion de la LINEA CONTABLE del banco (la del haber). Si viene vacia se usa
    /// el concepto del cheque.
    /// </summary>
    [StringLength(500, ErrorMessage = "La descripción de la línea del banco no puede superar 500 caracteres.")]
    public string? BancoDescripcion { get; set; }

    /// <summary>Referencia de la linea contable del banco. Si viene vacia se usa la referencia del cheque.</summary>
    [StringLength(120, ErrorMessage = "La referencia de la línea del banco no puede superar 120 caracteres.")]
    public string? BancoReferencia { get; set; }

    /// <summary>Contrapartidas contables del cheque (el banco es la contracuenta automatica).</summary>
    public List<BanTransaccionContraLineaDto> Lineas { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var lineasValidas = Lineas?.Where(l => l is not null && l.CuentaId > 0 && l.Monto > 0).ToList()
            ?? new List<BanTransaccionContraLineaDto>();

        if (lineasValidas.Count == 0)
        {
            yield return new ValidationResult(
                "Agregue al menos una línea de detalle contable.", new[] { nameof(Lineas) });
            yield break;
        }

        var total = lineasValidas.Sum(l => l.Monto);
        if (Math.Abs(total - Monto) > 0.01m)
        {
            yield return new ValidationResult(
                "El monto del cheque no coincide con el total del detalle contable.",
                new[] { nameof(Monto), nameof(Lineas) });
        }

        if (FechaEmision > DateOnly.FromDateTime(DateTime.Today))
        {
            yield return new ValidationResult(
                "No se permiten cheques con fecha futura.", new[] { nameof(FechaEmision) });
        }
    }
}

/// <summary>Resultado de la emision de un cheque manual.</summary>
public sealed class ChequeManualResultadoDto
{
    public long BanKardexId { get; set; }

    public long? ChequeId { get; set; }

    public decimal? NumeroCheque { get; set; }

    public decimal SaldoResultante { get; set; }

    public string Mensaje { get; set; } = string.Empty;
}
