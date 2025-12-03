using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class solicitud_servicio
{
    public int solicitud_servicio_id { get; set; }

    public string cliente_identidad { get; set; } = null!;

    public int categoria_servicio_id { get; set; }

    public string? cliente_rtn { get; set; }

    public string cliente_nombre { get; set; } = null!;

    public string? cliente_telefono { get; set; }

    public string cliente_movil { get; set; } = null!;

    public string? cliente_email { get; set; }

    public string cliente_direccion { get; set; } = null!;

    public string? cliente_color_casa { get; set; }

    public string? observacion { get; set; }

    public string? empresa_nombre { get; set; }

    public string? empresa_telefono { get; set; }

    public string? empresa_direccion { get; set; }

    public string? negocio_nombre { get; set; }

    public string? negocio_telefono { get; set; }

    public string? negocio_clave_catastral { get; set; }

    public bool estado { get; set; }

    public string? usuariocreacion { get; set; }

    public DateTime? fechacreacion { get; set; }

    public string? usuariomodificacion { get; set; }

    public DateTime? fechamodificacion { get; set; }

    public bool? asiginada { get; set; }

    public DateTime? fechanacimiento { get; set; }

    public string? clave_sure { get; set; }

    public virtual categoria_servicio categoria_servicio { get; set; } = null!;
}
