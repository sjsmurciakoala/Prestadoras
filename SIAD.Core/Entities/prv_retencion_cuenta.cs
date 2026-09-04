using System;

using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Cuenta contable del pasivo ("retenciones por pagar") por empresa y retención. TENANT-scoped.
/// <para>
/// El catálogo de retenciones (<see cref="cfg_retencion"/> / <see cref="cfg_retencion_tasa"/>) es
/// global — el % lo fija la ley. Lo único que cambia entre empresas es a qué cuenta del plan va el
/// pasivo, por eso esta tabla SÍ lleva <c>company_id</c> e implementa <see cref="ICompanyScopedEntity"/>.
/// No se siembra: se configura desde la pantalla de mantenimiento. La cuenta debe ser posteable
/// (<c>allows_posting</c>) — molde: <see cref="con_integracion_cuenta"/>.
/// </para>
/// </summary>
public partial class prv_retencion_cuenta : ICompanyScopedEntity
{
    public long id { get; set; }

    public long company_id { get; set; }

    public int retencion_id { get; set; }

    /// <summary>FK a <c>con_plan_cuentas(account_id)</c>. Debe ser una cuenta posteable del pasivo.</summary>
    public long account_id { get; set; }

    public bool activo { get; set; } = true;

    public string? usuariocreacion { get; set; }

    public DateTime? fechacreacion { get; set; }

    public string? usuariomodificacion { get; set; }

    public DateTime? fechamodificacion { get; set; }

    public virtual cfg_retencion? retencion { get; set; }

    public virtual con_plan_cuenta? account { get; set; }
}
