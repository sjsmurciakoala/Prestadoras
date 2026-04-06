using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class ban_kardex
{
    public long ban_kardex_id { get; set; }

    public long company_id { get; set; }

    public string? correlativo_t_transacc { get; set; }

    public long banco_cuenta_id { get; set; }

    public long? ban_banco_id { get; set; }

    public long? ban_moneda_id { get; set; }

    public long id_tipo_transaccion { get; set; }

    public DateOnly fecha_movimiento { get; set; }

    public DateTime fecha_registro { get; set; }

    public DateOnly? fecha_conciliacion { get; set; }

    public string? usuario_conciliacion { get; set; }

    public string? estado_conciliacion { get; set; }

    public string? descripcion { get; set; }

    public string? referencia { get; set; }

    public decimal monto { get; set; }

    public decimal saldo { get; set; }

    public int estado { get; set; }

    public decimal tasa_cambio { get; set; }

    public long? partida_cuenta_id { get; set; }

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }

    public virtual ban_banco? ban_banco { get; set; }

    public virtual ban_cuenta banco_cuenta { get; set; } = null!;

    public virtual ban_moneda? ban_moneda { get; set; }

    public virtual cfg_company company { get; set; } = null!;
}
