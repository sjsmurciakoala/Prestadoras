using System.Collections.Generic;
using System.Linq;

namespace SIAD.Core.Constants;

public static class PermissionModules
{
    public const string Ventas = "ventas";
    public const string Bancos = "bancos";
    public const string Compras = "compras";
    public const string Proveedores = "proveedores";
    public const string Inventario = "inventario";
    public const string Contabilidad = "contabilidad";
    public const string Reporteria = "reporteria";
    public const string Configuracion = "configuracion";

    public static readonly string[] All =
    [
        Ventas,
        Bancos,
        Compras,
        Proveedores,
        Inventario,
        Contabilidad,
        Reporteria,
        Configuracion
    ];
}

public static class PermissionResources
{
    public static class Ventas
    {
        public const string Clientes = "clientes";
        public const string CaptacionPagos = "captacion_pagos";
        public const string Cobranza = "cobranza";
        public const string FacturacionMiscelaneos = "facturacion_miscelaneos";
        public const string NotasCreditoDebito = "notas_credito_debito";
        public const string Caja = "caja";

        // Períodos comerciales F7: abrir mes / cerrar ciclo / cerrar mes son
        // operaciones sensibles del calendario de facturación — recurso propio.
        public const string PeriodosComerciales = "periodos_comerciales";

        // Condiciones de lectura (app_lectores 2026-07-06): catálogo por empresa
        // que administra el portal y consume la app; recurso propio.
        public const string CondicionesLectura = "condiciones_lectura";

        // Calendario de facturación (Fase A apertura-ciclo-único 2026-07-14):
        // fechas de lectura/facturación/vencimiento por año/mes/ciclo
        // (calendariopro). Recurso propio: editarlo mueve vencimientos reales.
        public const string CalendarioFacturacion = "calendario_facturacion";
    }

    public static class Contabilidad
    {
        public const string Integracion = "integracion";

        // Recurso propio del posteo por lote (D10: operación sensible —
        // configurar la integración NO debe implicar poder postear lotes).
        public const string LoteFacturacion = "lotefacturacion";

        // Reconciliación del caché oficial de saldos (F6): solo lectura,
        // pero con recurso propio para poder acotarla desde administración.
        public const string Saldos = "saldos";
    }

    public static class Inventario
    {
        // Carga inicial de existencias (2026-07-30): el corte que siembra el
        // inventario y su costo en el kardex.
        //
        // ⚠️ OJO con lo que un sub-recurso NO hace: ModuleAuthorize hace fallback al
        // permiso de MÓDULO, y BuildPolicies añade BuildModulePermission a cada policy.
        // Es decir, este recurso es un SUPERCONJUNTO de module.inventario.*, no una
        // restricción: quien ya tenga el permiso de módulo pasa igual. Sirve para
        // conceder permiso FINO a quien no tiene el de módulo y para que la acción
        // aparezca en la pantalla de roles.
        // Por eso CERRAR y REABRIR el corte NO se protegen con un recurso de inventario
        // sino con [ModuleAuthorize(PermissionModules.Configuracion)] SIN recurso —
        // con recurso volvería a caer en module.configuracion.create.
        public const string CargaInicial = "carga_inicial";

        // Ajustes de inventario: la vía legítima de mover stock una vez cerrada la
        // captura manual de existencia.
        public const string Ajustes = "ajustes";

        // Catálogo de conceptos de movimiento de almacén (2026-08-01): mantenimiento del
        // vocabulario de negocio ("Merma", "Donación"). Recurso propio porque configurar
        // el catálogo es más sensible que capturar con él: quien registra un movimiento
        // no tiene por qué poder inventar conceptos ni cambiarles la cuenta contable.
        // (En el vocabulario del usuario, el campo Entrada/Salida/Valor es el "tipo"; este
        // catálogo es el "concepto". En el código la columna sigue siendo alm_tipo_movimiento.)
        public const string ConceptosMovimiento = "conceptos_movimiento";

        // Documento de movimiento de almacén (2026-08-03): la captura de entradas y
        // salidas manuales. Es el recurso operativo; ConceptosMovimiento es el de configuración.
        public const string Movimientos = "movimientos";

