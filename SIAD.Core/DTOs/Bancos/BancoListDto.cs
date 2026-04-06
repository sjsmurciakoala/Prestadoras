using System;

namespace SIAD.Core.DTOs.Bancos;

public sealed class BancoListDto
{
    public long BancoId { get; set; }
    public long CompanyId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
