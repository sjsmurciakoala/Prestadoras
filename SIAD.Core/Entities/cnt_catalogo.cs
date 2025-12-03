using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class cnt_catalogo
{
    public string cod_cuenta { get; set; } = null!;

    public string? cod_mayor { get; set; }

    public int? cod_empresa { get; set; }

    public char? cod_grupo_cta { get; set; }

    public char? cuenta_ext { get; set; }

    public DateOnly? fecha_creacion { get; set; }

    public char? flag_budget { get; set; }

    public char? flag_fijovariable { get; set; }

    public string nombre { get; set; } = null!;

    public char? status { get; set; }

    public short? tipo_cuenta { get; set; }

    public DateOnly? ult_fecha_modificada { get; set; }

    public string? ult_usuario { get; set; }

    public string? usuario_creo { get; set; }

    public Guid? rowid { get; set; }

    public string? cod_sub_grupo { get; set; }

    public string? cscuenta { get; set; }
}
