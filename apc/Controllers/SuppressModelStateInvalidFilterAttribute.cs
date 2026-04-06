using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace apc.Controllers;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class SuppressModelStateInvalidFilterAttribute : Attribute, IActionModelConvention
{
    private const string ModelStateInvalidFilterFactoryName = "ModelStateInvalidFilterFactory";

    public void Apply(ActionModel action)
    {
        for (var i = action.Filters.Count - 1; i >= 0; i--)
        {
            if (string.Equals(action.Filters[i].GetType().Name, ModelStateInvalidFilterFactoryName, StringComparison.Ordinal))
            {
                action.Filters.RemoveAt(i);
            }
        }
    }
}
