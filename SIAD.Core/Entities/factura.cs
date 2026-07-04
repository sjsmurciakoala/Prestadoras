using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class factura
{
    public int id { get; set; }

    public long company_id { get; set; }

    public int numrecibo { get; set; }

    public string? numfactura { get; set; }

    public string? clientecodigo { get; set; }

    public string? tipofactura { get; set; }

    public string? ano { get; set; }

    public string? mes { get; set; }

    public DateOnly? fechaemision { get; set; }

    public DateOnly? fechavence { get; set; }

    public string? rtn { get; set; }

    public string? periodo { get; set; }

    public string? numdei { get; set; }

    public decimal? saldototal { get; set; }

    public string? usuario { get; set; }

    public string? identidad { get; set; }

    public string? estado { get; set; }

    public string? recolectora { get; set; }

    public DateOnly? fechapago { get; set; }

    public string? tipofacturacion { get; set; }

    public string? referencia { get; set; }

    // Snapshot dimensional al emitir (plan F3): categoría y medición del
    // cliente AL MOMENTO de facturar (trigger trg_factura_snapshot_dimensional).
    public int? categoria_servicio_id { get; set; }

    public bool? con_medicion { get; set; }
}
