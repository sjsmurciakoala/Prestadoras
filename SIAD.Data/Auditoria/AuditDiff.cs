using System;
using System.Collections.Generic;
using System.Text.Json;

namespace SIAD.Data.Auditoria;

public static class AuditDiff
{
    public sealed record Campo(string campo, object? antes, object? despues);

    private static readonly JsonSerializerOptions _opts = new() { WriteIndented = false };

    // Objeto JSON { campo: valor_anterior }; null si la lista está vacía.
    public static string? SerializeAnteriores(IReadOnlyList<Campo> campos) => Obj(campos, c => c.antes);

    // Objeto JSON { campo: valor_nuevo }; null si la lista está vacía.
    public static string? SerializeNuevos(IReadOnlyList<Campo> campos) => Obj(campos, c => c.despues);

    private static string? Obj(IReadOnlyList<Campo> campos, Func<Campo, object?> sel)
    {
        if (campos is not { Count: > 0 }) return null;
        var dict = new Dictionary<string, object?>(campos.Count);
        foreach (var c in campos) dict[c.campo] = sel(c);
        return JsonSerializer.Serialize(dict, _opts);
    }
}
