using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SIAD.Core.DTOs.CondicionesLectura;

namespace SIAD.Services.CondicionesLectura;

/// <summary>
/// ABM de condiciones de lectura por empresa (portal, 2026-07-06). Administra
/// adm_condicion_lectura (codigo/descripcion/orden/activo/facturacion/
/// aplica_descuento) sobre un `tipo` elegido de la referencia global
/// adm_condicion_lectura_tipo. El catálogo resultante lo consume la app de
/// lectores vía GET /api/condiciones (apc.MobileApi).
/// </summary>
public interface ICondicionesLecturaService
{
    /// <summary>Catálogo de la empresa: tipos (ref global) + condiciones editables (todas, activas e inactivas).</summary>
    Task<CondicionesLecturaCatalogoDto> ObtenerAsync(long companyId, CancellationToken ct = default);

    /// <summary>
    /// Persiste el conjunto de condiciones de la empresa (upsert por id; borra las
    /// que ya no vienen). Devuelve el catálogo refrescado. Valida códigos únicos,
    /// tipo existente y flags S/N.
    /// </summary>
    Task<CondicionesLecturaCatalogoDto> GuardarAsync(long companyId, List<CondicionLecturaAdminDto> condiciones, string usuario, CancellationToken ct = default);
}
