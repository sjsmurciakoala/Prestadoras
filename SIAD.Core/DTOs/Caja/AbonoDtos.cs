using System;

namespace SIAD.Core.DTOs.Caja;

public class AbonoCrearDto
{
    public string NumFactura { get; set; } = null!;
    public string ClienteClave { get; set; } = null!;
    public decimal Monto { get; set; }
    public string FormaPago { get; set; } = "EFECTIVO"; // "EFECTIVO" o "BANCO"
    public long? BancoCuentaId { get; set; }
    public string? Banco { get; set; }
    public DateTime? FechaPago { get; set; }
    public string? Usuario { get; set; }
    public int? ReciboPendienteId { get; set; }
}

public class FacturaConSaldoDto
{
    public int FacturaId { get; set; }
    public string NumFactura { get; set; } = null!;
    public int NumRecibo { get; set; }
    public string ClienteClave { get; set; } = null!;
    public string ClienteNombre { get; set; } = string.Empty;
    public DateTime FechaEmision { get; set; }
    public decimal SaldoTotal { get; set; }
    public decimal SaldoPendiente { get; set; }
    public string Estado { get; set; } = null!;
}

public class AbonoResponseDto
{
    public string NumFactura { get; set; } = null!;
    public int NumRecibo { get; set; }
    public decimal MontoAbonado { get; set; }
    public decimal NuevoSaldo { get; set; }
    public long? PolizaId { get; set; }
    public int TransaccionId { get; set; }
}

public class ReversoAbonoRequestDto
{
    public int TransaccionId { get; set; }
    public string Usuario { get; set; } = null!;
    public string Motivo { get; set; } = string.Empty;
}

public class ReciboAbonoLineaDto
{
    public string Descripcion { get; set; } = string.Empty;
    public string Moneda { get; set; } = "L.";
    public decimal Monto { get; set; }
}

public class ReciboAbonoDto
{
    // Encabezado empresa
    public string EmpresaNombre { get; set; } = string.Empty;
    public byte[]? EmpresaLogo { get; set; }
    public string? EmpresaLogoMime { get; set; }

    // Datos del recibo / factura
    public int NumRecibo { get; set; }
    public string NumFactura { get; set; } = string.Empty;
    public string Periodo { get; set; } = string.Empty;
    public string FechaEmision { get; set; } = string.Empty;
    public string RtnCliente { get; set; } = string.Empty;
    public string CuentaNo { get; set; } = string.Empty;
    public string Propietario { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;

    // Líneas de cargo (fuente: factura_detalle)
    public List<ReciboAbonoLineaDto> Lineas { get; set; } = new();

    // Totales
    public decimal Total { get; set; }
    public string TotalEnLetras { get; set; } = string.Empty;

    // Pie del recibo
    public string Cajero { get; set; } = string.Empty;
    public string FechaPago { get; set; } = string.Empty;
    public int NumeroTransaccion { get; set; }
    public string GeneradoPor { get; set; } = string.Empty;
    public bool EsPendiente { get; set; }
}

public class AbonoHistorialItemDto
{
    public int TransaccionId { get; set; }
    public string NumFactura { get; set; } = string.Empty;
    public int NumRecibo { get; set; }
    public string FechaPago { get; set; } = string.Empty;
    public decimal MontoAbonado { get; set; }
    public string Cajero { get; set; } = string.Empty;
    public string EstadoFactura { get; set; } = string.Empty;
    public decimal SaldoRestante { get; set; }
}

public class GenerarReciboDto
{
    public string NumFactura { get; set; } = null!;
    public string ClienteClave { get; set; } = null!;
    public decimal Monto { get; set; }
    public string? Usuario { get; set; }
}

public class GenerarReciboResponseDto
{
    public int TransaccionId { get; set; }
    public string NumFactura { get; set; } = string.Empty;
}

public class ReciboPendienteDto
{
    public int TransaccionId { get; set; }
    public string NumFactura { get; set; } = string.Empty;
    public int NumRecibo { get; set; }
    public decimal Monto { get; set; }
    public string FechaGenerado { get; set; } = string.Empty;
    public string Operador { get; set; } = string.Empty;
}

public class AnularReciboPendienteDto
{
    public int TransaccionId { get; set; }
    public string Usuario { get; set; } = null!;
    public string Motivo { get; set; } = string.Empty;
}
