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

        public Task<CompanyCreationDto> GetEmpresa(long id)
        {
            return _httpClient.GetFromJsonAsync<CompanyCreationDto>($"api/contabilidad/empresas/{id}");
        }

        public Task CreateEmpresa(CompanyCreationDto empresa)
        {
            return _httpClient.PostAsJsonAsync("api/contabilidad/empresas", empresa);
        }

        public Task UpdateEmpresa(long id, CompanyCreationDto empresa)
        {
            return _httpClient.PutAsJsonAsync($"api/contabilidad/empresas/{id}", empresa);
        }
    }
}
