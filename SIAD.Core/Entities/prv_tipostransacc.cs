using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class prv_tipostransacc
{
    public string cod_tipopartida { get; set; } = null!;

    public string correlativo { get; set; } = null!;

    public string cuenta_contable { get; set; } = null!;

    public string del_sistema { get; set; } = null!;

    public string entra_sale { get; set; } = null!;

    public DateTime fecha_creacion { get; set; }

    public DateTime? fecha_modificacion { get; set; }

    public string nombre { get; set; } = null!;

    public string? observaciones { get; set; }

    public string? pda { get; set; }

    public string tipo_transaccion { get; set; } = null!;

    public string usuario_creo { get; set; } = null!;

    public string? usuario_modifica { get; set; }

    public string rowid { get; set; } = null!;

    public int? cod_correlativo_dei { get; set; }
}
