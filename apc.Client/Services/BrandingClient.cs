using System.Net;
using System.Net.Http.Json;
using System.Threading;

namespace apc.Client.Services;

public class BrandingClient
{
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _brandingLock = new(1, 1);
    private BrandingResponse? _cachedBranding;
    private bool _brandingLoaded;

    public BrandingClient(HttpClient http) => _http = http;

    public HttpClient Http => _http;

    public record BrandingResponse(string CompanyName, string CompanyShortName, string LogoBase64, string LogoMime);

    public async Task<BrandingResponse?> GetBrandingAsync(CancellationToken ct = default)
    {
        if (_brandingLoaded)
        {
            return _cachedBranding;
        }

        await _brandingLock.WaitAsync(ct);
        try
        {
            if (_brandingLoaded)
            {
                return _cachedBranding;
            }

            using var response = await _http.GetAsync("api/branding", ct);

            if (response.StatusCode == HttpStatusCode.NoContent || response.StatusCode == HttpStatusCode.NotFound)
            {
                _brandingLoaded = true;
                _cachedBranding = null;
                return null;
            }

            response.EnsureSuccessStatusCode();

            if (response.Content?.Headers?.ContentLength == 0)
            {
                _brandingLoaded = true;
                _cachedBranding = null;
                return null;
            }

            _cachedBranding = await response.Content.ReadFromJsonAsync<BrandingResponse>(cancellationToken: ct);
            _brandingLoaded = true;
            return _cachedBranding;
        }
        finally
        {
            _brandingLock.Release();
        }
    }

    public async Task ActualizarAsync(string companyName, string companyShortName, CancellationToken ct = default)
    {
        var payload = new { CompanyName = companyName, CompanyShortName = companyShortName };
        var response = await _http.PutAsJsonAsync("api/branding", payload, cancellationToken: ct);

        try
        {
            await response.ReadFromJsonAsyncWithAuthCheck<object>(ct);
            InvalidateCache();
        }
        catch (UnauthorizedAccessException) { throw; }
        catch (HttpRequestException) { throw; }
        catch (Exception ex)
        {
            throw new HttpRequestException("No fue posible actualizar el branding.", ex);
        }
    }

    public void InvalidateCache()
    {
        _brandingLoaded = false;
        _cachedBranding = null;
    }
}
