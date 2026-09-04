using System.Collections.Generic;
using SIAD.Core.DTOs.Configuracion;
using SIAD.Services.Aprobaciones;

namespace SIAD.Tests.Infrastructure;

/// <summary>
/// No-op de <see cref="IAprobacionNotificador"/> para los tests que ejercitan la máquina de
/// estados de la orden y no el correo. Evita que las pruebas dependan de la configuración de
/// SendGrid de la base de prueba.
/// <para>
/// Registra lo que se le pidió enviar, para que un test pueda comprobar <b>que se intentó
/// notificar</b> sin mandar nada de verdad.
/// </para>
/// </summary>
public sealed class AprobacionNotificadorNoop : IAprobacionNotificador
{
    /// <summary>Niveles a los que se avisó que tienen algo pendiente, en orden.</summary>
    public List<string> Pendientes { get; } = new();

    /// <summary>Desenlaces notificados al creador ("aprobada", "rechazada", …).</summary>
    public List<string> Resueltas { get; } = new();

    public Task<CorreoEnvioResultado> NotificarPendienteOrdenCompraAsync(
        int ordenCompraId, string numero, string proveedor, decimal total, string nivel,
        CancellationToken ct = default)
    {
        Pendientes.Add(nivel);
        return Task.FromResult(CorreoEnvioResultado.Skip("noop"));
    }

    public Task<CorreoEnvioResultado> NotificarResueltaOrdenCompraAsync(
        string? creador, string numero, string proveedor, decimal total, string desenlace,
        string? motivo, CancellationToken ct = default)
    {
        Resueltas.Add(desenlace);
        return Task.FromResult(CorreoEnvioResultado.Skip("noop"));
    }
}
