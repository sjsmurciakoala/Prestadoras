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
                    MatchPrefixes = ["/facturacion/captacion/caja", "/facturacion/miscelaneos", "/facturacion/miscelaneos/catalogo"],
                    Children =
                    [
                        new SidebarNavItem { Id = "fact-captacion", Text = "Captación de Pagos", NavigateUrl = "/facturacion/captacion/caja", MatchPrefixes = ["/facturacion/captacion/caja"], IconCssClass = "bi bi-bag-check" },
                        new SidebarNavItem { Id = "fact-miscelaneos", Text = "Facturación Misceláneos", NavigateUrl = "/facturacion/miscelaneos", MatchPrefixes = ["/facturacion/miscelaneos"], IconCssClass = "bi bi-receipt" },
                        new SidebarNavItem { Id = "fact-catalogo-misc", Text = "Catálogo Misceláneos", NavigateUrl = "/facturacion/miscelaneos/catalogo", MatchPrefixes = ["/facturacion/miscelaneos/catalogo"], IconCssClass = "bi bi-journal-bookmark" }
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
                    NavigateUrl = "/facturacion/cobranza",
                    MatchPrefixes = ["/facturacion/cobranza"]
                },
                new SidebarNavItem
                {
                    Id = "fact-reversos",
                    Text = "Reversos",
                    IconCssClass = "bi bi-arrow-counterclockwise",
                    NavigateUrl = "/facturacion/captacion/reverso",
                    MatchPrefixes = ["/facturacion/captacion/reverso"]
                }
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

        // ===== INFORMES =====
        new SidebarNavSection
        {
            Label = "Informes",
            Items =
            [
                new SidebarNavItem
                {
                    Id = "informes-catalogo",
                    Text = "Catálogo",
                    IconCssClass = "bi bi-bar-chart-line",
                    NavigateUrl = "/informes",
                    MatchPrefixes = ["/informes"]
                },
                new SidebarNavItem
                {
                    Id = "informes-reportes",
                    Text = "Diseño Web",
                    IconCssClass = "bi bi-layout-text-window-reverse",
                    NavigateUrl = "/informes/reportes",
                    MatchPrefixes = ["/informes/reportes"]
                },
                new SidebarNavItem
                {
                    Id = "informes-datasets",
                    Text = "Datasets Web",
                    IconCssClass = "bi bi-database",
                    NavigateUrl = "/informes/reportes/datasets",
                    MatchPrefixes = ["/informes/reportes/datasets"]
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
