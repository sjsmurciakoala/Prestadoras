using apc.Client.Services;
using apc.Client.Services.Ordenes;
using apc.Client.Services.Parametros;
using apc.Client.Services.Rutas;
using apc.Client.Services.Tenant;
using apc.Client.Services.Clientes;
using apc.Client.Services.Proveedores;
using apc.Client.Services.Solicitudes;
using apc.Client.Services.Medidores;
using apc.Client.Services.AuxiliarLectura;
using apc.Services;
using apc.Client.Services.CaptacionPagos;
using apc.Client.Services.Facturacion;
using apc.Client.Services.Contabilidad;
using apc.Client.Services.Bancos;
using apc.Client.Services.Catalogos;
using apc.Client.Services.Abogados;
using apc.Client.Services.Ciclos;
using apc.Client.Services.Servicios;
using apc.Client.Services.Conceptos;
using apc.Client.Services.Letras;
using apc.Client.Services.TarifasBase;
using apc.Client.Services.TarifasContador;
using apc.Client.Services.AppLectores;
using apc.Client.Services.Layout;
using apc.Client.Services.Maps;
using apc.Client.Services.Informes;
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
            services.AddScoped<SidebarStateService>();
            services.AddScoped<BrandingClient>();
            services.AddScoped<MapClient>();
            services.AddScoped<OrdenesClient>();
            services.AddScoped<RutasClient>();
            services.AddScoped<CaptacionPagosClient>();
            services.AddScoped<FacturacionMiscelaneosClient>();
            services.AddScoped<NotasCreditoDebitoClient>();
            services.AddScoped<CobranzaClient>();
            services.AddScoped<EmpresasContabilidadClient>();
            services.AddScoped<ConfiguracionSistemaClient>();
            services.AddScoped<PeriodosContablesClient>();
            services.AddScoped<EmpresaClient>();
            services.AddScoped<BancoConfiguracionClient>();
            services.AddScoped<ConfiguracionTransaccionesClient>();
            services.AddScoped<BancosClient>();
            services.AddScoped<CuentasBancosClient>();
            services.AddScoped<BanMonedasClient>();
            services.AddScoped<BanTiposTransaccionesClient>();
            services.AddScoped<BanTransaccionesClient>();
            
            // Clientes HTTP para módulo de Clientes
            services.AddScoped<ClientesClient>();
            services.AddScoped<ProveedoresClient>();
            services.AddScoped<SolicitudesClient>();
            services.AddScoped<MedidoresClient>();
            services.AddScoped<AuxiliarLecturaClient>();
            services.AddScoped<CatalogosClient>();
            services.AddScoped<AbogadosClient>();
            services.AddScoped<CiclosClient>();
            services.AddScoped<ServiciosClient>();
            services.AddScoped<ConceptosClient>();
            services.AddScoped<LetrasClient>();
            services.AddScoped<InformesClient>();
            services.AddScoped<TarifasBaseClient>();
            services.AddScoped<TarifasContadorClient>();
            services.AddScoped<UsuariosAppClient>();
            services.AddScoped<ConfiguracionAppClient>();
            services.AddScoped<ServiciosRolesWsClient>();
            
            services.AddScoped<ITenantProvider, TenantProvider>();
            services.AddScoped<TenantState>();
            services.AddScoped<TenantCompanyContextClient>();
            services.AddScoped<TenantCompaniesClient>();
            services.AddScoped<TenantSessionClient>();

            // Parámetros (Super Admin)
            services.AddScoped<UsuariosPortalClient>();
            services.AddScoped<RolesPortalClient>();

            //contabilidad
            services.AddScoped<apc.Client.Services.Contabilidad.PolizasClient>();
        }
    }
}
