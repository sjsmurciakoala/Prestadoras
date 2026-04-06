using System.Net.Http.Json;
using System.Text.Json;
using SIAD.Core.DTOs.Contabilidad;

namespace apc.Client.Services.Contabilidad;

public sealed class ConfiguracionSistemaClient
{
    private readonly HttpClient http;

    public ConfiguracionSistemaClient(HttpClient http)
    {
        this.http = http;
    }

    /// <summary>
    /// Obtiene la configuración actual de una empresa
    /// </summary>
    public async Task<ConfiguracionSistemaDto?> ObtenerAsync(long companyId, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);

        try
        {
            var response = await http.GetAsync($"api/contabilidad/configuracion/{companyId}", cancellationToken: ct);
            
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }

                var mensaje = await ObtenerMensajeErrorAsync(response, ct);
                throw new HttpRequestException(
                    string.IsNullOrWhiteSpace(mensaje)
                        ? "No fue posible obtener la configuración."
                        : mensaje);
            }

            var resultado = await response.Content.ReadFromJsonAsync<ConfiguracionSistemaDto>(cancellationToken: ct);
            return resultado;
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error al obtener la configuración del servidor.", ex);
        }
    }

    /// <summary>
    /// Guarda la configuración completa de una empresa y valida que se guardó correctamente
    /// </summary>
    public async Task<ConfiguracionSistemaDto> GuardarAsync(long companyId, ConfiguracionSistemaDto dto, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);
        ArgumentNullException.ThrowIfNull(dto);

        try
        {
            // Convertir DateTime a UTC para compatibilidad con PostgreSQL
            ConvertirDateTimesAUtc(dto);

            var response = await http.PostAsJsonAsync(
                $"api/contabilidad/configuracion/{companyId}",
                dto,
                cancellationToken: ct);

            if (!response.IsSuccessStatusCode)
            {
                var mensaje = await ObtenerMensajeErrorAsync(response, ct);
                throw new HttpRequestException(
                    string.IsNullOrWhiteSpace(mensaje)
                        ? "No fue posible guardar la configuración."
                        : mensaje);
            }

            var resultado = await response.Content.ReadFromJsonAsync<ConfiguracionSistemaDto>(cancellationToken: ct);
            if (resultado is null)
            {
                throw new InvalidOperationException("El servidor devolvió una respuesta vacía.");
            }

            // Validar que los datos se guardaron correctamente
            ValidarConfiguracionGuardada(dto, resultado);

            return resultado;
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error al guardar la configuración.", ex);
        }
    }

    /// <summary>
    /// Obtiene la lista de cuentas contables disponibles para una empresa
    /// </summary>
    public async Task<IReadOnlyList<CuentaContableLookupDto>> ListarCuentasAsync(long companyId, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);

        try
        {
            var response = await http.GetAsync($"api/contabilidad/{companyId}/cuentas", cancellationToken: ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var mensaje = await ObtenerMensajeErrorAsync(response, ct);
                throw new HttpRequestException(
                    string.IsNullOrWhiteSpace(mensaje)
                        ? "No fue posible cargar las cuentas contables."
                        : mensaje);
            }

            var resultado = await response.Content.ReadFromJsonAsync<IReadOnlyList<CuentaContableLookupDto>>(cancellationToken: ct);
            return resultado ?? Array.Empty<CuentaContableLookupDto>();
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error al cargar las cuentas contables.", ex);
        }
    }

    private static async Task<string?> ObtenerMensajeErrorAsync(HttpResponseMessage response, CancellationToken ct)
{
    try
    {
        var contenido = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(contenido))
        {
            return null;
        }

        using var document = JsonDocument.Parse(contenido);
        var root = document.RootElement;

        if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
        {
            var mensajes = new List<string>();
            foreach (var propiedad in errors.EnumerateObject())
            {
                if (propiedad.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in propiedad.Value.EnumerateArray())
                    {
                        var mensaje = item.GetString();
                        if (!string.IsNullOrWhiteSpace(mensaje))
                        {
                            if (string.IsNullOrWhiteSpace(propiedad.Name))
                            {
                                mensajes.Add(mensaje);
                            }
                            else
                            {
                                mensajes.Add($"{propiedad.Name}: {mensaje}");
                            }
                        }
                    }
                }
                else if (propiedad.Value.ValueKind == JsonValueKind.String)
                {
                    var mensaje = propiedad.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(mensaje))
                    {
                        mensajes.Add(string.IsNullOrWhiteSpace(propiedad.Name)
                            ? mensaje
                            : $"{propiedad.Name}: {mensaje}");
                    }
                }
            }

            if (mensajes.Count > 0)
            {
                return string.Join(" | ", mensajes);
            }
        }

        if (root.TryGetProperty("detail", out var detail))
        {
            return detail.GetString();
        }

        if (root.TryGetProperty("title", out var title))
        {
            return title.GetString();
        }

        if (root.ValueKind == JsonValueKind.String)
        {
            return root.GetString();
        }

        return contenido;
    }
    catch
    {
        return null;
    }
}