        // Traslado entre bodegas (2026-08-04, Fase 5): documento de dos bodegas con tránsito y
        // recepción parcial. Recurso propio para poder concederlo por separado de los movimientos
        // de entrada/salida.
        public const string Traslados = "traslados";

        // Requisición de materiales (2026-08-04, Fase 6): la solicitud (no mueve inventario).
        // Recurso propio; la aprobación es un permiso aparte dentro de él.
        public const string Requisiciones = "requisiciones";

        // Descargo / entrega de materiales (2026-08-04, Fase 6): la salida real (sí postea).
        public const string Descargos = "descargos";
    }
}

public static class PermissionNames
{
    public static class Ventas
    {
        public const string View = "module.ventas.view";
        public const string Create = "module.ventas.create";
        public const string Edit = "module.ventas.edit";
        public const string Delete = "module.ventas.delete";

        public static class Clientes
        {
            public const string View = "module.ventas.clientes.view";
            public const string Create = "module.ventas.clientes.create";
            public const string Edit = "module.ventas.clientes.edit";
            public const string Delete = "module.ventas.clientes.delete";
            public const string EditarNoCortable = "module.ventas.clientes.no_cortable.edit";
        }

        public static class CaptacionPagos
        {
            public const string View = "module.ventas.captacion_pagos.view";
            public const string Create = "module.ventas.captacion_pagos.create";
            public const string Edit = "module.ventas.captacion_pagos.edit";
            public const string Delete = "module.ventas.captacion_pagos.delete";
        }

        public static class Cobranza
        {
            public const string View = "module.ventas.cobranza.view";
            public const string Create = "module.ventas.cobranza.create";
            public const string Edit = "module.ventas.cobranza.edit";
            public const string Delete = "module.ventas.cobranza.delete";
        }

        public static class FacturacionMiscelaneos
        {
            public const string View = "module.ventas.facturacion_miscelaneos.view";
            public const string Create = "module.ventas.facturacion_miscelaneos.create";
            public const string Edit = "module.ventas.facturacion_miscelaneos.edit";
            public const string Delete = "module.ventas.facturacion_miscelaneos.delete";
        }

        public static class NotasCreditoDebito
        {
            public const string View = "module.ventas.notas_credito_debito.view";
            public const string Create = "module.ventas.notas_credito_debito.create";
            public const string Edit = "module.ventas.notas_credito_debito.edit";
            public const string Delete = "module.ventas.notas_credito_debito.delete";
        }

        public static class Caja
        {
            public const string View = "module.ventas.caja.view";
            public const string Create = "module.ventas.caja.create";
            public const string Edit = "module.ventas.caja.edit";
            public const string Delete = "module.ventas.caja.delete";
            public const string AbonoBanco = "module.ventas.caja.abono.banco";
        }
    }

    public static class Bancos
    {
        public const string View = "module.bancos.view";
        public const string Create = "module.bancos.create";
        public const string Edit = "module.bancos.edit";
        public const string Delete = "module.bancos.delete";
    }

    public static class Compras
    {
        public const string View = "module.compras.view";
        public const string Create = "module.compras.create";
        public const string Edit = "module.compras.edit";
        public const string Delete = "module.compras.delete";
    }

    public static class Proveedores
    {
        public const string View = "module.proveedores.view";
        public const string Create = "module.proveedores.create";
        public const string Edit = "module.proveedores.edit";
        public const string Delete = "module.proveedores.delete";
    }

    public static class Inventario
    {
        public const string View = "module.inventario.view";
        public const string Create = "module.inventario.create";
        public const string Edit = "module.inventario.edit";
        public const string Delete = "module.inventario.delete";

        /// <summary>
        /// Carga inicial de existencias. Son permisos de OPCIÓN (recurso base): cada
        /// endpoint del catálogo genera además su permiso largo
        /// (<c>module.inventario.carga_inicial__almacen_carga_inicial_pendientes.view</c>),
        /// que actúa por encima de estos. Estos cortos se declaran a mano porque el
        /// foreach del catálogo no los crea solo.
        /// </summary>
        public static class CargaInicial
        {
            public const string View = "module.inventario.carga_inicial.view";
            public const string Create = "module.inventario.carga_inicial.create";
            public const string Edit = "module.inventario.carga_inicial.edit";
            public const string Delete = "module.inventario.carga_inicial.delete";
        }

