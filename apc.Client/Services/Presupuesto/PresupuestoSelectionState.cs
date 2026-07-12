namespace apc.Client.Services.Presupuesto;

/// <summary>
/// Recuerda el último presupuesto seleccionado en la lista de configuraciones
/// para poder restaurarlo al regresar desde las pantallas de nuevo presupuesto,
/// gestión de detalle o edición de detalle. Es un estado de sesión (scoped): vive
/// mientras dura la navegación SPA, pero no sobrevive a un refresco completo del
/// navegador (que en ese caso no aplica, ya que se re-entra a la lista limpia).
/// </summary>
public sealed class PresupuestoSelectionState
{
    /// <summary>Id del presupuesto (encabezado) seleccionado por última vez, o null.</summary>
    public string? SelectedPresupuestoId { get; set; }
}
