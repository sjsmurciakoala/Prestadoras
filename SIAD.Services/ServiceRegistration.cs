using Microsoft.Extensions.DependencyInjection;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Services;
using SIAD.Services.Clientes;
using SIAD.Services.Proveedores;
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
using SIAD.Services.Bancos;
using SIAD.Services.Contabilidad;
using SIAD.Services.Tenancy;
using SIAD.Services.Catalogos;
using SIAD.Services.Abogados;
using SIAD.Services.Ciclos;
using SIAD.Services.Servicios;
using SIAD.Services.Conceptos;
using SIAD.Services.Letras;
using SIAD.Services.TarifasBase;
using SIAD.Services.TarifasContador;
using SIAD.Services.AppLectores;

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
        services.AddScoped<IProveedoresService, ProveedoresService>();

        //solicitudes
        services.AddScoped<ISolicitudesService, SolicitudesService>();

        //medidores
        services.AddScoped<IMedidoresService, MedidoresService>();
        
        //auxiliar de lectura
        services.AddScoped<IAuxiliarLecturaService, AuxiliarLecturaService>();

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
        
        // contabilidad - registrar servicios de saldos PRIMERO (dependencia de pólizas)
        services.AddScoped<IContabilidadCatalogosService, ContabilidadCatalogosService>();
        services.AddScoped<ICompanyManagementService, CompanyManagementService>();
        services.AddScoped<IPeriodoContableService, PeriodoContableService>();
        services.AddScoped<IConfiguracionSistemaService, ConfiguracionSistemaService>();
        services.AddScoped<ISaldosService, SaldosService>();
        services.AddScoped<IPolizaService, PolizaService>();
        services.AddScoped<ITerceroService, TerceroService>();
        // bancos
        services.AddScoped<IBancoConfiguracionService, BancoConfiguracionService>();
        services.AddScoped<IBancoConfiguracionTransaccionesService, BancoConfiguracionTransaccionesService>();
        services.AddScoped<IBancosService, BancosService>();

        // catalogos generales
        services.AddScoped<ICatalogosService, CatalogosService>();

        // abogados
        services.AddScoped<IAbogadosService, AbogadosService>();
        
        // ciclos
        services.AddScoped<ICiclosService, CiclosService>();
        
        // servicios
        services.AddScoped<IServiciosService, ServiciosService>();

        // conceptos
        services.AddScoped<IConceptosService, ConceptosService>();
        
        // letras
        services.AddScoped<ILetrasService, LetrasService>();

        // tarifas
        services.AddScoped<ITarifasBaseService, TarifasBaseService>();
        services.AddScoped<ITarifasContadorService, TarifasContadorService>();

        // app lectores
        services.AddScoped<IUsuariosAppService, UsuariosAppService>();
        services.AddScoped<IConfiguracionAppService, ConfiguracionAppService>();
        services.AddScoped<IServiciosRolesWsService, ServiciosRolesWsService>();

        services.AddScoped<ICuentasBancosService, CuentasBancosService>();
        services.AddScoped<IBanMonedasService, BanMonedasService>();
        services.AddScoped<IBanTiposTransaccionesService, BanTiposTransaccionesService>();
        services.AddScoped<IBanTransaccionesService, BanTransaccionesService>();
        return services;
    }
}
