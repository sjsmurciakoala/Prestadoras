using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class adm_ajuste_tarifario
{
    public long ajuste_tarifario_id { get; set; }

    public long company_id { get; set; }

    public long cuadro_tarifario_id { get; set; }

    public long tipo_ajuste_tarifario_id { get; set; }

    public int orden { get; set; }

    public long? servicio_referencia_id { get; set; }

    public decimal? monto_fijo { get; set; }

    public decimal? porcentaje { get; set; }

    public decimal? tope_maximo { get; set; }

    public string? condicion_codigo { get; set; }

    /// <summary>jsonb — parámetros del ajuste (ej. tope_mensual, alcance, categoria).</summary>
    public string? parametros { get; set; }

    public short status_id { get; set; }

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }

    public virtual adm_cuadro_tarifario cuadro_tarifario { get; set; } = null!;

    public virtual adm_tipo_ajuste_tarifario tipo_ajuste_tarifario { get; set; } = null!;
}
