using Microsoft.Extensions.DependencyInjection;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Services;
using SIAD.Services.Clientes;
using SIAD.Services.Cobros;
using SIAD.Services.Proveedores;
using SIAD.Services.Solicitudes;
using SIAD.Services.Medidores;
using SIAD.Services.Branding;
using SIAD.Services.Ordenes;
using SIAD.Services.Rutas;
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
using SIAD.Services.TalentoHumano;

namespace SIAD.Services;

public static class ServiceRegistration
{
    public static IServiceCollection AddSiadServices(this IServiceCollection services)
    {
        // Add AutoMapper profiles and service implementations here.
        services.AddAutoMapper(typeof(ServiceRegistration).Assembly);

        // Conversores de Dapper que el paquete no trae (DateOnly como parámetro). Global e idempotente.
        SIAD.Services.Infrastructure.DapperTypeHandlers.EnsureRegistered();

        services.AddScoped<ICurrentCompanyService, CurrentCompanyService>();
        services.AddScoped<ITenantCompanyService, TenantCompanyService>();
        services.AddScoped<IClientesService, ClientesService>();
        services.AddScoped<IProveedoresService, ProveedoresService>();
        services.AddScoped<IProveedorEstadoCuentaService, ProveedorEstadoCuentaService>();
        // Antigüedad de saldos (2026-08-14_prv_antiguedad_saldos.sql, F1). Aging de CxP por tramo.
        services.AddScoped<IAntiguedadSaldosProveedorService, AntiguedadSaldosProveedorService>();
        // Scorecard de proveedores (2026-08-14_prv_evaluacion.sql, F1).
        services.AddScoped<IEvaluacionProveedorService, EvaluacionProveedorService>();
        // Incidencias de recepción (F4): alimentan el criterio CALIDAD del scorecard.
        services.AddScoped<IRecepcionIncidenciaService, RecepcionIncidenciaService>();

        // Talento Humano (2026-08-19_th_empleado.sql): catálogo de empleados.
        services.AddScoped<IEmpleadosService, EmpleadosService>();
        // Catálogos de cargos y departamentos (2026-08-19_th_cargo_departamento.sql).
        services.AddScoped<ICatalogoThService, CatalogoThService>();

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
        services.AddScoped<ICatalogosCobroService, CatalogosCobroService>();
        
        // gestión de caja
        services.AddScoped<ICajaService, CajaService>();
        services.AddScoped<IAbonoService, AbonoService>();

        // motor único de cobro (unificación cobranza F2)
        services.AddScoped<Cobros.ICobroService, Cobros.CobroService>();
        
        // facturación misceláneos
        services.AddScoped<IFacturacionMiscelaneosService, FacturacionMiscelaneosService>();
        
        // notas crédito/débito
        services.AddScoped<INotasCreditoDebitoService, NotasCreditoDebitoService>();

        // mantenimientos (recargo mora, ajustes tarifarios, formatos fiscales)
        services.AddScoped<IMantenimientosService, MantenimientosService>();
        services.AddScoped<IFormatoFiscalService, FormatoFiscalService>();

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
        services.AddScoped<ITerminoPagoService, TerminoPagoService>();
        services.AddScoped<ICompraCxpService, CompraCxpService>();
        services.AddScoped<ITipoArticuloService, TipoArticuloService>();
        services.AddScoped<IIsvCompraConfigService, IsvCompraConfigService>();
        // Correo y notificaciones por empresa: conexión SendGrid (API key cifrada) + enrutamiento
        // por área (F2). Una sola instancia sirve a las dos interfaces (config y resolver de envío).
        services.AddScoped<SIAD.Services.Configuracion.CorreoConfigService>();
        services.AddScoped<SIAD.Services.Configuracion.ICorreoConfigService>(
            sp => sp.GetRequiredService<SIAD.Services.Configuracion.CorreoConfigService>());
        services.AddScoped<SIAD.Services.Configuracion.ICorreoEnvioResolver>(
            sp => sp.GetRequiredService<SIAD.Services.Configuracion.CorreoConfigService>());
        // Envío real de correo: transporte SendGrid (HttpClient tipado a la API v3) + notificador
        // de alto nivel (notificaciones por área + correos de sistema de Identity).
        services.AddHttpClient<SIAD.Services.Configuracion.ISendGridCorreoTransport,
                               SIAD.Services.Configuracion.SendGridCorreoTransport>(c =>
        {
            c.BaseAddress = new Uri("https://api.sendgrid.com");
            c.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<SIAD.Services.Configuracion.ICorreoNotificador,
                           SIAD.Services.Configuracion.CorreoNotificador>();
        // Alertas de stock por correo (reusa GetAlertasStock + NotificarArea ALMACÉN).
        services.AddScoped<IAlertasStockNotificador, AlertasStockNotificador>();
        // Capa 1 del ISV de compras (tasa por tipo de artículo, vigente a la fecha). Fuente única
        // que consumen las órdenes de compra y la recepción de facturas.
        services.AddScoped<ITasaIsvArticuloResolver, TasaIsvArticuloResolver>();
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

        // retenciones a proveedores: catalogo global (concepto + tasas con vigencia) + cuenta del
        // pasivo por empresa (2026-08-06, F1)
        services.AddScoped<SIAD.Services.Retenciones.IRetencionesService,
                           SIAD.Services.Retenciones.RetencionesService>();

        // retenciones a proveedores: consulta del registro fiscal hdr/dtl (2026-08-07, F4)
        services.AddScoped<SIAD.Services.Retenciones.IRetencionRegistroService,
                           SIAD.Services.Retenciones.RetencionRegistroService>();

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
