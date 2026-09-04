using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Catálogo de términos de pago del proveedor: nombre + días de crédito, por empresa. Es la
/// base para autocalcular el vencimiento de la factura de compra (paridad con el catálogo de
/// ventas <c>CLN_TERMINOS_PAGO</c> del legacy Centura). Multiempresa vía
/// <see cref="ICompanyScopedEntity"/>.
/// </summary>
public partial class alm_termino_pago : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }
    public string nombre { get; set; } = null!;

    /// <summary>Días de crédito. 0 = contado. Se suma a la fecha de la factura para el vencimiento.</summary>
    public int dias { get; set; }

    /// <summary>Término propuesto por defecto en la factura. A lo sumo uno por empresa (índice único parcial).</summary>
    public bool es_default { get; set; }

    public bool activo { get; set; }
    public string? usuariocreacion { get; set; }
    public DateTime? fechacreacion { get; set; }
    public string? usuariomodificacion { get; set; }
    public DateTime? fechamodificacion { get; set; }
}
