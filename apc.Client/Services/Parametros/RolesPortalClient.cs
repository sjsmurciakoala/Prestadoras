using System.Net.Http.Json;
using SIAD.Core.DTOs.Roles;

namespace apc.Client.Services.Parametros;

public sealed class RolesPortalClient
{
    private readonly HttpClient http;

    public RolesPortalClient(HttpClient http)
    {
        this.http = http;
    }

    public async Task<List<RoleDto>> ListarAsync(CancellationToken ct = default)
    {
        try
        {
            return await http.GetFromJsonAsyncWithAuthCheck<List<RoleDto>>(
                "api/parametros/roles", ct) ?? [];
        }
        catch (UnauthorizedAccessException) { throw; }
        catch (HttpRequestException) { throw; }
        catch (Exception ex)
        {
            throw new HttpRequestException("No fue posible obtener la lista de roles.", ex);
        }
    }

    public async Task<RoleDto?> ObtenerAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var response = await http.GetAsync($"api/parametros/roles/{id}", ct);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        try
        {
            return await response.ReadFromJsonAsyncWithAuthCheck<RoleDto>(ct);
        }
        catch (UnauthorizedAccessException) { throw; }
        catch (HttpRequestException) { throw; }
        catch (Exception ex)
        {
            throw new HttpRequestException("No fue posible obtener el rol.", ex);
        }
    }

    public async Task<List<string>> ListarPermisosAsync(CancellationToken ct = default)
    {
        try
        {
            return await http.GetFromJsonAsyncWithAuthCheck<List<string>>(
                "api/parametros/roles/permisos", ct) ?? [];
        }
        catch (UnauthorizedAccessException) { throw; }
        catch (HttpRequestException) { throw; }
        catch (Exception ex)
        {
            throw new HttpRequestException("No fue posible obtener los permisos.", ex);
        }
    }

    public async Task CrearAsync(CreateRoleDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var response = await http.PostAsJsonAsync("api/parametros/roles", dto, cancellationToken: ct);

        try
        {
            await response.ReadFromJsonAsyncWithAuthCheck<object>(ct);
        }
        catch (UnauthorizedAccessException) { throw; }
        catch (HttpRequestException) { throw; }
        catch (Exception ex)
        {
            throw new HttpRequestException("No fue posible crear el rol.", ex);
        }
    }

    public async Task ActualizarAsync(string id, UpdateRoleDto dto, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(dto);

        var response = await http.PutAsJsonAsync($"api/parametros/roles/{id}", dto, cancellationToken: ct);

        try
        {
            await response.ReadFromJsonAsyncWithAuthCheck<object>(ct);
        }
        catch (UnauthorizedAccessException) { throw; }
        catch (HttpRequestException) { throw; }
        catch (Exception ex)
        {
            throw new HttpRequestException("No fue posible actualizar el rol.", ex);
        }
    }

    public async Task EliminarAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var response = await http.DeleteAsync($"api/parametros/roles/{id}", ct);

        try
        {
            await response.ReadFromJsonAsyncWithAuthCheck<object>(ct);
        }
        catch (UnauthorizedAccessException) { throw; }
        catch (HttpRequestException) { throw; }
        catch (Exception ex)
        {
            throw new HttpRequestException("No fue posible eliminar el rol.", ex);
        }
    }
}
