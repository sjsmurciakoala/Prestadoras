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
    /// F0 integración contable de almacén (2026-08-05): si true, los movimientos de
    /// almacén (alm_*) generan su partida por el motor de integración
    /// (sp_con_generar_comprobante_config, module = ALMACEN). Requiere además la fila
    /// de diario + tipo de partida en con_integracion_asiento. Default false = no postea.
    /// </summary>
    public bool activo_almacen { get; set; }

    /// <summary>
    /// Fase 2 de compras (2026-08-12, módulo COMPRAS): si true, la factura de compra y el pago de
    /// la CxP generan su partida por el motor de integración (module = COMPRAS), separado de
    /// inventario (ALMACEN) y de los pagos OPD (PROV). Requiere la fila de diario + tipo en
    /// con_integracion_asiento. Default false = no postea. Columna de
    /// 2026-08-12_con_integracion_compras_modulo.sql.
    /// </summary>
    public bool activo_compras { get; set; }

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
