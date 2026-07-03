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
using SIAD.Services.Mantenimientos;
using SIAD.Services.Cobranza;
using SIAD.Services.Bancos;
using SIAD.Services.Contabilidad;
using SIAD.Services.Tenancy;
using SIAD.Services.Catalogos;
using SIAD.Services.Abogados;
using SIAD.Services.Ciclos;
using SIAD.Services.Caja;
// [Sprint1/FaseC 2026-05-05] Removidos namespaces Letras, TarifasBase, TarifasContador (legacy).
using SIAD.Services.AppLectores;
using SIAD.Services.Tarifario;
using SIAD.Services.Presupuesto;

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
        
        // gestión de caja
        services.AddScoped<ICajaService, CajaService>();
        services.AddScoped<IAbonoService, AbonoService>();
        
        // facturación misceláneos
        services.AddScoped<IFacturacionMiscelaneosService, FacturacionMiscelaneosService>();
        
        // notas crédito/débito
        services.AddScoped<INotasCreditoDebitoService, NotasCreditoDebitoService>();

        // mantenimientos (recargo mora, ajustes tarifarios)
        services.AddScoped<IMantenimientosService, MantenimientosService>();

        // cobranza
        services.AddScoped<ICobranzaService, CobranzaService>();
        services.AddScoped<ICorteMasivoService, CorteMasivoService>();
        
        // contabilidad - registrar servicios de saldos PRIMERO (dependencia de pólizas)
        services.AddScoped<IContabilidadCatalogosService, ContabilidadCatalogosService>();
        services.AddScoped<ICompanyManagementService, CompanyManagementService>();
        services.AddScoped<IPeriodoContableService, PeriodoContableService>();
        services.AddScoped<IConfiguracionSistemaService, ConfiguracionSistemaService>();
        services.AddScoped<IIntegracionContableService, IntegracionContableService>();
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

        // presupuesto (de Combinacio_E_J_1.0; mantengo legacy retirado de Letras/Tarifas)
        services.AddScoped<IConfiguracionPresupuestoService, ConfiguracionPresupuestoService>();
        services.AddScoped<IOrdenesPagoDirectoService, OrdenesPagoDirectoService>();

        // app lectores
        services.AddScoped<IUsuariosAppService, UsuariosAppService>();

        // tipos de documento fiscal (catalogo SAR Acuerdo 481-2017)
        services.AddScoped<SIAD.Services.TiposDocumentoFiscal.ITiposDocumentoFiscalService,
                           SIAD.Services.TiposDocumentoFiscal.TiposDocumentoFiscalService>();

        services.AddScoped<ICuentasBancosService, CuentasBancosService>();
        services.AddScoped<IBanMonedasService, BanMonedasService>();
        services.AddScoped<IBanTiposTransaccionesService, BanTiposTransaccionesService>();
        services.AddScoped<IBanTransaccionesService, BanTransaccionesService>();

        // tarifario v3
        services.AddScoped<IClienteServicioTarifarioService, ClienteServicioTarifarioService>();
        services.AddScoped<IPruebaCalculoService, PruebaCalculoService>();
        services.AddScoped<ICuadroTarifarioService, CuadroTarifarioService>();
        services.AddScoped<ServicioTarifarioV3Service>();
        services.AddScoped<IServicioTarifarioV3Service, ServicioTarifarioV3Service>();
        services.AddScoped<CaiTarifarioService>();
        services.AddScoped<ICaiTarifarioService, CaiTarifarioService>();
        services.AddScoped<ITarifarioConflictoService, TarifarioConflictoService>();

        return services;
    }
}
