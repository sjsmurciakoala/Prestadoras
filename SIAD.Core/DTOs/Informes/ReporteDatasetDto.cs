using System;
using System.Collections.Generic;

namespace SIAD.Core.DTOs.Informes;

public sealed record ReporteDatasetCatalogoItemDto(
    long DatasetId,
    string Codigo,
    string Nombre,
    string? Descripcion,
    string TipoOrigen,
    string? OrigenClave,
    bool IsActive,
    int ParametrosCount
);

public sealed record ReporteDatasetDetalleDto(
    long DatasetId,
    string Codigo,
    string Nombre,
    string? Descripcion,
    string TipoOrigen,
    string? OrigenClave,
    string? SqlText,
    string? ConnectionName,
    bool IsActive,
    IReadOnlyList<ReporteDatasetParametroDto> Parametros
);

public sealed record ReporteDatasetParametroDto(
    long DatasetParametroId,
    string Nombre,
    string? NombreOrigen,
    string Etiqueta,
    string TipoDato,
    string FuenteValor,
    string? ValorDefault,
    bool Visible,
    bool PermiteNulo,
    bool Requerido,
    int Orden
);

public sealed class ReporteDatasetCreateDto
{
    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public string? Descripcion { get; set; }

    public string TipoOrigen { get; set; } = string.Empty;

    public string? OrigenClave { get; set; }

    public string? SqlText { get; set; }

    public string? ConnectionName { get; set; }

    public List<ReporteDatasetParametroCreateDto> Parametros { get; set; } = [];
}

public sealed class ReporteDatasetParametroCreateDto
{
    public string Nombre { get; set; } = string.Empty;

    public string? NombreOrigen { get; set; }

    public string Etiqueta { get; set; } = string.Empty;

    public string TipoDato { get; set; } = string.Empty;

    public string FuenteValor { get; set; } = string.Empty;

    public string? ValorDefault { get; set; }

    public bool Visible { get; set; } = true;

    public bool PermiteNulo { get; set; }

    public bool Requerido { get; set; }

    public int Orden { get; set; }
}

public sealed class ReporteDatasetPreviewRequestDto
{
    public Dictionary<string, string?> Parametros { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record ReporteDatasetPreviewResultDto(
    string Codigo,
    string TipoOrigen,
    IReadOnlyList<string> Columnas,
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Filas,
    int MaxRows
);
