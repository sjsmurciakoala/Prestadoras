using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SIAD.Core.DTOs.Common;

namespace SIAD.Core.DTOs.CaptacionPagos;

public class CaptacionHeaderDto
{
    public string NumFactura { get; set; } = string.Empty;
    public string ClienteClave { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public decimal Total { get; set; }
    public string Estado { get; set; } = string.Empty;
    public int? CajaId { get; set; }
    public string? CajaNombre { get; set; }
    public string? Banco { get; set; }
    public string? Usuario { get; set; }
}

public class CaptacionDetailDto
{
    public int Linea { get; set; }
    public string Servicio { get; set; } = string.Empty;
    public decimal MontoValor { get; set; }
}

public class CaptacionPagoResponseDto
{
    public CaptacionHeaderDto Header { get; set; } = new();
    public IReadOnlyList<CaptacionDetailDto> Detalles { get; set; } = Array.Empty<CaptacionDetailDto>();
}

public class PagoCrearDetalleDto
{
    [Required]
    public string Servicio { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal MontoValor { get; set; }
}

public class PagoCrearDto
{
    [Required]
    public string NumFactura { get; set; } = string.Empty;

    [Required]
    public string ClienteClave { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal Monto { get; set; }

    [Required]
    public int CajaId { get; set; }

    public string? Banco { get; set; }
    public long? BancoCuentaId { get; set; }

    public string Usuario { get; set; } = string.Empty;

    public DateTime? FechaPago { get; set; }

    public List<PagoCrearDetalleDto> Detalles { get; set; } = new();
}

public class CajaDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public DateTime? FechaApertura { get; set; }
    public string? Usuario { get; set; }
}

public class ArqueoDto
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public string NumFactura { get; set; } = string.Empty;
    public string ClienteClave { get; set; } = string.Empty;
    public string? Banco { get; set; }
    public string? Usuario { get; set; }
    public string? Estado { get; set; }
    public decimal Monto { get; set; }

    // Key for grids; set from service to ensure non-null
    public string RowKey { get; set; } = string.Empty;
}

public class ReciboMiscelaneoDto
{
    public long Recibo { get; set; }
    public string Cliente { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public decimal Total { get; set; }
    public string Estado { get; set; } = string.Empty;
}

public class ReversoRequestDto
{
    [Required]
    public string NumFactura { get; set; } = string.Empty;

    [Required]
    public string ClienteClave { get; set; } = string.Empty;

    public string Usuario { get; set; } = string.Empty;
}

public class CaptacionArqueoFilterDto
{
    public int? CajaId { get; set; }
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
}

// ==================== POSTEO MANUAL ====================

public class SaldoPosteoManualDto
{
    public int ReciboActual { get; set; }
    public int? ReciboAnterior { get; set; }
    public decimal Valor { get; set; }
    public decimal DistribucionAgua { get; set; }
    public decimal DistribucionAlcantarillado { get; set; }
    public decimal DistribucionOtros { get; set; }
    public long? DetalleId { get; set; }
    public long? DetalleAguaId { get; set; }
    public long? DetalleAlcantarilladoId { get; set; }
    public long? DetalleOtrosId { get; set; }

    // Un bucket (p. ej. "Otros") puede agrupar varios factura_detalle; el pago
    // debe distribuirse por detalle sin exceder el saldo individual de cada uno.
    public List<PagoManualDistribucionDto> DetallesAgua { get; set; } = new();
    public List<PagoManualDistribucionDto> DetallesAlcantarillado { get; set; } = new();
    public List<PagoManualDistribucionDto> DetallesOtros { get; set; } = new();
}

public class PagoManualDistribucionDto
{
    public long Id { get; set; }
    public decimal ValorDistribuido { get; set; }
}

public class PagoManualCrearDto
{
    [Required]
    public string ClienteClave { get; set; } = string.Empty;

    public int? NumReciboAnterior { get; set; }

    [Required]
    public int NumRecibo { get; set; }

    [Required]
    public string Banco { get; set; } = string.Empty;
    public long? BancoCuentaId { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal Valor { get; set; }

    [Required]
    public List<PagoManualDistribucionDto> Distribucion { get; set; } = new();

    public string Usuario { get; set; } = string.Empty;
}

public class ReversoManualRequestDto
{
    [Required]
    public string ClienteClave { get; set; } = string.Empty;

    [Required]
    public int Recibo { get; set; }

    public string Usuario { get; set; } = string.Empty;
}

// ==================== POSTEO MISCELÁNEOS ====================

public class ReciboMiscelaneoDetalleDto
{
    public int Linea { get; set; }
    public string Concepto { get; set; } = string.Empty;
    public decimal Monto { get; set; }
}

public class PagoMiscelaneoDetalleDto
{
    public string Concepto { get; set; } = string.Empty;
    public decimal Monto { get; set; }
}

public class PagoMiscelaneoCrearDto
{
    [Required]
    public string ClienteClave { get; set; } = string.Empty;

    [Required]
    public long Recibo { get; set; }

    [Required]
    public string Banco { get; set; } = string.Empty;
    public long? BancoCuentaId { get; set; }

    public List<PagoMiscelaneoDetalleDto> Detalles { get; set; } = new();

    public string Usuario { get; set; } = string.Empty;
}

public class ReversoMiscelaneoRequestDto
{
    [Required]
    public long Recibo { get; set; }

    public string? ClienteClave { get; set; }

    public string Usuario { get; set; } = string.Empty;
}

// ==================== BÚSQUEDA Y AUTOCOMPLETADO ====================

public class BusquedaFacturaDto
{
    public string NumFactura { get; set; } = string.Empty;
    public string ClienteClave { get; set; } = string.Empty;
    public string ClienteNombre { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public decimal Total { get; set; }
    public string Estado { get; set; } = string.Empty;
}

// ==================== COMBOS Y AUXILIARES ====================

public class BancoDto
{
    public long? BancoCuentaId { get; set; }
    public long? BancoId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
}

public class ClienteComboDto
{
    public string Clave { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string NombreCompleto => $"{Clave} - {Nombre}";
    public string? Direccion { get; set; }
}

public class PeriodoActualDto
{
    public string Periodo { get; set; } = string.Empty;
    public string Anio { get; set; } = string.Empty;
    public string Mes { get; set; } = string.Empty;
}
