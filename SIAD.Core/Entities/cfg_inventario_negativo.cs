using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Interruptor por empresa (la PK ES el <c>company_id</c>) para permitir existencia
/// NEGATIVA en salidas de inventario.
/// <para>
/// Nace en <c>false</c> = comportamiento actual: el motor
/// (<c>InventarioPostingService.ValidarAsync</c>) rechaza toda salida que cruzaría a
/// negativo. El override por bodega (<c>alm_bodega.permite_existencia_negativa</c>) gana
/// sobre este cuando no es NULL. Efectivo = <c>override_bodega ?? permitir_empresa</c>.
/// </para>
/// </summary>
public partial class cfg_inventario_negativo : ICompanyScopedEntity
{
    /// <summary>PK y tenant a la vez: un solo interruptor por empresa.</summary>
    public long company_id { get; set; }

    /// <summary>
    /// <c>true</c> = las salidas de la empresa pueden dejar la existencia en negativo
    /// (salvo que una bodega lo fuerce a <c>false</c>). <c>false</c> (default) = el motor
    /// rechaza toda salida que cruce a negativo. Ver el CHECK/columna en
    /// <c>2026-08-15_alm_existencia_negativa.sql</c>.
    /// </summary>
    public bool permitir { get; set; }

    public string? usuariocreacion { get; set; }
    public DateTime? fechacreacion { get; set; }
    public string? usuariomodificacion { get; set; }
    public DateTime? fechamodificacion { get; set; }
}
