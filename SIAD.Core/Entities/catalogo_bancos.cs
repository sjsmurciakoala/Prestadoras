namespace SIAD.Core.Entities;

public partial class catalogo_bancos
{
    public int id { get; set; }

    public string? codigo { get; set; }

    public string? nombre { get; set; }

    public bool activo { get; set; } = true;
}
