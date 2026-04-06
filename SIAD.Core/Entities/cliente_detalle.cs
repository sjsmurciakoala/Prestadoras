using System;
using System.Collections.Generic;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class cliente_detalle : ICompanyScopedEntity
{
    public int detalle_cliente_id { get; set; }

    public int maestro_cliente_id { get; set; }

    public long company_id { get; set; }

    public string? detalle_cliente_telefono { get; set; }

    public string? detalle_cliente_movil { get; set; }

    public string? detalle_cliente_email { get; set; }

    public string? detalle_cliente_direccion { get; set; }

    public string? detalle_cliente_color_casa { get; set; }

    public string? detalle_cliente_inquilino { get; set; }

    public int? maestro_medidor_id { get; set; }

    public string? empresa_nombre { get; set; }

    public string? empresa_telefono { get; set; }

    public string? empresa_direccion { get; set; }

    public string? negocio_nombre { get; set; }

    public string? negocio_telefono { get; set; }

    public string? negocio_clave_catastral { get; set; }

    public bool? estado { get; set; }

    public string? usuariocreacion { get; set; }

    public DateTime? fechacreacion { get; set; }

    public string? usuariomodificacion { get; set; }

    public DateTime? fechamodificacion { get; set; }

    public string? clave { get; set; }

    public string? observaciones { get; set; }

    public string? numero_contrato { get; set; }

    public decimal? descuento_valor { get; set; }

    public virtual cliente_maestro maestro_cliente { get; set; } = null!;

    public virtual maestro_medidor? maestro_medidor { get; set; }
}
