using System;
using System.Collections.Generic;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class cliente_maestro : ICompanyScopedEntity
{
    public int maestro_cliente_id { get; set; }

    public long company_id { get; set; }

    public string maestro_cliente_clave { get; set; } = null!;

    public string maestro_cliente_identidad { get; set; } = null!;

    public string? maestro_cliente_rtn { get; set; }

    public string maestro_cliente_nombre { get; set; } = null!;

    public bool? maestro_cliente_tercera_edad { get; set; }

    public int? categoria_servicio_id { get; set; }

    public string? barrio_codigo { get; set; }

    public DateTime? maestro_cliente_fecha_baja { get; set; }

    public string? maestro_cliente_indicativo_ruta { get; set; }

    public string? maestro_cliente_secuencia { get; set; }

    public bool estado { get; set; }

    public string? usuariocreacion { get; set; }

    public DateTime? fechacreacion { get; set; }

    public string? usuariomodificacion { get; set; }

    public DateTime? fechamodificacion { get; set; }

    public string? tipo_uso_codigo { get; set; }

    public int? ciclos_id { get; set; }

    public DateTime? cliente_fecha_nac { get; set; }

    public bool? maestro_cliente_tiene_contrato { get; set; }

    public bool? maestro_cliente_tiene_convenio { get; set; }

    public bool? maestro_cliente_tiene_medidor { get; set; }

    public string? clave_sure { get; set; }

    public string? contador { get; set; }

    public string? letracodigo { get; set; }

    public double? descuento_tercera_edad { get; set; }

    public bool? bloqueado_cobranza { get; set; }

    public bool? maestro_cliente_estudio_socioeconomico { get; set; }

    public bool? no_cortable { get; set; }

    public int? abogado { get; set; }

    public virtual barrio? barrio_codigoNavigation { get; set; }

    public virtual categoria_servicio? categoria_servicio { get; set; }

    public virtual ciclo? ciclos { get; set; }

    public virtual ICollection<cliente_detalle> cliente_detalles { get; set; } = new List<cliente_detalle>();

    public virtual ICollection<cln_plan_pago_hdr> cln_plan_pago_hdrs { get; set; } = new List<cln_plan_pago_hdr>();

    public virtual tipo_uso_servicio? tipo_uso_codigoNavigation { get; set; }
}
