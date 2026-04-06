using SIAD.Core.DTOs.Tenant;

namespace apc.Client.Services.Tenant;

public sealed class TenantCompanyContextClient
{
    private readonly HttpClient httpClient;

    public TenantCompanyContextClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<TenantCompanyContextDto> ObtenerAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await httpClient.GetFromJsonAsyncWithAuthCheck<TenantCompanyContextDto>(
                "api/tenant/context", ct);

            return result ?? new TenantCompanyContextDto
            {
                Message = "No fue posible validar la empresa activa."
            };
        }
        catch (UnauthorizedAccessException)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new HttpRequestException("No fue posible validar la empresa activa.", ex);
        }
    }
}
