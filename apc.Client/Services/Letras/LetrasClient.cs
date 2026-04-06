using System.Text;
using System.Text.Json;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Letras;

namespace apc.Client.Services.Letras;

public class LetrasClient
{
    private readonly HttpClient _httpClient;
    private const string ApiUrl = "api/letras";

    public LetrasClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<LetraListItemDto>> GetAsync(LetraFilterDto? filtro = null, CancellationToken ct = default)
    {
        var url = BuildQueryUrl(ApiUrl, filtro);
        var response = await _httpClient.GetAsync(url, ct);
        return await HandleResponse<List<LetraListItemDto>>(response);
    }

    public async Task<PagedResult<LetraListItemDto>> GetPagedAsync(
        LetraFilterDto? filtro = null,
        int skip = 0,
        int take = 50,
        string? sortField = null,
        bool sortDesc = false,
        CancellationToken ct = default)
    {
        var url = BuildPaginationUrl(ApiUrl, filtro, skip, take, sortField, sortDesc);
        var response = await _httpClient.GetAsync(url, ct);
        return await HandleResponse<PagedResult<LetraListItemDto>>(response);
    }

    public async Task<LetraDetailDto?> GetByIdAsync(string letra, CancellationToken ct = default)
    {
        var url = $"{ApiUrl}/{Uri.EscapeDataString(letra)}";
        var response = await _httpClient.GetAsync(url, ct);
        
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        return await HandleResponse<LetraDetailDto>(response);
    }

    public async Task CreateAsync(LetraEditDto dto, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(dto);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(ApiUrl, content, ct);
        
        if (!response.IsSuccessStatusCode)
        {
            await HandleErrorResponse(response);
        }
    }

    public async Task UpdateAsync(string letra, LetraEditDto dto, CancellationToken ct = default)
    {
        var url = $"{ApiUrl}/{Uri.EscapeDataString(letra)}";
        var json = JsonSerializer.Serialize(dto);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PutAsync(url, content, ct);
        
        if (!response.IsSuccessStatusCode)
        {
            await HandleErrorResponse(response);
        }
    }

    public async Task<bool> DeleteAsync(string letra, CancellationToken ct = default)
    {
        var url = $"{ApiUrl}/{Uri.EscapeDataString(letra)}";
        var response = await _httpClient.DeleteAsync(url, ct);
        return response.IsSuccessStatusCode;
    }

    private string BuildQueryUrl(string apiUrl, LetraFilterDto? filtro)
    {
        var queryParams = new List<string>();
        
        if (!string.IsNullOrWhiteSpace(filtro?.Search))
        {
            queryParams.Add($"search={Uri.EscapeDataString(filtro.Search)}");
        }

        return queryParams.Count > 0 ? $"{apiUrl}?{string.Join("&", queryParams)}" : apiUrl;
    }

    private string BuildPaginationUrl(
        string apiUrl,
        LetraFilterDto? filtro,
        int skip,
        int take,
        string? sortField,
        bool sortDesc)
    {
        var queryParams = new List<string>
        {
            $"skip={skip}",
            $"take={take}"
        };

        if (!string.IsNullOrWhiteSpace(filtro?.Search))
        {
            queryParams.Add($"search={Uri.EscapeDataString(filtro.Search)}");
        }

        if (!string.IsNullOrWhiteSpace(sortField))
        {
            queryParams.Add($"sortField={Uri.EscapeDataString(sortField)}");
            queryParams.Add($"sortDesc={sortDesc}");
        }

        return $"{apiUrl}/paged?{string.Join("&", queryParams)}";
    }

    private async Task<T> HandleResponse<T>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            await HandleErrorResponse(response);
        }

        var jsonContent = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) 
            ?? throw new InvalidOperationException("Unable to deserialize response");
    }

    private async Task HandleErrorResponse(HttpResponseMessage response)
    {
        var errorContent = await response.Content.ReadAsStringAsync();
        
        throw response.StatusCode switch
        {
            System.Net.HttpStatusCode.BadRequest => new HttpRequestException(
                $"Datos inválidos: {errorContent}") { Data = { { "StatusCode", 400 } } },
            System.Net.HttpStatusCode.Conflict => new HttpRequestException(
                $"La letra ya existe: {errorContent}") { Data = { { "StatusCode", 409 } } },
            System.Net.HttpStatusCode.NotFound => new HttpRequestException(
                "Letra no encontrada") { Data = { { "StatusCode", 404 } } },
            _ => new HttpRequestException($"Error: {response.StatusCode} - {errorContent}")
        };
    }
}
