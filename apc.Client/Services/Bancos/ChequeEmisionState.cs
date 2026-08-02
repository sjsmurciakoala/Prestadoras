using apc.Client.Services.Tenant;

namespace apc.Client.Services.Bancos;

/// <summary>
/// Estado por circuito: indica si la empresa actual puede emitir cheques, es decir si
/// existe al menos un tipo de transaccion de SALIDA con "Emite cheque" activado
/// (ban_tipos_transacciones.emite_cheque). Sin uno, el cheque manual no tiene con que
/// registrarse, asi que las vistas ocultan la opcion en vez de ofrecerla y fallar.
/// Las paginas deben llamar <see cref="EnsureLoadedAsync"/> antes de leer <see cref="Disponible"/>.
/// </summary>
public sealed class ChequeEmisionState
{
    private readonly BanTiposTransaccionesClient tiposClient;
    private readonly TenantState tenantState;
    private readonly SemaphoreSlim loadLock = new(1, 1);

    /// <summary>
    /// Empresa para la que YA se intento la lectura (haya salido bien o mal). El intento
    /// fallido tambien cuenta: el endpoint de tipos exige permiso de Bancos, y sin el
    /// reintentar en cada navegacion seria un round trip condenado a fallar.
    /// </summary>
    private long companyIdIntentada;

    public ChequeEmisionState(BanTiposTransaccionesClient tiposClient, TenantState tenantState)
    {
        this.tiposClient = tiposClient;
        this.tenantState = tenantState;
    }

    /// <summary>Hay al menos un tipo de transaccion de salida que emite cheque.</summary>
    public bool Disponible { get; private set; }

    /// <summary>
    /// Se dispara cuando <see cref="Disponible"/> cambia o cuando se invalida la cache.
    /// Lo usa el menu lateral para mostrar/ocultar el item del cheque manual sin recargar.
    /// </summary>
    public event Action? Cambiado;

    public async ValueTask EnsureLoadedAsync(CancellationToken ct = default)
    {
        long companyId;
        try
        {
            companyId = await tenantState.EnsureCompanyAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            // Sin empresa resuelta no se puede afirmar nada: la opcion queda oculta.
            Disponible = false;
            companyIdIntentada = 0;
            return;
        }

        if (companyId <= 0)
        {
            Disponible = false;
            companyIdIntentada = 0;
            return;
        }

        if (companyId == companyIdIntentada)
        {
            return;
        }

        var previo = Disponible;
        var cambio = false;

        await loadLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (companyId == companyIdIntentada)
            {
                return;
            }

            // Cambio de empresa: nunca arrastrar la respuesta del tenant anterior, ni
            // siquiera si la relectura falla (seria mostrar la opcion en una empresa que
            // no puede emitir cheques).
            Disponible = false;

            try
            {
                var tipos = await tiposClient.GetAsync(companyId, ct).ConfigureAwait(false);
                Disponible = tipos.Any(t => t.EmiteCheque &&
                    !string.Equals(t.EntraSale?.Trim(), "E", StringComparison.OrdinalIgnoreCase));
            }
            catch (OperationCanceledException)
            {
                // La pagina canceló su carga: no cuenta como intento, se reintenta luego.
                cambio = Disponible != previo;
                return;
            }
            catch
            {
                // Falla real (sin permiso de Bancos, red, 500): queda como intentada para
                // no repetir el request en cada navegacion. Se recupera con Invalidar().
            }

            companyIdIntentada = companyId;
            cambio = Disponible != previo;
        }
        finally
        {
            loadLock.Release();
            if (cambio)
            {
                Cambiado?.Invoke();
            }
        }
    }

    /// <summary>
    /// Fuerza una relectura en la proxima carga. Lo llama la configuracion de tipos de
    /// transaccion al guardar, para que activar/desactivar "Emite cheque" se refleje sin
    /// recargar la aplicacion.
    /// </summary>
    public void Invalidar()
    {
        companyIdIntentada = 0;
        Cambiado?.Invoke();
    }
}
