namespace apc.Client.Layout.Navigation;

/// <summary>
/// Decide si una ruta enciende un item del menú. Vivía privada dentro de SidebarNavNode;
/// se extrajo porque el acordeón necesita saber qué SECCIÓN contiene la ruta actual, y esa
/// pregunta se responde con exactamente las mismas reglas. La lógica no cambió.
/// </summary>
public static class SidebarPathMatcher
{
    public static string Normalizar(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        var cleaned = path.Split('?', '#')[0];
        if (!cleaned.StartsWith("/"))
        {
            cleaned = "/" + cleaned;
        }

        cleaned = cleaned.TrimEnd('/');
        if (string.IsNullOrEmpty(cleaned))
        {
            cleaned = "/";
        }

        return cleaned.ToLowerInvariant();
    }

    public static bool Coincide(string rutaActual, string? navigateUrl, IReadOnlyList<string> prefixes, bool matchExact)
    {
        if (!string.IsNullOrWhiteSpace(navigateUrl) && EsMatch(rutaActual, navigateUrl, matchExact))
        {
            return true;
        }

        foreach (var prefix in prefixes)
        {
            if (EsMatch(rutaActual, prefix, matchExact))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Recorre los hijos (a cualquier profundidad) buscando uno encendido. Es lo que usa un
    /// grupo para saber si está activo: un item CON hijos nunca mira sus propios prefijos.
    /// </summary>
    public static bool TieneDescendienteActivo(SidebarNavItem item, string rutaActual)
    {
        foreach (var child in item.Children)
        {
            if (Coincide(rutaActual, child.NavigateUrl, child.MatchPrefixes, child.MatchExact))
            {
                return true;
            }

            if (child.HasChildren && TieneDescendienteActivo(child, rutaActual))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>La ruta corresponde a un item concreto de la sección, a cualquier profundidad.</summary>
    public static bool ContienePorItem(SidebarNavSection section, string rutaActual)
    {
        foreach (var item in section.Items)
        {
            if (Coincide(rutaActual, item.NavigateUrl, item.MatchPrefixes, item.MatchExact))
            {
                return true;
            }

            if (item.HasChildren && TieneDescendienteActivo(item, rutaActual))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// La ruta cae bajo el territorio declarado por la sección, sin corresponder a ningún item.
    /// Se consulta sólo después de agotar ContienePorItem: los prefijos de sección son amplios
    /// y una coincidencia por item siempre es más precisa.
    /// </summary>
    public static bool ContienePorPrefijo(SidebarNavSection section, string rutaActual)
    {
        foreach (var prefix in section.MatchPrefixes)
        {
            if (EsMatch(rutaActual, prefix, matchExact: false))
            {
                return true;
            }
        }

        return false;
    }

    private static bool EsMatch(string rutaActual, string target, bool matchExact) =>
        matchExact ? EsExacto(rutaActual, target) : EsExactoOHijo(rutaActual, target);

    private static bool EsExacto(string rutaActual, string target) =>
        Normalizar(rutaActual).Equals(Normalizar(target), StringComparison.OrdinalIgnoreCase);

    private static bool EsExactoOHijo(string rutaActual, string target)
    {
        var actual = Normalizar(rutaActual);
        var objetivo = Normalizar(target);

        if (objetivo == "/")
        {
            return actual == "/";
        }

        return actual.Equals(objetivo, StringComparison.OrdinalIgnoreCase) ||
               actual.StartsWith(objetivo + "/", StringComparison.OrdinalIgnoreCase);
    }
}