        /// <summary>Ajustes de inventario (entrada / salida / valor).</summary>
        public static class Ajustes
        {
            public const string View = "module.inventario.ajustes.view";
            public const string Create = "module.inventario.ajustes.create";
            public const string Edit = "module.inventario.ajustes.edit";
            public const string Delete = "module.inventario.ajustes.delete";
        }

        /// <summary>
        /// Catálogo de conceptos de movimiento de almacén: el vocabulario de negocio que el
        /// usuario da de alta sin recompilar (equivalente de <c>INV_TIPOSTRANSACC</c> de
        /// Centura). No hay <c>Delete</c>: un concepto no se borra, se desactiva — borrarlo
        /// dejaría huérfano el histórico que lo referencia.
        /// </summary>
        public static class ConceptosMovimiento
        {
            public const string View = "module.inventario.conceptos_movimiento.view";
            public const string Create = "module.inventario.conceptos_movimiento.create";
            public const string Edit = "module.inventario.conceptos_movimiento.edit";
        }

        /// <summary>
        /// Documento de movimiento de almacén (entradas y salidas manuales). No hay
        /// <c>Delete</c>: un movimiento posteado no se borra, se anula con reversa — por eso
        /// la anulación es <c>Edit</c>.
        /// </summary>
        public static class Movimientos
        {
            public const string View = "module.inventario.movimientos.view";
            public const string Create = "module.inventario.movimientos.create";
            public const string Edit = "module.inventario.movimientos.edit";

            /// <summary>
            /// Habilita usar tipos marcados <c>requiere_autorizacion</c> (merma grande,
            /// donación). Es UN permiso para todos los tipos sensibles, no uno por tipo: la
            /// matriz usuario × tipo de Centura (<c>AXL_USUARIOS_TRN</c>) NO se portó porque
            /// la evidencia mostró que dejó de mantenerse.
            /// <para>
            /// Su policy NO admite fallback a <see cref="Create"/> ni al permiso de módulo: si
            /// lo hiciera, cualquiera que pueda capturar podría usar los tipos sensibles y la
            /// bandera no restringiría a nadie.
            /// </para>
            /// </summary>
            public const string AutorizarSensibles = "module.inventario.movimientos.autorizar_sensibles";
        }

        /// <summary>
        /// Traslado entre bodegas (Fase 5). No hay <c>Delete</c>: se anula con reversa (<c>Edit</c>).
        /// <c>Create</c> = enviar; <c>Edit</c> = recibir (recepción parcial) y anular.
        /// </summary>
        public static class Traslados
        {
            public const string View = "module.inventario.traslados.view";
            public const string Create = "module.inventario.traslados.create";
            public const string Edit = "module.inventario.traslados.edit";
        }

        /// <summary>
        /// Requisición de materiales (Fase 6). <c>Create</c> = crear/editar borrador y enviar a
        /// revisión; <c>Edit</c> = anular; <c>Aprobar</c> = aprobar/rechazar (permiso aparte, el
        /// control de aprobación decidido por el usuario: quien tenga el permiso, aprueba).
        /// </summary>
        public static class Requisiciones
        {
            public const string View = "module.inventario.requisiciones.view";
            public const string Create = "module.inventario.requisiciones.create";
            public const string Edit = "module.inventario.requisiciones.edit";
            public const string Aprobar = "module.inventario.requisiciones.aprobar";
        }

        /// <summary>
        /// Descargo / entrega de materiales (Fase 6). <c>Create</c> = entregar (postea la salida);
        /// <c>Edit</c> = anular (reversa). Sin <c>Delete</c>.
        /// </summary>
        public static class Descargos
        {
            public const string View = "module.inventario.descargos.view";
            public const string Create = "module.inventario.descargos.create";
            public const string Edit = "module.inventario.descargos.edit";
        }
    }

    public static class Contabilidad
    {
        public const string View = "module.contabilidad.view";
        public const string Create = "module.contabilidad.create";
        public const string Edit = "module.contabilidad.edit";
        public const string Delete = "module.contabilidad.delete";
    }

