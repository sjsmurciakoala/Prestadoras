using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class ban_banco
{
    public long ban_banco_id { get; set; }

    public long company_id { get; set; }

    public string code { get; set; } = null!;

    public string nombre { get; set; } = null!;

    public string? sucursal { get; set; }

    public string? nombre_sucursal { get; set; }

    public int pais_id { get; set; }

    public int estado_id { get; set; }

    public int ciudad_id { get; set; }

    public int municipio_id { get; set; }

    public string? zipcode { get; set; }

    public string? direccion1 { get; set; }

    public string? direccion2 { get; set; }

    public string? gerente { get; set; }

    public string? telefonos { get; set; }

    public string? fax { get; set; }

    public string? email { get; set; }

    public string? memo { get; set; }

    public bool activo { get; set; }

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }

    public virtual ICollection<ban_cuenta> ban_cuenta { get; set; } = new List<ban_cuenta>();

    public virtual cfg_company company { get; set; } = null!;
}
