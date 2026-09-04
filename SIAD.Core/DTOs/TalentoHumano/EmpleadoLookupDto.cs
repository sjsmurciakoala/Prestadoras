namespace SIAD.Core.DTOs.TalentoHumano;

/// <summary>
/// Empleado activo para poblar combos con autocompletar (p. ej. "Recibe" en el Descargo de
/// almacén). Sin FK: el consumidor guarda el texto que elija (el nombre), igual que el catálogo
/// de Bancos en Proveedores. El combo declara Código y Nombre como columnas visibles para que el
/// autocompletar busque por cualquiera de los dos (DxComboBox busca en todas las columnas visibles).
/// </summary>
public sealed class EmpleadoLookupDto
{
    public int Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;

    /// <summary>Cargo del empleado (del catálogo), para autollenar campos como el "solicitante" de la requisición.</summary>
    public string? CargoNombre { get; init; }

    /// <summary>Departamento del empleado (del catálogo), para autoseleccionar el departamento de la requisición.</summary>
    public string? DepartamentoNombre { get; init; }
}
