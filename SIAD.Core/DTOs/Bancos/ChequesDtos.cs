using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Bancos;

/// <summary>Origen de un cheque en la bitacora ban_cheque.</summary>
public static class ChequeOrigen
{
    public const string Procesar = "PROCESAR";
    public const string Abono = "ABONO";
    public const string Transaccion = "TRANSACCION";
    public const string Manual = "MANUAL";
}

/// <summary>Accion de un evento en la bitacora ban_cheque_bitacora (append-only).</summary>
public static class ChequeAccion
{
    public const string Emitido = "EMITIDO";
    public const string Anulado = "ANULADO";
}

/// <summary>Fila de la bitacora de cheques (consulta).</summary>
public sealed class ChequeListItemDto
{
    public long ChequeId { get; set; }

    public long BancoCuentaId { get; set; }

    public string NumeroCuenta { get; set; } = string.Empty;

    public string BancoNombre { get; set; } = string.Empty;

    public decimal NumeroCheque { get; set; }

    public DateTime FechaEmision { get; set; }

    public decimal Monto { get; set; }

    public string? Beneficiario { get; set; }

    public string? Concepto { get; set; }

    public string Origen { get; set; } = string.Empty;

    public string? OrigenDocumento { get; set; }

    public long? BanKardexId { get; set; }

    /// <summary>'E' emitido, 'A' anulado.</summary>
    public string Estado { get; set; } = string.Empty;

    public string UsuarioEmision { get; set; } = string.Empty;

    public string? MotivoAnulacion { get; set; }

    public string? UsuarioAnulacion { get; set; }

    public DateTime? FechaAnulacion { get; set; }
}

/// <summary>Filtros de la consulta de la bitacora.</summary>
public sealed class ChequeFilterDto
{
    /// <summary>Filtra por banco de la cuenta (0 = cuentas sin banco asociado).</summary>
    public long? BancoId { get; set; }

    public long? BancoCuentaId { get; set; }

    /// <summary>'E' | 'A' | null (todos).</summary>
    public string? Estado { get; set; }

    public DateTime? Desde { get; set; }

    public DateTime? Hasta { get; set; }

    public decimal? NumeroCheque { get; set; }
}

/// <summary>Fila de la bitacora de eventos de cheques (ban_cheque_bitacora, consulta).</summary>
public sealed class ChequeBitacoraListItemDto
{
    public long BitacoraId { get; set; }

    public long ChequeId { get; set; }

    public long BancoCuentaId { get; set; }

    public string NumeroCuenta { get; set; } = string.Empty;

    public string BancoNombre { get; set; } = string.Empty;

    public decimal NumeroCheque { get; set; }

    /// <summary>'EMITIDO' | 'ANULADO'.</summary>
    public string Accion { get; set; } = string.Empty;

    public DateTime Fecha { get; set; }

    public string Usuario { get; set; } = string.Empty;

    public decimal Monto { get; set; }

    public string? Beneficiario { get; set; }

    public string? Concepto { get; set; }

    public string? Motivo { get; set; }

    public string Origen { get; set; } = string.Empty;

    public string? OrigenDocumento { get; set; }

    public long? BanKardexId { get; set; }
}

/// <summary>Filtros de la consulta de la bitacora de eventos.</summary>
public sealed class ChequeBitacoraFilterDto
{
    /// <summary>Filtra por banco de la cuenta (0 = cuentas sin banco asociado).</summary>
    public long? BancoId { get; set; }

    public long? BancoCuentaId { get; set; }

    /// <summary>'EMITIDO' | 'ANULADO' | null (todos).</summary>
    public string? Accion { get; set; }

    public DateTime? Desde { get; set; }

    public DateTime? Hasta { get; set; }

    public decimal? NumeroCheque { get; set; }
}

/// <summary>Estado de la numeracion de una cuenta (para "Se emitira el cheque N° X").</summary>
public sealed class ProximoChequeDto
{
    public long BancoCuentaId { get; set; }

    public decimal ProximoCheque { get; set; }

    public decimal ChequeMaximo { get; set; }

    public bool Agotado { get; set; }
}

/// <summary>Entrada de la anulacion manual de un numero (cheque danado).</summary>
public sealed class AnularNumeroChequeDto
{
    [Required(ErrorMessage = "El motivo es obligatorio.")]
    [StringLength(250, ErrorMessage = "El motivo no puede superar 250 caracteres.")]
    public string Motivo { get; set; } = string.Empty;
}
