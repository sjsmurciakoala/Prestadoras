namespace SIAD.Core.DTOs.Presupuesto;

/// <summary>
/// Aviso devuelto por el control presupuestario cuando el modo es <b>Advertencia</b>: la operación
/// pasó, pero excedió el disponible o no encontró presupuesto para una cuenta marcada como
/// presupuestable.
/// <para>
/// En modo <b>Bloqueo</b> no hay avisos: la operación falla con
/// <see cref="System.InvalidOperationException"/> y el mensaje al usuario. En modo <b>Apagado</b>
/// la lista siempre viene vacía.
/// </para>
/// </summary>
public sealed class PresupuestoAvisoDto
{
    /// <summary>
    /// Código de la cuenta contable afectada, <b>sin formato</b>. La pantalla lo formatea con
    /// <c>IAccountFormatService</c> antes de mostrarlo. Null cuando el aviso es por renglones sin
    /// cuenta presupuestaria configurada.
    /// </summary>
    public string? CuentaCode { get; set; }

    /// <summary>Disponible de la partida al momento de validar. Null si la cuenta no tiene presupuesto vigente.</summary>
    public decimal? Disponible { get; set; }

    /// <summary>Monto que la operación necesitaba de esa partida.</summary>
    public decimal? Requerido { get; set; }

    /// <summary>Cuánto se pasó del disponible. Null si no hay presupuesto contra el cual medirlo.</summary>
    public decimal? Exceso { get; set; }

    /// <summary>Siempre <c>true</c> en los avisos: son justamente los casos que habrían fallado en modo Bloqueo.</summary>
    public bool Excedio { get; set; }
}

/// <summary>
/// Cómo quedaría el presupuesto si se aprobara la orden, <b>antes</b> de intentarlo. Alimenta el
/// panel de la pantalla: ver el tope después del rechazo es la peor forma de enterarse.
/// </summary>
public sealed class PresupuestoPrevioDto
{
    /// <summary>0 Apagado · 1 Advertencia · 2 Bloqueo. Con 0 la pantalla no muestra el panel.</summary>
    public short Modo { get; set; }

    /// <summary>true si el control está encendido (modo 1 o 2).</summary>
    public bool Activo => Modo > 0;

    /// <summary>true si alguna partida no alcanza. En modo Bloqueo, aprobar fallaría.</summary>
    public bool TieneFaltantes => Partidas.Exists(p => p.Falta);

    /// <summary>Una fila por partida presupuestaria afectada por la orden.</summary>
    public List<PresupuestoPrevioPartidaDto> Partidas { get; set; } = new();
}

/// <summary>Una partida presupuestaria y cómo la deja esta orden.</summary>
public sealed class PresupuestoPrevioPartidaDto
{
    /// <summary>Cuenta contable, sin formato. La pantalla la formatea con <c>IAccountFormatService</c>.</summary>
    public string? CuentaCode { get; set; }

    public string? CuentaNombre { get; set; }

    /// <summary>Lo que esta orden necesita de la partida.</summary>
    public decimal Requerido { get; set; }

    /// <summary>Disponible actual. Null si la cuenta no tiene presupuesto vigente.</summary>
    public decimal? Disponible { get; set; }

    /// <summary>false = la cuenta no está marcada como presupuestable: no participa del control.</summary>
    public bool Presupuestable { get; set; }

    /// <summary>Lo que quedaría si se aprueba. Null si no hay presupuesto contra el cual medirlo.</summary>
    public decimal? Restante => Disponible.HasValue ? Disponible.Value - Requerido : null;

    /// <summary>
    /// La orden no cabe en esta partida: o no alcanza el disponible, o la cuenta está marcada como
    /// presupuestable pero no tiene presupuesto vigente. Ambos casos rechazan en modo Bloqueo.
    /// </summary>
    public bool Falta => Presupuestable && (Disponible is null || Requerido > Disponible.Value);

    /// <summary>Cuánto falta. Cero si alcanza.</summary>
    public decimal Faltante => Falta && Disponible.HasValue ? Requerido - Disponible.Value : 0m;

    /// <summary>El renglón no tiene cuenta presupuestaria resoluble (ni capturada ni en su tipo).</summary>
    public bool SinCuenta => string.IsNullOrWhiteSpace(CuentaCode);
}
