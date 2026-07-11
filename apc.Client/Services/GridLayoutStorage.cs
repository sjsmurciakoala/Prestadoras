using System.Text.Json;
using DevExpress.Blazor;
using Microsoft.JSInterop;

namespace apc.Client.Services;

/// <summary>
/// Persiste el layout de un DxGrid (visibilidad/orden/ancho de columnas, ordenamiento y tamaño de página)
/// en el localStorage del navegador, para restaurarlo al volver a la página.
/// Usar desde los eventos LayoutAutoSaving / LayoutAutoLoading del grid con una clave única por página.
/// </summary>
public static class GridLayoutStorage
{
    public static async Task SaveAsync(IJSRuntime js, string storageKey, GridPersistentLayoutEventArgs e)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", storageKey, JsonSerializer.Serialize(e.Layout));
        }
        catch
        {
            // Sin localStorage disponible el grid sigue funcionando, solo no persiste el layout.
        }
    }

    public static async Task LoadAsync(IJSRuntime js, string storageKey, GridPersistentLayoutEventArgs e)
    {
        try
        {
            var json = await js.InvokeAsync<string?>("localStorage.getItem", storageKey);
            if (!string.IsNullOrWhiteSpace(json))
                e.Layout = JsonSerializer.Deserialize<GridPersistentLayout>(json);
        }
        catch
        {
            // Layout corrupto o storage inaccesible: se usa la configuración por defecto.
        }
    }
}
