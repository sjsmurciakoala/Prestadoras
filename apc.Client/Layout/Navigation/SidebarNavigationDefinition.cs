using SIAD.Core.Constants;

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
                    RequiredPermission = PermissionNames.Ventas.Clientes.View,
                    Text = "Clientes",
                    IconCssClass = "bi bi-people",
                    MatchPrefixes = ["/clientes", "/solicitudes", "/mi-app/facturas"],
                    Children =
                    [
                        new SidebarNavItem { Id = "clientes", RequiredPermission = PermissionNames.Ventas.Clientes.View, Text = "Clientes", NavigateUrl = "/clientes", MatchPrefixes = ["/clientes"], IconCssClass = "bi bi-people" },
                        new SidebarNavItem { Id = "solicitudes", RequiredPermission = PermissionNames.Ventas.View, Text = "Solicitudes", NavigateUrl = "/solicitudes", MatchPrefixes = ["/solicitudes"], IconCssClass = "bi bi-chat-left-text" },
                        new SidebarNavItem { Id = "app-facturas", RequiredPermission = PermissionNames.Ventas.View, Text = "Facturas App", NavigateUrl = "/mi-app/facturas", MatchPrefixes = ["/mi-app/facturas"], IconCssClass = "bi bi-receipt" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "adm-caja",
                    RequiredPermission = PermissionNames.Ventas.Caja.View,
                    Text = "Caja",
                    IconCssClass = "bi bi-cash-coin",
                    MatchPrefixes = ["/facturacion/caja", "/facturacion/cajas"],
                    Children =
                    [
                        // Unificación cobranza F3: UNA sola vista para cobrar
                        // (reemplaza captación, abonos especiales y reversos).
                        new SidebarNavItem { Id = "fact-caja", RequiredPermission = PermissionNames.Ventas.Caja.View, Text = "Caja", NavigateUrl = "/facturacion/caja", MatchPrefixes = ["/facturacion/caja"], MatchExact = true, IconCssClass = "bi bi-cash-coin" },
                        new SidebarNavItem { Id = "fact-caja-consulta", RequiredPermission = PermissionNames.Ventas.Caja.View, Text = "Consulta de cobros", NavigateUrl = "/facturacion/caja/consulta", MatchPrefixes = ["/facturacion/caja/consulta"], IconCssClass = "bi bi-search" },
                        new SidebarNavItem { Id = "fact-caja-cajas", RequiredPermission = PermissionNames.Ventas.Caja.View, Text = "Cajas", NavigateUrl = "/facturacion/cajas", MatchPrefixes = ["/facturacion/cajas"], IconCssClass = "bi bi-gear" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "adm-facturacion",
                    RequiredPermission = PermissionNames.Ventas.FacturacionMiscelaneos.View,
                    Text = "Facturación",
                    IconCssClass = "bi bi-receipt-cutoff",
                    MatchPrefixes = ["/facturacion/miscelaneos", "/facturacion/notas", "/facturacion/calendario-facturacion", "/facturacion/periodos-comerciales"],
                    Children =
                    [
                        new SidebarNavItem { Id = "fact-miscelaneos", RequiredPermission = PermissionNames.Ventas.FacturacionMiscelaneos.View, Text = "Misceláneos", NavigateUrl = "/facturacion/miscelaneos", MatchPrefixes = ["/facturacion/miscelaneos"], MatchExact = true, IconCssClass = "bi bi-receipt" },
                        new SidebarNavItem { Id = "fact-consulta-misc", RequiredPermission = PermissionNames.Ventas.FacturacionMiscelaneos.View, Text = "Consulta misceláneos", NavigateUrl = "/facturacion/miscelaneos/consulta", MatchPrefixes = ["/facturacion/miscelaneos/consulta"], IconCssClass = "bi bi-search" },
                        new SidebarNavItem { Id = "fact-catalogo-misc", RequiredPermission = PermissionNames.Ventas.FacturacionMiscelaneos.View, Text = "Catálogo misceláneos", NavigateUrl = "/facturacion/miscelaneos/catalogo", MatchPrefixes = ["/facturacion/miscelaneos/catalogo"], IconCssClass = "bi bi-journal-bookmark" },
                        new SidebarNavItem { Id = "fact-notas", RequiredPermission = PermissionNames.Ventas.NotasCreditoDebito.View, Text = "Notas Crédito/Débito", NavigateUrl = "/facturacion/notas", MatchPrefixes = ["/facturacion/notas"], MatchExact = true, IconCssClass = "bi bi-journal-text" },
                        new SidebarNavItem { Id = "fact-calendario-facturacion", RequiredPermission = PermissionNames.Ventas.View, Text = "Calendario de facturación", NavigateUrl = "/facturacion/calendario-facturacion", MatchPrefixes = ["/facturacion/calendario-facturacion"], IconCssClass = "bi bi-calendar-week" },
                        new SidebarNavItem { Id = "fact-periodos-comerciales", RequiredPermission = PermissionNames.Ventas.View, Text = "Períodos comerciales", NavigateUrl = "/facturacion/periodos-comerciales", MatchPrefixes = ["/facturacion/periodos-comerciales"], IconCssClass = "bi bi-calendar-month" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "adm-cobranza",
                    RequiredPermission = PermissionNames.Ventas.Cobranza.View,
                    Text = "Cobranza",
                    IconCssClass = "bi bi-collection",
                    MatchPrefixes = ["/facturacion/cobranza"],
                    Children =
                    [
                        new SidebarNavItem { Id = "fact-cobranza-main", RequiredPermission = PermissionNames.Ventas.Cobranza.View, Text = "Gestión de cobranza", NavigateUrl = "/facturacion/cobranza", MatchPrefixes = ["/facturacion/cobranza"], MatchExact = true, IconCssClass = "bi bi-collection" },
                        new SidebarNavItem { Id = "fact-cortes-masivos", RequiredPermission = PermissionNames.Ventas.Cobranza.View, Text = "Cortes masivos", NavigateUrl = "/facturacion/cobranza/cortes-masivos", MatchPrefixes = ["/facturacion/cobranza/cortes-masivos"], IconCssClass = "bi bi-scissors" },
                        new SidebarNavItem { Id = "fact-acciones-cobranza", RequiredPermission = PermissionNames.Ventas.Cobranza.View, Text = "Acciones de cobranza", NavigateUrl = "/facturacion/cobranza/acciones-cobranza", MatchPrefixes = ["/facturacion/cobranza/acciones-cobranza"], IconCssClass = "bi bi-journal-text" },
                        new SidebarNavItem { Id = "fact-historial-bitacora", RequiredPermission = PermissionNames.Ventas.Cobranza.View, Text = "Historial de bitácora", NavigateUrl = "/facturacion/cobranza/historial-acciones", MatchPrefixes = ["/facturacion/cobranza/historial-acciones"], IconCssClass = "bi bi-clock-history" },
                        new SidebarNavItem { Id = "fact-bloqueo-clientes", RequiredPermission = PermissionNames.Ventas.Cobranza.View, Text = "Bloqueo de clientes", NavigateUrl = "/facturacion/cobranza/bloqueo-clientes", MatchPrefixes = ["/facturacion/cobranza/bloqueo-clientes"], IconCssClass = "bi bi-lock" },
                        new SidebarNavItem { Id = "fact-clientes-cobros", RequiredPermission = PermissionNames.Ventas.Cobranza.View, Text = "Clientes para cobros", NavigateUrl = "/facturacion/cobranza/clientes-cobros", MatchPrefixes = ["/facturacion/cobranza/clientes-cobros"], IconCssClass = "bi bi-people" },
                        new SidebarNavItem { Id = "fact-cartera-vencida", RequiredPermission = PermissionNames.Ventas.Cobranza.View, Text = "Cartera vencida", NavigateUrl = "/facturacion/cobranza/cartera-vencida", MatchPrefixes = ["/facturacion/cobranza/cartera-vencida"], IconCssClass = "bi bi-calendar-x" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "adm-campo",
                    RequiredPermission = PermissionNames.Ventas.View,
                    Text = "Órdenes y campo",
                    IconCssClass = "bi bi-geo",
                    MatchPrefixes = ["/ordenes", "/mapa"],
                    Children =
                    [
                        new SidebarNavItem { Id = "ordenes", RequiredPermission = PermissionNames.Ventas.View, Text = "Órdenes", NavigateUrl = "/ordenes", MatchPrefixes = ["/ordenes"], IconCssClass = "bi bi-list-check" },
                        new SidebarNavItem { Id = "mapa", RequiredPermission = PermissionNames.Ventas.View, Text = "Mapa", NavigateUrl = "/mapa", MatchPrefixes = ["/mapa"], IconCssClass = "bi bi-geo-alt" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "adm-tarifario-operativo",
                    RequiredPermission = PermissionNames.Ventas.View,
                    Text = "Tarifario operativo",
                    IconCssClass = "bi bi-calculator",
                    MatchPrefixes = ["/tarifario/cliente-servicio-v3", "/tarifario/conflictos-v3"],
                    Children =
                    [
                        new SidebarNavItem { Id = "tarv3-cliente-servicio", RequiredPermission = PermissionNames.Ventas.View, Text = "Cliente servicio", NavigateUrl = "/tarifario/cliente-servicio-v3", MatchPrefixes = ["/tarifario/cliente-servicio-v3"], IconCssClass = "bi bi-diagram-3" },
                        new SidebarNavItem { Id = "tarv3-conflictos", RequiredPermission = PermissionNames.Ventas.View, Text = "Conflictos", NavigateUrl = "/tarifario/conflictos-v3", MatchPrefixes = ["/tarifario/conflictos-v3"], IconCssClass = "bi bi-exclamation-diamond" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "adm-informes",
                    RequiredPermission = PermissionNames.Reporteria.View,
                    Text = "Informes",
                    IconCssClass = "bi bi-bar-chart-line",
                    MatchPrefixes = ["/informes"],
                    Children =
                    [
                        new SidebarNavItem { Id = "informes-panel", RequiredPermission = PermissionNames.Reporteria.View, Text = "Panel de informes", NavigateUrl = "/informes", MatchExact = true, IconCssClass = "bi bi-grid-3x3-gap" },
                        new SidebarNavItem { Id = "informes-catalogo", RequiredPermission = PermissionNames.Reporteria.View, Text = "Catálogo", NavigateUrl = "/informes/catalogo", MatchPrefixes = ["/informes/catalogo"], IconCssClass = "bi bi-collection" }
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
                new SidebarNavItem { Id = "bn-gestion", RequiredPermission = PermissionNames.Bancos.View, Text = "Gestión de bancos", NavigateUrl = "/contabilidad/bancos", MatchPrefixes = ["/contabilidad/bancos"], IconCssClass = "bi bi-building" },
                new SidebarNavItem { Id = "bn-transacciones", RequiredPermission = PermissionNames.Bancos.View, Text = "Config. transacciones", NavigateUrl = "/bancos/configuracion_transacciones", MatchPrefixes = ["/bancos/configuracion_transacciones"], IconCssClass = "bi bi-sliders" },
                // MatchExact: sin esto /bancos/cheques/manual encendería también "Cheques emitidos".
                new SidebarNavItem { Id = "bn-cheques", RequiredPermission = PermissionNames.Bancos.View, Text = "Cheques emitidos", NavigateUrl = "/bancos/cheques", MatchPrefixes = ["/bancos/cheques"], MatchExact = true, IconCssClass = "bi bi-card-checklist" },
                new SidebarNavItem { Id = "bn-cheque-manual", RequiredPermission = PermissionNames.Bancos.View, Text = "Nuevo cheque manual", NavigateUrl = "/bancos/cheques/manual", MatchPrefixes = ["/bancos/cheques/manual"], IconCssClass = "bi bi-cash-stack", RequiredCapability = SidebarCapabilities.ChequeManual },
                new SidebarNavItem { Id = "bn-config", RequiredPermission = PermissionNames.Bancos.View, Text = "Configuración", NavigateUrl = "/bancos/configuracion", MatchPrefixes = ["/bancos/configuracion"], IconCssClass = "bi bi-gear" }
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
                    RequiredPermission = PermissionNames.Contabilidad.View,
                    Text = "Partidas",
                    IconCssClass = "bi bi-file-earmark-check",
                    MatchPrefixes = ["/contabilidad/partidas", "/contabilidad/polizas", "/contabilidad/partidas-facturacion", "/informes/partidas-contabilidad"],
                    Children =
                    [
                        new SidebarNavItem { Id = "cb-polizas", RequiredPermission = PermissionNames.Contabilidad.View, Text = "Partidas", NavigateUrl = "/contabilidad/partidas", MatchPrefixes = ["/contabilidad/partidas", "/contabilidad/polizas"], MatchExact = true, IconCssClass = "bi bi-file-earmark-check" },
                        new SidebarNavItem { Id = "cb-partidas-facturacion", RequiredPermission = PermissionNames.Contabilidad.View, Text = "Partidas de facturación", NavigateUrl = "/contabilidad/partidas-facturacion", MatchPrefixes = ["/contabilidad/partidas-facturacion"], IconCssClass = "bi bi-journal-plus" },
                        new SidebarNavItem { Id = "informes-partidas", RequiredPermission = PermissionNames.Reporteria.View, Text = "Informe de partidas", NavigateUrl = "/informes/partidas-contabilidad", MatchPrefixes = ["/informes/partidas-contabilidad"], IconCssClass = "bi bi-journal-check" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "cont-catalogo",
                    RequiredPermission = PermissionNames.Contabilidad.View,
                    Text = "Catálogo contable",
                    IconCssClass = "bi bi-diagram-3",
                    MatchPrefixes = ["/contabilidad/plan-cuentas", "/contabilidad/centros-costo", "/contabilidad/terceros", "/contabilidad/diarios", "/contabilidad/tipos-transaccion"],
                    Children =
                    [
                        new SidebarNavItem { Id = "cb-plan-cuentas", RequiredPermission = PermissionNames.Contabilidad.View, Text = "Plan de cuentas", NavigateUrl = "/contabilidad/plan-cuentas", MatchPrefixes = ["/contabilidad/plan-cuentas"], IconCssClass = "bi bi-diagram-3" },
                        new SidebarNavItem { Id = "cb-centros-costo", RequiredPermission = PermissionNames.Contabilidad.View, Text = "Centros de costo", NavigateUrl = "/contabilidad/centros-costo", MatchPrefixes = ["/contabilidad/centros-costo"], IconCssClass = "bi bi-boxes" },
                        new SidebarNavItem { Id = "cb-terceros", RequiredPermission = PermissionNames.Contabilidad.View, Text = "Terceros", NavigateUrl = "/contabilidad/terceros", MatchPrefixes = ["/contabilidad/terceros"], IconCssClass = "bi bi-people" },
                        new SidebarNavItem { Id = "cb-diarios", RequiredPermission = PermissionNames.Contabilidad.View, Text = "Diarios contables", NavigateUrl = "/contabilidad/diarios", MatchPrefixes = ["/contabilidad/diarios"], IconCssClass = "bi bi-book" },
                        new SidebarNavItem { Id = "cb-tipos-comprobantes", RequiredPermission = PermissionNames.Contabilidad.View, Text = "Tipos de comprobantes", NavigateUrl = "/contabilidad/tipos-transaccion", MatchPrefixes = ["/contabilidad/tipos-transaccion"], IconCssClass = "bi bi-tags" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "cb-periodos",
                    RequiredPermission = PermissionNames.Contabilidad.View,
                    Text = "Períodos contables",
                    NavigateUrl = "/contabilidad/periodos",
                    MatchPrefixes = ["/contabilidad/periodos"],
                    IconCssClass = "bi bi-calendar"
                },
                new SidebarNavItem
                {
                    Id = "cont-integracion",
                    RequiredPermission = PermissionNames.Contabilidad.View,
                    Text = "Integración",
                    IconCssClass = "bi bi-arrow-left-right",
                    MatchPrefixes = ["/contabilidad/empresas/integracion", "/contabilidad/empresas/configuracion"],
                    Children =
                    [
                        new SidebarNavItem { Id = "cb-integracion", RequiredPermission = PermissionNames.Contabilidad.View, Text = "Integración Contable", NavigateUrl = "/contabilidad/empresas/integracion", MatchPrefixes = ["/contabilidad/empresas/integracion"], IconCssClass = "bi bi-arrow-left-right" },
                        new SidebarNavItem { Id = "cb-config-sistema", RequiredPermission = PermissionNames.Contabilidad.View, Text = "Configuración Sistema", NavigateUrl = "/contabilidad/empresas/configuracion", MatchPrefixes = ["/contabilidad/empresas/configuracion"], IconCssClass = "bi bi-sliders" }
                    ]
                },
                new SidebarNavItem { Id = "presupuesto", RequiredPermission = PermissionNames.Contabilidad.View, Text = "Presupuesto", NavigateUrl = "/presupuesto/configuraciones", MatchPrefixes = ["/presupuesto/configuraciones"], IconCssClass = "bi bi-cash-stack" }
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
                    RequiredPermission = PermissionNames.Inventario.View,
                    Text = "Almacén",
                    IconCssClass = "bi bi-box-seam",
                    MatchPrefixes = ["/almacen/articulos", "/almacen/kardex", "/almacen/alertas-stock", "/almacen/bodegas"],
                    Children =
                    [
                        new SidebarNavItem { Id = "alm-articulos", RequiredPermission = PermissionNames.Inventario.View, Text = "Artículos", NavigateUrl = "/almacen/articulos", MatchPrefixes = ["/almacen/articulos"], IconCssClass = "bi bi-box-seam" },
                        new SidebarNavItem { Id = "alm-kardex", RequiredPermission = PermissionNames.Inventario.View, Text = "Kardex", NavigateUrl = "/almacen/kardex", MatchPrefixes = ["/almacen/kardex"], IconCssClass = "bi bi-journal-arrow-down" },
                        new SidebarNavItem { Id = "alm-alertas", RequiredPermission = PermissionNames.Inventario.View, Text = "Alertas de stock", NavigateUrl = "/almacen/alertas-stock", MatchPrefixes = ["/almacen/alertas-stock"], IconCssClass = "bi bi-exclamation-triangle" },
                        new SidebarNavItem { Id = "alm-bodegas", RequiredPermission = PermissionNames.Inventario.View, Text = "Bodegas", NavigateUrl = "/almacen/bodegas", MatchPrefixes = ["/almacen/bodegas"], IconCssClass = "bi bi-building" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "inv-movimientos",
                    RequiredPermission = PermissionNames.Inventario.View,
                    Text = "Movimientos",
                    IconCssClass = "bi bi-arrow-left-right",
                    MatchPrefixes = ["/almacen/compras", "/almacen/requisiciones", "/almacen/descargos"],
                    Children =
                    [
                        new SidebarNavItem { Id = "alm-compras", RequiredPermission = PermissionNames.Compras.View, Text = "Compras", NavigateUrl = "/almacen/compras", MatchPrefixes = ["/almacen/compras"], IconCssClass = "bi bi-cart-plus" },
                        new SidebarNavItem { Id = "alm-requisiciones", RequiredPermission = PermissionNames.Inventario.View, Text = "Requisiciones", NavigateUrl = "/almacen/requisiciones", MatchPrefixes = ["/almacen/requisiciones"], IconCssClass = "bi bi-clipboard-check" },
                        new SidebarNavItem { Id = "alm-descargos", RequiredPermission = PermissionNames.Inventario.View, Text = "Descargos", NavigateUrl = "/almacen/descargos", MatchPrefixes = ["/almacen/descargos"], IconCssClass = "bi bi-box-arrow-up" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "proveedores",
                    RequiredPermission = PermissionNames.Proveedores.View,
                    Text = "Proveedores",
                    IconCssClass = "bi bi-truck",
                    NavigateUrl = "/proveedores",
                    MatchPrefixes = ["/proveedores"]
                },
                new SidebarNavItem
                {
                    Id = "inv-catalogos",
                    RequiredPermission = PermissionNames.Inventario.View,
                    Text = "Catálogos de almacén",
                    IconCssClass = "bi bi-tags",
                    MatchPrefixes = ["/almacen/tipos-articulo", "/almacen/categorias-unidad", "/almacen/unidades-medida"],
                    Children =
                    [
                        new SidebarNavItem { Id = "alm-tipos-articulo", RequiredPermission = PermissionNames.Inventario.View, Text = "Tipos de artículos", NavigateUrl = "/almacen/tipos-articulo", MatchPrefixes = ["/almacen/tipos-articulo"], IconCssClass = "bi bi-tags" },
                        new SidebarNavItem { Id = "alm-categorias-unidad", RequiredPermission = PermissionNames.Inventario.View, Text = "Categorías por unidad", NavigateUrl = "/almacen/categorias-unidad", MatchPrefixes = ["/almacen/categorias-unidad"], IconCssClass = "bi bi-diagram-2" },
                        new SidebarNavItem { Id = "alm-unidades", RequiredPermission = PermissionNames.Inventario.View, Text = "Unidades de medida", NavigateUrl = "/almacen/unidades-medida", MatchPrefixes = ["/almacen/unidades-medida"], IconCssClass = "bi bi-rulers" }
                    ]
                }
            ]
        },

        // ===== 5. CONFIGURACIÓN (al final, decisión del usuario 2026-08-05) (TODOS los mantenimientos + admin del sistema) =====
        new SidebarNavSection
        {
            Id = "configuracion",
            Label = "Configuración",
            Items =
            [
                new SidebarNavItem
                {
                    Id = "cfg-catalogos-comerciales",
                    RequiredPermission = PermissionNames.Configuracion.View,
                    Text = "Catálogos comerciales",
                    IconCssClass = "bi bi-journal-bookmark",
                    MatchPrefixes = ["/mantenimientos/barrios", "/ciclos", "/libretas", "/medidores", "/mantenimientos/clases-medidor", "/facturacion/condiciones-lectura", "/mantenimientos/codigo-cliente", "/abogados", "/tarifario/cai-offline"],
                    Children =
                    [
                        new SidebarNavItem { Id = "mant-barrios", RequiredPermission = PermissionNames.Configuracion.View, Text = "Barrios", NavigateUrl = "/mantenimientos/barrios", MatchPrefixes = ["/mantenimientos/barrios"], IconCssClass = "bi bi-map-fill" },
                        new SidebarNavItem { Id = "ciclos", RequiredPermission = PermissionNames.Configuracion.View, Text = "Ciclos", NavigateUrl = "/ciclos", MatchPrefixes = ["/ciclos"], IconCssClass = "bi bi-arrow-repeat" },
                        new SidebarNavItem { Id = "libretas", RequiredPermission = PermissionNames.Configuracion.View, Text = "Libretas", NavigateUrl = "/libretas", MatchPrefixes = ["/libretas"], IconCssClass = "bi bi-journal-bookmark" },
                        new SidebarNavItem { Id = "medidores", RequiredPermission = PermissionNames.Configuracion.View, Text = "Medidores", NavigateUrl = "/medidores", MatchPrefixes = ["/medidores"], IconCssClass = "bi bi-speedometer2" },
                        new SidebarNavItem { Id = "mant-clases-medidor", RequiredPermission = PermissionNames.Configuracion.View, Text = "Clases de medidor", NavigateUrl = "/mantenimientos/clases-medidor", MatchPrefixes = ["/mantenimientos/clases-medidor"], IconCssClass = "bi bi-speedometer" },
                        new SidebarNavItem { Id = "fact-condiciones-lectura", RequiredPermission = PermissionNames.Configuracion.View, Text = "Condiciones de lectura", NavigateUrl = "/facturacion/condiciones-lectura", MatchPrefixes = ["/facturacion/condiciones-lectura"], IconCssClass = "bi bi-list-check" },
                        new SidebarNavItem { Id = "mant-codigo-cliente", RequiredPermission = PermissionNames.Configuracion.View, Text = "Código de cliente", NavigateUrl = "/mantenimientos/codigo-cliente", MatchPrefixes = ["/mantenimientos/codigo-cliente"], IconCssClass = "bi bi-123" },
                        new SidebarNavItem { Id = "abogados", RequiredPermission = PermissionNames.Configuracion.View, Text = "Abogados", NavigateUrl = "/abogados", MatchPrefixes = ["/abogados"], IconCssClass = "bi bi-briefcase" },
                        new SidebarNavItem { Id = "tarv3-cai-offline", RequiredPermission = PermissionNames.Configuracion.View, Text = "CAI offline", NavigateUrl = "/tarifario/cai-offline", MatchPrefixes = ["/tarifario/cai-offline"], IconCssClass = "bi bi-upc-scan" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "cfg-catalogos-cobranza",
                    RequiredPermission = PermissionNames.Configuracion.View,
                    Text = "Catálogos de cobranza",
                    IconCssClass = "bi bi-journal-check",
                    MatchPrefixes = ["/facturacion/notas/motivos", "/mantenimientos/acciones-cobranza", "/mantenimientos/observaciones-cobranza", "/mantenimientos/recargo-mora"],
                    Children =
                    [
                        new SidebarNavItem { Id = "mant-motivos-notas", RequiredPermission = PermissionNames.Configuracion.View, Text = "Motivos de Notas C/D", NavigateUrl = "/facturacion/notas/motivos", MatchPrefixes = ["/facturacion/notas/motivos"], IconCssClass = "bi bi-tags" },
                        new SidebarNavItem { Id = "mant-acciones-cobranza", RequiredPermission = PermissionNames.Configuracion.View, Text = "Acciones de cobranza", NavigateUrl = "/mantenimientos/acciones-cobranza", MatchPrefixes = ["/mantenimientos/acciones-cobranza"], IconCssClass = "bi bi-journal-check" },
                        new SidebarNavItem { Id = "mant-observaciones-cobranza", RequiredPermission = PermissionNames.Configuracion.View, Text = "Observaciones cobranza", NavigateUrl = "/mantenimientos/observaciones-cobranza", MatchPrefixes = ["/mantenimientos/observaciones-cobranza"], IconCssClass = "bi bi-chat-square-text" },
                        new SidebarNavItem { Id = "mant-recargo-mora", RequiredPermission = PermissionNames.Configuracion.View, Text = "Recargo por mora", NavigateUrl = "/mantenimientos/recargo-mora", MatchPrefixes = ["/mantenimientos/recargo-mora"], IconCssClass = "bi bi-clock-history" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "cfg-tarifario",
                    RequiredPermission = PermissionNames.Configuracion.View,
                    Text = "Tarifario",
                    IconCssClass = "bi bi-calculator",
                    MatchPrefixes = ["/tarifario/cuadros", "/tarifario/maestro-servicios-v3", "/tarifario/desglose-abonos", "/mantenimientos/ajustes-tarifarios", "/mantenimientos/impuestos"],
                    Children =
                    [
                        new SidebarNavItem { Id = "tarv3-cuadros", RequiredPermission = PermissionNames.Configuracion.View, Text = "Cuadros tarifarios", NavigateUrl = "/tarifario/cuadros", MatchPrefixes = ["/tarifario/cuadros"], IconCssClass = "bi bi-table" },
                        new SidebarNavItem { Id = "tarv3-maestro-servicios", RequiredPermission = PermissionNames.Configuracion.View, Text = "Maestro servicios", NavigateUrl = "/tarifario/maestro-servicios-v3", MatchPrefixes = ["/tarifario/maestro-servicios-v3"], IconCssClass = "bi bi-list-ul" },
                        new SidebarNavItem { Id = "tarv3-desglose-abonos", RequiredPermission = PermissionNames.Configuracion.View, Text = "Distribución de abonos", NavigateUrl = "/tarifario/desglose-abonos", MatchPrefixes = ["/tarifario/desglose-abonos"], IconCssClass = "bi bi-percent" },
                        new SidebarNavItem { Id = "mant-ajustes-tarifarios", RequiredPermission = PermissionNames.Configuracion.View, Text = "Ajustes tarifarios", NavigateUrl = "/mantenimientos/ajustes-tarifarios", MatchPrefixes = ["/mantenimientos/ajustes-tarifarios"], IconCssClass = "bi bi-percent" },
                        new SidebarNavItem { Id = "mant-impuestos", RequiredPermission = PermissionNames.Configuracion.View, Text = "Impuestos", NavigateUrl = "/mantenimientos/impuestos", MatchPrefixes = ["/mantenimientos/impuestos"], IconCssClass = "bi bi-receipt" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "cfg-proveedores",
                    RequiredPermission = PermissionNames.Configuracion.View,
                    Text = "Catálogos de proveedor",
                    IconCssClass = "bi bi-tag",
                    MatchPrefixes = ["/mantenimientos/tipos-proveedor", "/mantenimientos/tipos-contacto"],
                    Children =
                    [
                        new SidebarNavItem { Id = "mant-tipos-proveedor", RequiredPermission = PermissionNames.Configuracion.View, Text = "Tipos de proveedor", NavigateUrl = "/mantenimientos/tipos-proveedor", MatchPrefixes = ["/mantenimientos/tipos-proveedor"], IconCssClass = "bi bi-tag" },
                        new SidebarNavItem { Id = "mant-tipos-contacto", RequiredPermission = PermissionNames.Configuracion.View, Text = "Tipos de contacto", NavigateUrl = "/mantenimientos/tipos-contacto", MatchPrefixes = ["/mantenimientos/tipos-contacto"], IconCssClass = "bi bi-person-lines-fill" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "cfg-sistema",
                    RequiredPermission = PermissionNames.Configuracion.View,
                    Text = "Sistema",
                    IconCssClass = "bi bi-shield-lock",
                    MatchPrefixes = ["/parametros", "/mi-app/usuarios", "/contabilidad/empresas", "/tipos-documento-fiscal", "/auditoria", "/informes/reportes", "/informes/datasets"],
                    Children =
                    [
                        new SidebarNavItem { Id = "param-usuarios", Text = "Usuarios", NavigateUrl = "/parametros/usuarios", MatchPrefixes = ["/parametros/usuarios"], IconCssClass = "bi bi-people-fill", SoloSuperAdmin = true },
                        new SidebarNavItem { Id = "param-roles", Text = "Roles y permisos", NavigateUrl = "/parametros/roles", MatchPrefixes = ["/parametros/roles"], IconCssClass = "bi bi-shield-check", SoloSuperAdmin = true },
                        new SidebarNavItem { Id = "app-usuarios", RequiredPermission = PermissionNames.Configuracion.View, Text = "Usuarios App", NavigateUrl = "/mi-app/usuarios", MatchPrefixes = ["/mi-app/usuarios"], IconCssClass = "bi bi-phone" },
                        new SidebarNavItem { Id = "cb-empresas", RequiredPermission = PermissionNames.Configuracion.View, Text = "Empresas", NavigateUrl = "/contabilidad/empresas", MatchPrefixes = ["/contabilidad/empresas"], MatchExact = true, IconCssClass = "bi bi-buildings" },
                        new SidebarNavItem { Id = "cb-crear-empresa", RequiredPermission = PermissionNames.Configuracion.View, Text = "Crear empresa", NavigateUrl = "/contabilidad/empresas/nueva", MatchPrefixes = ["/contabilidad/empresas/nueva"], IconCssClass = "bi bi-plus-circle" },
                        new SidebarNavItem { Id = "param-branding", Text = "Branding del Portal", NavigateUrl = "/parametros/branding", MatchPrefixes = ["/parametros/branding"], IconCssClass = "bi bi-palette", SoloSuperAdmin = true },
                        new SidebarNavItem { Id = "tipos-documento-fiscal", RequiredPermission = PermissionNames.Configuracion.View, Text = "Tipos de documento (SAR)", NavigateUrl = "/tipos-documento-fiscal", MatchPrefixes = ["/tipos-documento-fiscal"], IconCssClass = "bi bi-file-earmark-text" },
                        new SidebarNavItem { Id = "auditoria-config", Text = "Configuración de auditoría", NavigateUrl = "/auditoria/configuracion", MatchPrefixes = ["/auditoria/configuracion"], IconCssClass = "bi bi-sliders", SoloSuperAdmin = true },
                        new SidebarNavItem { Id = "auditoria-bitacora-maestros", RequiredPermission = PermissionNames.Configuracion.View, Text = "Bitácora de maestros", NavigateUrl = "/auditoria/bitacora-maestros", MatchPrefixes = ["/auditoria/bitacora-maestros"], IconCssClass = "bi bi-clock-history" },
                        new SidebarNavItem { Id = "informes-reportes", RequiredPermission = PermissionNames.Reporteria.View, Text = "Diseño Web (informes)", NavigateUrl = "/informes/reportes", MatchPrefixes = ["/informes/reportes"], IconCssClass = "bi bi-layout-text-window-reverse" },
                        new SidebarNavItem { Id = "informes-datasets", RequiredPermission = PermissionNames.Reporteria.View, Text = "Datasets Web (informes)", NavigateUrl = "/informes/datasets", MatchPrefixes = ["/informes/datasets"], IconCssClass = "bi bi-database" }
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
