using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class bnc_tipotransaccione
{
    public string cod_centrocosto { get; set; } = null!;

    public int cod_partida { get; set; }

    public string correlativo { get; set; } = null!;

    public string cuenta_contable { get; set; } = null!;

    public char del_sistema { get; set; }

    public string? destino { get; set; }

    public char emite_cheque { get; set; }

    public char entra_sale { get; set; }

    public DateTime fecha_creacion { get; set; }

    public DateTime fecha_modificacion { get; set; }

    public short? filtro { get; set; }

    public string nombre { get; set; } = null!;

    public string? observaciones { get; set; }

    public char? pad { get; set; }

    public char? pda { get; set; }

    public char? rel_empleados { get; set; }

    public string tipo_transaccion { get; set; } = null!;

    public char? trn_prestamo { get; set; }

    public string usuario_creo { get; set; } = null!;

    public string? usuario_modifica { get; set; }

    public Guid? rowid { get; set; }

    public bool? cuenta_alterna { get; set; }
}
