using System;
using System.Collections.Generic;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Cabecera de la requisición interna de materiales (SOLICITUD). <b>NUNCA genera asientos de
/// kardex</b>: la salida la produce el DESCARGO (<see cref="alm_descargo_hdr"/>). El histórico
/// migrado de SIMAFI vive en la tabla plana <see cref="alm_requisicion"/> con
/// <c>requisicion_hdr_id</c> NULL. Ver <c>Database/2026-08-01_alm_requisicion_descargo.sql</c> y
/// <c>docs/plans/2026-08-04-requisiciones-descargos-fase6-plan.md</c>.
/// </summary>
public partial class alm_requisicion_hdr : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }

    /// <summary>Correlativo por empresa (continúa el histórico: la 1ª nueva es 17125).</summary>
    public int numero { get; set; }

    /// <summary>1 Salida de bodega (consume stock) · 2 Reabastecimiento (alimenta O/C, no toca inventario).</summary>
    public short tipo { get; set; }

    /// <summary>Ver <see cref="SIAD.Core.Constants.EstadoRequisicionHdr"/>. 4/5 son derivados de cantidad_despachada.</summary>
    public short estado { get; set; }

    public DateOnly fecha { get; set; }
    public DateOnly? fecha_requerida { get; set; }

    /// <summary>Bodega de la que saldrá la mercadería.</summary>
    public int bodega_id { get; set; }

    /// <summary>Texto libre: no hay catálogo de departamentos (decisión D-15a).</summary>
    public string? departamento { get; set; }

    public string solicitante { get; set; } = null!;
    public string? cargo_solicitante { get; set; }

    /// <summary>Login del que captura (distinto del nombre mostrado).</summary>
    public string usuario_solicita { get; set; } = null!;

    /// <summary>Destino/uso de lo solicitado.</summary>
    public string? aplicacion { get; set; }
    public string? observacion { get; set; }

    public string? aprobado_por { get; set; }
    public DateTime? fecha_aprobacion { get; set; }
    public string? rechazado_por { get; set; }
    public DateTime? fecha_rechazo { get; set; }
    public string? motivo_rechazo { get; set; }
    public string? anulado_por { get; set; }
    public DateTime? fecha_anulacion { get; set; }
    public string? motivo_anulacion { get; set; }

    /// <summary>Referencial (precio de solicitud); el costo real lo estampa el descargo al postear.</summary>
    public decimal total { get; set; }

    /// <summary>Idempotencia del ALTA.</summary>
    public Guid? uuid { get; set; }

    public string? usuariocreacion { get; set; }
    public DateTime? fechacreacion { get; set; }
    public string? usuariomodificacion { get; set; }
    public DateTime? fechamodificacion { get; set; }

    /// <summary>Renglones de la solicitud (filas de la tabla plana con este <c>requisicion_hdr_id</c>).</summary>
    public virtual ICollection<alm_requisicion> lineas { get; set; } = new List<alm_requisicion>();
}
