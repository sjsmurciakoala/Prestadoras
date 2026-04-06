namespace SIAD.Core.Entities;

public partial class servicios_roles_ws
{
    public string rol { get; set; } = null!;

    public string servicios_codigo { get; set; } = null!;

    public bool activo { get; set; }

    public string? descripcion { get; set; }
}
