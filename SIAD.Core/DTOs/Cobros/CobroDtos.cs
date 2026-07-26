using System;
using System.Collections.Generic;

namespace SIAD.Core.DTOs.Cobros;

// Unificación cobranza F2 — DTOs del motor único de cobro (CobroService).
// docs/PLAN_UNIFICACION_COBRANZA_2026-07.md §4.

/// <summary>Una aplicación del cobro a un documento concreto.</summary>
public class CobroAplicacionDto
{
    /// <summary>DocumentoCobroTipo: 1 = factura (único soportado en F2).</summary>
    public short DocumentoTipo { get; set; } = 1;

    public int? FacturaId { get; set; }

    /// <summary>Monto a aplicar al documento. Total = parcial con monto igual al saldo.</summary>
    public decimal Monto { get; set; }
}

public class CobroCrearDto
{
    /// <summary>CanalCobro: 1 caja (exige sesión abierta), 2 banco, 3 app.</summary>
    public short Canal { get; set; } = 1;

    public string ClienteClave { get; set; } = null!;

    public List<CobroAplicacionDto> Aplicaciones { get; set; } = new();

    /// <summary>"EFECTIVO" o "BANCO".</summary>
    public string FormaPago { get; set; } = "EFECTIVO";

    public long? BancoCuentaId { get; set; }

    public string? Banco { get; set; }

    /// <summary>Idempotencia entre canales (referencia bancaria / uuid app). Nullable en ventanilla.</summary>
    public string? ReferenciaExterna { get; set; }

    /// <summary>Recibo pendiente legacy (transaccion_abonado estado 'P') que este cobro liquida.</summary>
    public int? ReciboPendienteId { get; set; }

    public DateTime? FechaPago { get; set; }

    public string? Usuario { get; set; }

    /// <summary>
    /// Compatibilidad del dual-write (F2–F7): tipotransaccion con que se escribe la
    /// fila espejo legacy en transaccion_abonado ("201" captación, "202" abono) para
    /// que arqueos/consultas/reportes existentes sigan cuadrando. Muere en F7.
    /// </summary>
    public string TipoLegacy { get; set; } = "202";

    /// <summary>tipo_partida legacy de la fila espejo ("002" caja/abono, "01" misceláneo).</summary>
    public string TipoPartidaLegacy { get; set; } = "002";

    /// <summary>
    /// true = las líneas contables del cobro van contra la CxC GENERAL en vez de
    /// la analítica por servicio (recibos misceláneos legacy: solo saldan la CxC
    /// que dejó la emisión del recibo). Muere con los recibos 'R' en F3.
    /// </summary>
    public bool CxcGeneral { get; set; }

    /// <summary>Descripción de la fila espejo legacy; si es null el motor usa la genérica.</summary>
    public string? DescripcionLegacy { get; set; }
}

public class CobroResultadoDto
{
    public long PagoId { get; set; }
    public string NumeroRecibo { get; set; } = null!;
    public decimal MontoTotal { get; set; }
    public decimal NuevoSaldoCliente { get; set; }
    public long? PolizaId { get; set; }
    /// <summary>true si la integración de caja está activa pero el comprobante quedó encolado en con_partida_pendiente.</summary>
    public bool PolizaEncolada { get; set; }
    /// <summary>Movimiento bancario (kardex DEP) cuando el cobro fue por banco.</summary>
    public long? BanKardexId { get; set; }
    /// <summary>ide de la fila espejo legacy (dual-write) — para recibos PDF y consultas actuales.</summary>
    public int TransaccionId { get; set; }
    /// <summary>True si la referencia_externa ya estaba aplicada y se devolvió el cobro original.</summary>
    public bool Idempotente { get; set; }
    public List<CobroAplicacionResultadoDto> Aplicaciones { get; set; } = new();
}

public class CobroAplicacionResultadoDto
{
    public int FacturaId { get; set; }
    public string NumFactura { get; set; } = string.Empty;
    public int NumRecibo { get; set; }
    public decimal MontoAplicado { get; set; }
    public decimal SaldoRestante { get; set; }
    public string EstadoFactura { get; set; } = string.Empty; // "C" saldada, "B" parcial
}

public class CobroReversoDto
{
    public long PagoId { get; set; }
    public string Usuario { get; set; } = null!;
    public string Motivo { get; set; } = string.Empty;
}

/// <summary>Fila de "Cobros del día" (fuente: adm_pago — el modelo nuevo).</summary>
public class CobroDelDiaDto
{
    public long PagoId { get; set; }
    public string NumeroRecibo { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public string ClienteClave { get; set; } = string.Empty;
    public string ClienteNombre { get; set; } = string.Empty;
    public decimal MontoTotal { get; set; }
    public string FormaPago { get; set; } = string.Empty;
    public short EstadoId { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string Usuario { get; set; } = string.Empty;
    public string? CajaNombre { get; set; }
    /// <summary>Fila espejo legacy — para reimprimir el recibo PDF actual.</summary>
    public int? TransaccionId { get; set; }
}
