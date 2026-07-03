namespace apc.Client.Shared.Components;

/// <summary>
/// Tipo visual de <see cref="SiadAlert"/>. Nota histórica: las páginas usaban
/// un componente &lt;DxAlert&gt; que NO existe en DevExpress Blazor (compilaba
/// como HTML desconocido y se renderizaba sin estilo); SiadAlert lo reemplaza
/// con una alerta Bootstrap real. Error es alias visual de Danger.
/// </summary>
public enum AlertType
{
    Info,
    Success,
    Warning,
    Danger,
    Error
}
