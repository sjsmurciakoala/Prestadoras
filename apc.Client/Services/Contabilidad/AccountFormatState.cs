using System.Net.Http.Json;
using apc.Client.Services.Tenant;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Core.Utilities;

namespace apc.Client.Services.Contabilidad;

/// <summary>
/// Estado por circuito con la máscara de cuentas contables de la empresa actual
/// (con_configuracion_sistema.formato_cuentas / separador_codigo), cargada una sola vez
/// desde api/contabilidad/formato-cuentas (endpoint sin permiso de módulo).
/// Las páginas deben llamar <see cref="EnsureLoadedAsync"/> antes de formatear.
/// </summary>
public sealed class AccountFormatState
{
    private readonly HttpClient http;
    private readonly TenantState tenantState;
    private readonly SemaphoreSlim loadLock = new(1, 1);
    private long loadedCompanyId;

    public AccountFormatState(HttpClient http, TenantState tenantState)
    {
        this.http = http;
        this.tenantState = tenantState;
    }

    public string Mask { get; private set; } = AccountCodeFormatter.DefaultMask;

    public string Separator { get; private set; } = AccountCodeFormatter.DefaultSeparator;

    public async ValueTask EnsureLoadedAsync(CancellationToken ct = default)
    {
        long companyId;
        try
        {
            companyId = await tenantState.EnsureCompanyAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            // Sin empresa resuelta se formatea con la máscara por defecto; se reintenta en la próxima carga.
            return;
        }

        if (companyId <= 0 || companyId == loadedCompanyId)
        {
            return;
        }

        await loadLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (companyId == loadedCompanyId)
            {
                return;
            }

            try
            {
                var dto = await http.GetFromJsonAsync<AccountFormatDto>("api/contabilidad/formato-cuentas", ct)
                    .ConfigureAwait(false);
                Mask = string.IsNullOrWhiteSpace(dto?.FormatoCuentas) ? AccountCodeFormatter.DefaultMask : dto.FormatoCuentas;
                Separator = string.IsNullOrWhiteSpace(dto?.SeparadorCodigo) ? AccountCodeFormatter.DefaultSeparator : dto.SeparadorCodigo;
                loadedCompanyId = companyId;
            }
            catch
            {
                // Fallo transitorio: se mantienen los valores actuales y se reintenta en la próxima carga.
            }
        }
        finally
        {
            loadLock.Release();
        }
    }

    public string Format(string? code) => AccountCodeFormatter.Format(code, Mask, Separator);

    public string FormatDisplay(string? code, string? description) =>
        AccountCodeFormatter.FormatDisplay(code, description, Mask, Separator);

    public string Normalize(string? value) => AccountCodeFormatter.Normalize(value);
}
