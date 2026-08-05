using Microsoft.Extensions.DependencyInjection;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Services;
using SIAD.Services.Clientes;
using SIAD.Services.Proveedores;
using SIAD.Services.Solicitudes;
using SIAD.Services.Medidores;
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
using SIAD.Services.Almacen;
using SIAD.Services.Impuestos;
using SIAD.Services.Ciclos;
using SIAD.Services.Caja;
// [Sprint1/FaseC 2026-05-05] Removidos namespaces Letras, TarifasBase, TarifasContador (legacy).
using SIAD.Services.AppLectores;
using SIAD.Services.Tarifario;
using SIAD.Services.Presupuesto;
using SIAD.Services.Auditoria;

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
        
        // Branding
        services.AddScoped<IBrandingService, BrandingService>();
        
        //ordenes
        services.AddScoped<IOrdenesService, OrdenesService>();
        
        // rutas
        services.AddScoped<IRutasService, RutasService>();
        services.AddScoped<Libretas.ILibretasService, Libretas.LibretasService>();
        
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
        services.AddScoped<IAccountFormatService, AccountFormatService>();
        services.AddScoped<IIntegracionContableService, IntegracionContableService>();
        services.AddScoped<ILoteFacturacionService, LoteFacturacionService>();
        services.AddScoped<PeriodosComerciales.IPeriodoComercialService, PeriodosComerciales.PeriodoComercialService>();
        services.AddScoped<CondicionesLectura.ICondicionesLecturaService, CondicionesLectura.CondicionesLecturaService>();
        services.AddScoped<Facturacion.ICalendarioFacturacionService, Facturacion.CalendarioFacturacionService>();
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

        // almacén / inventario
        services.AddScoped<IArticulosService, ArticulosService>();
        services.AddScoped<IKardexService, KardexService>();
        services.AddScoped<IComprasService, ComprasService>();
        services.AddScoped<IRequisicionesService, RequisicionesService>();
        services.AddScoped<IDescargosService, DescargosService>();
        services.AddScoped<IUnidadesMedidaService, UnidadesMedidaService>();
        services.AddScoped<ICategoriaUnidadService, CategoriaUnidadService>();
        services.AddScoped<ITipoArticuloService, TipoArticuloService>();
        services.AddScoped<IIsvCompraConfigService, IsvCompraConfigService>();
        services.AddScoped<IBodegaService, BodegaService>();
        services.AddScoped<IArticuloUbicacionService, ArticuloUbicacionService>();
        services.AddScoped<IArticuloProveedorService, ArticuloProveedorService>();
        services.AddScoped<IOrdenCompraService, OrdenCompraService>();
        services.AddScoped<IRecepcionCompraService, RecepcionCompraService>();
        services.AddScoped<IGrupoService, GrupoService>();
        // Reporte de existencias por bodega (valorado) — alimenta pantalla y PDF.
        services.AddScoped<IExistenciasBodegaService, ExistenciasBodegaService>();
        // Valuación de inventario a una fecha (reconstruye el saldo desde el kardex) — pantalla y PDF.
        services.AddScoped<IValuacionInventarioService, ValuacionInventarioService>();

        // Motor de movimientos (Fase 1): rollup compartido de cabecera + posteo al kardex.
        services.AddScoped<IArticuloRollupService, ArticuloRollupService>();
        services.AddScoped<IInventarioPostingService, InventarioPostingService>();

        // Carga inicial de existencias y ajustes (Fase 4).
        services.AddScoped<ICargaInicialInventarioService, CargaInicialInventarioService>();
        services.AddScoped<IAjusteInventarioService, AjusteInventarioService>();

        // Catálogo de tipos de movimiento (movimientos de almacén, Fase 1).
        services.AddScoped<ITipoMovimientoService, TipoMovimientoService>();
        services.AddScoped<IMovimientoAlmacenService, MovimientoAlmacenService>();
        // Traslado entre bodegas (Fase 5).
        services.AddScoped<ITrasladoAlmacenService, TrasladoAlmacenService>();
        // Requisición → descargo (Fase 6). El documento de requisición (solicitud, no postea).
        services.AddScoped<IRequisicionDocumentoService, RequisicionDocumentoService>();
        // El descargo (la entrega real, sí postea).
        services.AddScoped<IDescargoDocumentoService, DescargoDocumentoService>();

        // ciclos
        services.AddScoped<ICiclosService, CiclosService>();

        // presupuesto (de Combinacio_E_J_1.0; mantengo legacy retirado de Letras/Tarifas)
        services.AddScoped<IConfiguracionPresupuestoService, ConfiguracionPresupuestoService>();
        services.AddScoped<IOrdenesPagoDirectoService, OrdenesPagoDirectoService>();

        // app lectores V3: mantenimiento de credenciales (adm_lector_credencial, bcrypt).
        // Reemplaza al viejo usuarioapc/IUsuariosAppService (app Java retirada).
        services.AddScoped<ILectoresCredencialService, LectoresCredencialService>();

        // app lectores V3: consulta de facturas subidas por la sincronización de la app
        services.AddScoped<IFacturasAppService, FacturasAppService>();

        // tipos de documento fiscal (catalogo SAR Acuerdo 481-2017)
        services.AddScoped<SIAD.Services.TiposDocumentoFiscal.ITiposDocumentoFiscalService,
                           SIAD.Services.TiposDocumentoFiscal.TiposDocumentoFiscalService>();

        // impuestos y sus tasas con vigencia (catalogo global SAR; ISV Honduras)
        services.AddScoped<IImpuestosService, ImpuestosService>();

        services.AddScoped<ICuentasBancosService, CuentasBancosService>();
        services.AddScoped<IChequesService, ChequesService>();
        services.AddScoped<IBanMonedasService, BanMonedasService>();
        services.AddScoped<IBanTiposTransaccionesService, BanTiposTransaccionesService>();
        services.AddScoped<IBanTransaccionesService, BanTransaccionesService>();

        // WS bancario F8 (lo consume el host apc.BancosWs; contrato SIMAFI congelado)
        services.AddScoped<SIAD.Services.BancosWs.IBancosWsService, SIAD.Services.BancosWs.BancosWsService>();

        // tarifario v3
        services.AddScoped<IClienteServicioTarifarioService, ClienteServicioTarifarioService>();
        services.AddScoped<IPruebaCalculoService, PruebaCalculoService>();
        services.AddScoped<ICuadroTarifarioService, CuadroTarifarioService>();
        services.AddScoped<ServicioTarifarioV3Service>();
        services.AddScoped<IServicioTarifarioV3Service, ServicioTarifarioV3Service>();
        services.AddScoped<CaiTarifarioService>();
        services.AddScoped<ICaiTarifarioService, CaiTarifarioService>();
        services.AddScoped<ITarifarioConflictoService, TarifarioConflictoService>();
        services.AddScoped<IDesgloseAbonoConfigService, DesgloseAbonoConfigService>();

        // auditoría / bitácora de maestros
        services.AddMemoryCache();
        services.AddScoped<ICurrentUserAudit, SystemUserAudit>();          // fallback; apc lo reemplaza
        services.AddScoped<IAuditConfigProvider, AuditConfigProvider>();
        services.AddScoped<IAuditableCatalogProvider, AuditableCatalogProvider>();
        services.AddScoped<IBitacoraMaestrosService, BitacoraMaestrosService>();
        services.AddScoped<IAuditoriaConfigService, AuditoriaConfigService>();
        services.AddScoped<IBitacoraMaestrosWriter, BitacoraMaestrosWriter>();

        return services;
    }
}
