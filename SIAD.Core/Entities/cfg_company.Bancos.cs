using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class cfg_company
{
    public virtual ICollection<ban_banco> ban_banco { get; set; } = new List<ban_banco>();

    public virtual ban_config? ban_config { get; set; }

    public virtual ICollection<ban_cta> ban_cta { get; set; } = new List<ban_cta>();

    public virtual ICollection<ban_cuenta> ban_cuenta { get; set; } = new List<ban_cuenta>();

    public virtual ICollection<ban_moneda> ban_moneda { get; set; } = new List<ban_moneda>();

    public virtual ICollection<ban_movimiento> ban_movimiento { get; set; } = new List<ban_movimiento>();

    public virtual ICollection<ban_movimiento_detalle> ban_movimiento_detalle { get; set; } = new List<ban_movimiento_detalle>();

    public virtual ICollection<ban_movimiento_transito> ban_movimiento_transito { get; set; } = new List<ban_movimiento_transito>();
}
