using System.Threading;
using System.Threading.Tasks;
using SIAD.Core.DTOs.Cobros;
using SIAD.Core.DTOs.Common;

namespace SIAD.Services.Cobros;

/// <summary>
/// Motor único de cobro (unificación cobranza F2 — plan §4). Toda entrada de
/// pago del sistema pasa por aquí: caja (F3), fachadas legacy (F2) y, desde F5,
/// el WS bancario. Reglas únicas: parcialidad siempre permitida, aplicación por
/// documento (adm_pago + adm_pago_aplicacion), sesión de caja obligatoria en
/// ventanilla, idempotencia por referencia externa, reverso sin DELETE y
/// dual-write hacia transaccion_abonado hasta F7.
/// </summary>
public interface ICobroService
{
    Task<ResponseModelDto> RegistrarCobroAsync(CobroCrearDto dto, CancellationToken ct = default);

    Task<ResponseModelDto> ReversarCobroAsync(CobroReversoDto dto, CancellationToken ct = default);

    /// <summary>Cobros del día desde el modelo nuevo (adm_pago), opcionalmente por usuario.</summary>
    Task<IReadOnlyList<CobroDelDiaDto>> ListarCobrosDelDiaAsync(DateTime? fecha, string? usuario, CancellationToken ct = default);
}