    public static class Reporteria
    {
        public const string View = "module.reporteria.view";
        public const string Create = "module.reporteria.create";
        public const string Edit = "module.reporteria.edit";
        public const string Delete = "module.reporteria.delete";
    }

    public static class Configuracion
    {
        public const string View = "module.configuracion.view";
        public const string Create = "module.configuracion.create";
        public const string Edit = "module.configuracion.edit";
        public const string Delete = "module.configuracion.delete";
    }

    public static class Legacy
    {
        public const string Ventas = "module.ventas";
        public const string Bancos = "module.bancos";
        public const string Compras = "module.compras";
        public const string Proveedores = "module.proveedores";
        public const string Inventario = "module.inventario";
        public const string Contabilidad = "module.contabilidad";
        public const string Reporteria = "module.reporteria";
        public const string Configuracion = "module.configuracion";
    }

    public static readonly string[] All = BuildAll();

    private static string[] BuildAll()
    {
        var list = new List<string>
        {
            Ventas.View,
            Ventas.Create,
            Ventas.Edit,
            Ventas.Delete,
            Bancos.View,
            Bancos.Create,
            Bancos.Edit,
            Bancos.Delete,
            Compras.View,
            Compras.Create,
            Compras.Edit,
            Compras.Delete,
            Proveedores.View,
            Proveedores.Create,
            Proveedores.Edit,
            Proveedores.Delete,
            Inventario.View,
            Inventario.Create,
            Inventario.Edit,
            Inventario.Delete,
            Contabilidad.View,
            Contabilidad.Create,
            Contabilidad.Edit,
            Contabilidad.Delete,
            Reporteria.View,
            Reporteria.Create,
            Reporteria.Edit,
            Reporteria.Delete,
            Configuracion.View,
            Configuracion.Create,
            Configuracion.Edit,
            Configuracion.Delete,

            Ventas.Clientes.View,
            Ventas.Clientes.Create,
            Ventas.Clientes.Edit,
            Ventas.Clientes.Delete,
            Ventas.Clientes.EditarNoCortable,
            Ventas.CaptacionPagos.View,
            Ventas.CaptacionPagos.Create,
            Ventas.CaptacionPagos.Edit,
            Ventas.CaptacionPagos.Delete,
            Ventas.Cobranza.View,
            Ventas.Cobranza.Create,
            Ventas.Cobranza.Edit,
            Ventas.Cobranza.Delete,
            Ventas.FacturacionMiscelaneos.View,
            Ventas.FacturacionMiscelaneos.Create,
            Ventas.FacturacionMiscelaneos.Edit,
            Ventas.FacturacionMiscelaneos.Delete,
            Ventas.NotasCreditoDebito.View,
            Ventas.NotasCreditoDebito.Create,
            Ventas.NotasCreditoDebito.Edit,
            Ventas.NotasCreditoDebito.Delete,
            Ventas.Caja.View,
            Ventas.Caja.Create,
            Ventas.Caja.Edit,
            Ventas.Caja.Delete,
            Ventas.Caja.AbonoBanco,

            Inventario.CargaInicial.View,
            Inventario.CargaInicial.Create,
            Inventario.CargaInicial.Edit,
            Inventario.CargaInicial.Delete,
            Inventario.Ajustes.View,
            Inventario.Ajustes.Create,
            Inventario.Ajustes.Edit,
            Inventario.Ajustes.Delete,
            Inventario.ConceptosMovimiento.View,
            Inventario.ConceptosMovimiento.Create,
            Inventario.ConceptosMovimiento.Edit,
            Inventario.Movimientos.View,
            Inventario.Movimientos.Create,
            Inventario.Movimientos.Edit,
            Inventario.Movimientos.AutorizarSensibles,
            Inventario.Traslados.View,
            Inventario.Traslados.Create,
            Inventario.Traslados.Edit,
            Inventario.Requisiciones.View,
            Inventario.Requisiciones.Create,
            Inventario.Requisiciones.Edit,
            Inventario.Requisiciones.Aprobar,
            Inventario.Descargos.View,
            Inventario.Descargos.Create,
            Inventario.Descargos.Edit
        };

        list.AddRange(PermissionEndpointCatalog.All.Select(e => e.Permission));

        return list.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public sealed record PermissionPolicyDefinition(string Policy, string[] Permissions);

    public static readonly PermissionPolicyDefinition[] Policies = BuildPolicies();

    private static PermissionPolicyDefinition[] BuildPolicies()
    {
        var policies = new List<PermissionPolicyDefinition>
        {
        new PermissionPolicyDefinition(Ventas.View, [Ventas.View, Legacy.Ventas]),
        new PermissionPolicyDefinition(Ventas.Create, [Ventas.Create]),
        new PermissionPolicyDefinition(Ventas.Edit, [Ventas.Edit]),
        new PermissionPolicyDefinition(Ventas.Delete, [Ventas.Delete]),
        new PermissionPolicyDefinition(Bancos.View, [Bancos.View, Legacy.Bancos]),
        new PermissionPolicyDefinition(Bancos.Create, [Bancos.Create]),
        new PermissionPolicyDefinition(Bancos.Edit, [Bancos.Edit]),
        new PermissionPolicyDefinition(Bancos.Delete, [Bancos.Delete]),
        new PermissionPolicyDefinition(Compras.View, [Compras.View, Legacy.Compras]),
        new PermissionPolicyDefinition(Compras.Create, [Compras.Create]),
        new PermissionPolicyDefinition(Compras.Edit, [Compras.Edit]),
        new PermissionPolicyDefinition(Compras.Delete, [Compras.Delete]),
        new PermissionPolicyDefinition(Proveedores.View, [Proveedores.View, Legacy.Proveedores]),
        new PermissionPolicyDefinition(Proveedores.Create, [Proveedores.Create]),
        new PermissionPolicyDefinition(Proveedores.Edit, [Proveedores.Edit]),
        new PermissionPolicyDefinition(Proveedores.Delete, [Proveedores.Delete]),
        new PermissionPolicyDefinition(Inventario.View, [Inventario.View, Legacy.Inventario]),
        new PermissionPolicyDefinition(Inventario.Create, [Inventario.Create]),
        new PermissionPolicyDefinition(Inventario.Edit, [Inventario.Edit]),
        new PermissionPolicyDefinition(Inventario.Delete, [Inventario.Delete]),
        new PermissionPolicyDefinition(Contabilidad.View, [Contabilidad.View, Legacy.Contabilidad]),
        new PermissionPolicyDefinition(Contabilidad.Create, [Contabilidad.Create]),
        new PermissionPolicyDefinition(Contabilidad.Edit, [Contabilidad.Edit]),
        new PermissionPolicyDefinition(Contabilidad.Delete, [Contabilidad.Delete]),
        new PermissionPolicyDefinition(Reporteria.View, [Reporteria.View, Legacy.Reporteria]),
        new PermissionPolicyDefinition(Reporteria.Create, [Reporteria.Create]),
        new PermissionPolicyDefinition(Reporteria.Edit, [Reporteria.Edit]),
        new PermissionPolicyDefinition(Reporteria.Delete, [Reporteria.Delete]),
        new PermissionPolicyDefinition(Configuracion.View, [Configuracion.View, Legacy.Configuracion]),
        new PermissionPolicyDefinition(Configuracion.Create, [Configuracion.Create]),
        new PermissionPolicyDefinition(Configuracion.Edit, [Configuracion.Edit]),
        new PermissionPolicyDefinition(Configuracion.Delete, [Configuracion.Delete]),

        new PermissionPolicyDefinition(Ventas.Clientes.View, [Ventas.Clientes.View, Ventas.View, Legacy.Ventas]),
        new PermissionPolicyDefinition(Ventas.Clientes.Create, [Ventas.Clientes.Create, Ventas.Create]),
        new PermissionPolicyDefinition(Ventas.Clientes.Edit, [Ventas.Clientes.Edit, Ventas.Edit]),
        new PermissionPolicyDefinition(Ventas.Clientes.Delete, [Ventas.Clientes.Delete, Ventas.Delete]),
        new PermissionPolicyDefinition(Ventas.Clientes.EditarNoCortable, [Ventas.Clientes.EditarNoCortable]),
        new PermissionPolicyDefinition(Ventas.CaptacionPagos.View, [Ventas.CaptacionPagos.View, Ventas.View, Legacy.Ventas]),
        new PermissionPolicyDefinition(Ventas.CaptacionPagos.Create, [Ventas.CaptacionPagos.Create, Ventas.Create]),
        new PermissionPolicyDefinition(Ventas.CaptacionPagos.Edit, [Ventas.CaptacionPagos.Edit, Ventas.Edit]),
        new PermissionPolicyDefinition(Ventas.CaptacionPagos.Delete, [Ventas.CaptacionPagos.Delete, Ventas.Delete]),
        new PermissionPolicyDefinition(Ventas.Cobranza.View, [Ventas.Cobranza.View, Ventas.View, Legacy.Ventas]),
        new PermissionPolicyDefinition(Ventas.Cobranza.Create, [Ventas.Cobranza.Create, Ventas.Create]),
        new PermissionPolicyDefinition(Ventas.Cobranza.Edit, [Ventas.Cobranza.Edit, Ventas.Edit]),
        new PermissionPolicyDefinition(Ventas.Cobranza.Delete, [Ventas.Cobranza.Delete, Ventas.Delete]),
        new PermissionPolicyDefinition(Ventas.FacturacionMiscelaneos.View, [Ventas.FacturacionMiscelaneos.View, Ventas.View, Legacy.Ventas]),
        new PermissionPolicyDefinition(Ventas.FacturacionMiscelaneos.Create, [Ventas.FacturacionMiscelaneos.Create, Ventas.Create]),
        new PermissionPolicyDefinition(Ventas.FacturacionMiscelaneos.Edit, [Ventas.FacturacionMiscelaneos.Edit, Ventas.Edit]),
        new PermissionPolicyDefinition(Ventas.FacturacionMiscelaneos.Delete, [Ventas.FacturacionMiscelaneos.Delete, Ventas.Delete]),
        new PermissionPolicyDefinition(Ventas.NotasCreditoDebito.View, [Ventas.NotasCreditoDebito.View, Ventas.View, Legacy.Ventas]),
        new PermissionPolicyDefinition(Ventas.NotasCreditoDebito.Create, [Ventas.NotasCreditoDebito.Create, Ventas.Create]),
        new PermissionPolicyDefinition(Ventas.NotasCreditoDebito.Edit, [Ventas.NotasCreditoDebito.Edit, Ventas.Edit]),
        new PermissionPolicyDefinition(Ventas.NotasCreditoDebito.Delete, [Ventas.NotasCreditoDebito.Delete, Ventas.Delete]),
        new PermissionPolicyDefinition(Ventas.Caja.View, [Ventas.Caja.View, Ventas.View, Legacy.Ventas]),
        new PermissionPolicyDefinition(Ventas.Caja.Create, [Ventas.Caja.Create, Ventas.Create]),
        new PermissionPolicyDefinition(Ventas.Caja.Edit, [Ventas.Caja.Edit, Ventas.Edit]),
        new PermissionPolicyDefinition(Ventas.Caja.Delete, [Ventas.Caja.Delete, Ventas.Delete]),
        new PermissionPolicyDefinition(Ventas.Caja.AbonoBanco, [Ventas.Caja.AbonoBanco, Ventas.Caja.Create]),

        // Carga inicial y ajustes de inventario. Mismo patrón que los recursos de Ventas:
        // la policy admite el permiso fino O el de módulo, así que el recurso NO restringe
        // a quien ya tiene module.inventario.* (ver la nota de PermissionResources.Inventario).
        new PermissionPolicyDefinition(Inventario.CargaInicial.View, [Inventario.CargaInicial.View, Inventario.View, Legacy.Inventario]),
        new PermissionPolicyDefinition(Inventario.CargaInicial.Create, [Inventario.CargaInicial.Create, Inventario.Create]),
        new PermissionPolicyDefinition(Inventario.CargaInicial.Edit, [Inventario.CargaInicial.Edit, Inventario.Edit]),
        new PermissionPolicyDefinition(Inventario.CargaInicial.Delete, [Inventario.CargaInicial.Delete, Inventario.Delete]),
        new PermissionPolicyDefinition(Inventario.Ajustes.View, [Inventario.Ajustes.View, Inventario.View, Legacy.Inventario]),
        new PermissionPolicyDefinition(Inventario.Ajustes.Create, [Inventario.Ajustes.Create, Inventario.Create]),
        new PermissionPolicyDefinition(Inventario.Ajustes.Edit, [Inventario.Ajustes.Edit, Inventario.Edit]),
        new PermissionPolicyDefinition(Inventario.Ajustes.Delete, [Inventario.Ajustes.Delete, Inventario.Delete]),
        // Catálogo de conceptos de movimiento. Sin Delete: un concepto se desactiva, no se borra.
        new PermissionPolicyDefinition(Inventario.ConceptosMovimiento.View, [Inventario.ConceptosMovimiento.View, Inventario.View, Legacy.Inventario]),
        new PermissionPolicyDefinition(Inventario.ConceptosMovimiento.Create, [Inventario.ConceptosMovimiento.Create, Inventario.Create]),
        new PermissionPolicyDefinition(Inventario.ConceptosMovimiento.Edit, [Inventario.ConceptosMovimiento.Edit, Inventario.Edit]),
        // Documento de movimiento de almacén. Sin Delete: se anula, no se borra.
        new PermissionPolicyDefinition(Inventario.Movimientos.View, [Inventario.Movimientos.View, Inventario.View, Legacy.Inventario]),
        new PermissionPolicyDefinition(Inventario.Movimientos.Create, [Inventario.Movimientos.Create, Inventario.Create]),
        new PermissionPolicyDefinition(Inventario.Movimientos.Edit, [Inventario.Movimientos.Edit, Inventario.Edit]),
        // SIN fallback a propósito: si admitiera Movimientos.Create o Inventario.Create, quien
        // puede capturar podría usar los tipos sensibles y la bandera no restringiría a nadie.
        new PermissionPolicyDefinition(Inventario.Movimientos.AutorizarSensibles, [Inventario.Movimientos.AutorizarSensibles]),
        // Traslado entre bodegas. Sin Delete: se anula, no se borra.
        new PermissionPolicyDefinition(Inventario.Traslados.View, [Inventario.Traslados.View, Inventario.View, Legacy.Inventario]),
        new PermissionPolicyDefinition(Inventario.Traslados.Create, [Inventario.Traslados.Create, Inventario.Create]),
        new PermissionPolicyDefinition(Inventario.Traslados.Edit, [Inventario.Traslados.Edit, Inventario.Edit]),
        // Requisición de materiales (Fase 6). La solicitud no mueve inventario.
        new PermissionPolicyDefinition(Inventario.Requisiciones.View, [Inventario.Requisiciones.View, Inventario.View, Legacy.Inventario]),
        new PermissionPolicyDefinition(Inventario.Requisiciones.Create, [Inventario.Requisiciones.Create, Inventario.Create]),
        new PermissionPolicyDefinition(Inventario.Requisiciones.Edit, [Inventario.Requisiciones.Edit, Inventario.Edit]),
        // Aprobar: SIN fallback a Create/Inventario.Create — quien captura no aprueba por defecto.
        new PermissionPolicyDefinition(Inventario.Requisiciones.Aprobar, [Inventario.Requisiciones.Aprobar]),
        // Descargo (la salida real). Sin Delete: se anula con reversa.
        new PermissionPolicyDefinition(Inventario.Descargos.View, [Inventario.Descargos.View, Inventario.View, Legacy.Inventario]),
        new PermissionPolicyDefinition(Inventario.Descargos.Create, [Inventario.Descargos.Create, Inventario.Create]),
        new PermissionPolicyDefinition(Inventario.Descargos.Edit, [Inventario.Descargos.Edit, Inventario.Edit])
        };

        foreach (var endpoint in PermissionEndpointCatalog.All)
        {
            var permissions = new List<string>
            {
                endpoint.Permission,
                PermissionKeyBuilder.BuildPermission(endpoint.Module, endpoint.Option, endpoint.Action),
                PermissionKeyBuilder.BuildModulePermission(endpoint.Module, endpoint.Action)
            };

            if (endpoint.Action == PermissionAction.View)
            {
                permissions.Add($"module.{endpoint.Module}");
            }

            var distinct = permissions.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            policies.Add(new PermissionPolicyDefinition(endpoint.Permission, distinct));
        }

        return policies.ToArray();
    }
}
