using SIAD.Core.DTOs.Contabilidad;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace apc.Client.Services.Contabilidad
{
    public class EmpresaClient
    {
        private readonly HttpClient _httpClient;

        public EmpresaClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<CompanyCreationDto> GetEmpresa(long id)
        {
            try
            {
                return await _httpClient.GetFromJsonAsyncWithAuthCheck<CompanyCreationDto>(
                    $"api/contabilidad/empresas/{id}");
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new HttpRequestException("No fue posible obtener la empresa.", ex);
            }
        }

        public async Task CreateEmpresa(CompanyCreationDto empresa)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsyncWithAuthCheck("api/contabilidad/empresas", empresa);
                if (!response.IsSuccessStatusCode)
                {
                    var mensaje = await HttpClientExtensions.ObtenerMensajeErrorAsync(response);
                    throw new HttpRequestException(mensaje ?? "No fue posible crear la empresa.");
                }
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

        public async Task UpdateEmpresa(long id, CompanyCreationDto empresa)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsyncWithAuthCheck($"api/contabilidad/empresas/{id}", empresa);
                if (!response.IsSuccessStatusCode)
                {
                    var mensaje = await HttpClientExtensions.ObtenerMensajeErrorAsync(response);
                    throw new HttpRequestException(mensaje ?? "No fue posible actualizar la empresa.");
                }
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
}
