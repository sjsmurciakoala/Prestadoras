using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class alm_bodega : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }
    public string codigo { get; set; } = null!;
    public string nombre { get; set; } = null!;
    public string? direccion { get; set; }
    public string? responsable { get; set; }
    public bool activo { get; set; }

    /// <summary>
    /// Override del interruptor de existencia negativa, por bodega. TRI-ESTADO:
    /// <c>null</c> = hereda de <c>cfg_inventario_negativo.permitir</c> de la empresa;
    /// <c>true</c> = fuerza PERMITIR aquí; <c>false</c> = fuerza BLOQUEAR aquí. Nace
    /// <c>null</c> en todas las bodegas. Ver <c>2026-08-15_alm_existencia_negativa.sql</c>.
    /// </summary>
    public bool? permite_existencia_negativa { get; set; }

    public string? usuariocreacion { get; set; }
    public DateTime? fechacreacion { get; set; }
    public string? usuariomodificacion { get; set; }
    public DateTime? fechamodificacion { get; set; }
}
