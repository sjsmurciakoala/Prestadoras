using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class prv_proveedore
{
    public string cod_proveedor { get; set; } = null!;

    public short cod_tipoproveedor { get; set; }

    public string nombre { get; set; } = null!;

    public string cuenta_contable { get; set; } = null!;

    public string direccion { get; set; } = null!;

    public DateTime fecha_creacion { get; set; }

    public DateTime? fecha_modificacion { get; set; }

    public bool? status { get; set; }

    public string cuenta_bancaria { get; set; } = null!;

    public Guid? rowid { get; set; }

    public double? compras_acum { get; set; }

    public double? compras_dolares { get; set; }

    public double? saldo_actual { get; set; }

    public double? saldo_act_dolares { get; set; }

    public double? saldo_anterior { get; set; }

    public double? saldo_ant_doleres { get; set; }

    public string? razon_social { get; set; }

    public string? rtn { get; set; }

    public string? telefono { get; set; }

    public string? pagina_web { get; set; }

    public string? fax { get; set; }

    public string? email { get; set; }

    public string? nombrebanco1 { get; set; }

    public string? nombrebanco2 { get; set; }
}
