using System;
using System.ComponentModel.DataAnnotations.Schema;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public class con_tipo_transaccion_rule : ICompanyScopedEntity
{
    public long rule_id { get; set; }
    public long company_id { get; set; }
    public long type_id { get; set; }
    public int line_number { get; set; }

    public string? account_code_from { get; set; }
    public string? account_code_to { get; set; }
    public string? cost_center_code_from { get; set; }
    public string? cost_center_code_to { get; set; }
    public string? third_party_code_from { get; set; }
    public string? third_party_code_to { get; set; }

    public bool is_active { get; set; } = true;

    public DateTime created_at { get; set; } = DateTime.UtcNow;
    public string created_by { get; set; } = string.Empty;
    public DateTime? updated_at { get; set; }
    public string? updated_by { get; set; }

    [ForeignKey(nameof(type_id))]
    public virtual con_tipo_transaccion? tipo_transaccion { get; set; }
}
