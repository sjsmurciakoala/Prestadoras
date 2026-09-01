namespace apc.Client.Layout.Navigation;

/// <summary>
/// Menú lateral reorganizado en 5 secciones (decisión del usuario, 2026-08-05):
/// Administración (operación del día a día), Bancos, Contabilidad,
/// Configuración (TODOS los mantenimientos/catálogos en un solo lugar + admin
/// del sistema) e Inventario. Cada opción conserva su Id, ruta y restricciones
/// originales; las que eran de la sección Parámetros (solo Super Administrador)
/// llevan SoloSuperAdmin.
/// </summary>
public static class SidebarNavigationDefinition
{
    public static IReadOnlyList<SidebarNavSection> Sections { get; } =
    [
        // ===== HOME (sin label de sección) =====
        new SidebarNavSection
        {
            Id = "inicio",
            Label = "",
            Items =
            [
                new SidebarNavItem
                {
                    Id = "home",
                    Text = "Inicio",
                    IconCssClass = "bi bi-house",
                    NavigateUrl = "/"
                }
            ]
        },

        // ===== 1. ADMINISTRACIÓN (operación comercial diaria) =====
        new SidebarNavSection
        {
            Id = "administracion",
            Label = "Administración",
            Items =
            [
                new SidebarNavItem
                {
                    Id = "adm-clientes",
                    Text = "Clientes",
                    IconCssClass = "bi bi-people",
                    MatchPrefixes = ["/clientes", "/solicitudes", "/mi-app/facturas"],
                    Children =
                    [
                        new SidebarNavItem { Id = "clientes", Text = "Clientes", NavigateUrl = "/clientes", MatchPrefixes = ["/clientes"], IconCssClass = "bi bi-people" },
                        new SidebarNavItem { Id = "solicitudes", Text = "Solicitudes", NavigateUrl = "/solicitudes", MatchPrefixes = ["/solicitudes"], IconCssClass = "bi bi-chat-left-text" },
                        new SidebarNavItem { Id = "app-facturas", Text = "Facturas App", NavigateUrl = "/mi-app/facturas", MatchPrefixes = ["/mi-app/facturas"], IconCssClass = "bi bi-receipt" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "adm-caja",
                    Text = "Caja",
                    IconCssClass = "bi bi-cash-coin",
                    MatchPrefixes = ["/facturacion/caja", "/facturacion/cajas"],
                    Children =
                    [
                        // Unificación cobranza F3: UNA sola vista para cobrar
                        // (reemplaza captación, abonos especiales y reversos).
                        new SidebarNavItem { Id = "fact-caja", Text = "Caja", NavigateUrl = "/facturacion/caja", MatchPrefixes = ["/facturacion/caja"], MatchExact = true, IconCssClass = "bi bi-cash-coin" },
                        new SidebarNavItem { Id = "fact-caja-consulta", Text = "Consulta de cobros", NavigateUrl = "/facturacion/caja/consulta", MatchPrefixes = ["/facturacion/caja/consulta"], IconCssClass = "bi bi-search" },
                        new SidebarNavItem { Id = "fact-caja-cajas", Text = "Cajas", NavigateUrl = "/facturacion/cajas", MatchPrefixes = ["/facturacion/cajas"], IconCssClass = "bi bi-gear" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "adm-facturacion",
                    Text = "Facturación",
                    IconCssClass = "bi bi-receipt-cutoff",
                    MatchPrefixes = ["/facturacion/miscelaneos", "/facturacion/notas", "/facturacion/calendario-facturacion", "/facturacion/periodos-comerciales"],
                    Children =
                    [
                        new SidebarNavItem { Id = "fact-miscelaneos", Text = "Misceláneos", NavigateUrl = "/facturacion/miscelaneos", MatchPrefixes = ["/facturacion/miscelaneos"], MatchExact = true, IconCssClass = "bi bi-receipt" },
                        new SidebarNavItem { Id = "fact-consulta-misc", Text = "Consulta misceláneos", NavigateUrl = "/facturacion/miscelaneos/consulta", MatchPrefixes = ["/facturacion/miscelaneos/consulta"], IconCssClass = "bi bi-search" },
                        new SidebarNavItem { Id = "fact-catalogo-misc", Text = "Catálogo misceláneos", NavigateUrl = "/facturacion/miscelaneos/catalogo", MatchPrefixes = ["/facturacion/miscelaneos/catalogo"], IconCssClass = "bi bi-journal-bookmark" },
                        new SidebarNavItem { Id = "fact-notas", Text = "Notas Crédito/Débito", NavigateUrl = "/facturacion/notas", MatchPrefixes = ["/facturacion/notas"], MatchExact = true, IconCssClass = "bi bi-journal-text" },
                        new SidebarNavItem { Id = "fact-calendario-facturacion", Text = "Calendario de facturación", NavigateUrl = "/facturacion/calendario-facturacion", MatchPrefixes = ["/facturacion/calendario-facturacion"], IconCssClass = "bi bi-calendar-week" },
                        new SidebarNavItem { Id = "fact-periodos-comerciales", Text = "Períodos comerciales", NavigateUrl = "/facturacion/periodos-comerciales", MatchPrefixes = ["/facturacion/periodos-comerciales"], IconCssClass = "bi bi-calendar-month" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "adm-cobranza",
                    Text = "Cobranza",
                    IconCssClass = "bi bi-collection",
                    MatchPrefixes = ["/facturacion/cobranza"],
                    Children =
                    [
                        new SidebarNavItem { Id = "fact-cobranza-main", Text = "Gestión de cobranza", NavigateUrl = "/facturacion/cobranza", MatchPrefixes = ["/facturacion/cobranza"], MatchExact = true, IconCssClass = "bi bi-collection" },
                        new SidebarNavItem { Id = "fact-cortes-masivos", Text = "Cortes masivos", NavigateUrl = "/facturacion/cobranza/cortes-masivos", MatchPrefixes = ["/facturacion/cobranza/cortes-masivos"], IconCssClass = "bi bi-scissors" },
                        new SidebarNavItem { Id = "fact-acciones-cobranza", Text = "Acciones de cobranza", NavigateUrl = "/facturacion/cobranza/acciones-cobranza", MatchPrefixes = ["/facturacion/cobranza/acciones-cobranza"], IconCssClass = "bi bi-journal-text" },
                        new SidebarNavItem { Id = "fact-historial-bitacora", Text = "Historial de bitácora", NavigateUrl = "/facturacion/cobranza/historial-acciones", MatchPrefixes = ["/facturacion/cobranza/historial-acciones"], IconCssClass = "bi bi-clock-history" },
                        new SidebarNavItem { Id = "fact-bloqueo-clientes", Text = "Bloqueo de clientes", NavigateUrl = "/facturacion/cobranza/bloqueo-clientes", MatchPrefixes = ["/facturacion/cobranza/bloqueo-clientes"], IconCssClass = "bi bi-lock" },
                        new SidebarNavItem { Id = "fact-clientes-cobros", Text = "Clientes para cobros", NavigateUrl = "/facturacion/cobranza/clientes-cobros", MatchPrefixes = ["/facturacion/cobranza/clientes-cobros"], IconCssClass = "bi bi-people" },
                        new SidebarNavItem { Id = "fact-cartera-vencida", Text = "Cartera vencida", NavigateUrl = "/facturacion/cobranza/cartera-vencida", MatchPrefixes = ["/facturacion/cobranza/cartera-vencida"], IconCssClass = "bi bi-calendar-x" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "adm-campo",
                    Text = "Órdenes y campo",
                    IconCssClass = "bi bi-geo",
                    MatchPrefixes = ["/ordenes", "/mapa"],
                    Children =
                    [
                        new SidebarNavItem { Id = "ordenes", Text = "Órdenes", NavigateUrl = "/ordenes", MatchPrefixes = ["/ordenes"], IconCssClass = "bi bi-list-check" },
                        new SidebarNavItem { Id = "mapa", Text = "Mapa", NavigateUrl = "/mapa", MatchPrefixes = ["/mapa"], IconCssClass = "bi bi-geo-alt" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "adm-tarifario-operativo",
                    Text = "Tarifario operativo",
                    IconCssClass = "bi bi-calculator",
                    MatchPrefixes = ["/tarifario/cliente-servicio-v3", "/tarifario/conflictos-v3"],
                    Children =
                    [
                        new SidebarNavItem { Id = "tarv3-cliente-servicio", Text = "Cliente servicio", NavigateUrl = "/tarifario/cliente-servicio-v3", MatchPrefixes = ["/tarifario/cliente-servicio-v3"], IconCssClass = "bi bi-diagram-3" },
                        new SidebarNavItem { Id = "tarv3-conflictos", Text = "Conflictos", NavigateUrl = "/tarifario/conflictos-v3", MatchPrefixes = ["/tarifario/conflictos-v3"], IconCssClass = "bi bi-exclamation-diamond" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "adm-informes",
                    Text = "Informes",
                    IconCssClass = "bi bi-bar-chart-line",
                    MatchPrefixes = ["/informes"],
                    Children =
                    [
                        new SidebarNavItem { Id = "informes-panel", Text = "Panel de informes", NavigateUrl = "/informes", MatchExact = true, IconCssClass = "bi bi-grid-3x3-gap" },
                        new SidebarNavItem { Id = "informes-catalogo", Text = "Catálogo", NavigateUrl = "/informes/catalogo", MatchPrefixes = ["/informes/catalogo"], IconCssClass = "bi bi-collection" }
                    ]
                }
            ]
        },

        // ===== 2. BANCOS =====
        new SidebarNavSection
        {
            Id = "bancos",
            Label = "Bancos",
            Items =
            [
                new SidebarNavItem { Id = "bn-gestion", Text = "Gestión de bancos", NavigateUrl = "/contabilidad/bancos", MatchPrefixes = ["/contabilidad/bancos"], IconCssClass = "bi bi-building" },
                new SidebarNavItem { Id = "bn-transacciones", Text = "Config. transacciones", NavigateUrl = "/bancos/configuracion_transacciones", MatchPrefixes = ["/bancos/configuracion_transacciones"], IconCssClass = "bi bi-sliders" },
                // MatchExact: sin esto /bancos/cheques/manual encendería también "Cheques emitidos".
                new SidebarNavItem { Id = "bn-cheques", Text = "Cheques emitidos", NavigateUrl = "/bancos/cheques", MatchPrefixes = ["/bancos/cheques"], MatchExact = true, IconCssClass = "bi bi-card-checklist" },
                new SidebarNavItem { Id = "bn-cheque-manual", Text = "Nuevo cheque manual", NavigateUrl = "/bancos/cheques/manual", MatchPrefixes = ["/bancos/cheques/manual"], IconCssClass = "bi bi-cash-stack", RequiredCapability = SidebarCapabilities.ChequeManual },
                new SidebarNavItem { Id = "bn-config", Text = "Configuración", NavigateUrl = "/bancos/configuracion", MatchPrefixes = ["/bancos/configuracion"], IconCssClass = "bi bi-gear" }
            ]
        },

        // ===== 3. CONTABILIDAD =====
        new SidebarNavSection
        {
            Id = "contabilidad",
            Label = "Contabilidad",
            Items =
            [
                new SidebarNavItem
                {
                    Id = "cont-partidas",
                    Text = "Partidas",
                    IconCssClass = "bi bi-file-earmark-check",
                    MatchPrefixes = ["/contabilidad/partidas", "/contabilidad/polizas", "/contabilidad/partidas-facturacion", "/informes/partidas-contabilidad"],
                    Children =
                    [
                        new SidebarNavItem { Id = "cb-polizas", Text = "Partidas", NavigateUrl = "/contabilidad/partidas", MatchPrefixes = ["/contabilidad/partidas", "/contabilidad/polizas"], MatchExact = true, IconCssClass = "bi bi-file-earmark-check" },
                        new SidebarNavItem { Id = "cb-partidas-facturacion", Text = "Partidas de facturación", NavigateUrl = "/contabilidad/partidas-facturacion", MatchPrefixes = ["/contabilidad/partidas-facturacion"], IconCssClass = "bi bi-journal-plus" },
                        new SidebarNavItem { Id = "informes-partidas", Text = "Informe de partidas", NavigateUrl = "/informes/partidas-contabilidad", MatchPrefixes = ["/informes/partidas-contabilidad"], IconCssClass = "bi bi-journal-check" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "cont-catalogo",
                    Text = "Catálogo contable",
                    IconCssClass = "bi bi-diagram-3",
                    MatchPrefixes = ["/contabilidad/plan-cuentas", "/contabilidad/centros-costo", "/contabilidad/terceros", "/contabilidad/diarios", "/contabilidad/tipos-transaccion"],
                    Children =
                    [
                        new SidebarNavItem { Id = "cb-plan-cuentas", Text = "Plan de cuentas", NavigateUrl = "/contabilidad/plan-cuentas", MatchPrefixes = ["/contabilidad/plan-cuentas"], IconCssClass = "bi bi-diagram-3" },
                        new SidebarNavItem { Id = "cb-centros-costo", Text = "Centros de costo", NavigateUrl = "/contabilidad/centros-costo", MatchPrefixes = ["/contabilidad/centros-costo"], IconCssClass = "bi bi-boxes" },
                        new SidebarNavItem { Id = "cb-terceros", Text = "Terceros", NavigateUrl = "/contabilidad/terceros", MatchPrefixes = ["/contabilidad/terceros"], IconCssClass = "bi bi-people" },
                        new SidebarNavItem { Id = "cb-diarios", Text = "Diarios contables", NavigateUrl = "/contabilidad/diarios", MatchPrefixes = ["/contabilidad/diarios"], IconCssClass = "bi bi-book" },
                        new SidebarNavItem { Id = "cb-tipos-comprobantes", Text = "Tipos de comprobantes", NavigateUrl = "/contabilidad/tipos-transaccion", MatchPrefixes = ["/contabilidad/tipos-transaccion"], IconCssClass = "bi bi-tags" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "cb-periodos",
                    Text = "Períodos contables",
                    NavigateUrl = "/contabilidad/periodos",
                    MatchPrefixes = ["/contabilidad/periodos"],
                    IconCssClass = "bi bi-calendar"
                },
                new SidebarNavItem
                {
                    Id = "cont-integracion",
                    Text = "Integración",
                    IconCssClass = "bi bi-arrow-left-right",
                    MatchPrefixes = ["/contabilidad/empresas/integracion", "/contabilidad/empresas/configuracion"],
                    Children =
                    [
                        new SidebarNavItem { Id = "cb-integracion", Text = "Integración Contable", NavigateUrl = "/contabilidad/empresas/integracion", MatchPrefixes = ["/contabilidad/empresas/integracion"], IconCssClass = "bi bi-arrow-left-right" },
                        new SidebarNavItem { Id = "cb-config-sistema", Text = "Configuración Sistema", NavigateUrl = "/contabilidad/empresas/configuracion", MatchPrefixes = ["/contabilidad/empresas/configuracion"], IconCssClass = "bi bi-sliders" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "presupuesto", Text = "Presupuesto", IconCssClass = "bi bi-cash-stack",
                    Children =
                    [
                        new SidebarNavItem { Id = "pst-configuraciones", Text = "Configuración de presupuestos", NavigateUrl = "/presupuesto/configuraciones", MatchPrefixes = ["/presupuesto/configuraciones"], IconCssClass = "bi bi-cash-stack" },
                        new SidebarNavItem { Id = "pst-ejecucion", Text = "Ejecución presupuestaria", NavigateUrl = "/presupuesto/ejecucion", MatchPrefixes = ["/presupuesto/ejecucion"], IconCssClass = "bi bi-bar-chart-line" },
                        new SidebarNavItem { Id = "pst-compromisos", Text = "Compromisos pendientes", NavigateUrl = "/presupuesto/compromisos", MatchPrefixes = ["/presupuesto/compromisos"], IconCssClass = "bi bi-hourglass-split" },
                        new SidebarNavItem { Id = "pst-control", Text = "Control presupuestario", NavigateUrl = "/presupuesto/control", MatchPrefixes = ["/presupuesto/control"], IconCssClass = "bi bi-toggles" }
                    ]
                }
            ]
        },

        // ===== 4. INVENTARIO =====
        new SidebarNavSection
        {
            Id = "inventario",
            Label = "Inventario",
            Items =
            [
                new SidebarNavItem
                {
                    Id = "inv-almacen",
                    Text = "Almacén",
                    IconCssClass = "bi bi-box-seam",
                    MatchPrefixes = ["/almacen/articulos", "/almacen/kardex", "/almacen/alertas-stock", "/almacen/existencias-bodega", "/almacen/kardex-bodega", "/almacen/valuacion-inventario"],
                    Children =
                    [
                        new SidebarNavItem { Id = "alm-articulos", Text = "Artículos", NavigateUrl = "/almacen/articulos", MatchPrefixes = ["/almacen/articulos"], IconCssClass = "bi bi-box-seam" },
                        new SidebarNavItem { Id = "alm-kardex", Text = "Estado de cuenta de artículos", NavigateUrl = "/almacen/kardex", MatchPrefixes = ["/almacen/kardex"], MatchExact = true, IconCssClass = "bi bi-journal-arrow-down" },
                        new SidebarNavItem { Id = "alm-existencias-bodega", Text = "Existencias por bodega", NavigateUrl = "/almacen/existencias-bodega", MatchPrefixes = ["/almacen/existencias-bodega"], IconCssClass = "bi bi-boxes" },
                        new SidebarNavItem { Id = "alm-kardex-bodega", Text = "Movimientos por bodega", NavigateUrl = "/almacen/kardex-bodega", MatchPrefixes = ["/almacen/kardex-bodega"], IconCssClass = "bi bi-journal-text" },
                        new SidebarNavItem { Id = "alm-valuacion", Text = "Valuación de inventario", NavigateUrl = "/almacen/valuacion-inventario", MatchPrefixes = ["/almacen/valuacion-inventario"], IconCssClass = "bi bi-cash-stack" },
                        new SidebarNavItem { Id = "alm-alertas", Text = "Alertas de stock", NavigateUrl = "/almacen/alertas-stock", MatchPrefixes = ["/almacen/alertas-stock"], IconCssClass = "bi bi-exclamation-triangle" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "inv-movimientos",
                    Text = "Movimientos",
                    IconCssClass = "bi bi-arrow-left-right",
                    MatchPrefixes = ["/almacen/movimientos", "/almacen/traslados", "/almacen/ordenes-compra", "/almacen/mis-aprobaciones", "/almacen/compras", "/almacen/carga-inicial", "/almacen/requisiciones", "/almacen/descargos"],
                    Children =
                    [
                        new SidebarNavItem { Id = "alm-movimientos", Text = "Movimientos de almacén", NavigateUrl = "/almacen/movimientos", MatchPrefixes = ["/almacen/movimientos"], IconCssClass = "bi bi-arrow-left-right" },
                        new SidebarNavItem { Id = "alm-traslados", Text = "Traslados entre bodegas", NavigateUrl = "/almacen/traslados", MatchPrefixes = ["/almacen/traslados"], IconCssClass = "bi bi-box-arrow-in-right" },
                        new SidebarNavItem { Id = "alm-ordenes-compra", Text = "Órdenes de compra", NavigateUrl = "/almacen/ordenes-compra", MatchPrefixes = ["/almacen/ordenes-compra"], IconCssClass = "bi bi-file-earmark-text" },
                        new SidebarNavItem { Id = "alm-mis-aprobaciones", Text = "Mis aprobaciones", NavigateUrl = "/almacen/mis-aprobaciones", MatchPrefixes = ["/almacen/mis-aprobaciones"], IconCssClass = "bi bi-pen" },
                        new SidebarNavItem { Id = "alm-recepciones", Text = "Recepción de compras", NavigateUrl = "/almacen/compras/recepciones", MatchPrefixes = ["/almacen/compras/recepciones"], IconCssClass = "bi bi-box-arrow-in-down" },
                        new SidebarNavItem { Id = "alm-compras", Text = "Consulta de compras", NavigateUrl = "/almacen/compras", MatchPrefixes = ["/almacen/compras"], MatchExact = true, IconCssClass = "bi bi-cart-plus" },
                        new SidebarNavItem { Id = "alm-carga-inicial", Text = "Carga inicial", NavigateUrl = "/almacen/carga-inicial", MatchPrefixes = ["/almacen/carga-inicial"], IconCssClass = "bi bi-flag" },
                        new SidebarNavItem { Id = "alm-requisiciones", Text = "Requisiciones", NavigateUrl = "/almacen/requisiciones", MatchPrefixes = ["/almacen/requisiciones"], IconCssClass = "bi bi-clipboard-check" },
                        new SidebarNavItem { Id = "alm-descargos", Text = "Descargos", NavigateUrl = "/almacen/descargos", MatchPrefixes = ["/almacen/descargos"], IconCssClass = "bi bi-box-arrow-up" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "proveedores",
                    Text = "Proveedores",
                    IconCssClass = "bi bi-truck",
                    MatchPrefixes = ["/proveedores"],
                    Children =
                    [
                        new SidebarNavItem { Id = "prov-lista", Text = "Proveedores", NavigateUrl = "/proveedores", MatchPrefixes = ["/proveedores"], MatchExact = true, IconCssClass = "bi bi-truck" },
                        // Vista unificada: facturas de compra y compromisos se pagan desde aquí. La
                        // ruta vieja (/almacen/compras/pagos) sigue existiendo y marca esta entrada.
                        new SidebarNavItem { Id = "prov-cuentas-por-pagar", Text = "Cuentas por pagar", NavigateUrl = "/proveedores/cuentas-por-pagar", MatchPrefixes = ["/proveedores/cuentas-por-pagar", "/almacen/compras/pagos"], IconCssClass = "bi bi-cash-stack" },
                        new SidebarNavItem { Id = "prov-antiguedad-saldos", Text = "Antigüedad de saldos", NavigateUrl = "/proveedores/antiguedad-saldos", MatchPrefixes = ["/proveedores/antiguedad-saldos"], MatchExact = true, IconCssClass = "bi bi-hourglass-split" },
                        new SidebarNavItem { Id = "prov-retenciones", Text = "Retenciones", NavigateUrl = "/proveedores/retenciones", MatchPrefixes = ["/proveedores/retenciones"], MatchExact = true, IconCssClass = "bi bi-cash-coin" },
                        new SidebarNavItem { Id = "prov-retenciones-declaracion", Text = "Declaración de retenciones", NavigateUrl = "/proveedores/retenciones/declaracion", MatchPrefixes = ["/proveedores/retenciones/declaracion"], IconCssClass = "bi bi-file-earmark-spreadsheet" },
                        new SidebarNavItem { Id = "prov-evaluacion", Text = "Evaluación", NavigateUrl = "/proveedores/evaluacion", MatchPrefixes = ["/proveedores/evaluacion"], IconCssClass = "bi bi-clipboard-data" },
                        new SidebarNavItem { Id = "prov-incidencias", Text = "Incidencias de recepción", NavigateUrl = "/proveedores/incidencias", MatchPrefixes = ["/proveedores/incidencias"], IconCssClass = "bi bi-exclamation-diamond" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "inv-catalogos",
                    Text = "Catálogos de almacén",
                    IconCssClass = "bi bi-tags",
                    MatchPrefixes = ["/almacen/tipos-articulo", "/almacen/categorias-unidad", "/almacen/unidades-medida", "/almacen/conceptos-movimiento", "/almacen/isv-compras", "/almacen/terminos-pago", "/almacen/bodegas"],
                    Children =
                    [
                        new SidebarNavItem { Id = "alm-tipos-articulo", Text = "Tipos de artículos", NavigateUrl = "/almacen/tipos-articulo", MatchPrefixes = ["/almacen/tipos-articulo"], IconCssClass = "bi bi-tags" },
                        new SidebarNavItem { Id = "alm-categorias-unidad", Text = "Categorías por unidad", NavigateUrl = "/almacen/categorias-unidad", MatchPrefixes = ["/almacen/categorias-unidad"], IconCssClass = "bi bi-diagram-2" },
                        new SidebarNavItem { Id = "alm-unidades", Text = "Unidades de medida", NavigateUrl = "/almacen/unidades-medida", MatchPrefixes = ["/almacen/unidades-medida"], IconCssClass = "bi bi-rulers" },
                        new SidebarNavItem { Id = "alm-conceptos-movimiento", Text = "Conceptos de movimiento", NavigateUrl = "/almacen/conceptos-movimiento", MatchPrefixes = ["/almacen/conceptos-movimiento"], IconCssClass = "bi bi-arrow-left-right" },
                        new SidebarNavItem { Id = "alm-terminos-pago", Text = "Términos de pago", NavigateUrl = "/almacen/terminos-pago", MatchPrefixes = ["/almacen/terminos-pago"], IconCssClass = "bi bi-calendar-check" },
                        new SidebarNavItem { Id = "alm-isv-compras", Text = "ISV en compras", NavigateUrl = "/almacen/isv-compras", MatchPrefixes = ["/almacen/isv-compras"], IconCssClass = "bi bi-percent" },
                        new SidebarNavItem { Id = "alm-bodegas", Text = "Bodegas", NavigateUrl = "/almacen/bodegas", MatchPrefixes = ["/almacen/bodegas"], IconCssClass = "bi bi-building" }
                    ]
                }
            ]
        },

        // ===== 5. TALENTO HUMANO (2026-08-19) =====
        new SidebarNavSection
        {
            Id = "talento-humano",
            Label = "Talento Humano",
            Items =
            [
                new SidebarNavItem { Id = "th-empleados", Text = "Empleados", NavigateUrl = "/talento-humano/empleados", MatchPrefixes = ["/talento-humano/empleados"], IconCssClass = "bi bi-person-badge" },
                new SidebarNavItem
                {
                    Id = "th-catalogos",
                    Text = "Catálogos",
                    IconCssClass = "bi bi-tags",
                    MatchPrefixes = ["/talento-humano/cargos", "/talento-humano/departamentos"],
                    Children =
                    [
                        new SidebarNavItem { Id = "th-cargos", Text = "Cargos", NavigateUrl = "/talento-humano/cargos", MatchPrefixes = ["/talento-humano/cargos"], IconCssClass = "bi bi-person-workspace" },
                        new SidebarNavItem { Id = "th-departamentos", Text = "Departamentos", NavigateUrl = "/talento-humano/departamentos", MatchPrefixes = ["/talento-humano/departamentos"], IconCssClass = "bi bi-diagram-3" }
                    ]
                }
            ]
        },

        // ===== 6. CONFIGURACIÓN (al final, decisión del usuario 2026-08-05) (TODOS los mantenimientos + admin del sistema) =====
        new SidebarNavSection
        {
            Id = "configuracion",
            Label = "Configuración",
            Items =
            [
                new SidebarNavItem
                {
                    Id = "cfg-catalogos-comerciales",
                    Text = "Catálogos comerciales",
                    IconCssClass = "bi bi-journal-bookmark",
                    MatchPrefixes = ["/mantenimientos/barrios", "/ciclos", "/libretas", "/medidores", "/mantenimientos/clases-medidor", "/facturacion/condiciones-lectura", "/mantenimientos/codigo-cliente", "/mantenimientos/formatos-fiscales", "/abogados", "/tarifario/cai-offline"],
                    Children =
                    [
                        new SidebarNavItem { Id = "mant-barrios", Text = "Barrios", NavigateUrl = "/mantenimientos/barrios", MatchPrefixes = ["/mantenimientos/barrios"], IconCssClass = "bi bi-map-fill" },
                        new SidebarNavItem { Id = "ciclos", Text = "Ciclos", NavigateUrl = "/ciclos", MatchPrefixes = ["/ciclos"], IconCssClass = "bi bi-arrow-repeat" },
                        new SidebarNavItem { Id = "libretas", Text = "Libretas", NavigateUrl = "/libretas", MatchPrefixes = ["/libretas"], IconCssClass = "bi bi-journal-bookmark" },
                        new SidebarNavItem { Id = "medidores", Text = "Medidores", NavigateUrl = "/medidores", MatchPrefixes = ["/medidores"], IconCssClass = "bi bi-speedometer2" },
                        new SidebarNavItem { Id = "mant-clases-medidor", Text = "Clases de medidor", NavigateUrl = "/mantenimientos/clases-medidor", MatchPrefixes = ["/mantenimientos/clases-medidor"], IconCssClass = "bi bi-speedometer" },
                        new SidebarNavItem { Id = "fact-condiciones-lectura", Text = "Condiciones de lectura", NavigateUrl = "/facturacion/condiciones-lectura", MatchPrefixes = ["/facturacion/condiciones-lectura"], IconCssClass = "bi bi-list-check" },
                        new SidebarNavItem { Id = "mant-codigo-cliente", Text = "Código de cliente", NavigateUrl = "/mantenimientos/codigo-cliente", MatchPrefixes = ["/mantenimientos/codigo-cliente"], IconCssClass = "bi bi-123" },
                        new SidebarNavItem { Id = "mant-formatos-fiscales", Text = "Formatos fiscales", NavigateUrl = "/mantenimientos/formatos-fiscales", MatchPrefixes = ["/mantenimientos/formatos-fiscales"], IconCssClass = "bi bi-input-cursor-text" },
                        new SidebarNavItem { Id = "abogados", Text = "Abogados", NavigateUrl = "/abogados", MatchPrefixes = ["/abogados"], IconCssClass = "bi bi-briefcase" },
                        new SidebarNavItem { Id = "tarv3-cai-offline", Text = "CAI offline", NavigateUrl = "/tarifario/cai-offline", MatchPrefixes = ["/tarifario/cai-offline"], IconCssClass = "bi bi-upc-scan" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "cfg-catalogos-cobranza",
                    Text = "Catálogos de cobranza",
                    IconCssClass = "bi bi-journal-check",
                    MatchPrefixes = ["/facturacion/notas/motivos", "/mantenimientos/acciones-cobranza", "/mantenimientos/observaciones-cobranza", "/mantenimientos/recargo-mora"],
                    Children =
                    [
                        new SidebarNavItem { Id = "mant-motivos-notas", Text = "Motivos de Notas C/D", NavigateUrl = "/facturacion/notas/motivos", MatchPrefixes = ["/facturacion/notas/motivos"], IconCssClass = "bi bi-tags" },
                        new SidebarNavItem { Id = "mant-acciones-cobranza", Text = "Acciones de cobranza", NavigateUrl = "/mantenimientos/acciones-cobranza", MatchPrefixes = ["/mantenimientos/acciones-cobranza"], IconCssClass = "bi bi-journal-check" },
                        new SidebarNavItem { Id = "mant-observaciones-cobranza", Text = "Observaciones cobranza", NavigateUrl = "/mantenimientos/observaciones-cobranza", MatchPrefixes = ["/mantenimientos/observaciones-cobranza"], IconCssClass = "bi bi-chat-square-text" },
                        new SidebarNavItem { Id = "mant-recargo-mora", Text = "Recargo por mora", NavigateUrl = "/mantenimientos/recargo-mora", MatchPrefixes = ["/mantenimientos/recargo-mora"], IconCssClass = "bi bi-clock-history" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "cfg-tarifario",
                    Text = "Tarifario",
                    IconCssClass = "bi bi-calculator",
                    MatchPrefixes = ["/tarifario/cuadros", "/tarifario/maestro-servicios-v3", "/tarifario/desglose-abonos", "/mantenimientos/ajustes-tarifarios", "/mantenimientos/impuestos"],
                    Children =
                    [
                        new SidebarNavItem { Id = "tarv3-cuadros", Text = "Cuadros tarifarios", NavigateUrl = "/tarifario/cuadros", MatchPrefixes = ["/tarifario/cuadros"], IconCssClass = "bi bi-table" },
                        new SidebarNavItem { Id = "tarv3-maestro-servicios", Text = "Maestro servicios", NavigateUrl = "/tarifario/maestro-servicios-v3", MatchPrefixes = ["/tarifario/maestro-servicios-v3"], IconCssClass = "bi bi-list-ul" },
                        new SidebarNavItem { Id = "tarv3-desglose-abonos", Text = "Distribución de abonos", NavigateUrl = "/tarifario/desglose-abonos", MatchPrefixes = ["/tarifario/desglose-abonos"], IconCssClass = "bi bi-percent" },
                        new SidebarNavItem { Id = "mant-ajustes-tarifarios", Text = "Ajustes tarifarios", NavigateUrl = "/mantenimientos/ajustes-tarifarios", MatchPrefixes = ["/mantenimientos/ajustes-tarifarios"], IconCssClass = "bi bi-percent" },
                        new SidebarNavItem { Id = "mant-impuestos", Text = "Impuestos", NavigateUrl = "/mantenimientos/impuestos", MatchPrefixes = ["/mantenimientos/impuestos"], IconCssClass = "bi bi-receipt" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "cfg-proveedores",
                    Text = "Catálogos de proveedor",
                    IconCssClass = "bi bi-tag",
                    MatchPrefixes = ["/mantenimientos/tipos-proveedor", "/mantenimientos/tipos-contacto", "/mantenimientos/retenciones"],
                    Children =
                    [
                        new SidebarNavItem { Id = "mant-tipos-proveedor", Text = "Tipos de proveedor", NavigateUrl = "/mantenimientos/tipos-proveedor", MatchPrefixes = ["/mantenimientos/tipos-proveedor"], IconCssClass = "bi bi-tag" },
                        new SidebarNavItem { Id = "mant-tipos-contacto", Text = "Tipos de contacto", NavigateUrl = "/mantenimientos/tipos-contacto", MatchPrefixes = ["/mantenimientos/tipos-contacto"], IconCssClass = "bi bi-person-lines-fill" },
                        // El catálogo de retenciones vive con los proveedores, que es a quienes se
                        // les retiene; la consulta y la declaración están en la sección Proveedores.
                        new SidebarNavItem { Id = "mant-retenciones", Text = "Catálogo de retenciones", NavigateUrl = "/mantenimientos/retenciones", MatchPrefixes = ["/mantenimientos/retenciones"], IconCssClass = "bi bi-cash-coin" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "cfg-sistema",
                    Text = "Sistema",
                    IconCssClass = "bi bi-shield-lock",
                    MatchPrefixes = ["/parametros", "/mi-app/usuarios", "/contabilidad/empresas", "/tipos-documento-fiscal", "/auditoria", "/informes/reportes", "/informes/datasets", "/configuracion/correo", "/configuracion/aprobaciones"],
                    Children =
                    [
                        new SidebarNavItem { Id = "param-usuarios", Text = "Usuarios", NavigateUrl = "/parametros/usuarios", MatchPrefixes = ["/parametros/usuarios"], IconCssClass = "bi bi-people-fill", SoloSuperAdmin = true },
                        new SidebarNavItem { Id = "param-roles", Text = "Roles y permisos", NavigateUrl = "/parametros/roles", MatchPrefixes = ["/parametros/roles"], IconCssClass = "bi bi-shield-check", SoloSuperAdmin = true },
                        new SidebarNavItem { Id = "app-usuarios", Text = "Usuarios App", NavigateUrl = "/mi-app/usuarios", MatchPrefixes = ["/mi-app/usuarios"], IconCssClass = "bi bi-phone" },
                        new SidebarNavItem { Id = "cb-empresas", Text = "Empresas", NavigateUrl = "/contabilidad/empresas", MatchPrefixes = ["/contabilidad/empresas"], MatchExact = true, IconCssClass = "bi bi-buildings" },
                        new SidebarNavItem { Id = "cb-crear-empresa", Text = "Crear empresa", NavigateUrl = "/contabilidad/empresas/nueva", MatchPrefixes = ["/contabilidad/empresas/nueva"], IconCssClass = "bi bi-plus-circle" },
                        new SidebarNavItem { Id = "param-branding", Text = "Branding del Portal", NavigateUrl = "/parametros/branding", MatchPrefixes = ["/parametros/branding"], IconCssClass = "bi bi-palette", SoloSuperAdmin = true },
                        new SidebarNavItem { Id = "tipos-documento-fiscal", Text = "Tipos de documento (SAR)", NavigateUrl = "/tipos-documento-fiscal", MatchPrefixes = ["/tipos-documento-fiscal"], IconCssClass = "bi bi-file-earmark-text" },
                        new SidebarNavItem { Id = "cfg-correo", Text = "Correo y notificaciones", NavigateUrl = "/configuracion/correo", MatchPrefixes = ["/configuracion/correo"], IconCssClass = "bi bi-envelope-gear", SoloSuperAdmin = true },
                        new SidebarNavItem { Id = "cfg-aprobaciones", Text = "Aprobaciones", NavigateUrl = "/configuracion/aprobaciones", MatchPrefixes = ["/configuracion/aprobaciones"], IconCssClass = "bi bi-diagram-3" },
                        new SidebarNavItem { Id = "auditoria-config", Text = "Configuración de auditoría", NavigateUrl = "/auditoria/configuracion", MatchPrefixes = ["/auditoria/configuracion"], IconCssClass = "bi bi-sliders", SoloSuperAdmin = true },
                        new SidebarNavItem { Id = "auditoria-bitacora-maestros", Text = "Bitácora de maestros", NavigateUrl = "/auditoria/bitacora-maestros", MatchPrefixes = ["/auditoria/bitacora-maestros"], IconCssClass = "bi bi-clock-history" },
                        new SidebarNavItem { Id = "informes-reportes", Text = "Diseño Web (informes)", NavigateUrl = "/informes/reportes", MatchPrefixes = ["/informes/reportes"], IconCssClass = "bi bi-layout-text-window-reverse" },
                        new SidebarNavItem { Id = "informes-datasets", Text = "Datasets Web (informes)", NavigateUrl = "/informes/datasets", MatchPrefixes = ["/informes/datasets"], IconCssClass = "bi bi-database" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "cfg-cuenta",
                    Text = "Cuenta",
                    IconCssClass = "bi bi-person-circle",
                    MatchPrefixes = ["/Account/Manage", "/Account/Logout"],
                    Children =
                    [
                        new SidebarNavItem { Id = "user-account", Text = "Mi cuenta", NavigateUrl = "/Account/Manage", MatchPrefixes = ["/Account/Manage"], IconCssClass = "bi bi-person-circle" },
                        new SidebarNavItem { Id = "logout", Text = "Cerrar sesión", NavigateUrl = "/Account/Logout", MatchPrefixes = ["/Account/Logout"], IconCssClass = "bi bi-box-arrow-right" }
                    ]
                }
            ]
        }
    ];
}
