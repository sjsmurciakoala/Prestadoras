using System.Net.Http.Json;
using apc.Client.Services.Tenant;
using SIAD.Core.DTOs.Mantenimientos;
using SIAD.Core.Utilities;

namespace apc.Client.Services.Mantenimientos;

/// <summary>
/// Estado por circuito con los formatos fiscales activos de la empresa actual
/// (cfg_formato_fiscal), cargados una sola vez desde
/// api/mantenimientos/formatos-fiscales/lookup (endpoint sin permiso de módulo).
/// Las páginas deben llamar <see cref="EnsureLoadedAsync"/> antes de formatear o validar.
/// </summary>
/// <remarks>
/// Degrada en silencio: sin empresa resuelta o con el GET caído se comporta como si no
/// hubiera formato configurado, y la vista sigue capturando texto libre igual que antes.
/// </remarks>
public sealed class FormatoFiscalState
{
    /// <summary>Código del formato del No. de factura del proveedor.</summary>
    public const string CodigoNumeroSar = "NUMERO_SAR";

    /// <summary>Código del formato del CAI del proveedor.</summary>
    public const string CodigoCai = "CAI";

    private readonly HttpClient http;
    private readonly TenantState tenantState;
    private readonly SemaphoreSlim loadLock = new(1, 1);
    private Dictionary<string, FormatoFiscalLookupDto> formatos = new(StringComparer.OrdinalIgnoreCase);
    private long loadedCompanyId;

    public FormatoFiscalState(HttpClient http, TenantState tenantState)
    {
        this.http = http;
        this.tenantState = tenantState;
    }

    public async ValueTask EnsureLoadedAsync(CancellationToken ct = default)
    {
        long companyId;
        try
        {
            companyId = await tenantState.EnsureCompanyAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            // Sin empresa resuelta no hay formato: la vista queda como texto libre. Se reintenta luego.
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
                var lista = await http
                    .GetFromJsonAsync<List<FormatoFiscalLookupDto>>("api/mantenimientos/formatos-fiscales/lookup", ct)
                    .ConfigureAwait(false);

                var mapa = new Dictionary<string, FormatoFiscalLookupDto>(StringComparer.OrdinalIgnoreCase);
                if (lista is not null)
                {
                    foreach (var f in lista)
                    {
                        if (!string.IsNullOrWhiteSpace(f.Codigo))
                        {
                            mapa[f.Codigo] = f;
                        }
                    }
                }

                formatos = mapa;
                loadedCompanyId = companyId;
            }
            catch
            {
                // Fallo transitorio: se mantiene lo que haya y se reintenta en la próxima carga.
            }
        }
        finally
        {
            loadLock.Release();
        }
    }

    /// <summary>
    /// Fuerza la recarga en la próxima llamada a <see cref="EnsureLoadedAsync"/>. La llama el
    /// mantenimiento al guardar, para que el formato nuevo se vea sin refrescar el circuito.
    /// </summary>
    public void Invalidar() => loadedCompanyId = 0;

    /// <summary>El formato configurado y activo para ese campo, o <c>null</c> si no hay ninguno.</summary>
    public FormatoFiscalLookupDto? Get(string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return null;
        return formatos.TryGetValue(codigo, out var f) ? f : null;
    }

    public bool TieneFormato(string codigo) => Get(codigo) is not null;

    /// <summary>Máscara para <c>DxMaskedInput</c>, o cadena vacía si no hay formato.</summary>
    public string MascaraDevExpress(string codigo) => Get(codigo)?.MascaraDevExpress ?? string.Empty;

    /// <summary>Ejemplo para usar como <c>NullText</c>, o el texto de respaldo si no hay formato.</summary>
    public string Ejemplo(string codigo, string respaldo = "") =>
        Get(codigo) is { } f && !string.IsNullOrWhiteSpace(f.Ejemplo) ? f.Ejemplo : respaldo;

    public bool EsObligatorio(string codigo) => Get(codigo)?.Obligatorio ?? false;

    public bool Bloquea(string codigo) => Get(codigo)?.Bloquea ?? false;

    public bool Advierte(string codigo) => Get(codigo)?.Advierte ?? false;

    /// <summary>Sin formato configurado todo valor es válido: la vista se comporta como antes.</summary>
    public bool EsValido(string codigo, string? valor)
    {
        var f = Get(codigo);
        if (f is null) return true;
        if (f.ModoValidacion == ModoValidacionFormatoFiscal.Libre) return true;
        return FiscalCodeFormatter.EsValido(valor, f.Mascara, f.Patron, f.Mayusculas);
    }

    /// <summary>Cómo se muestra el valor guardado. Sin formato, tal cual vino.</summary>
    public string Formatear(string codigo, string? valor)
    {
        var f = Get(codigo);
        if (f is null) return valor ?? string.Empty;
        return FiscalCodeFormatter.Formatear(valor, f.Mascara, f.Mayusculas);
    }

    /// <summary>Cómo se guarda el valor. Sin formato (o sin normalizar), tal cual se tecleó.</summary>
    public string? Normalizar(string codigo, string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return valor;

        var f = Get(codigo);
        if (f is null) return valor.Trim();
        if (!f.Normalizar) return f.Mayusculas ? valor.Trim().ToUpperInvariant() : valor.Trim();

        return FiscalCodeFormatter.Normalizar(valor, f.Mayusculas);
    }

    /// <summary>Mensaje para el usuario cuando el valor no cumple. Nunca muestra la expresión regular.</summary>
    public string MensajeFormato(string codigo)
    {
        var f = Get(codigo);
        if (f is null) return string.Empty;
        return $"El {f.Nombre} debe tener el formato {f.Ejemplo}.";
    }
}
