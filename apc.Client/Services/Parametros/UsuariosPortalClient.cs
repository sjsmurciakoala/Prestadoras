using System.Net.Http.Json;
using SIAD.Core.DTOs.Usuarios;

namespace apc.Client.Services.Parametros;

public sealed class UsuariosPortalClient
{
    private readonly HttpClient http;

    public UsuariosPortalClient(HttpClient http)
    {
        this.http = http;
    }

    public async Task<List<UsuarioPortalDto>> ListarAsync(CancellationToken ct = default)
    {
        try
        {
            return await http.GetFromJsonAsyncWithAuthCheck<List<UsuarioPortalDto>>(
                "api/parametros/usuarios", ct) ?? [];
        }
        catch (UnauthorizedAccessException) { throw; }
        catch (HttpRequestException) { throw; }
        catch (Exception ex)
        {
            throw new HttpRequestException("No fue posible obtener la lista de usuarios.", ex);
        }
    }

    public async Task<UsuarioPortalDto?> ObtenerAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var response = await http.GetAsync($"api/parametros/usuarios/{id}", ct);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        try
        {
            return await response.ReadFromJsonAsyncWithAuthCheck<UsuarioPortalDto>(ct);
        }
        catch (UnauthorizedAccessException) { throw; }
        catch (HttpRequestException) { throw; }
        catch (Exception ex)
        {
            throw new HttpRequestException("No fue posible obtener el usuario.", ex);
        }
    }

    public async Task<List<string>> ListarRolesAsync(CancellationToken ct = default)
    {
        try
        {
            return await http.GetFromJsonAsyncWithAuthCheck<List<string>>(
                "api/parametros/usuarios/roles", ct) ?? [];
        }
        catch (UnauthorizedAccessException) { throw; }
        catch (HttpRequestException) { throw; }
        catch (Exception ex)
        {
            throw new HttpRequestException("No fue posible obtener los roles.", ex);
        }
    }

    public async Task SincronizarRolesAsync(CancellationToken ct = default)
    {
        var response = await http.PostAsync("api/parametros/usuarios/roles/sync", null, ct);
        await response.ReadFromJsonAsyncWithAuthCheck<object>(ct);
    }

    public async Task CrearAsync(CrearUsuarioPortalDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var response = await http.PostAsJsonAsync("api/parametros/usuarios", dto, cancellationToken: ct);

        try
        {
            await response.ReadFromJsonAsyncWithAuthCheck<object>(ct);
        }
        catch (UnauthorizedAccessException) { throw; }
        catch (HttpRequestException) { throw; }
        catch (Exception ex)
        {
            throw new HttpRequestException("No fue posible crear el usuario.", ex);
        }
    }

    public async Task ActualizarAsync(string id, EditarUsuarioPortalDto dto, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(dto);

        var response = await http.PutAsJsonAsync($"api/parametros/usuarios/{id}", dto, cancellationToken: ct);

        try
        {
            await response.ReadFromJsonAsyncWithAuthCheck<object>(ct);
        }
        catch (UnauthorizedAccessException) { throw; }
        catch (HttpRequestException) { throw; }
        catch (Exception ex)
        {
            throw new HttpRequestException("No fue posible actualizar el usuario.", ex);
        }
    }
}
