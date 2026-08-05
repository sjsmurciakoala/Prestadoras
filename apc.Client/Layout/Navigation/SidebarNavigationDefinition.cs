namespace apc.Client.Layout.Navigation;

/// <summary>
/// Árbol del menú lateral. Reorganizado en 2026-07-31: de 11 secciones a 10, con la
/// profundidad bajada de 3 niveles a 2 y 96 destinos reducidos a 92, con dos criterios:
///
///   1. Orden por frecuencia de uso, no por historia del sistema. La caja subió del
///      tercer nivel al primero; lo esporádico (abogados, carga inicial) bajó.
///   2. Un solo "Mantenimiento" por sección, con los catálogos que esa sección usa.
///      Antes había tres, con cuatro ítems repetidos (mismo texto, misma ruta y
///      mismo Id) entre "Operación > Mantenimiento" y la sección "Mantenimientos".
///
/// Ningún destino se perdió y ninguna ruta cambió: esto es sólo el orden y el
/// agrupamiento. Los Id son únicos a propósito y no se referencian fuera de este
/// archivo.
///
/// Nota sobre MatchPrefixes: un item CON hijos nunca los lee — SidebarNavNode decide
/// su estado activo con HasActiveDescendant. Por eso los grupos aquí no los declaran;
/// antes lo hacían duplicando a mano las rutas de sus hijos, que es exactamente lo que
/// se desincroniza.
///
/// Los MatchPrefixes de la SECCIÓN son otra cosa: declaran su territorio para que el
/// acordeón sepa dónde está el usuario en las 29 rutas que no tienen entrada propia en el
/// menú (los previews de informes, /bancos/cuentas, /facturacion/caja...). No encienden
/// nada, y un item siempre gana sobre ellos: por eso /contabilidad/bancos abre Bancos y no
/// Contabilidad, aunque ésta declare "/contabilidad".
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

        // ===== FACTURACIÓN Y COBRO =====
        // Primero lo de uso diario (caja, abonos, reversos), después lo periódico.
        new SidebarNavSection
        {
            Id = "facturacion",
            Label = "Facturación y cobro",
            MatchPrefixes = ["/facturacion", "/tarifario"],
            Items =
            [
                new SidebarNavItem
                {
                    Id = "fac-caja",
                    Text = "Caja",
                    IconCssClass = "bi bi-bag-check",
                    NavigateUrl = "/facturacion/captacion/caja",
                    MatchPrefixes = ["/facturacion/captacion/caja"]
                },
                new SidebarNavItem
                {
                    Id = "fac-abonos",
                    Text = "Abonos especiales",
                    IconCssClass = "bi bi-cash-coin",
                    Children =
                    [
                        // MatchExact: la consulta cuelga de esta misma ruta.
                        new SidebarNavItem { Id = "fac-abonos-registrar", Text = "Registrar", NavigateUrl = "/facturacion/captacion/abonos-especiales", MatchPrefixes = ["/facturacion/captacion/abonos-especiales"], MatchExact = true, IconCssClass = "bi bi-cash-coin" },
                        new SidebarNavItem { Id = "fac-abonos-consulta", Text = "Consultar", NavigateUrl = "/facturacion/captacion/abonos-especiales/consulta", MatchPrefixes = ["/facturacion/captacion/abonos-especiales/consulta"], IconCssClass = "bi bi-search" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "fac-reversos",
                    Text = "Reversos",
                    IconCssClass = "bi bi-arrow-counterclockwise",
                    NavigateUrl = "/facturacion/captacion/reverso",
                    MatchPrefixes = ["/facturacion/captacion/reverso"]
                },
                new SidebarNavItem
                {
                    Id = "fac-misc",
                    Text = "Misceláneos",
                    IconCssClass = "bi bi-receipt",
                    Children =
                    [
                        // MatchExact: la consulta y el catálogo cuelgan de esta ruta.
                        new SidebarNavItem { Id = "fac-misc-facturar", Text = "Facturar", NavigateUrl = "/facturacion/miscelaneos", MatchPrefixes = ["/facturacion/miscelaneos"], MatchExact = true, IconCssClass = "bi bi-receipt" },
                        new SidebarNavItem { Id = "fac-misc-consulta", Text = "Consulta", NavigateUrl = "/facturacion/miscelaneos/consulta", MatchPrefixes = ["/facturacion/miscelaneos/consulta"], IconCssClass = "bi bi-search" },
                        new SidebarNavItem { Id = "fac-misc-catalogo", Text = "Catálogo", NavigateUrl = "/facturacion/miscelaneos/catalogo", MatchPrefixes = ["/facturacion/miscelaneos/catalogo"], IconCssClass = "bi bi-journal-bookmark" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "fac-notas",
                    Text = "Notas de crédito y débito",
                    IconCssClass = "bi bi-journal-text",
                    Children =
                    [
                        // MatchExact nuevo: los motivos vivían en otra sección y ahora son
                        // hermanos, así que "/facturacion/notas" los capturaría a ambos.
                        new SidebarNavItem { Id = "fac-notas-emision", Text = "Emisión", NavigateUrl = "/facturacion/notas", MatchPrefixes = ["/facturacion/notas"], MatchExact = true, IconCssClass = "bi bi-journal-text" },
                        new SidebarNavItem { Id = "fac-notas-motivos", Text = "Motivos", NavigateUrl = "/facturacion/notas/motivos", MatchPrefixes = ["/facturacion/notas/motivos"], IconCssClass = "bi bi-tags" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "fac-cobranza",
                    Text = "Cobranza",
                    IconCssClass = "bi bi-collection",
                    Children =
                    [
                        // MatchExact: todo lo demás de cobranza cuelga de esta ruta.
                        new SidebarNavItem { Id = "fac-cob-gestion", Text = "Gestión de cobranza", NavigateUrl = "/facturacion/cobranza", MatchPrefixes = ["/facturacion/cobranza"], MatchExact = true, IconCssClass = "bi bi-collection" },
                        new SidebarNavItem { Id = "fac-cob-cortes", Text = "Cortes masivos", NavigateUrl = "/facturacion/cobranza/cortes-masivos", MatchPrefixes = ["/facturacion/cobranza/cortes-masivos"], IconCssClass = "bi bi-scissors" },
                        new SidebarNavItem { Id = "fac-cob-acciones", Text = "Acciones de cobranza", NavigateUrl = "/facturacion/cobranza/acciones-cobranza", MatchPrefixes = ["/facturacion/cobranza/acciones-cobranza"], IconCssClass = "bi bi-journal-text" },
                        new SidebarNavItem { Id = "fac-cob-historial", Text = "Historial de bitácora", NavigateUrl = "/facturacion/cobranza/historial-acciones", MatchPrefixes = ["/facturacion/cobranza/historial-acciones"], IconCssClass = "bi bi-clock-history" },
                        new SidebarNavItem { Id = "fac-cob-bloqueo", Text = "Bloqueo de clientes", NavigateUrl = "/facturacion/cobranza/bloqueo-clientes", MatchPrefixes = ["/facturacion/cobranza/bloqueo-clientes"], IconCssClass = "bi bi-lock" },
                        new SidebarNavItem { Id = "fac-cob-clientes", Text = "Clientes para cobros", NavigateUrl = "/facturacion/cobranza/clientes-cobros", MatchPrefixes = ["/facturacion/cobranza/clientes-cobros"], IconCssClass = "bi bi-people" },
                        new SidebarNavItem { Id = "fac-cob-cartera", Text = "Cartera vencida", NavigateUrl = "/facturacion/cobranza/cartera-vencida", MatchPrefixes = ["/facturacion/cobranza/cartera-vencida"], IconCssClass = "bi bi-calendar-x" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "fac-periodos",
                    Text = "Períodos y calendario",
                    IconCssClass = "bi bi-calendar-month",
                    Children =
                    [
                        new SidebarNavItem { Id = "fac-per-comerciales", Text = "Períodos comerciales", NavigateUrl = "/facturacion/periodos-comerciales", MatchPrefixes = ["/facturacion/periodos-comerciales"], IconCssClass = "bi bi-calendar-month" },
                        new SidebarNavItem { Id = "fac-per-calendario", Text = "Calendario de facturación", NavigateUrl = "/facturacion/calendario-facturacion", MatchPrefixes = ["/facturacion/calendario-facturacion"], IconCssClass = "bi bi-calendar-week" },
                        new SidebarNavItem { Id = "fac-per-condiciones", Text = "Condiciones de lectura", NavigateUrl = "/facturacion/condiciones-lectura", MatchPrefixes = ["/facturacion/condiciones-lectura"], IconCssClass = "bi bi-list-check" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "fac-tarifario",
                    Text = "Tarifario",
                    IconCssClass = "bi bi-calculator",
                    Children =
                    [
                        new SidebarNavItem { Id = "fac-tar-cuadros", Text = "Cuadros tarifarios", NavigateUrl = "/tarifario/cuadros", MatchPrefixes = ["/tarifario/cuadros"], IconCssClass = "bi bi-table" },
                        new SidebarNavItem { Id = "fac-tar-cliente-servicio", Text = "Cliente servicio", NavigateUrl = "/tarifario/cliente-servicio-v3", MatchPrefixes = ["/tarifario/cliente-servicio-v3"], IconCssClass = "bi bi-diagram-3" },
                        new SidebarNavItem { Id = "fac-tar-maestro", Text = "Maestro de servicios", NavigateUrl = "/tarifario/maestro-servicios-v3", MatchPrefixes = ["/tarifario/maestro-servicios-v3"], IconCssClass = "bi bi-list-ul" },
                        new SidebarNavItem { Id = "fac-tar-desglose", Text = "Distribución de abonos", NavigateUrl = "/tarifario/desglose-abonos", MatchPrefixes = ["/tarifario/desglose-abonos"], IconCssClass = "bi bi-percent" },
                        new SidebarNavItem { Id = "fac-tar-cai", Text = "CAI offline", NavigateUrl = "/tarifario/cai-offline", MatchPrefixes = ["/tarifario/cai-offline"], IconCssClass = "bi bi-upc-scan" },
                        new SidebarNavItem { Id = "fac-tar-conflictos", Text = "Conflictos", NavigateUrl = "/tarifario/conflictos-v3", MatchPrefixes = ["/tarifario/conflictos-v3"], IconCssClass = "bi bi-exclamation-diamond" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "fac-mant",
                    Text = "Mantenimiento",
                    IconCssClass = "bi bi-tools",
                    Children =
                    [
                        new SidebarNavItem { Id = "fac-mant-mora", Text = "Recargo por mora", NavigateUrl = "/mantenimientos/recargo-mora", MatchPrefixes = ["/mantenimientos/recargo-mora"], IconCssClass = "bi bi-clock-history" },
                        new SidebarNavItem { Id = "fac-mant-ajustes", Text = "Ajustes tarifarios", NavigateUrl = "/mantenimientos/ajustes-tarifarios", MatchPrefixes = ["/mantenimientos/ajustes-tarifarios"], IconCssClass = "bi bi-percent" },
                        new SidebarNavItem { Id = "fac-mant-impuestos", Text = "Impuestos", NavigateUrl = "/mantenimientos/impuestos", MatchPrefixes = ["/mantenimientos/impuestos"], IconCssClass = "bi bi-receipt" },
                        new SidebarNavItem { Id = "fac-mant-sar", Text = "Tipos de documento (SAR)", NavigateUrl = "/tipos-documento-fiscal", MatchPrefixes = ["/tipos-documento-fiscal"], IconCssClass = "bi bi-file-earmark-text" }
                    ]
                }
            ]
        },

        // ===== CLIENTES Y OPERACIÓN =====
        // "Auxiliares" (Auxiliar de Lectura) eliminado en la Fase C del plan
        // apertura-ciclo-único (2026-07-15): la apertura integral genera la planilla y su
        // consulta vive en Períodos comerciales.
        new SidebarNavSection
        {
            Id = "operacion",
            Label = "Clientes y operación",
            Items =
            [
                new SidebarNavItem
                {
                    Id = "ope-clientes",
                    Text = "Clientes",
                    IconCssClass = "bi bi-people",
                    NavigateUrl = "/clientes",
                    MatchPrefixes = ["/clientes"]
                },
                new SidebarNavItem
                {
                    Id = "ope-solicitudes",
                    Text = "Solicitudes",
                    IconCssClass = "bi bi-chat-left-text",
                    NavigateUrl = "/solicitudes",
                    MatchPrefixes = ["/solicitudes"]
                },
                new SidebarNavItem
                {
                    Id = "ope-ordenes",
                    Text = "Órdenes",
                    IconCssClass = "bi bi-list-check",
                    NavigateUrl = "/ordenes",
                    MatchPrefixes = ["/ordenes"]
                },
                new SidebarNavItem
                {
                    Id = "ope-ciclos",
                    Text = "Ciclos",
                    IconCssClass = "bi bi-arrow-repeat",
                    NavigateUrl = "/ciclos",
                    MatchPrefixes = ["/ciclos"]
                },
                new SidebarNavItem
                {
                    Id = "ope-libretas",
                    Text = "Libretas",
                    IconCssClass = "bi bi-journal-bookmark",
                    NavigateUrl = "/libretas",
                    MatchPrefixes = ["/libretas"]
                },
                new SidebarNavItem
                {
                    Id = "ope-medidores",
                    Text = "Medidores",
                    IconCssClass = "bi bi-speedometer2",
                    NavigateUrl = "/medidores",
                    MatchPrefixes = ["/medidores"]
                },
                new SidebarNavItem
                {
                    Id = "ope-mapa",
                    Text = "Mapa",
                    IconCssClass = "bi bi-geo-alt",
                    NavigateUrl = "/mapa",
                    MatchPrefixes = ["/mapa"]
                },
                new SidebarNavItem
                {
                    Id = "ope-abogados",
                    Text = "Abogados",
                    IconCssClass = "bi bi-briefcase",
                    NavigateUrl = "/abogados",
                    MatchPrefixes = ["/abogados"]
                },
                new SidebarNavItem
                {
                    Id = "ope-mant",
                    Text = "Mantenimiento",
                    IconCssClass = "bi bi-tools",
                    Children =
                    [
                        new SidebarNavItem { Id = "ope-mant-barrios", Text = "Barrios", NavigateUrl = "/mantenimientos/barrios", MatchPrefixes = ["/mantenimientos/barrios"], IconCssClass = "bi bi-map-fill" },
                        new SidebarNavItem { Id = "ope-mant-codigo-cliente", Text = "Código de cliente", NavigateUrl = "/mantenimientos/codigo-cliente", MatchPrefixes = ["/mantenimientos/codigo-cliente"], IconCssClass = "bi bi-123" },
                        new SidebarNavItem { Id = "ope-mant-clases-medidor", Text = "Clases de medidor", NavigateUrl = "/mantenimientos/clases-medidor", MatchPrefixes = ["/mantenimientos/clases-medidor"], IconCssClass = "bi bi-speedometer" },
                        // "Catálogo de..." los distingue de las pantallas operativas del mismo
                        // nombre que viven en Facturación y cobro > Cobranza.
                        new SidebarNavItem { Id = "ope-mant-acciones-cobranza", Text = "Catálogo de acciones", NavigateUrl = "/mantenimientos/acciones-cobranza", MatchPrefixes = ["/mantenimientos/acciones-cobranza"], IconCssClass = "bi bi-journal-check" },
                        new SidebarNavItem { Id = "ope-mant-observaciones-cobranza", Text = "Catálogo de observaciones", NavigateUrl = "/mantenimientos/observaciones-cobranza", MatchPrefixes = ["/mantenimientos/observaciones-cobranza"], IconCssClass = "bi bi-chat-square-text" }
                    ]
                }
            ]
        },

        // ===== ALMACÉN =====
        new SidebarNavSection
        {
            Id = "almacen",
            Label = "Almacén",
            MatchPrefixes = ["/almacen"],
            Items =
            [
                new SidebarNavItem
                {
                    Id = "alm-articulos",
                    Text = "Artículos",
                    IconCssClass = "bi bi-box-seam",
                    NavigateUrl = "/almacen/articulos",
                    MatchPrefixes = ["/almacen/articulos"]
                },
                new SidebarNavItem
                {
                    Id = "alm-movimientos",
                    Text = "Movimientos de almacén",
                    IconCssClass = "bi bi-arrow-down-up",
                    NavigateUrl = "/almacen/movimientos",
                    MatchPrefixes = ["/almacen/movimientos"]
                },
                new SidebarNavItem
                {
                    Id = "alm-traslados",
                    Text = "Traslados entre bodegas",
                    IconCssClass = "bi bi-truck",
                    NavigateUrl = "/almacen/traslados",
                    MatchPrefixes = ["/almacen/traslados"]
                },
                new SidebarNavItem
                {
                    Id = "alm-kardex",
                    Text = "Kardex",
                    IconCssClass = "bi bi-journal-arrow-down",
                    NavigateUrl = "/almacen/kardex",
                    MatchPrefixes = ["/almacen/kardex"]
                },
                new SidebarNavItem
                {
                    Id = "alm-alertas",
                    Text = "Alertas de stock",
                    IconCssClass = "bi bi-exclamation-triangle",
                    NavigateUrl = "/almacen/alertas-stock",
                    MatchPrefixes = ["/almacen/alertas-stock"]
                },
                new SidebarNavItem
                {
                    Id = "alm-requisiciones",
                    Text = "Requisiciones",
                    IconCssClass = "bi bi-clipboard-check",
                    NavigateUrl = "/almacen/requisiciones",
                    MatchPrefixes = ["/almacen/requisiciones"]
                },
                new SidebarNavItem
                {
                    Id = "alm-descargos",
                    Text = "Descargos",
                    IconCssClass = "bi bi-box-arrow-up",
                    NavigateUrl = "/almacen/descargos",
                    MatchPrefixes = ["/almacen/descargos"]
                },
                new SidebarNavItem
                {
                    Id = "alm-compras",
                    Text = "Compras",
                    IconCssClass = "bi bi-cart-plus",
                    Children =
                    [
                        new SidebarNavItem { Id = "alm-compras-ordenes", Text = "Órdenes de compra", NavigateUrl = "/almacen/ordenes-compra", MatchPrefixes = ["/almacen/ordenes-compra"], IconCssClass = "bi bi-file-earmark-text" },
                        new SidebarNavItem { Id = "alm-compras-recepciones", Text = "Recepción de compras", NavigateUrl = "/almacen/compras/recepciones", MatchPrefixes = ["/almacen/compras/recepciones"], IconCssClass = "bi bi-box-arrow-in-down" },
                        // MatchExact: si no, "/almacen/compras" también capturaría las recepciones,
                        // que cuelgan de esa ruta, y quedarían dos entradas activas a la vez.
                        new SidebarNavItem { Id = "alm-compras-consulta", Text = "Consulta de compras", NavigateUrl = "/almacen/compras", MatchPrefixes = ["/almacen/compras"], MatchExact = true, IconCssClass = "bi bi-cart-plus" },
                        new SidebarNavItem { Id = "alm-compras-carga-inicial", Text = "Carga inicial", NavigateUrl = "/almacen/carga-inicial", MatchPrefixes = ["/almacen/carga-inicial"], IconCssClass = "bi bi-flag" }
                    ]
                },
                new SidebarNavItem
                {
                    Id = "alm-mant",
                    Text = "Mantenimiento",
                    IconCssClass = "bi bi-tools",
                    Children =
                    [
                        new SidebarNavItem { Id = "alm-mant-bodegas", Text = "Bodegas", NavigateUrl = "/almacen/bodegas", MatchPrefixes = ["/almacen/bodegas"], IconCssClass = "bi bi-building" },
                        new SidebarNavItem { Id = "alm-mant-tipos-articulo", Text = "Tipos de artículo", NavigateUrl = "/almacen/tipos-articulo", MatchPrefixes = ["/almacen/tipos-articulo"], IconCssClass = "bi bi-tags" },
                        new SidebarNavItem { Id = "alm-mant-conceptos-movimiento", Text = "Conceptos de movimiento", NavigateUrl = "/almacen/conceptos-movimiento", MatchPrefixes = ["/almacen/conceptos-movimiento"], IconCssClass = "bi bi-arrow-left-right" },
                        new SidebarNavItem { Id = "alm-mant-categorias-unidad", Text = "Categorías por unidad", NavigateUrl = "/almacen/categorias-unidad", MatchPrefixes = ["/almacen/categorias-unidad"], IconCssClass = "bi bi-diagram-2" },
                        new SidebarNavItem { Id = "alm-mant-unidades", Text = "Unidades de medida", NavigateUrl = "/almacen/unidades-medida", MatchPrefixes = ["/almacen/unidades-medida"], IconCssClass = "bi bi-rulers" },
                        new SidebarNavItem { Id = "alm-mant-isv", Text = "ISV en compras", NavigateUrl = "/almacen/isv-compras", MatchPrefixes = ["/almacen/isv-compras"], IconCssClass = "bi bi-percent" }
                    ]
                }
            ]
        },

        // ===== PROVEEDORES =====
        new SidebarNavSection
        {
            Id = "proveedores",
            Label = "Proveedores",
            Items =
            [
                new SidebarNavItem
                {
                    Id = "prv-proveedores",
                    Text = "Proveedores",
                    IconCssClass = "bi bi-truck",
                    NavigateUrl = "/proveedores",
                    MatchPrefixes = ["/proveedores"]
                },
                new SidebarNavItem
                {
                    Id = "prv-mant",
                    Text = "Mantenimiento",
                    IconCssClass = "bi bi-tools",
                    Children =
                    [
                        new SidebarNavItem { Id = "prv-mant-tipos-proveedor", Text = "Tipos de proveedor", NavigateUrl = "/mantenimientos/tipos-proveedor", MatchPrefixes = ["/mantenimientos/tipos-proveedor"], IconCssClass = "bi bi-tag" },
                        new SidebarNavItem { Id = "prv-mant-tipos-contacto", Text = "Tipos de contacto", NavigateUrl = "/mantenimientos/tipos-contacto", MatchPrefixes = ["/mantenimientos/tipos-contacto"], IconCssClass = "bi bi-person-lines-fill" }
                    ]
                }
            ]
        },

        // ===== BANCOS =====
        // Sección propia: antes era un grupo dentro de Contabilidad, y todo su contenido
        // quedaba a dos clics.
        new SidebarNavSection
        {
            Id = "bancos",
            Label = "Bancos",
            MatchPrefixes = ["/bancos"],
            Items =
            [
                new SidebarNavItem
                {
                    Id = "ban-gestion",
                    Text = "Gestión de bancos",
                    IconCssClass = "bi bi-bank",
                    NavigateUrl = "/contabilidad/bancos",
                    MatchPrefixes = ["/contabilidad/bancos"]
                },
                new SidebarNavItem
                {
                    Id = "ban-cheques",
                    Text = "Cheques emitidos",
                    IconCssClass = "bi bi-card-checklist",
                    NavigateUrl = "/bancos/cheques",
                    // MatchExact: sin esto /bancos/cheques/manual encendería también "Cheques emitidos".
                    MatchExact = true,
                    MatchPrefixes = ["/bancos/cheques"]
                },
                new SidebarNavItem
                {
                    Id = "ban-cheque-manual",
                    Text = "Nuevo cheque manual",
                    IconCssClass = "bi bi-cash-stack",
                    NavigateUrl = "/bancos/cheques/manual",
                    MatchPrefixes = ["/bancos/cheques/manual"],
                    RequiredCapability = SidebarCapabilities.ChequeManual
                },
                new SidebarNavItem
                {
                    Id = "ban-transacciones",
                    Text = "Config. transacciones",
                    IconCssClass = "bi bi-sliders",
                    NavigateUrl = "/bancos/configuracion_transacciones",
                    MatchPrefixes = ["/bancos/configuracion_transacciones"]
                },
                new SidebarNavItem
                {
                    Id = "ban-config",
                    Text = "Configuración",
                    IconCssClass = "bi bi-gear",
                    NavigateUrl = "/bancos/configuracion",
                    MatchPrefixes = ["/bancos/configuracion"]
                }
            ]
        },

        // ===== CONTABILIDAD =====
        // Lo que se consulta a diario sale del grupo de 12; el resto queda en Catálogos.
        new SidebarNavSection
        {
            Id = "contabilidad",
            Label = "Contabilidad",
            MatchPrefixes = ["/contabilidad", "/presupuesto"],
            Items =
            [
                new SidebarNavItem
                {
                    Id = "con-partidas",
                    Text = "Partidas",
                    IconCssClass = "bi bi-file-earmark-check",
                    NavigateUrl = "/contabilidad/partidas",
                    MatchPrefixes = ["/contabilidad/partidas", "/contabilidad/polizas"]
                },
                new SidebarNavItem
                {
                    Id = "con-plan-cuentas",
                    Text = "Plan de cuentas",
                    IconCssClass = "bi bi-diagram-3",
                    NavigateUrl = "/contabilidad/plan-cuentas",
                    MatchPrefixes = ["/contabilidad/plan-cuentas"]
                },
                new SidebarNavItem
                {
                    Id = "con-periodos",
                    Text = "Períodos contables",
                    IconCssClass = "bi bi-calendar",
                    NavigateUrl = "/contabilidad/periodos",
                    MatchPrefixes = ["/contabilidad/periodos"]
                },
                new SidebarNavItem
                {
                    Id = "con-presupuesto",
                    Text = "Presupuesto",
                    IconCssClass = "bi bi-cash-stack",
                    NavigateUrl = "/presupuesto/configuraciones",
                    MatchPrefixes = ["/presupuesto/configuraciones"]
                },
                new SidebarNavItem
                {
                    Id = "con-catalogos",
                    Text = "Catálogos",
                    IconCssClass = "bi bi-journal-text",
                    Children =
                    [
                        // Sin MatchExact a propósito: /contabilidad/empresas/editar/{id} no tiene
                        // entrada propia y así al menos deja encendida la de Empresas. El efecto
                        // conocido es que /nueva, /configuracion e /integracion encienden dos.
                        new SidebarNavItem { Id = "con-cat-empresas", Text = "Empresas", NavigateUrl = "/contabilidad/empresas", MatchPrefixes = ["/contabilidad/empresas"], IconCssClass = "bi bi-buildings" },
                        new SidebarNavItem { Id = "con-cat-crear-empresa", Text = "Crear empresa", NavigateUrl = "/contabilidad/empresas/nueva", MatchPrefixes = ["/contabilidad/empresas/nueva"], IconCssClass = "bi bi-plus-circle" },
                        new SidebarNavItem { Id = "con-cat-config-sistema", Text = "Configuración del sistema", NavigateUrl = "/contabilidad/empresas/configuracion", MatchPrefixes = ["/contabilidad/empresas/configuracion"], IconCssClass = "bi bi-sliders" },
                        new SidebarNavItem { Id = "con-cat-integracion", Text = "Integración contable", NavigateUrl = "/contabilidad/empresas/integracion", MatchPrefixes = ["/contabilidad/empresas/integracion"], IconCssClass = "bi bi-arrow-left-right" },
                        new SidebarNavItem { Id = "con-cat-partidas-facturacion", Text = "Partidas de facturación", NavigateUrl = "/contabilidad/partidas-facturacion", MatchPrefixes = ["/contabilidad/partidas-facturacion"], IconCssClass = "bi bi-journal-plus" },
                        new SidebarNavItem { Id = "con-cat-centros-costo", Text = "Centros de costo", NavigateUrl = "/contabilidad/centros-costo", MatchPrefixes = ["/contabilidad/centros-costo"], IconCssClass = "bi bi-boxes" },
                        new SidebarNavItem { Id = "con-cat-terceros", Text = "Terceros", NavigateUrl = "/contabilidad/terceros", MatchPrefixes = ["/contabilidad/terceros"], IconCssClass = "bi bi-people" },
                        new SidebarNavItem { Id = "con-cat-diarios", Text = "Diarios contables", NavigateUrl = "/contabilidad/diarios", MatchPrefixes = ["/contabilidad/diarios"], IconCssClass = "bi bi-book" },
                        new SidebarNavItem { Id = "con-cat-tipos-comprobante", Text = "Tipos de comprobante", NavigateUrl = "/contabilidad/tipos-transaccion", MatchPrefixes = ["/contabilidad/tipos-transaccion"], IconCssClass = "bi bi-tags" }
                    ]
                }
            ]
        },

        // ===== INFORMES =====
        // Deja de ser un grupo de un solo ítem: sus cinco destinos suben al primer nivel.
        new SidebarNavSection
        {
            Id = "informes",
            Label = "Informes",
            MatchPrefixes = ["/informes"],
            Items =
            [
                new SidebarNavItem
                {
                    Id = "inf-panel",
                    Text = "Panel de informes",
                    IconCssClass = "bi bi-grid-3x3-gap",
                    NavigateUrl = "/informes",
                    // MatchExact: el resto de /informes/* tiene entrada propia.
                    MatchExact = true
                },
                new SidebarNavItem
                {
                    Id = "inf-catalogo",
                    Text = "Catálogo",
                    IconCssClass = "bi bi-collection",
                    NavigateUrl = "/informes/catalogo",
                    MatchPrefixes = ["/informes/catalogo"]
                },
                new SidebarNavItem
                {
                    Id = "inf-partidas",
                    Text = "Partidas contables",
                    IconCssClass = "bi bi-journal-check",
                    NavigateUrl = "/informes/partidas-contabilidad",
                    MatchPrefixes = ["/informes/partidas-contabilidad"]
                },
                new SidebarNavItem
                {
                    Id = "inf-diseno",
                    Text = "Diseño",
                    IconCssClass = "bi bi-bar-chart-line",
                    Children =
                    [
                        new SidebarNavItem { Id = "inf-diseno-web", Text = "Diseño web", NavigateUrl = "/informes/reportes", MatchPrefixes = ["/informes/reportes"], IconCssClass = "bi bi-layout-text-window-reverse" },
                        new SidebarNavItem { Id = "inf-datasets", Text = "Datasets", NavigateUrl = "/informes/datasets", MatchPrefixes = ["/informes/datasets"], IconCssClass = "bi bi-database" }
                    ]
                }
            ]
        },

        // ===== CONFIGURACIÓN =====
        // Absorbe la sección Auditoría (tenía un solo ítem). "Cuenta" y "Cerrar sesión"
        // siguen aquí al final: anclarlos a un pie fijo requiere tocar SidebarNavigation.
        new SidebarNavSection
        {
            Id = "configuracion",
            Label = "Configuración",
            MatchPrefixes = ["/mi-app", "/auditoria", "/Account"],
            Items =
            [
                new SidebarNavItem
                {
                    Id = "cfg-app-usuarios",
                    Text = "Usuarios de la app móvil",
                    IconCssClass = "bi bi-phone",
                    NavigateUrl = "/mi-app/usuarios",
                    MatchPrefixes = ["/mi-app/usuarios"]
                },
                new SidebarNavItem
                {
                    Id = "cfg-app-facturas",
                    Text = "Facturas de la app móvil",
                    IconCssClass = "bi bi-receipt",
                    NavigateUrl = "/mi-app/facturas",
                    MatchPrefixes = ["/mi-app/facturas"]
                },
                new SidebarNavItem
                {
                    Id = "cfg-bitacora-maestros",
                    Text = "Bitácora de maestros",
                    IconCssClass = "bi bi-clock-history",
                    NavigateUrl = "/auditoria/bitacora-maestros",
                    MatchPrefixes = ["/auditoria/bitacora-maestros"]
                },
                new SidebarNavItem
                {
                    Id = "cfg-cuenta",
                    Text = "Cuenta",
                    IconCssClass = "bi bi-person-circle",
                    NavigateUrl = "/Account/Manage",
                    MatchPrefixes = ["/Account/Manage"]
                },
                new SidebarNavItem
                {
                    Id = "cfg-logout",
                    Text = "Cerrar sesión",
                    IconCssClass = "bi bi-box-arrow-right",
                    NavigateUrl = "/Account/Logout",
                    MatchPrefixes = ["/Account/Logout"]
                }
            ]
        },

        // ===== PARÁMETROS (Solo Super Administrador) =====
        // Sigue siendo sección propia: RequiredPolicy sólo existe a nivel de sección, así
        // que fundirla en Configuración perdería el filtro por rol.
        new SidebarNavSection
        {
            Id = "parametros",
            Label = "Parámetros",
            RequiredPolicy = SIAD.Core.Constants.AuthorizationPolicies.SuperAdmin,
            Items =
            [
                new SidebarNavItem
                {
                    Id = "par-branding",
                    Text = "Branding del Portal",
                    IconCssClass = "bi bi-palette",
                    NavigateUrl = "/parametros/branding",
                    MatchPrefixes = ["/parametros/branding"]
                },
                new SidebarNavItem
                {
                    Id = "par-usuarios",
                    Text = "Usuarios",
                    IconCssClass = "bi bi-people-fill",
                    NavigateUrl = "/parametros/usuarios",
                    MatchPrefixes = ["/parametros/usuarios"]
                },
                new SidebarNavItem
                {
                    Id = "par-roles",
                    Text = "Roles y permisos",
                    IconCssClass = "bi bi-shield-check",
                    NavigateUrl = "/parametros/roles",
                    MatchPrefixes = ["/parametros/roles"]
                },
                new SidebarNavItem
                {
                    Id = "par-auditoria-config",
                    Text = "Configuración de auditoría",
                    IconCssClass = "bi bi-sliders",
                    NavigateUrl = "/auditoria/configuracion",
                    MatchPrefixes = ["/auditoria/configuracion"]
                }
            ]
        }
    ];
}
