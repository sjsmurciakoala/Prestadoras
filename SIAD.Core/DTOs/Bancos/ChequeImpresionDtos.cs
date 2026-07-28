using System;
using System.Collections.Generic;
using SIAD.Core.Utilities;

namespace SIAD.Core.DTOs.Bancos;

/// <summary>
/// Datos para imprimir un cheque en sus dos formatos migrados de la APP Espanola:
/// el comprobante interno contable (COMPAGOL) y el cheque para el cliente (COMPAGOLG).
/// </summary>
public sealed class ChequeImpresionDto
{
    // ---- Empresa (cfg_company) ----
    public string EmpresaNombre { get; set; } = string.Empty;

    public string? EmpresaRazonSocial { get; set; }

    public string? EmpresaRtn { get; set; }

    public string? EmpresaDireccion { get; set; }

    public string? EmpresaTelefono { get; set; }

    public string? EmpresaEmail { get; set; }

    public byte[]? EmpresaLogo { get; set; }

    /// <summary>Ciudad de emision impresa en el cheque (de con_empresa_configuracion.ciudad).</summary>
    public string Ciudad { get; set; } = string.Empty;

    // ---- Cheque (ban_cheque) ----
    public long ChequeId { get; set; }

    public decimal NumeroCheque { get; set; }

    public DateTime FechaEmision { get; set; }

    public string? Beneficiario { get; set; }

    public string? Concepto { get; set; }

    public decimal Monto { get; set; }

    /// <summary>Monto en letras ya cerrado con la moneda (p. ej. "... CON 00/100 LEMPIRAS").</summary>
    public string MontoEnLetras { get; set; } = string.Empty;

    public string BancoNombre { get; set; } = string.Empty;

    public string NumeroCuenta { get; set; } = string.Empty;

    /// <summary>'E' emitido, 'A' anulado.</summary>
    public string Estado { get; set; } = "E";

    public string? MotivoAnulacion { get; set; }

    /// <summary>Documento que origino el cheque (p. ej. la orden de pago); "Orden de Pago No.".</summary>
    public string? OrigenDocumento { get; set; }

    // ---- Comprobante contable (con_partida_hdr/dtl ligada por partida_id) ----
    public string? ComprobanteNumero { get; set; }

    public DateTime? PartidaFecha { get; set; }

    public string? PartidaDescripcion { get; set; }

    public IReadOnlyList<ChequeDistribucionLineaDto> Distribucion { get; set; } = new List<ChequeDistribucionLineaDto>();

    // ---- Metadatos de impresion ----
    public string ImpresoPor { get; set; } = string.Empty;

    public string FormatoCuentas { get; set; } = AccountCodeFormatter.DefaultMask;

    public string SeparadorCodigo { get; set; } = AccountCodeFormatter.DefaultSeparator;

    public bool Anulado => string.Equals(Estado, "A", StringComparison.OrdinalIgnoreCase);

    public bool TieneDistribucion => Distribucion.Count > 0;
}

/// <summary>Una linea de la distribucion contable del cheque (cargo/credito por cuenta).</summary>
public sealed class ChequeDistribucionLineaDto
{
    public string CodigoCuenta { get; set; } = string.Empty;

    public string NombreCuenta { get; set; } = string.Empty;

    public string? CentroCosto { get; set; }

    public string? Descripcion { get; set; }

    /// <summary>Cargo (debe).</summary>
    public decimal Cargo { get; set; }

    /// <summary>Credito (haber).</summary>
    public decimal Credito { get; set; }
}
