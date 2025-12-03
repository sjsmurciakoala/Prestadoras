using apc.Client.Services;
using apc.Client.Services.Ordenes;
using apc.Client.Services.Rutas;
using apc.Client.Services.Tenant;
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
            services.AddScoped<ITenantProvider, TenantProvider>();
            services.AddScoped<TenantState>();
            services.AddScoped<TenantCompaniesClient>();
            services.AddScoped<TenantSessionClient>();
        }
    }
}
