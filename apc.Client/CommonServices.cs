using apc.Client.Services;
using apc.Client.Services.Ordenes;
using apc.Client.Services.Rutas;
using apc.Client.Services.Tenant;
using apc.Client.Services.Clientes;
using apc.Client.Services.Solicitudes;
using apc.Client.Services.Medidores;
using apc.Client.Services.AuxiliarLectura;
using apc.Services;
using apc.Client.Services.CaptacionPagos;
using apc.Client.Services.Facturacion;
using apc.Client.Services.Contabilidad;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace apc.Client
{
    public class CommonServices
    {
        public static void Configure(IServiceCollection services, IConfiguration configuration)
        {
            services.AddDevExpressBlazor(options =>
            {
                options.SizeMode = DevExpress.Blazor.SizeMode.Small;
            });

            services.AddScoped<DxThemesService>();
            services.AddScoped<BrandingClient>();
            services.AddScoped<OrdenesClient>();
            services.AddScoped<RutasClient>();
            services.AddScoped<CaptacionPagosClient>();
            services.AddScoped<FacturacionMiscelaneosClient>();
            services.AddScoped<NotasCreditoDebitoClient>();
            services.AddScoped<CobranzaClient>();
            services.AddScoped<EmpresasContabilidadClient>();
            services.AddScoped<ConfiguracionSistemaClient>();
            services.AddScoped<EmpresaClient>();
            
            // Clientes HTTP para módulo de Clientes
            services.AddScoped<ClientesClient>();
            services.AddScoped<SolicitudesClient>();
            services.AddScoped<MedidoresClient>();
            services.AddScoped<AuxiliarLecturaClient>();
            
            services.AddScoped<ITenantProvider, TenantProvider>();
            services.AddScoped<TenantState>();
            services.AddScoped<TenantCompaniesClient>();
            services.AddScoped<TenantSessionClient>();
        }
    }
}
