using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class usuarios_tipotransaccion_dtl
{
    public int id_usertransacc_dtl { get; set; }

    public int cod_usertransacc_hdr { get; set; }

    public string cod_tipotransaccion { get; set; } = null!;
}
