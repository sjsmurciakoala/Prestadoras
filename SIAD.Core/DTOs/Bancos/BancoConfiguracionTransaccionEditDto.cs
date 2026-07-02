using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Bancos;

public class BancoConfiguracionTransaccionEditDto
{
    [Required(ErrorMessage = "El codigo es obligatorio.")]
    [StringLength(3, ErrorMessage = "El codigo no puede superar 3 caracteres.")]
    public string TipoTransaccionId { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripcion es obligatoria.")]
    [StringLength(40, ErrorMessage = "La descripcion no puede superar 40 caracteres.")]
    public string DescripcionTransaccion { get; set; } = string.Empty;

    [Required(ErrorMessage = "El tipo de partida es obligatorio.")]
    [Range(typeof(long), "1", "9223372036854775807", ErrorMessage = "Seleccione un tipo de partida valido.")]
    public long? TipoPartidaTypeId { get; set; }

    public bool UsaCentroCosto { get; set; }

    [Range(typeof(long), "1", "9223372036854775807", ErrorMessage = "Seleccione un centro de costo valido.")]
    public long? CentroCostoId { get; set; }

    [Required(ErrorMessage = "El correlativo es obligatorio.")]
    [StringLength(6, ErrorMessage = "El correlativo no puede superar 6 caracteres.")]
    public string Correlativo { get; set; } = string.Empty;

    [StringLength(13, ErrorMessage = "La cuenta contable no puede superar 13 caracteres.")]
    public string? CuentaContable { get; set; }

    [StringLength(9, ErrorMessage = "El destino no puede superar 9 caracteres.")]
    public string? Destino { get; set; }

    [Required(ErrorMessage = "El tipo de movimiento es obligatorio.")]
    [StringLength(1, ErrorMessage = "El tipo de movimiento debe tener 1 caracter.")]
    public string EntraSale { get; set; } = string.Empty;

    public bool DelSistema { get; set; }
    public bool EmiteCheque { get; set; }
    public bool Pad { get; set; }
    public bool Pda { get; set; }
    public bool DetalleContable { get; set; }
    public bool CuentaAlterna { get; set; }
    public string? Observaciones { get; set; }
}
