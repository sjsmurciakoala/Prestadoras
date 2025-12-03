using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class cnt_partidas_hdr
{
    public int cod_empresa { get; set; }

    public int cod_partida { get; set; }

    public int cod_tipopartid { get; set; }

    public string? correlativo { get; set; }

    public DateOnly? fecha_creacion { get; set; }

    public DateTime? hora_creacion { get; set; }

    public DateOnly? fecha_partida { get; set; }

    public char? maestro { get; set; }

    public string? sinopsis { get; set; }

    public string? tipo_transaccion { get; set; }

    public string? usuario_creacion { get; set; }

    public Guid? rowid { get; set; }
}
