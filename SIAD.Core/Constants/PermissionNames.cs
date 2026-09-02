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

    // Talento Humano (2026-08-19): catálogo de empleados. Primer consumidor: el combo
    // "Recibe" del Descargo de almacén.
    public const string TalentoHumano = "talentohumano";

    public static readonly string[] All =
    [
        Ventas,
        Bancos,
        Compras,
        Proveedores,
        Inventario,
        Contabilidad,
        Reporteria,
        Configuracion,
        TalentoHumano
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

        // Presupuesto: aprobar compromete fondos, así que no basta con poder
        // editar contabilidad. Sustituye a la policy por rol CanPresupuestoAprobacion.
        public const string Presupuesto = "presupuesto";
    }

    public static class Reporteria
    {
        // SQL libre en el diseñador de informes: consulta arbitraria contra la BD.
        // Sustituye al chequeo por rol Admin de ReportesDatasetsController.
        public const string SqlPersonalizado = "sql_personalizado";
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

    public static class Compras
    {
        // Orden de compra (2026-08-31): recurso propio para separar FIRMAR de editar. Hasta ahora
        // aprobar una O/C solo exigía module.compras.edit, es decir que quien capturaba aprobaba.
        public const string Ordenes = "ordenes";
    }

    public static class Configuracion
    {
        // Catálogo de retenciones a proveedores (2026-08-06, F1): mantenimiento del vocabulario
        // fiscal (concepto + tasas con vigencia) y de la cuenta del pasivo por empresa. Recurso
        // propio para poder concederlo aparte de otras opciones de configuración. Como en
        // Inventario, la policy admite el permiso fino O el de módulo: no restringe a quien ya
        // tiene module.configuracion.*, pero permite concederlo a quien no lo tiene y hace que la
        // opción aparezca en la pantalla de roles.
        public const string Retenciones = "retenciones";

        // Configuración de correo y notificaciones (2026-08-13): conexión SendGrid (API key cifrada)
        // + áreas de notificación (remitente y destinatarios por área). Recurso propio para poder
        // restringir el manejo del secreto aparte del resto de configuración.
        public const string Correo = "correo";

        // Catálogo de formatos fiscales (2026-08-22): máscara y validación del No. de factura (SAR)
        // y del CAI que se transcriben del proveedor. Recurso propio porque cambiar la máscara puede
        // trabar la captura de facturas: no todo usuario de configuración debe poder tocarla.
        public const string FormatosFiscales = "formatos_fiscales";

        // Aprobación por niveles (2026-08-31): interruptor por documento, escalera de montos y
        // quién firma cada nivel. Recurso propio: decide quién autoriza compras y por cuánto.
        public const string Aprobaciones = "aprobaciones";
    }

    public static class Proveedores
    {
        // Registro fiscal de retenciones aplicadas (2026-08-07, F4): consulta del libro hdr/dtl.
        // Recurso propio para concederlo aparte del permiso de módulo (aparece en la pantalla de roles).
        public const string Retenciones = "retenciones";

        // Estado de cuenta del proveedor (2026-08-13): saldo, documentos por pagar y movimientos.
        // Recurso propio porque expone la deuda consolidada del proveedor (compras + compromisos),
        // que no todo usuario del maestro tiene por qué ver.
        public const string EstadoCuenta = "estado_cuenta";

        // Antigüedad de saldos (2026-08-14): aging de CxP de TODOS los proveedores por tramo.
        // Misma familia que estado_cuenta (lee la misma deuda), pero recurso propio: es un reporte
        // gerencial de toda la cartera, no la consulta de un proveedor puntual.
        public const string AntiguedadSaldos = "antiguedad_saldos";

        // Scorecard de proveedores (2026-08-14): calificación por período. Recurso propio porque
        // califica el desempeño del proveedor —un dato sensible para compras— y porque calificar
        // y cerrar períodos debe poder concederse aparte de ver el maestro.
        public const string Evaluacion = "evaluacion";

        // Incidencias de recepción (2026-08-14, F4): alimentan el criterio CALIDAD. Recurso
        // aparte de `evaluacion` porque quien las registra es quien RECIBE la mercadería
        // (almacén), no necesariamente quien califica al proveedor.
        public const string Incidencias = "incidencias";
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

        /// <summary>
        /// Orden de compra (2026-08-31, aprobación por niveles). <c>Aprobar</c> = firmar un nivel,
        /// rechazar y devolver a borrador. Es un permiso <b>aparte</b> de <c>Edit</c>: hasta ahora
        /// aprobar una orden solo exigía editar compras, o sea que quien la capturaba la aprobaba.
        /// <para>
        /// El permiso abre la bandeja y habilita el botón; <b>quién puede firmar qué nivel</b> lo
        /// decide <c>cfg_aprobacion_aprobador</c>, que se configura sin desplegar código.
        /// </para>
        /// </summary>
        public static class Ordenes
        {
            public const string Aprobar = "module.compras.ordenes.aprobar";
        }
    }

    public static class Proveedores
    {
        public const string View = "module.proveedores.view";
        public const string Create = "module.proveedores.create";
        public const string Edit = "module.proveedores.edit";
        public const string Delete = "module.proveedores.delete";

        /// <summary>
        /// Consulta del registro fiscal de retenciones aplicadas (F4). Solo <c>View</c>: es una
        /// consulta; el registro lo escribe el flujo de pago (procesar/abonar), no esta pantalla.
        /// </summary>
        public static class Retenciones
        {
            public const string View = "module.proveedores.retenciones.view";
        }

        /// <summary>
        /// Estado de cuenta del proveedor. Solo <c>View</c>: es una consulta que unifica lo que
        /// ya registran Compras y Compromisos; esta pantalla no crea ni modifica documentos.
        /// </summary>
        public static class EstadoCuenta
        {
            public const string View = "module.proveedores.estado_cuenta.view";
        }

        /// <summary>
        /// Antigüedad de saldos del proveedor (aging de CxP). Solo <c>View</c>: reporte de solo
        /// lectura que reparte por tramos la misma deuda que calcula el estado de cuenta.
        /// </summary>
        public static class AntiguedadSaldos
        {
            public const string View = "module.proveedores.antiguedad_saldos.view";
        }

        /// <summary>
        /// Scorecard de proveedores. <c>View</c> consulta ranking, ficha y reporte; <c>Edit</c>
        /// abre y recalcula períodos, califica los criterios manuales y cierra el período —por eso
        /// son dos permisos y no uno: la mayoría sólo debe poder mirar la calificación.
        /// </summary>
        public static class Evaluacion
        {
            public const string View = "module.proveedores.evaluacion.view";
            public const string Edit = "module.proveedores.evaluacion.edit";
        }

        /// <summary>
        /// Incidencias de recepción (F4). Las registra almacén al recibir, así que la política
        /// también acepta los permisos de inventario: exigir permiso de proveedores dejaría al
        /// bodeguero sin poder anotar la devolución que él mismo detectó.
        /// </summary>
        public static class Incidencias
        {
            public const string View = "module.proveedores.incidencias.view";
            public const string Edit = "module.proveedores.incidencias.edit";
        }
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

        public static class Presupuesto
        {
            /// <summary>Aprobar un presupuesto compromete fondos: permiso aparte de Edit.</summary>
            public const string Aprobar = "module.contabilidad.presupuesto.aprobar";
        }
    }

    public static class Reporteria
    {
        public const string View = "module.reporteria.view";
        public const string Create = "module.reporteria.create";
        public const string Edit = "module.reporteria.edit";
        public const string Delete = "module.reporteria.delete";

        /// <summary>Permite escribir SQL a mano en el diseñador de informes.</summary>
        public const string SqlPersonalizado = "module.reporteria.sql_personalizado.edit";
    }

    /// <summary>
    /// Talento Humano (2026-08-19): catálogo de empleados. Módulo simple sin recurso fino —
    /// es la única entidad del módulo por ahora, igual que Ventas/Bancos/Compras.
    /// </summary>
    public static class TalentoHumano
    {
        public const string View = "module.talentohumano.view";
        public const string Create = "module.talentohumano.create";
        public const string Edit = "module.talentohumano.edit";
        public const string Delete = "module.talentohumano.delete";
    }

    public static class Configuracion
    {
        public const string View = "module.configuracion.view";
        public const string Create = "module.configuracion.create";
        public const string Edit = "module.configuracion.edit";
        public const string Delete = "module.configuracion.delete";

        /// <summary>
        /// Catálogo de retenciones a proveedores (concepto + tasas con vigencia + cuenta del pasivo
        /// por empresa). Sin <c>Delete</c>: una retención no se borra, se desactiva — borrarla
        /// dejaría huérfano lo que la referencie.
        /// </summary>
        public static class Retenciones
        {
            public const string View = "module.configuracion.retenciones.view";
            public const string Create = "module.configuracion.retenciones.create";
            public const string Edit = "module.configuracion.retenciones.edit";
        }

        /// <summary>
        /// Mantenimiento de correo y notificaciones (2026-08-13). Sin <c>Create</c>/<c>Delete</c>:
        /// es un upsert de configuración (la conexión y cada área se crean o actualizan con Edit).
        /// </summary>
        public static class Correo
        {
            public const string View = "module.configuracion.correo.view";
            public const string Edit = "module.configuracion.correo.edit";
        }

        /// <summary>
        /// Configuración de la aprobación por niveles (2026-08-31): el interruptor por documento,
        /// la escalera de montos y quién firma cada nivel. Sin <c>Create</c>/<c>Delete</c>: es un
        /// upsert de configuración, igual que Correo.
        /// <para>
        /// Es la pantalla que decide <b>quién puede autorizar compras y por cuánto</b>, así que
        /// merece recurso propio: no todo usuario de configuración debe poder tocarla.
        /// </para>
        /// </summary>
        public static class Aprobaciones
        {
            public const string View = "module.configuracion.aprobaciones.view";
            public const string Edit = "module.configuracion.aprobaciones.edit";
        }

        /// <summary>
        /// Catálogo de formatos fiscales (2026-08-22): máscara del No. de factura (SAR) y del CAI.
        /// Sin <c>Delete</c>: un formato no se borra, se desactiva — borrarlo dejaría sin explicación
        /// los valores ya guardados con esa máscara.
        /// </summary>
        public static class FormatosFiscales
        {
            public const string View = "module.configuracion.formatos_fiscales.view";
            public const string Create = "module.configuracion.formatos_fiscales.create";
            public const string Edit = "module.configuracion.formatos_fiscales.edit";
        }
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
            Reporteria.SqlPersonalizado,
            Contabilidad.Presupuesto.Aprobar,
            Configuracion.View,
            Configuracion.Create,
            Configuracion.Edit,
            Configuracion.Delete,

            TalentoHumano.View,
            TalentoHumano.Create,
            TalentoHumano.Edit,
            TalentoHumano.Delete,

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
            Compras.Ordenes.Aprobar,
            Inventario.Descargos.View,
            Inventario.Descargos.Create,
            Inventario.Descargos.Edit,

            Configuracion.Retenciones.View,
            Configuracion.Retenciones.Create,
            Configuracion.Retenciones.Edit,

            Configuracion.Correo.View,
            Configuracion.Correo.Edit,
            Configuracion.Aprobaciones.View,
            Configuracion.Aprobaciones.Edit,
            Configuracion.FormatosFiscales.View,
            Configuracion.FormatosFiscales.Create,
            Configuracion.FormatosFiscales.Edit,

            Proveedores.Retenciones.View,

            Proveedores.EstadoCuenta.View,

            Proveedores.AntiguedadSaldos.View,

            Proveedores.Evaluacion.View,
            Proveedores.Evaluacion.Edit,

            Proveedores.Incidencias.View,
            Proveedores.Incidencias.Edit
        };

        list.AddRange(PermissionEndpointCatalog.All.Select(e => e.Permission));

        // Escalón intermedio de la cascada. Cada policy de endpoint admite además el permiso de
        // su OPCIÓN (module.<modulo>.<opcion>.<accion>), pero ese escalón no estaba en el
        // catálogo: se referenciaba y no existía, así que no se podía conceder desde la pantalla
        // de roles y quedaba muerto. Se deriva del catálogo para que no vuelva a faltar.
        list.AddRange(PermissionEndpointCatalog.All.Select(
            e => PermissionKeyBuilder.BuildPermission(e.Module, e.Option, e.Action)));

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

        // Talento Humano: módulo nuevo, sin permiso legacy previo (no hay fallback a Legacy.*).
        new PermissionPolicyDefinition(TalentoHumano.View, [TalentoHumano.View]),
        new PermissionPolicyDefinition(TalentoHumano.Create, [TalentoHumano.Create]),
        new PermissionPolicyDefinition(TalentoHumano.Edit, [TalentoHumano.Edit]),
        new PermissionPolicyDefinition(TalentoHumano.Delete, [TalentoHumano.Delete]),

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

        // Sustituyen a los dos ultimos chequeos por rol del sistema.
        new PermissionPolicyDefinition(Contabilidad.Presupuesto.Aprobar, [Contabilidad.Presupuesto.Aprobar]),
        new PermissionPolicyDefinition(Reporteria.SqlPersonalizado, [Reporteria.SqlPersonalizado]),

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
        // Firmar una orden de compra (2026-08-31). Mismo criterio que la requisición: SIN fallback
        // a Compras.Edit, o el permiso no separaría nada de lo que separa hoy.
        new PermissionPolicyDefinition(Compras.Ordenes.Aprobar, [Compras.Ordenes.Aprobar]),
        // Descargo (la salida real). Sin Delete: se anula con reversa.
        new PermissionPolicyDefinition(Inventario.Descargos.View, [Inventario.Descargos.View, Inventario.View, Legacy.Inventario]),
        new PermissionPolicyDefinition(Inventario.Descargos.Create, [Inventario.Descargos.Create, Inventario.Create]),
        new PermissionPolicyDefinition(Inventario.Descargos.Edit, [Inventario.Descargos.Edit, Inventario.Edit]),

        // Catálogo de retenciones a proveedores (configuracion). Mismo patrón que Inventario: la
        // policy admite el permiso fino O el de módulo, así que el recurso NO restringe a quien ya
        // tiene module.configuracion.* — permite concederlo aparte y aparece en la pantalla de roles.
        new PermissionPolicyDefinition(Configuracion.Retenciones.View, [Configuracion.Retenciones.View, Configuracion.View, Legacy.Configuracion]),
        new PermissionPolicyDefinition(Configuracion.Retenciones.Create, [Configuracion.Retenciones.Create, Configuracion.Create]),
        new PermissionPolicyDefinition(Configuracion.Retenciones.Edit, [Configuracion.Retenciones.Edit, Configuracion.Edit]),

        // Mantenimiento de correo y notificaciones (2026-08-13). Mismo patrón: el permiso fino O el
        // de módulo bastan, así que no restringe a quien ya tiene module.configuracion.*.
        new PermissionPolicyDefinition(Configuracion.Correo.View, [Configuracion.Correo.View, Configuracion.View, Legacy.Configuracion]),
        new PermissionPolicyDefinition(Configuracion.Correo.Edit, [Configuracion.Correo.Edit, Configuracion.Edit]),
        // Configuración de la aprobación por niveles (2026-08-31).
        new PermissionPolicyDefinition(Configuracion.Aprobaciones.View, [Configuracion.Aprobaciones.View, Configuracion.View, Legacy.Configuracion]),
        new PermissionPolicyDefinition(Configuracion.Aprobaciones.Edit, [Configuracion.Aprobaciones.Edit, Configuracion.Edit]),
        new PermissionPolicyDefinition(Configuracion.FormatosFiscales.View, [Configuracion.FormatosFiscales.View, Configuracion.View, Legacy.Configuracion]),
        new PermissionPolicyDefinition(Configuracion.FormatosFiscales.Create, [Configuracion.FormatosFiscales.Create, Configuracion.Create]),
        new PermissionPolicyDefinition(Configuracion.FormatosFiscales.Edit, [Configuracion.FormatosFiscales.Edit, Configuracion.Edit]),

        // Registro fiscal de retenciones aplicadas (F4): consulta bajo el módulo Proveedores. El
        // permiso fino O el de módulo bastan (no restringe a quien ya tiene module.proveedores.*).
        new PermissionPolicyDefinition(Proveedores.Retenciones.View, [Proveedores.Retenciones.View, Proveedores.View, Legacy.Proveedores]),

        // Estado de cuenta del proveedor (2026-08-13). Mismo patrón: el permiso fino O el de
        // módulo bastan, así que no restringe a quien ya tiene module.proveedores.*.
        new PermissionPolicyDefinition(Proveedores.EstadoCuenta.View, [Proveedores.EstadoCuenta.View, Proveedores.View, Legacy.Proveedores]),

        // Antigüedad de saldos (2026-08-14). Mismo patrón que estado de cuenta: el permiso fino O el
        // de módulo bastan.
        new PermissionPolicyDefinition(Proveedores.AntiguedadSaldos.View, [Proveedores.AntiguedadSaldos.View, Proveedores.View, Legacy.Proveedores]),

        // Scorecard de proveedores (F1). Ver: basta con el permiso de módulo, igual que el estado
        // de cuenta. Calificar y cerrar períodos exige el permiso fino o el Edit del módulo: no
        // alcanza con poder ver proveedores.
        new PermissionPolicyDefinition(Proveedores.Evaluacion.View, [Proveedores.Evaluacion.View, Proveedores.View, Legacy.Proveedores]),
        new PermissionPolicyDefinition(Proveedores.Evaluacion.Edit, [Proveedores.Evaluacion.Edit, Proveedores.Edit]),

        // Incidencias de recepción (F4): también las concede inventario, porque quien recibe es
        // quien detecta la devolución o el faltante.
        new PermissionPolicyDefinition(Proveedores.Incidencias.View,
            [Proveedores.Incidencias.View, Proveedores.View, Inventario.View, Legacy.Proveedores]),
        new PermissionPolicyDefinition(Proveedores.Incidencias.Edit,
            [Proveedores.Incidencias.Edit, Proveedores.Edit, Inventario.Edit])
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

        // El permiso de OPCIÓN también necesita su policy: es el escalón que permite conceder
        // una opción completa sin abrir el módulo entero. Se derivan igual que las de endpoint y
        // solo se agregan las que no estén definidas a mano más arriba.
        var yaDefinidas = policies
            .Select(p => p.Policy)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var endpoint in PermissionEndpointCatalog.All)
        {
            var opcion = PermissionKeyBuilder.BuildPermission(
                endpoint.Module, endpoint.Option, endpoint.Action);

            if (!yaDefinidas.Add(opcion))
            {
                continue;
            }

            var permisosOpcion = new List<string>
            {
                opcion,
                PermissionKeyBuilder.BuildModulePermission(endpoint.Module, endpoint.Action)
            };

            if (endpoint.Action == PermissionAction.View)
            {
                permisosOpcion.Add($"module.{endpoint.Module}");
            }

            policies.Add(new PermissionPolicyDefinition(
                opcion, permisosOpcion.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()));
        }

        return policies.ToArray();
    }
}
