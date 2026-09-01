using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Retenciones;

/// <summary>
/// Configuración de la cuenta contable del pasivo ("retenciones por pagar") para una retención,
/// EN LA EMPRESA ACTUAL. Tenant-scoped: el <c>company_id</c> lo resuelve el servidor, no viaja acá.
/// La cuenta debe ser posteable (<c>allows_posting</c>) del plan de la empresa.
/// </summary>
public sealed class RetencionCuentaDto
{
    public int RetencionId { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Debe seleccionar una cuenta contable.")]
    public long AccountId { get; set; }

    public bool Activo { get; set; } = true;

    // ----- solo lectura (para mostrar) -----
    public string? RetencionCodigo { get; init; }
    public string? CuentaCodigo { get; init; }
    public string? CuentaNombre { get; init; }
}
