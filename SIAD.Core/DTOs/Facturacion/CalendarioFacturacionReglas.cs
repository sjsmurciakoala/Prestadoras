using System;
using System.Collections.Generic;
using System.Linq;

namespace SIAD.Core.DTOs.Facturacion;

/// <summary>
/// Reglas de negocio del calendario de facturación, compartidas entre el
/// servicio (SIAD.Services) y el cliente Blazor (apc.Client): una sola
/// definición de la normalización de ciclo y de las validaciones, para que
/// el popup y el POST nunca diverjan.
/// </summary>
public static class CalendarioFacturacionReglas
{
    private static readonly string[] NombresMeses =
    [
        "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
        "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"
    ];

    public static string NombreMes(int mes)
        => mes is >= 1 and <= 12 ? NombresMeses[mes - 1] : mes.ToString();

    /// <summary>Mismo criterio que la planilla V3: numérico → 2 dígitos ('1' → '01').</summary>
    public static string NormalizarCiclo(string? ciclo)
    {
        var limpio = (ciclo ?? string.Empty).Trim();
        return int.TryParse(limpio, out var numero) ? numero.ToString("D2") : limpio;
    }

    /// <summary>
    /// Valida una fila individual. Devuelve el mensaje de error o null.
    /// Exige al menos una fecha: una fila sin fechas no genera eventos en el
    /// calendario y quedaría invisible e ineditable (la vista de calendario es
    /// la única superficie de edición desde 2026-07-14).
    /// </summary>
    public static string? ValidarFila(CalendarioCicloDto f)
    {
        if (f.Mes is < 1 or > 12)
        {
            return $"Mes inválido ({f.Mes}) en el ciclo '{f.Ciclo}'.";
        }

        var ciclo = NormalizarCiclo(f.Ciclo);
        if (string.IsNullOrWhiteSpace(ciclo))
        {
            return $"El ciclo del mes {NombreMes(f.Mes)} es obligatorio.";
        }

        if (ciclo.Length > 10)
        {
            return $"El ciclo '{ciclo}' excede 10 caracteres.";
        }

        if (f.FechaLectura is null && f.FechaFacturacion is null && f.FechaVencimiento is null)
        {
            return $"La fila {NombreMes(f.Mes)}/{ciclo} necesita al menos una fecha (lectura, facturación o vencimiento).";
        }

        if (f.DiasVencimiento is < 0 or > 99)
        {
            return $"Días de vencimiento inválidos en {NombreMes(f.Mes)}/{ciclo}.";
        }

        if (f.FechaFacturacion.HasValue && f.FechaVencimiento.HasValue
            && f.FechaVencimiento.Value.Date < f.FechaFacturacion.Value.Date)
        {
            return $"El vencimiento de {NombreMes(f.Mes)}/{ciclo} es anterior a su fecha de facturación.";
        }

        return null;
    }

    /// <summary>
    /// Valida que la candidata no duplique (mes, ciclo) dentro del conjunto.
    /// <paramref name="reemplaza"/> excluye la fila que se está editando.
    /// </summary>
    public static string? ValidarDuplicado(IEnumerable<CalendarioCicloDto> existentes,
        CalendarioCicloDto candidata, CalendarioCicloDto? reemplaza = null)
    {
        var ciclo = NormalizarCiclo(candidata.Ciclo);
        var duplicada = existentes.Any(f =>
            !ReferenceEquals(f, reemplaza) &&
            f.Mes == candidata.Mes &&
            string.Equals(NormalizarCiclo(f.Ciclo), ciclo, StringComparison.OrdinalIgnoreCase));

        return duplicada
            ? $"El ciclo '{ciclo}' ya existe en {NombreMes(candidata.Mes)}."
            : null;
    }

    /// <summary>Valida el conjunto completo de un año (filas + duplicados).</summary>
    public static string? ValidarConjunto(IReadOnlyList<CalendarioCicloDto> filas)
    {
        foreach (var f in filas)
        {
            var error = ValidarFila(f);
            if (error is not null)
            {
                return error;
            }
        }

        var duplicado = filas
            .GroupBy(f => (f.Mes, Ciclo: NormalizarCiclo(f.Ciclo).ToUpperInvariant()))
            .FirstOrDefault(g => g.Count() > 1);

        return duplicado is null
            ? null
            : $"El ciclo '{duplicado.Key.Ciclo}' está repetido en {NombreMes(duplicado.Key.Mes)}.";
    }
}
