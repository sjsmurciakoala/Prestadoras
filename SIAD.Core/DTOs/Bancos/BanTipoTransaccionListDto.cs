namespace SIAD.Core.DTOs.Bancos;

public sealed class BanTipoTransaccionListDto
{
    public string TipoTransaccion { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public string? EntraSale { get; set; }

    /// <summary>El tipo asigna numero de cheque al registrar el movimiento (emite_cheque afirmativo).</summary>
    public bool EmiteCheque { get; set; }
}
