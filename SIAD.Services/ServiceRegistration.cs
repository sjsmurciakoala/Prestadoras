using Microsoft.Extensions.DependencyInjection;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Services;
using SIAD.Services.Clientes;
using SIAD.Services.Solicitudes;
using SIAD.Services.Medidores;
using SIAD.Services.AuxiliarLectura;
using SIAD.Services.Branding;
using SIAD.Services.Ordenes;
using SIAD.Services.Rutas;
using SIAD.Services.CaptacionPagos;
using SIAD.Services.FacturacionMiscelaneos;
using SIAD.Services.NotasCreditoDebito;
using SIAD.Services.Cobranza;
using SIAD.Services.Contabilidad;
using SIAD.Services.Tenancy;

namespace SIAD.Services;

public static class ServiceRegistration
{
    public static IServiceCollection AddSiadServices(this IServiceCollection services)
    {
        // Add AutoMapper profiles and service implementations here.
        services.AddAutoMapper(typeof(ServiceRegistration).Assembly);

        services.AddScoped<ICurrentCompanyService, CurrentCompanyService>();
        services.AddScoped<ITenantCompanyService, TenantCompanyService>();
        services.AddScoped<IClientesService, ClientesService>();

        //solicitudes
        services.AddScoped<ISolicitudesService, SolicitudesService>();


        //medidores
        services.AddScoped<IMedidoresService, MedidoresService>();
        //auxiliar de lectura
        services.AddScoped<IAuxiliarLecturaService, AuxiliarLecturaService>();

        // agregar más servicios después

        // Branding
        services.AddScoped<IBrandingService, BrandingService>();
        //ordenes
         services.AddScoped<IOrdenesService, OrdenesService>();
         // rutas
         services.AddScoped<IRutasService, RutasService>();
         // captación de pagos
         services.AddScoped<ICaptacionPagosService, CaptacionPagosService>();
         // facturación misceláneos
         services.AddScoped<IFacturacionMiscelaneosService, FacturacionMiscelaneosService>();
         // notas crédito/débito
         services.AddScoped<INotasCreditoDebitoService, NotasCreditoDebitoService>();
         // cobranza
         services.AddScoped<ICobranzaService, CobranzaService>();
         // contabilidad
         services.AddScoped<IContabilidadCatalogosService, ContabilidadCatalogosService>();
         services.AddScoped<ICompanyManagementService, CompanyManagementService>();
        return services;
    }
}
