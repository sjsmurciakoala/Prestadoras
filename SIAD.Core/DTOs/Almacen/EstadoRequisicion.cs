namespace SIAD.Core.DTOs.Almacen;

/// <summary>
/// Mapeo del código estatus de alm_requisicion. Inferido de los datos:
/// E = entregada (aprobada + descargada), P = pendiente, A = anulada/rechazada.
/// </summary>
public static class EstadoRequisicion
{
    public const string Entregada = "E";
    public const string Pendiente = "P";
    public const string Anulada = "A";

    public static string Describir(string? estatus) => estatus switch
    {
        "E" => "Entregada",
        "P" => "Pendiente",
        "A" => "Anulada",
        _ => "—"
    };
}
