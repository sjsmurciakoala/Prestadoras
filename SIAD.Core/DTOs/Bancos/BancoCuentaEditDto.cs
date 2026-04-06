using System;

namespace SIAD.Core.DTOs.Bancos;

public sealed class BancoCuentaEditDto : BancoCuentaCreateDto
{
    public long BancoCuentaId { get; set; }
    public long CompanyId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
