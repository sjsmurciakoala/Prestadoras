using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class cfg_motivo_aumento
{
    public short motivo_aumento_id { get; set; }

    public string codigo { get; set; } = null!;

    public string descripcion { get; set; } = null!;

    public bool activo { get; set; }

    public virtual ICollection<adm_nota_debito> adm_nota_debitos { get; set; } = new List<adm_nota_debito>();
}
