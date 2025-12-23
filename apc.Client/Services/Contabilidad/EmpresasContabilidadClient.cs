using System.Net.Http.Json;
using SIAD.Core.DTOs.Contabilidad;

namespace apc.Client.Services.Contabilidad;

public sealed class EmpresasContabilidadClient
{
    private readonly HttpClient http;

    public EmpresasContabilidadClient(HttpClient http)
    {
        this.http = http;
    }

    // Exponer HttpClient para operaciones especiales (ej. upload manual)
    public HttpClient Http => http;

    public async Task<CompanyCreationDto> CrearAsync(CompanyCreationDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var response = await http.PostAsJsonAsync("api/contabilidad/empresas", dto, cancellationToken: ct);
        
        try
        {
            var resultado = await response.ReadFromJsonAsyncWithAuthCheck<CompanyCreationDto>(ct);
            if (resultado is null)
            {
                throw new InvalidOperationException("El servicio devolvió una respuesta vacía.");
            }
            return resultado;
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
            throw new HttpRequestException("No fue posible crear la empresa.", ex);
        }
    }

    public async Task<CompanyCreationDto?> ObtenerAsync(long companyId, CancellationToken ct = default)
    {
        if (companyId <= 0)
        {
            return null;
        }

        var response = await http.GetAsync($"api/contabilidad/empresas/{companyId}", ct);
        
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        try
        {
            return await response.ReadFromJsonAsyncWithAuthCheck<CompanyCreationDto>(ct);
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
            throw new HttpRequestException("No fue posible obtener la empresa.", ex);
        }
    }

    public async Task<CompanyCreationDto> ActualizarAsync(long companyId, CompanyCreationDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (companyId <= 0)
        {
            throw new ArgumentException("El ID de la empresa debe ser mayor que cero.", nameof(companyId));
        }

        var response = await http.PutAsJsonAsync($"api/contabilidad/empresas/{companyId}", dto, cancellationToken: ct);
        
        try
        {
            var resultado = await response.ReadFromJsonAsyncWithAuthCheck<CompanyCreationDto>(ct);
            if (resultado is null)
            {
                throw new InvalidOperationException("El servicio devolvió una respuesta vacía.");
            }
            return resultado;
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
            throw new HttpRequestException("No fue posible actualizar la empresa.", ex);
        }
    }
}
