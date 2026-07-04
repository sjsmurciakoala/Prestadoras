using System;

using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Cabecera de configuración de integración contable-comercial por empresa
/// (plan 2026-07-02 F1). Los modos definen la granularidad de las líneas
/// analíticas: GENERAL / POR_SERVICIO / POR_SERVICIO_CATEGORIA.
/// </summary>
public partial class con_integracion_config : ICompanyScopedEntity
{
    public long config_id { get; set; }

    public long company_id { get; set; }

    public string modo_ventas { get; set; } = "GENERAL";

    public string modo_cxc { get; set; } = "GENERAL";

    public bool encolar_sin_periodo { get; set; } = true;

    public bool activo_facturacion { get; set; }

    public bool activo_caja { get; set; }

    public bool activo_bancos { get; set; }

    public bool activo_notas { get; set; }

    public bool activo_miscelaneos { get; set; }

    public bool activo_proveedores { get; set; }

    /// <summary>
    /// Meses de desfase tolerados entre el mes comercial abierto y el período
    /// contable abierto antes de emitir aviso (F7, decisión D6).
    /// </summary>
    public short desfase_max_meses { get; set; } = 1;

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }
}
