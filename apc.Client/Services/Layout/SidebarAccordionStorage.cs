using Microsoft.JSInterop;

namespace apc.Client.Services.Layout;

/// <summary>
/// Recuerda qué sección del menú dejó abierta el usuario. Mismo patrón que
/// <see cref="apc.Client.Services.GridLayoutStorage"/>: localStorage directo y try/catch que
/// traga todo, porque sin almacenamiento el menú tiene que seguir funcionando igual.
///
/// Sólo se puede llamar después del primer render: durante el prerender no hay JS y el
/// interop lanza.
/// </summary>
public static class SidebarAccordionStorage
{
    public const string StorageKey = "siad.sidebar.seccion";

    /// <summary>
    /// null = nunca se guardó nada (o el storage no está disponible);
    /// cadena vacía = el usuario dejó todas las secciones cerradas, que es una elección válida.
    /// </summary>
    public static async Task<string?> LeerAsync(IJSRuntime js)
    {
        try
        {
            return await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        }
        catch
        {
            return null;
        }
    }

    public static async Task GuardarAsync(IJSRuntime js, string? seccionId)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", StorageKey, seccionId ?? string.Empty);
        }
        catch
        {
            // Sin localStorage el acordeón sigue funcionando, sólo no sobrevive al F5.
        }
    }
}
