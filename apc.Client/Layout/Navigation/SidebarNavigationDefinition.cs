namespace apc.Client.Layout.Navigation;

public static class SidebarNavigationDefinition
{
    public static IReadOnlyList<SidebarNavSection> Sections { get; } =
    [
        // ===== HOME (sin label de sección) =====
        new SidebarNavSection
        {
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

        // ===== OPERACIÓN =====
        new SidebarNavSection
        {
            Label = "Operación",
            Items =
            [
                new SidebarNavItem
                {
                    Id = "clientes",
                    Text = "Clientes",
                    IconCssClass = "bi bi-people",
                    NavigateUrl = "/clientes",
                    MatchPrefixes = ["/clientes"]
                },
                new SidebarNavItem
                {
                    Id = "proveedores",
                    Text = "Proveedores",
                    IconCssClass = "bi bi-truck",
                    MatchPrefixes = ["/proveedores"],
                    Children =
                    [
                        new SidebarNavItem { Id = "prov-catalogo", Text = "Catálogo proveedores", NavigateUrl = "/proveedores", MatchPrefixes = ["/proveedores"], IconCssClass = "bi bi-list-ul" },
                        new SidebarNavItem { Id = "prov-tipos", Text = "Tipos de proveedor", NavigateUrl = "/proveedores/tipos", MatchPrefixes = ["/proveedores/tipos"], IconCssClass = "bi bi-tag" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "abogados",
                    Text = "Abogados",
                    IconCssClass = "bi bi-briefcase",
                    NavigateUrl = "/abogados",
                    MatchPrefixes = ["/abogados"]
                },
                new SidebarNavItem
                {
                    Id = "ordenes",
                    Text = "Órdenes",
                    IconCssClass = "bi bi-list-check",
                    NavigateUrl = "/ordenes",
                    MatchPrefixes = ["/ordenes"]
                },
                new SidebarNavItem
                {
                    Id = "rutas",
                    Text = "Rutas",
                    IconCssClass = "bi bi-map",
                    NavigateUrl = "/rutas",
                    MatchPrefixes = ["/rutas"]
                },
                new SidebarNavItem
                {
                    Id = "mapa",
                    Text = "Mapa",
                    IconCssClass = "bi bi-geo-alt",
                    NavigateUrl = "/mapa",
                    MatchPrefixes = ["/mapa"]
                },
                new SidebarNavItem
                {
                    Id = "ciclos",
                    Text = "Ciclos",
                    IconCssClass = "bi bi-arrow-repeat",
                    NavigateUrl = "/ciclos",
                    MatchPrefixes = ["/ciclos"]
                },
                new SidebarNavItem
                {
                    Id = "medidores",
                    Text = "Medidores",
                    IconCssClass = "bi bi-speedometer2",
                    NavigateUrl = "/medidores",
                    MatchPrefixes = ["/medidores"]
                },
                new SidebarNavItem
                {
                    Id = "auxiliares",
                    Text = "Auxiliares",
                    IconCssClass = "bi bi-file-earmark-text",
                    MatchPrefixes = ["/auxiliares"],
                    Children =
                    [
                        new SidebarNavItem { Id = "aux-lectura", Text = "Auxiliar de Lectura", NavigateUrl = "/auxiliares/auxiliar-lectura", MatchPrefixes = ["/auxiliares/auxiliar-lectura"], IconCssClass = "bi bi-pencil-square" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "tarifario-v3",
                    Text = "Tarifario",
                    IconCssClass = "bi bi-calculator",
                    MatchPrefixes = ["/tarifario"],
                    Children =
                    [
                        new SidebarNavItem { Id = "tarv3-cuadros", Text = "Cuadros tarifarios", NavigateUrl = "/tarifario/cuadros", MatchPrefixes = ["/tarifario/cuadros"], IconCssClass = "bi bi-table" },
                        new SidebarNavItem { Id = "tarv3-cliente-servicio", Text = "Cliente servicio", NavigateUrl = "/tarifario/cliente-servicio-v3", MatchPrefixes = ["/tarifario/cliente-servicio-v3"], IconCssClass = "bi bi-diagram-3" },
                        new SidebarNavItem { Id = "tarv3-maestro-servicios", Text = "Maestro servicios", NavigateUrl = "/tarifario/maestro-servicios-v3", MatchPrefixes = ["/tarifario/maestro-servicios-v3"], IconCssClass = "bi bi-list-ul" },
                        new SidebarNavItem { Id = "tarv3-cai-offline", Text = "CAI offline", NavigateUrl = "/tarifario/cai-offline", MatchPrefixes = ["/tarifario/cai-offline"], IconCssClass = "bi bi-upc-scan" },
                        new SidebarNavItem { Id = "tarv3-conflictos", Text = "Conflictos", NavigateUrl = "/tarifario/conflictos-v3", MatchPrefixes = ["/tarifario/conflictos-v3"], IconCssClass = "bi bi-exclamation-diamond" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "solicitudes",
                    Text = "Solicitudes",
                    IconCssClass = "bi bi-chat-left-text",
                    NavigateUrl = "/solicitudes",
                    MatchPrefixes = ["/solicitudes"]
                }
            ]
        },

        // ===== FACTURACIÓN =====
        new SidebarNavSection
        {
            Label = "Facturación",
            Items =
            [
                new SidebarNavItem
                {
                    Id = "fact-servicios",
                    Text = "Servicios públicos",
                    IconCssClass = "bi bi-lightning-charge",
                    MatchPrefixes = ["/facturacion/captacion/caja", "/facturacion/miscelaneos", "/facturacion/miscelaneos/catalogo", "/facturacion/miscelaneos/consulta"],
                    Children =
                    [
                        new SidebarNavItem { Id = "fact-captacion", Text = "Captación de Pagos", NavigateUrl = "/facturacion/captacion/caja", MatchPrefixes = ["/facturacion/captacion/caja"], IconCssClass = "bi bi-bag-check" },
                        new SidebarNavItem
                        {
                            Id = "fact-miscelaneos-group",
                            Text = "Misceláneos",
                            IconCssClass = "bi bi-receipt",
                            MatchPrefixes = ["/facturacion/miscelaneos"],
                            Children =
                            [
                                new SidebarNavItem {
                                    Id = "fact-miscelaneos",
                                    Text = "Facturación",
                                    NavigateUrl = "/facturacion/miscelaneos",
                                    MatchPrefixes = ["/facturacion/miscelaneos"],
                                    MatchExact = true,
                                    IconCssClass = "bi bi-receipt"
                                },
                                new SidebarNavItem {
                                    Id = "fact-consulta-misc",
                                    Text = "Consulta",
                                    NavigateUrl = "/facturacion/miscelaneos/consulta",
                                    MatchPrefixes = ["/facturacion/miscelaneos/consulta"],
                                    IconCssClass = "bi bi-search"
                                },
                                new SidebarNavItem { Id = "fact-catalogo-misc", Text = "Catálogo Misceláneos", NavigateUrl = "/facturacion/miscelaneos/catalogo", MatchPrefixes = ["/facturacion/miscelaneos/catalogo"], IconCssClass = "bi bi-journal-bookmark" }
                            ]
                        }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "fact-notas",
                    Text = "Notas Crédito/Débito",
                    IconCssClass = "bi bi-journal-text",
                    NavigateUrl = "/facturacion/notas",
                    MatchPrefixes = ["/facturacion/notas"]
                },
                new SidebarNavItem
                {
                    Id = "fact-cobranza",
                    Text = "Cobranza",
                    IconCssClass = "bi bi-collection",
                    MatchPrefixes = ["/facturacion/cobranza"],
                    Children =
                    [
                        new SidebarNavItem {
                            Id = "fact-cobranza-main",
                            Text = "Gestión de cobranza",
                            NavigateUrl = "/facturacion/cobranza",
                            MatchPrefixes = ["/facturacion/cobranza"],
                            MatchExact = true,
                            IconCssClass = "bi bi-collection"
                        },
                        new SidebarNavItem {
                            Id = "fact-cortes-masivos",
                            Text = "Cortes masivos",
                            NavigateUrl = "/facturacion/cobranza/cortes-masivos",
                            MatchPrefixes = ["/facturacion/cobranza/cortes-masivos"],
                            IconCssClass = "bi bi-scissors"
                        },
                        new SidebarNavItem {
                            Id = "fact-acciones-cobranza",
                            Text = "Acciones de cobranza",
                            NavigateUrl = "/facturacion/cobranza/acciones-cobranza",
                            MatchPrefixes = ["/facturacion/cobranza/acciones-cobranza"],
                            IconCssClass = "bi bi-journal-text"
                        },
                        new SidebarNavItem {
                            Id = "fact-historial-bitacora",
                            Text = "Historial de bitácora",
                            NavigateUrl = "/facturacion/cobranza/historial-acciones",
                            MatchPrefixes = ["/facturacion/cobranza/historial-acciones"],
                            IconCssClass = "bi bi-clock-history"
                        },
                        new SidebarNavItem {
                            Id = "fact-bloqueo-clientes",
                            Text = "Bloqueo de clientes",
                            NavigateUrl = "/facturacion/cobranza/bloqueo-clientes",
                            MatchPrefixes = ["/facturacion/cobranza/bloqueo-clientes"],
                            IconCssClass = "bi bi-lock"
                        },
                        new SidebarNavItem {
                            Id = "fact-clientes-cobros",
                            Text = "Clientes para cobros",
                            NavigateUrl = "/facturacion/cobranza/clientes-cobros",
                            MatchPrefixes = ["/facturacion/cobranza/clientes-cobros"],
                            IconCssClass = "bi bi-people"
                        },
                        new SidebarNavItem {
                            Id = "fact-cartera-vencida",
                            Text = "Cartera vencida",
                            NavigateUrl = "/facturacion/cobranza/cartera-vencida",
                            MatchPrefixes = ["/facturacion/cobranza/cartera-vencida"],
                            IconCssClass = "bi bi-calendar-x"
                        }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "fact-reversos",
                    Text = "Reversos",
                    IconCssClass = "bi bi-arrow-counterclockwise",
                    NavigateUrl = "/facturacion/captacion/reverso",
                    MatchPrefixes = ["/facturacion/captacion/reverso"]
                },
                // Gestión de Caja desvinculada — módulo no utilizado (2026-06-04)
                // new SidebarNavItem { Id = "fact-caja", Text = "Gestión de Caja", ... }
            ]
        },

        // ===== MANTENIMIENTOS =====
        new SidebarNavSection
        {
            Label = "Mantenimientos",
            Items =
            [
                new SidebarNavItem
                {
                    Id = "mant-recargo-mora",
                    Text = "Recargo por mora",
                    IconCssClass = "bi bi-clock-history",
                    NavigateUrl = "/mantenimientos/recargo-mora",
                    MatchPrefixes = ["/mantenimientos/recargo-mora"]
                },
                new SidebarNavItem
                {
                    Id = "mant-ajustes-tarifarios",
                    Text = "Ajustes tarifarios",
                    IconCssClass = "bi bi-percent",
                    NavigateUrl = "/mantenimientos/ajustes-tarifarios",
                    MatchPrefixes = ["/mantenimientos/ajustes-tarifarios"]
                },
                new SidebarNavItem
                {
                    Id = "mant-motivos-notas",
                    Text = "Motivos de Notas C/D",
                    IconCssClass = "bi bi-tags",
                    NavigateUrl = "/facturacion/notas/motivos",
                    MatchPrefixes = ["/facturacion/notas/motivos"]
                },
                new SidebarNavItem
                {
                    Id = "mant-barrios",
                    Text = "Barrios",
                    NavigateUrl = "/mantenimientos/barrios",
                    MatchPrefixes = ["/mantenimientos/barrios"],
                    IconCssClass = "bi bi-map-fill"
                },
                new SidebarNavItem
                {
                    Id = "mant-clases-medidor",
                    Text = "Clases de medidor",
                    NavigateUrl = "/mantenimientos/clases-medidor",
                    MatchPrefixes = ["/mantenimientos/clases-medidor"],
                    IconCssClass = "bi bi-speedometer"
                },
                new SidebarNavItem
                {
                    Id = "mant-acciones-cobranza",
                    Text = "Acciones de cobranza",
                    IconCssClass = "bi bi-journal-check",
                    NavigateUrl = "/mantenimientos/acciones-cobranza",
                    MatchPrefixes = ["/mantenimientos/acciones-cobranza"]
                },
                new SidebarNavItem
                {
                    Id = "mant-observaciones-cobranza",
                    Text = "Observaciones cobranza",
                    IconCssClass = "bi bi-chat-square-text",
                    NavigateUrl = "/mantenimientos/observaciones-cobranza",
                    MatchPrefixes = ["/mantenimientos/observaciones-cobranza"]
                }
            ]
        },

        // ===== CONTABILIDAD =====
        new SidebarNavSection
        {
            Label = "Contabilidad",
            Items =
            [
                new SidebarNavItem
                {
                    Id = "cont-bancos",
                    Text = "Bancos",
                    IconCssClass = "bi bi-bank",
                    MatchPrefixes = ["/contabilidad/bancos", "/bancos/configuracion_transacciones", "/bancos/configuracion"],
                    Children =
                    [
                        new SidebarNavItem { Id = "bn-gestion", Text = "Gestión de bancos", NavigateUrl = "/contabilidad/bancos", MatchPrefixes = ["/contabilidad/bancos"], IconCssClass = "bi bi-building" },
                        new SidebarNavItem { Id = "bn-transacciones", Text = "Config. transacciones", NavigateUrl = "/bancos/configuracion_transacciones", MatchPrefixes = ["/bancos/configuracion_transacciones"], IconCssClass = "bi bi-sliders" },
                        new SidebarNavItem { Id = "bn-config", Text = "Configuración", NavigateUrl = "/bancos/configuracion", MatchPrefixes = ["/bancos/configuracion"], IconCssClass = "bi bi-gear" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "cont-contab",
                    Text = "Contabilidad",
                    IconCssClass = "bi bi-journal-text",
                    MatchPrefixes = ["/contabilidad/empresas", "/contabilidad/plan-cuentas", "/contabilidad/centros-costo", "/contabilidad/terceros", "/contabilidad/diarios", "/contabilidad/periodos", "/contabilidad/tipos-transaccion", "/contabilidad/polizas", "/contabilidad/partidas"],
                    Children =
                    [
                        new SidebarNavItem { Id = "cb-empresas", Text = "Empresas", NavigateUrl = "/contabilidad/empresas", MatchPrefixes = ["/contabilidad/empresas"], IconCssClass = "bi bi-buildings" },
                        new SidebarNavItem { Id = "cb-config-sistema", Text = "Configuración Sistema", NavigateUrl = "/contabilidad/empresas/configuracion", MatchPrefixes = ["/contabilidad/empresas/configuracion"], IconCssClass = "bi bi-sliders" },
                        new SidebarNavItem { Id = "cb-integracion", Text = "Integración Comercial", NavigateUrl = "/contabilidad/empresas/integracion", MatchPrefixes = ["/contabilidad/empresas/integracion"], IconCssClass = "bi bi-arrow-left-right" },
                        new SidebarNavItem { Id = "cb-crear-empresa", Text = "Crear empresa", NavigateUrl = "/contabilidad/empresas/nueva", MatchPrefixes = ["/contabilidad/empresas/nueva"], IconCssClass = "bi bi-plus-circle" },
                        new SidebarNavItem { Id = "cb-plan-cuentas", Text = "Plan de cuentas", NavigateUrl = "/contabilidad/plan-cuentas", MatchPrefixes = ["/contabilidad/plan-cuentas"], IconCssClass = "bi bi-diagram-3" },
                        new SidebarNavItem { Id = "cb-centros-costo", Text = "Centros de costo", NavigateUrl = "/contabilidad/centros-costo", MatchPrefixes = ["/contabilidad/centros-costo"], IconCssClass = "bi bi-boxes" },
                        new SidebarNavItem { Id = "cb-terceros", Text = "Terceros", NavigateUrl = "/contabilidad/terceros", MatchPrefixes = ["/contabilidad/terceros"], IconCssClass = "bi bi-people" },
                        new SidebarNavItem { Id = "cb-diarios", Text = "Diarios contables", NavigateUrl = "/contabilidad/diarios", MatchPrefixes = ["/contabilidad/diarios"], IconCssClass = "bi bi-book" },
                        new SidebarNavItem { Id = "cb-periodos", Text = "Períodos contables", NavigateUrl = "/contabilidad/periodos", MatchPrefixes = ["/contabilidad/periodos"], IconCssClass = "bi bi-calendar" },
                        new SidebarNavItem { Id = "cb-tipos-comprobantes", Text = "Tipos de comprobantes", NavigateUrl = "/contabilidad/tipos-transaccion", MatchPrefixes = ["/contabilidad/tipos-transaccion"], IconCssClass = "bi bi-tags" },
                        new SidebarNavItem { Id = "cb-polizas", Text = "Partidas", NavigateUrl = "/contabilidad/partidas", MatchPrefixes = ["/contabilidad/partidas", "/contabilidad/polizas"], IconCssClass = "bi bi-file-earmark-check" }
                    ]
                }
            ]
        },

        // ===== PRESUPUESTO =====
        new SidebarNavSection
        {
            Label = "Presupuesto",
            Items =
            [
                new SidebarNavItem { Id = "presupuesto", Text = "Presupuesto", NavigateUrl = "/presupuesto/configuraciones", MatchPrefixes = ["/presupuesto/configuraciones"], IconCssClass = "bi bi-cash-stack" }
            ]
        },

        // ===== INFORMES =====
        new SidebarNavSection
        {
            Label = "Informes",
            Items =
            [
                new SidebarNavItem
                {
                    Id = "informes",
                    Text = "Informes",
                    IconCssClass = "bi bi-bar-chart-line",
                    MatchPrefixes = ["/informes"],
                    Children =
                    [
                        new SidebarNavItem { Id = "informes-panel", Text = "Panel de informes", NavigateUrl = "/informes", MatchExact = true, IconCssClass = "bi bi-grid-3x3-gap" },
                        new SidebarNavItem { Id = "informes-catalogo", Text = "Catálogo", NavigateUrl = "/informes/catalogo", MatchPrefixes = ["/informes/catalogo"], IconCssClass = "bi bi-collection" },
                        new SidebarNavItem { Id = "informes-partidas", Text = "Partidas contables", NavigateUrl = "/informes/partidas-contabilidad", MatchPrefixes = ["/informes/partidas-contabilidad"], IconCssClass = "bi bi-journal-check" },
                        new SidebarNavItem { Id = "informes-reportes", Text = "Diseño Web", NavigateUrl = "/informes/reportes", MatchPrefixes = ["/informes/reportes"], IconCssClass = "bi bi-layout-text-window-reverse" },
                        new SidebarNavItem { Id = "informes-datasets", Text = "Datasets Web", NavigateUrl = "/informes/datasets", MatchPrefixes = ["/informes/datasets"], IconCssClass = "bi bi-database" }
                    ]
                }
            ]
        },

        // ===== CONFIGURACIÓN =====
        new SidebarNavSection
        {
            Label = "Configuración",
            Items =
            [
                new SidebarNavItem
                {
                    Id = "app-usuarios",
                    Text = "Usuarios App",
                    IconCssClass = "bi bi-phone",
                    NavigateUrl = "/mi-app/usuarios",
                    MatchPrefixes = ["/mi-app/usuarios"]
                },
                new SidebarNavItem
                {
                    Id = "tipos-documento-fiscal",
                    Text = "Tipos de documento (SAR)",
                    IconCssClass = "bi bi-file-earmark-text",
                    NavigateUrl = "/tipos-documento-fiscal",
                    MatchPrefixes = ["/tipos-documento-fiscal"]
                },
                new SidebarNavItem
                {
                    Id = "user-account",
                    Text = "Cuenta",
                    IconCssClass = "bi bi-person-circle",
                    NavigateUrl = "/Account/Manage",
                    MatchPrefixes = ["/Account/Manage"]
                },
                new SidebarNavItem
                {
                    Id = "logout",
                    Text = "Cerrar sesión",
                    IconCssClass = "bi bi-box-arrow-right",
                    NavigateUrl = "/Account/Logout",
                    MatchPrefixes = ["/Account/Logout"]
                }
            ]
        },

        // ===== PARÁMETROS (Solo Super Administrador) =====
        new SidebarNavSection
        {
            Label = "Parámetros",
            RequiredPolicy = SIAD.Core.Constants.AuthorizationPolicies.SuperAdmin,
            Items =
            [
                new SidebarNavItem
                {
                    Id = "param-branding",
                    Text = "Branding del Portal",
                    IconCssClass = "bi bi-palette",
                    NavigateUrl = "/parametros/branding",
                    MatchPrefixes = ["/parametros/branding"]
                },
                new SidebarNavItem
                {
                    Id = "param-usuarios",
                    Text = "Usuarios",
                    IconCssClass = "bi bi-people-fill",
                    NavigateUrl = "/parametros/usuarios",
                    MatchPrefixes = ["/parametros/usuarios"]
                },
                new SidebarNavItem
                {
                    Id = "param-roles",
                    Text = "Roles y permisos",
                    IconCssClass = "bi bi-shield-check",
                    NavigateUrl = "/parametros/roles",
                    MatchPrefixes = ["/parametros/roles"]
                }
            ]
        }
    ];
}
