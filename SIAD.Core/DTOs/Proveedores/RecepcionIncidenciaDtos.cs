using System.ComponentModel.DataAnnotations;
using SIAD.Core.Constants;

namespace SIAD.Core.DTOs.Proveedores;

/// <summary>
/// Incidencia detectada al recibir mercadería (devolución, daño, especificación distinta,
/// faltante). Es lo que hace medible el criterio CALIDAD del scorecard: mientras no exista
/// ninguna, ese criterio se reporta sin datos y su peso se redistribuye.
/// </summary>
public sealed class RecepcionIncidenciaDto
{
    public int Id { get; set; }
    public int CompraHdrId { get; set; }

    // Datos de la recepción afectada (para mostrar sin pedir otra consulta).
    public int RecepcionNumero { get; set; }
    public DateOnly RecepcionFecha { get; set; }
    public string? NumeroFacturaSar { get; set; }
    public string CodProveedor { get; set; } = string.Empty;
    public string? ProveedorNombre { get; set; }

    public DateOnly Fecha { get; set; }
    public short Tipo { get; set; } = TipoIncidenciaRecepcion.Devolucion;
    public int? ArticuloId { get; set; }
    public string? ArticuloDescripcion { get; set; }
    public decimal? Cantidad { get; set; }
    public decimal? Monto { get; set; }
    public string Descripcion { get; set; } = string.Empty;

    public string? UsuarioCreacion { get; set; }
    public DateTime? FechaCreacion { get; set; }

    public string TipoDescripcion => TipoIncidenciaRecepcion.Descripcion(Tipo);

    /// <summary>"Factura F-00918" o, si no la trae, "Recepción 00042".</summary>
    public string DocumentoTexto => string.IsNullOrWhiteSpace(NumeroFacturaSar)
        ? $"Recepción {RecepcionNumero:00000}"
        : $"Factura {NumeroFacturaSar}";
}

/// <summary>Alta o edición de una incidencia.</summary>
public sealed class RecepcionIncidenciaUpsertDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Debe indicar la recepción afectada.")]
    public int CompraHdrId { get; set; }

    /// <summary>Cuándo se detectó. Vacía = la fecha de la recepción.</summary>
    public DateOnly? Fecha { get; set; }

    public short Tipo { get; set; } = TipoIncidenciaRecepcion.Devolucion;

    public int? ArticuloId { get; set; }

    [Range(0, 9_999_999_999d, ErrorMessage = "La cantidad está fuera de rango.")]
    public decimal? Cantidad { get; set; }

    [Range(0, 999_999_999_999d, ErrorMessage = "El monto está fuera de rango.")]
    public decimal? Monto { get; set; }

    [Required(ErrorMessage = "Describa la incidencia.")]
    [StringLength(500, ErrorMessage = "La descripción no puede superar 500 caracteres.")]
    public string Descripcion { get; set; } = string.Empty;
}

/// <summary>Filtro del listado de incidencias.</summary>
public sealed class RecepcionIncidenciaFilterDto
{
    public string? CodProveedor { get; set; }
    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }
    public short? Tipo { get; set; }
    public int? CompraHdrId { get; set; }
    public string? Search { get; set; }
}

/// <summary>Recepción elegible para registrarle una incidencia (combo del alta).</summary>
public sealed class RecepcionIncidenciaLookupDto
{
    public int Id { get; set; }
    public int Numero { get; set; }
    public DateOnly Fecha { get; set; }
    public string? NumeroFacturaSar { get; set; }
    public decimal Total { get; set; }

    /// <summary>"00042 — 05/08/2026 — F-00918 — 16,500.00".</summary>
    public string Display
    {
        get
        {
            var factura = string.IsNullOrWhiteSpace(NumeroFacturaSar) ? "sin factura" : NumeroFacturaSar;
            return $"{Numero:00000} — {Fecha:dd/MM/yyyy} — {factura} — {Total:N2}";
        }
    }
}
