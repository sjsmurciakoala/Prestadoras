using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class pagovariostemp
{
    public int id { get; set; }

    public int? recibo { get; set; }

    public string? codigo { get; set; }

    public DateOnly? fecha { get; set; }

    public DateOnly? fecha_vence { get; set; }

    public string? identidad { get; set; }

    public string? nombre { get; set; }

    public string? descripcion { get; set; }

    public decimal? valor_ { get; set; }

    public string? usuario { get; set; }

    public string? tipo_servicio { get; set; }

    public string? tipo_factura { get; set; }

    public string? cod_banco { get; set; }

    public string? cajero { get; set; }

    public string? cliente_clave { get; set; }

    public string? estado { get; set; }

    public string? expe { get; set; }
}
