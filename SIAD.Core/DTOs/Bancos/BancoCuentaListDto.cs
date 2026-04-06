using System;

namespace SIAD.Core.DTOs.Bancos;

public sealed class BancoCuentaListDto
{
    public long BancoCuentaId { get; set; }
    public long CompanyId { get; set; }
    public long BancoId { get; set; }
    public string? BancoNombre { get; set; }
    public string NumeroCuenta { get; set; } = string.Empty;
    public string TipoCuenta { get; set; } = string.Empty;
    public string? Moneda { get; set; }
    public decimal SaldoActual { get; set; }
    public string? Titular { get; set; }
    public string? Observaciones { get; set; }
    public bool Activo { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string? CtaConc { get; set; }
    public string? CuentaContableCodigo { get; set; }
    public string? CuentaContableNombre { get; set; }
}
