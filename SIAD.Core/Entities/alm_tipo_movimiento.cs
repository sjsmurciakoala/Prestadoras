using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Catálogo de tipos de movimiento de almacén, configurable por el usuario. Es el
/// equivalente sano de <c>INV_TIPOSTRANSACC</c> de Centura: el comportamiento de inventario
/// no está compilado, es un dato que el usuario da de alta sin recompilar.
/// <para>
/// Dos capas (ver <c>docs/plans/2026-08-01-movimientos-almacen-catalogo-diseno.md</c> §2):
/// la <see cref="clase"/> es la semántica que el motor de posteo sabe ejecutar
/// (ENTRADA/SALIDA/VALOR, espejo de <see cref="SIAD.Core.Constants.ClaseAjusteInventario"/>);
/// esta fila es el nombre de negocio ("Merma por vencimiento", "Donación") que la agrupa.
/// El motor ejecuta la clase sin conocer el tipo.
/// </para>
/// <para>Multiempresa (<see cref="ICompanyScopedEntity"/>): una fila por (company_id, codigo).</para>
/// </summary>
public partial class alm_tipo_movimiento : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }

    /// <summary>Código estable del tipo (se usa en referencias y en el asiento del kardex). Único por empresa.</summary>
    public string codigo { get; set; } = null!;

    /// <summary>Nombre de negocio; editable sin romper histórico (el código es el ancla).</summary>
    public string nombre { get; set; } = null!;

    /// <summary>
    /// <c>ENTRADA</c> suma existencia y recalcula el costo promedio · <c>SALIDA</c> resta al
    /// promedio vigente sin moverlo · <c>VALOR</c> corrige sólo el costo (cantidad 0).
    /// Ver <see cref="SIAD.Core.Constants.ClaseAjusteInventario"/>. NO es editable una vez que
    /// el tipo tiene movimientos posteados (lo impone el servicio).
    /// </summary>
    public string clase { get; set; } = null!;

    /// <summary>
    /// true = usar este tipo en un documento exige el permiso
    /// <c>module.inventario.movimientos.autorizar_sensibles</c> (validado en el servicio del
    /// documento, Fase 2). No bifurca un flujo de aprobación.
    /// </summary>
    public bool requiere_autorizacion { get; set; }

    /// <summary>
    /// Override de la cuenta contable del asiento. NULL = el asiento hereda la cuenta del tipo
    /// de artículo. Sólo se llena cuando el tipo exige una cuenta distinta (p. ej. Donación).
    /// </summary>
    public string? cuenta_contable { get; set; }

    /// <summary>false = oculto del combo de captura y rechazado en documentos nuevos; el histórico lo sigue resolviendo. No se borra: se desactiva.</summary>
    public bool activo { get; set; } = true;

    /// <summary>Orden de despliegue en el combo de captura.</summary>
    public short orden { get; set; }

    public string? usuariocreacion { get; set; }
    public DateTime? fechacreacion { get; set; }
    public string? usuariomodificacion { get; set; }
    public DateTime? fechamodificacion { get; set; }
}