/// <summary>
    /// Convierte todos los DateTime del DTO a UTC
    /// </summary>
    private static void ConvertirDateTimesAUtc(ConfiguracionSistemaDto dto)
    {
        if (dto?.Principal != null)
        {
            if (dto.Principal.FechaInicioEjercicio.HasValue)
            {
                dto.Principal.FechaInicioEjercicio = EspecificarComoUtc(dto.Principal.FechaInicioEjercicio.Value);
            }

            if (dto.Principal.FechaFinEjercicio.HasValue)
            {
                dto.Principal.FechaFinEjercicio = EspecificarComoUtc(dto.Principal.FechaFinEjercicio.Value);
            }

            if (dto.Principal.UltimaDepreciacion.HasValue)
            {
                dto.Principal.UltimaDepreciacion = EspecificarComoUtc(dto.Principal.UltimaDepreciacion.Value);
            }
        }
    }

    /// <summary>
    /// Especifica un DateTime como UTC, evitando ambigüedades
    /// </summary>
    private static DateTime EspecificarComoUtc(DateTime dateTime)
    {
        return dateTime.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
            : dateTime.ToUniversalTime();
    }

    /// <summary>
    /// Valida que la configuración se guardó correctamente comparando valores
    /// </summary>
    private static void ValidarConfiguracionGuardada(ConfiguracionSistemaDto original, ConfiguracionSistemaDto guardada)
    {
        var errores = new List<string>();

        // Validar configuración principal
        if (original.Principal.SeparadorCodigo != guardada.Principal.SeparadorCodigo)
            errores.Add("El separador de código no coincide");
        
        if (original.Principal.FormatoCuentas != guardada.Principal.FormatoCuentas)
            errores.Add("El formato de cuentas no coincide");
        
        if (original.Principal.FormatoCentros != guardada.Principal.FormatoCentros)
            errores.Add("El formato de centros no coincide");
        
        if (original.Principal.MontoMaximo != guardada.Principal.MontoMaximo)
            errores.Add("El monto máximo no coincide");
        
        if (original.Principal.FrecuenciaDepreciacion != guardada.Principal.FrecuenciaDepreciacion)
            errores.Add("La frecuencia de depreciación no coincide");

        // Validar fechas (convertir a UTC para comparación)
        var fechaInicioOriginal = original.Principal.FechaInicioEjercicio?.ToUniversalTime();
        var fechaInicioGuardada = guardada.Principal.FechaInicioEjercicio?.ToUniversalTime();
        
        if (fechaInicioOriginal?.Date != fechaInicioGuardada?.Date)
            errores.Add("La fecha de inicio del ejercicio no coincide");

        var fechaFinOriginal = original.Principal.FechaFinEjercicio?.ToUniversalTime();
        var fechaFinGuardada = guardada.Principal.FechaFinEjercicio?.ToUniversalTime();
        
        if (fechaFinOriginal?.Date != fechaFinGuardada?.Date)
            errores.Add("La fecha de fin del ejercicio no coincide");

        if (errores.Count > 0)
        {
            throw new InvalidOperationException(
                $"Validación fallida después de guardar. Errores: {string.Join("; ", errores)}");
        }
    }
}
