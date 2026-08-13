using System;
using System.Collections.Generic;
using SIAD.Core.Constants;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

// Libro fiscal de retenciones (F4, DDL 2026-08-07). Una cabecera por pago
// (company_id, numero_orden, numero_abono), ligada a la partida POSTED (partida_id) y al abono.
// Es el subledger fiscal por proveedor que lee F5 (constancia + reporte mensual).
public partial class prv_retencion_hdr : ICompanyScopedEntity
{
    public long retencion_hdr_id { get; set; }

    public long company_id { get; set; }

    public int numero_orden { get; set; }

    public int numero_abono { get; set; }

    public int folio { get; set; }

    public DateOnly fecha_emision { get; set; }

    public string? cod_proveedor { get; set; }

    public string? rtn_proveedor { get; set; }

    public decimal base_total { get; set; }

    public decimal total_retenido { get; set; }

    public long? partida_id { get; set; }

    public string? poliza_number { get; set; }

    public short estado_id { get; set; } = EstadoRetencion.Vigente;

    public long? cai_id { get; set; }

    public string? cai_proveedor { get; set; }

    public string? motivo_anulacion { get; set; }

    public string usuario_creo { get; set; } = null!;

    public DateTime fecha_creacion { get; set; }

    public string? usuario_modifica { get; set; }

    public DateTime? fecha_modificacion { get; set; }

    public string? usuario_anulacion { get; set; }

    public DateTime? fecha_anulacion { get; set; }

    public Guid rowid { get; set; }

    public virtual ICollection<prv_retencion_dtl> lineas { get; set; } = new List<prv_retencion_dtl>();
}
