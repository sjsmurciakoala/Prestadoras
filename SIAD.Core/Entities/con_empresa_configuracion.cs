using System;

using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class con_empresa_configuracion : ICompanyScopedEntity
{
    public long company_id { get; set; }

    public string? tipo_empresa { get; set; }

    public string? id_fiscal_siglas { get; set; }

    public string? id_fiscal_valor { get; set; }

    public string? tamano { get; set; }

    public string? capital { get; set; }

    public DateOnly? fecha_constitucion { get; set; }

    public string? contacto { get; set; }

    public string? direccion { get; set; }

    public string? telefonos { get; set; }

    public string? ciudad { get; set; }

    public string? pais { get; set; }

    public string? email { get; set; }

    public string? pagina_web { get; set; }

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }

    public byte[]? logo { get; set; }

    public string? logo_mime { get; set; }
}