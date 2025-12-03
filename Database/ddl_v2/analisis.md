El sistema contable-administrativo integrado se divide en seis módulos principales: Administrativo, Ventas, Compras, Inventarios, Contabilidad y Configuración. Cada módulo abarca un área funcional clave del negocio (comercio, servicios e industria) con un conjunto de tablas de base de datos normalizadas para representar sus entidades. A continuación se detalla cada módulo con sus entidades principales (estructuras/tablas), funciones clave, relación con otros módulos y consideraciones de campos específicos a la legislación hondureña (ej. RTN, CAI, impuestos, libros fiscales). La arquitectura sigue buenas prácticas de diseño (separación modular, claves foráneas para relaciones, eliminación de redundancias) y cumple con los requerimientos contables y fiscales de Honduras.

Módulo Administrativo

Entidades principales:

Cuentas bancarias (registro de cuentas de banco de la empresa, p. ej. cuentas corrientes, cajas chicas).

Movimientos de tesorería (operaciones de cobro de clientes y pago a proveedores, vinculadas a facturas por cobrar/pagar).

Otros auxiliares financieros (p. ej. tablas para controlar cuentas por cobrar y por pagar agregadas, si no se derivan directamente de Ventas/Compras).

Funciones clave:

Gestión de cuentas por cobrar: seguimiento de facturas de clientes pendientes, registro de cobros parciales o totales, generación de recibos e informes de antigüedad de saldos.

Gestión de cuentas por pagar: control de obligaciones con proveedores, programación y registro de pagos (emitir órdenes de pago, cheques o transferencias) y control de vencimientos.

Administración de efectivo y bancos: conciliación bancaria (comparar los movimientos registrados con extractos bancarios), transferencias entre cuentas, manejo de caja general y arqueos si aplica.

Balances iniciales y cierres: posibilidad de cargar saldos iniciales de clientes/proveedores al iniciar en el sistema y realizar ajustes administrativos de cierre de periodo (ej. provisiones, ajustes de redondeo).

Relación con otros módulos:

Ventas/Compras: El módulo administrativo toma las facturas generadas en Ventas y Compras para actualizarlas con pagos/cobros realizados, afectando sus estados (de PENDIENTE a PAGADA).

Contabilidad: Cada cobro o pago registrado genera automáticamente asientos contables (ejemplo: pago a proveedor acredita la cuenta bancaria y debita la cuenta por pagar) integrándose al libro diario. Asimismo, las conciliaciones bancarias garantizan que Contabilidad refleje el saldo real de bancos.

Configuración: Utiliza datos básicos definidos en Configuración, como las cuentas bancarias configuradas de la empresa, divisas para pagos, y parámetros como métodos de pago permitidos. También respeta la seguridad y roles definidos (solo usuarios autorizados pueden registrar pagos/cobros).

Campos y consideraciones (Honduras):

Los RTN de clientes y proveedores se utilizan en todos los recibos de cobro y comprobantes de pago para garantizar la identificación fiscal de las partes. El sistema asegura que cada tercero tenga su RTN único registrado, conforme exige el SAR.

Posibilidad de manejar retenciones de impuestos al pagar a proveedores (p. ej. retención de ISV o ISR cuando aplique). Al registrar un pago con retención, el módulo debe calcular el monto retenido y permitir la emisión de la constancia de retención correspondiente, con numeración fiscal si es requerida. Esto garantiza cumplir con las obligaciones de agente de retención
solucionescmc.net
.

Aunque el módulo administrativo no genera documentos fiscales primarios, sí produce reportes de gestión (cuentas por cobrar/pagar) que respaldan las operaciones. Estos reportes deben estar alineados con los libros auxiliares que la empresa debe llevar (no obligatorios por ley para entrega, pero importantes para control interno y auditorías). Por ejemplo, un reporte de Cuentas por Cobrar detallado por cliente complementa al Libro de Ventas oficial.

Módulo de Ventas

Entidades principales:

Clientes (maestro de clientes con datos como nombre, RTN, dirección, límites de crédito, etc.).

Factura de venta (encabezado de la factura emitida a clientes, con campos como número, fecha, cliente, total, impuestos, estado).

Detalle de factura (líneas de productos/servicios por factura, cada una con código de producto, descripción, cantidad, precio, impuesto aplicado, subtotal).

Notas de crédito/débito (documentos de ajuste sobre facturas de venta, para devoluciones o correcciones, vinculados a la factura original).

Recibos de cobro (registro de pagos recibidos de clientes, aplicados a una o varias facturas; puede incluir detalle de forma de pago: efectivo, transferencia, etc.).

Funciones clave:

Ciclo de venta: Emisión de documentos comerciales desde cotizaciones/pedidos (opcional, para registrar pedidos de clientes) hasta facturación. Permite convertir un pedido en factura y aplicar automáticamente los productos y precios acordados.

Facturación fiscal: Generación de facturas fiscales cumpliendo todos los requisitos legales (incluyendo numeración, impuestos, formatos). Al facturar, el sistema descuenta automáticamente las existencias en Inventarios y marca la factura como pendiente de cobro. También permite emitir notas de crédito para anular o reducir facturas (p. ej. por devoluciones de mercancía).

Gestión de clientes y créditos: Consulta de historial de ventas por cliente, gestión de límites de crédito y días de crédito (alertando si un cliente excede su límite antes de facturar). También permite registrar distintos vendedores o representantes asociados a ventas para fines de comisión o seguimiento.

Reportes de ventas: Emisión de reportes como ventas diarias, mensuales, por producto o vendedor, listas de precios, análisis de márgenes, así como el Libro de Ventas fiscal mensual. Estos reportes ayudan a la toma de decisiones comerciales y al cumplimiento fiscal.

Relación con otros módulos:

Inventarios: Cada factura de venta reduce el stock de los productos vendidos en el almacén correspondiente. La integración asegura que no se puedan vender productos sin stock (opcionalmente puede bloquear o alertar) y actualiza los niveles de inventario en tiempo real.

Contabilidad: Al confirmar una factura, el sistema genera un asiento contable automático que registra la cuenta por cobrar (cliente), la venta (ingreso) y el impuesto sobre ventas por pagar. Estas pólizas quedan referenciadas al documento de factura. Las notas de crédito también generan asientos inversos (revirtiendo ingresos/impuestos) y ajustando los saldos de clientes.

Administrativo: El módulo de Ventas provee al Administrativo la información de nuevas cuentas por cobrar. Cuando el módulo Administrativo registra un cobro, esa información retorna a Ventas para marcar la factura como pagada. Existe coherencia entre ambos: Ventas crea la deuda, Administrativo gestiona su cobro.

Configuración: Utiliza parámetros definidos en Configuración, por ejemplo las series de facturación autorizadas para emitir facturas y notas de crédito (cada factura referencia una serie y número predefinido), tipos de impuestos (para calcular ISV, exento o exonerado según el producto) y la información de la empresa/emisor para los formatos.

Campos y consideraciones (Honduras):

Cada Factura incluye todos los datos fiscales requeridos por el SAR: número de factura con formato de 16 dígitos, RTN de la empresa emisora y del cliente, dirección y datos del establecimiento, y especialmente la Clave de Autorización de Impresión (CAI) otorgada por el SAR junto con el rango autorizado y fecha de vencimiento de la serie
comunidad.sube.la
comunidad.sube.la
. Estos campos se configuran previamente y el sistema los inserta automáticamente en cada factura impresa.

El sistema distingue operaciones gravadas, exentas y exoneradas. En el detalle de la factura se identifica qué ítems llevan ISV y cuáles no, para calcular los subtotales por tipo de operación y el Impuesto sobre Ventas (15% ISV) por separado, tal como exige la ley
comunidad.sube.la
. Por ejemplo, si un cliente exonerado realiza una compra, se debe registrar el número de constancia de exoneración y el sistema no cobra ISV a esos ítems.

Soporta la identificación de cliente como “Consumidor Final” en ventas menores al umbral legal, mientras que obliga a ingresar los datos completos del cliente (nombre, RTN) cuando la venta supera los L.10,000, de acuerdo con la normativa
comunidad.sube.la
.

Genera el Libro de Ventas fiscal mensualmente, listando todas las facturas emitidas con campos como fecha, número, nombre/RTN del cliente, base exenta/exonerada, base gravada, ISV y total. Este libro cumple con el formato requerido por el SAR y la legislación hondureña
asesorcontablehn.com
, sirviendo para declaraciones de impuestos y auditorías.

Módulo de Compras

Entidades principales:

Proveedores (maestro de proveedores con datos de identificación fiscal – nombre, RTN –, contactos, condiciones de crédito ofrecidas, etc.).

Órdenes de compra (solicitudes de compra a proveedores, con su detalle de productos/insumos solicitados, cantidades y precios acordados).

Factura de compra (registro de facturas de proveedores recibidas, con cabecera – número de factura del proveedor, fechas, proveedor, totales – y detalle de líneas de productos/servicios adquiridos, sus costos e impuestos).

Detalle de factura de compra (líneas asociadas a cada factura de proveedor, semejante al detalle de ventas: código de producto/servicio, descripción, cantidad, costo unitario, impuestos, subtotal).

Pagos a proveedores (transacciones de pago realizadas a facturas de compra, indicando factura pagada, fecha, monto, forma de pago y referencia, por ejemplo número de cheque o transferencia).

Notas de crédito de proveedor (opcional, documentos cuando un proveedor emite crédito por devolución o descuento; pueden registrarse para reducir el saldo de una factura de compra).

Funciones clave:

Ciclo de compra: Emisión de órdenes de compra a proveedores y seguimiento de su cumplimiento. Permite registrar la recepción de mercancías y servicios; posteriormente, ingreso de la factura del proveedor contra la orden (verificando cantidades y precios según lo ordenado). Puede manejar estados de orden (Borrador, Aprobada, Cerrada, Cancelada) y vincular múltiples facturas parciales a una orden si el proveedor factura en entregas parciales.

Registro de facturas proveedor: Ingreso de las facturas de compra con detalle de conceptos e impuestos. El sistema calcula automáticamente el impuesto acreditable (p. ej. ISV acreditable) y determina el saldo por pagar a cada proveedor. Las facturas quedan en estado pendiente hasta su pago.

Gestión de pagos y obligaciones: Programación de pagos según fechas de vencimiento; al registrar un pago, se actualiza el estado de la factura (si es pago total, a Pagada; si parcial, calcula saldo restante). Soporta pagos múltiples por factura o pago conjunto de varias facturas. También controla las condiciones de crédito otorgadas por proveedores (días crédito, límite) para priorizar pagos.

Control de inventario en compras: Integra con Inventarios para aumentar existencias al recibir mercadería. Si el sistema registra recepciones, cada recepción confirmada incrementa el stock; o si se usa la factura directamente para actualizar existencias, cada línea de producto incrementa el inventario disponible en su almacén.

Reportes y análisis: Emite el Libro de Compras fiscal, además de informes internos como órdenes pendientes, historial de compras por proveedor o producto, comparativo de precios de proveedores, etc. El Libro de Compras detalla cada factura con su fecha, proveedor, RTN, valores gravados/exentos y crédito fiscal ISV, para sustentar el crédito de impuesto en las declaraciones.

Relación con otros módulos:

Inventarios: Al registrar una orden recibida o una factura de compra de mercancías, se incrementan las existencias de esos productos en el almacén correspondiente. El módulo de Inventarios proporciona la lista de productos disponibles y sus datos (códigos, descripciones) para seleccionar en las órdenes/facturas, garantizando consistencia. Además, si se devuelven productos a un proveedor (nota de crédito), se reduce el stock en Inventarios.

Contabilidad: La integración contabiliza automáticamente las compras. Cada factura de proveedor genera un asiento contable reconociendo la cuenta por pagar (pasivo) al proveedor, el gasto o inventario correspondiente (dependiendo si es mercancía o servicio) y el impuesto acreditable (IVA por acreditar) que corresponda. Igualmente, los pagos a proveedores generan pólizas contables (debito a cuentas por pagar, crédito a banco/caja). Estas transacciones quedan identificadas con el documento origen para trazabilidad.

Administrativo: El módulo de Compras alimenta al Administrativo con las cuentas por pagar registradas. La planificación y ejecución de pagos se suele realizar desde Administrativo/Tesorería, pero reflejando la información de facturas de Compras. Cuando se efectúa un pago en Administrativo, se actualiza el estado de la factura en Compras a pagada. Inversamente, Compras provee la lista de obligaciones y sus vencimientos que Tesorería debe gestionar.

Configuración: Utiliza catálogos definidos en Configuración, por ejemplo impuestos (para calcular retenciones o ISV según el tipo de bien/servicio), moneda local/extranjera (para registrar facturas en USD u otras divisas si aplica, usando tasas de cambio configuradas), y series de numeración si se manejan documentos internos como órdenes de compra numeradas. También se apoya en los datos de proveedores base (algunos sistemas podrían compartir el catálogo de terceros entre Ventas/Compras para evitar duplicados, según diseño).

Campos y consideraciones (Honduras):

Cada proveedor debe registrarse con su RTN correcto, ya que al listar el Libro de Compras y al acreditar impuestos es necesario reflejar el RTN del emisor de cada factura fiscal. El sistema valida que no existan proveedores duplicados con el mismo RTN (campo único) para integridad.

El Libro de Compras mensual que produce el sistema cumple los requisitos fiscales hondureños
asesorcontablehn.com
: por cada factura se indica fecha, número (tal como figura en el comprobante del proveedor), nombre y RTN del proveedor, subtotal exento/exonerado, subtotal gravado, ISV pagado (crédito fiscal) y total. Esto facilita la declaración del ISV comprado y el control por el SAR.

Retenciones: Al efectuar pagos, el sistema permite registrar retenciones de impuestos aplicables (por ejemplo, retención del 1% ISR a profesionales independientes, u otras según la ley). Estas retenciones se configuran en el catálogo de impuestos con su porcentaje y se aplican automáticamente si corresponden. Al registrar una retención, deberá generarse un comprobante de retención para entregar al proveedor, con un número de folio autorizado y referencia a la ley aplicable. Esto asegura la documentación adecuada de impuestos retenidos.

Crédito fiscal y CAI del proveedor: Si bien el CAI del proveedor no es obligatorio almacenarlo, es buena práctica guardar el número de documento fiscal del proveedor exactamente como aparece (podría incluir el prefijo autorizado por SAR para ese proveedor). Esto ayuda en auditorías cruzadas. El sistema también puede marcar si una compra proviene de importación, servicios externos u otras condiciones especiales que tengan tratamiento fiscal distinto.

Módulo de Inventarios

Entidades principales:

Productos/Artículos (catálogo de ítems comercializados o utilizados por la empresa, con código único, descripción, unidad de medida, categoría, costo promedio o estándar, estatus activo/inactivo, etc.).

Almacenes/Sucursales (lugares físicos donde se almacena inventario; puede haber una tabla de almacenes vinculada a la empresa y/o a cada sucursal para manejar stock en múltiples ubicaciones).

Existencias/Stock (registro de la cantidad disponible por producto y por almacén. Puede representarse mediante una tabla de movimientos de inventario, donde cada entrada/salida actualiza el saldo, o una tabla acumulada de stock con campos de cantidad y valor actual).

Movimientos de inventario (transacciones individuales de ingreso o egreso de stock: por compras, ventas, devoluciones, ajustes o traslados. Cada movimiento indica producto, fecha, tipo de movimiento – entrada, salida, transferencia –, cantidad, valor unitario para valorización, almacén origen/destino).

Ajustes/Conteos físicos (registros de ajustes manuales al inventario producto de conteos físicos o correcciones, con detalle de diferencia encontrada, motivo del ajuste y autorización).

Funciones clave:

Gestión de productos: Alta y mantenimiento de la lista de productos/servicios. Incluye definir datos generales, clasificaciones (categoría, departamento), unidades de medida y, de ser necesario, códigos de barra. Se evita duplicidad de ítems mediante un código único normalizado.

Control de existencias: Registro en tiempo real de las existencias disponibles. Cada vez que ocurre una compra, venta u otra transacción, el sistema actualiza las cantidades. Permite consultar stock por producto y ubicación, ver reservas (pedidos pendientes de entregar) y puntos de reorden. Se pueden definir niveles mínimos y máximos por producto para generar alertas o sugerir órdenes de compra cuando el stock cae por debajo del mínimo.

Movimientos y trazabilidad: Maneja distintos tipos de movimientos: entradas por compra o devoluciones de clientes, salidas por ventas o devoluciones a proveedores, traspasos entre almacenes, y ajustes por merma o sobrantes. Cada movimiento queda documentado con su referencia (ej. número de factura de compra/venta asociada, número de orden de traslado o número de acta de ajuste). Esto brinda trazabilidad completa de cada unidad de producto (se puede rastrear desde la compra hasta la venta).

Valorización de inventario: Cálculo del costo de las mercancías. El sistema puede soportar métodos de valoración (PEPS/FIFO, costo promedio, UEPS si se usa localmente, etc.) para reflejar el costo del inventario y el costo de ventas. Con cada movimiento, especialmente salidas por venta, se determina el costo de la mercancía vendida para efectos contables.

Reporte de inventarios: Generación de listados como inventario valorizado (cantidad y costo total por ítem), kardex o historial de movimientos por producto, rotación de inventarios, productos próximos a caducar (si aplica), y soporte para el Libro de Inventarios requerido legalmente al cierre del ejercicio. Este libro de inventarios y balances incluiría el listado de bienes, mercancías y su valor, sirviendo de respaldo para los estados financieros
asesorcontablehn.com
.

Relación con otros módulos:

Compras: Cuando el módulo de Compras registra la recepción de un pedido o la entrada de una factura de mercancías, se generan movimientos de entrada en Inventarios aumentando las existencias. Además, la información de costos de compra puede actualizar el costo promedio del producto en el catálogo.

Ventas: Al facturar en Ventas, por cada línea de producto vendido se registra una salida de inventario correspondiente. Inventarios verifica la disponibilidad y, de no haber suficiente stock, podría bloquear la venta o marcar la venta como pendiente de entrega. Las devoluciones de clientes (nota de crédito en Ventas) se reflejan como movimientos de entrada (retorno al stock) en este módulo.

Contabilidad: El módulo de Inventarios proporciona información clave para asientos automáticos de costo de ventas. Si la empresa lleva contabilidad de inventario perpetua, cada salida por venta genera un cargo a costo de ventas y abono a inventario (activo) por el valor en libros del producto vendido. Alternativamente, el sistema permite obtener el valor del inventario final y costo de ventas de forma periódica para contabilizar al cierre de mes. También, los ajustes por diferencia de inventario pueden generar asientos (por ejemplo, pérdida por robo o obsolescencia contra inventario).

Configuración: Usa catálogos base definidos en Configuración, por ejemplo unidades de medida estándar, quizá tipos de productos (mercancía, servicio, activo fijo) que determinen si un ítem controla stock. También aplica parámetros fiscales desde Configuración/Impuestos para saber si un producto está sujeto a ISV o exento, lo cual se refleja en Ventas/Compras.

Administrativo: Interactúa en cuanto a autorizaciones de ajustes y vistas generales de inventario. Por ejemplo, un ajuste de inventario significativo puede requerir aprobación administrativa. Asimismo, la valoración de inventario alimenta a Contabilidad para balances generales (inventario final reflejado en estados financieros), lo cual es revisado por el área administrativa/financiera.

Campos y consideraciones (Honduras):

Para fines fiscales, si bien el Libro de Inventarios es un libro contable más que un registro transaccional diario, el sistema facilita su elaboración. Al cierre del periodo fiscal (generalmente 31 de diciembre), se puede listar todos los activos inventariables con sus cantidades y valores, cumpliendo con la obligación legal de presentar el libro de inventarios y balances
asesorcontablehn.com
.

Los productos deben tener definida su clasificación fiscal: por ejemplo, si están afectos a ISV estándar (15%) o a alguna tasa especial (Honduras aplica 18% a ciertos bienes específicos) o bien si están exentos. Esto permite que al venderlos o comprarlos se aplique el impuesto correcto automáticamente. Dichos parámetros se configuran en el maestro de productos o en grupos de productos.

Si la empresa maneja lotes o series de productos (por trazabilidad, p. ej. en industria farmacéutica), el módulo deberá contar con estructuras adicionales para controlar lotes/fechas de expiración, aunque para efectos contables generales sólo interesa el total. De ser relevante al giro, también se consideraría en Inventarios.

Los ajustes de inventario (por diferencia en conteo físico) deben documentarse internamente con actas, pero no hay un formato fiscal impuesto. Sin embargo, cualquier pérdida de inventario por robo/destrucción debería contabilizarse y podría requerir aviso a autoridades tributarias si son montos materialmente significativos (para justificar deducciones). El sistema puede ayudar registrando dichas pérdidas con sus motivos.

Módulo de Contabilidad

Entidades principales:

Empresas y periodo fiscal (la identificación de la entidad contable - normalmente una empresa o razón social con su RTN - y sus periodos contables; pueden existir tablas para ejercicios anuales y periodos mensuales definidas para controlar aperturas/cierres).

Plan de Cuentas (tabla de catálogo de cuentas contables, estructurada por código y niveles jerárquicos: cuentas de activo, pasivo, capital, ingresos, gastos, etc., con segmentación por niveles para subcuentas. Incluye campos como código, nombre, tipo de cuenta y si admite imputaciones directamente).

Centros de costo (opcional, catálogo de departamentos o proyectos para contabilizar gastos e ingresos segmentados).

Asientos contables (Pólizas): encabezado del asiento con número de póliza, fecha, concepto y detalle de origen (ej. si proviene de Ventas, Compras, etc.), y sus líneas de asiento correspondientes. Cada línea indica cuenta contable, monto débito o crédito, centro de costo si aplica y descripción.

Plantillas contables automáticas (estructura para definir reglas de cómo se generan asientos desde otros módulos: por ejemplo, para facturas de venta, qué cuentas se cargan/abonan. Esto permite que la integración sea flexible sin hardcodear cuentas en código).

Comprobantes fiscales (si se requiere emitir balances generales, estados de resultados u otros informes en formatos específicos, pueden no ser tablas sino reportes formados a partir de los datos contables; sin embargo, se podría considerar entidad para configurar estados financieros).

Funciones clave:

Registro integral de operaciones: Recibir y almacenar todos los asientos contables de la empresa, ya sea generados automáticamente por otros módulos (ventas, compras, pagos, etc.) o introducidos manualmente por contadores (ajustes, provisiones, depreciaciones, reclasificaciones). Se asegura la dualidad débito-crédito y que el plan de cuentas se use consistentemente.

Conciliación y cierre: Permite la conciliación de auxiliares con cuentas de mayor (por ejemplo, que el total de cuentas por cobrar de clientes coincida con la cuenta contable clientes en el mayor general). Ofrece funciones para cerrar periodos contables mensuales (bloqueando modificaciones una vez emitidos estados) y realizar el cierre anual (traspaso de resultados a cuentas de capital, etc.).

Informes financieros: Generación de Estados Financieros básicos y avanzados: Balance General, Estado de Resultados, Estado de Flujo de Efectivo, Estado de Patrimonio, con posibilidad de comparativos vs. periodos anteriores. Además, producción de auxiliares detallados, balanza de comprobación, y los libros contables obligatorios: Libro Diario (lista cronológica de asientos) y Libro Mayor (movimientos por cuenta)
asesorcontablehn.com
asesorcontablehn.com
. Estos informes pueden exportarse a formatos como PDF o Excel para presentar a bancos, auditorías o entidades reguladoras.

Cumplimiento normativo: Configuración para cumplir con NIIF/NIIF para PYMES según aplique en Honduras, garantizando que la clasificación de cuentas y presentación de estados siga estándares internacionales. Posibilidad de llevar contabilización paralela si hubiera diferencias fiscales vs. financieras (por ejemplo, ajustes por impuestos diferidos).

Auditoría y seguridad: Mantenimiento de un historial de cambios en asientos (qué usuario creó/modificó un asiento y cuándo), con controles de acceso para que solo personal autorizado pueda hacer ajustes. Integridad referencial: todas las partidas de diario referencian cuentas válidas del Plan de Cuentas, evitando descuadres.

Relación con otros módulos:

Ventas/Compras/Administrativo/Inventarios: El módulo de Contabilidad es el receptor de la información financiera de los demás módulos. Mediante las plantillas contables configuradas, se generan asientos por cada transacción relevante: facturas de venta (ventas e impuestos por cobrar), facturas de compra (obligaciones e impuestos por acreditar), pagos y cobros (movimiento de efectivo contra cuentas por pagar/cobrar), ajustes de inventario (pérdidas o sobrantes) y así sucesivamente. Cada asiento conserva referencia al documento origen (ID y tipo), facilitando rastrear desde los libros contables hasta la transacción en módulos operativos.

Configuración: Usa información de empresa (por ejemplo el RTN y nombre legal para membretes de reportes) y monedas (para conversión de transacciones en moneda extranjera). También se configuran en conjunto con Configuración las cuentas contables predeterminadas para ciertas operaciones (por ejemplo, cuenta de IVA por pagar asociada al impuesto de venta, cuentas de clientes/proveedores por defecto, etc.). Estas referencias pueden almacenarse en catálogos de Configuración (como el campo ledger_account_code en impuestos) o en la plantilla contable.

Reportes fiscales: La Contabilidad provee los datos para armar los libros fiscales requeridos. Por ejemplo, a partir de los asientos de Ventas se forma el Libro de Ventas (aunque también pueda formarse desde Ventas directamente), y a partir de los asientos se obtiene la sumatoria para declaraciones de impuestos (ISV por pagar, retenciones efectuadas, etc.). Contabilidad interactúa con Administrativo para cuadrar que los auxiliares (ventas, compras, bancos) coincidan con los saldos contables.

Módulo Administrativo (Finanzas): Si existiera un sub-módulo de Activos Fijos o Nomina separado, igualmente enviarían información a Contabilidad (depreciaciones, obligaciones laborales), pero dado el alcance actual, se asume todo flujo financiero centraliza aquí.

Campos y consideraciones (Honduras):

RTN y datos de entidad: La tabla de empresa en contabilidad almacena el RTN y razón social que deben aparecer en los libros legales. Todos los libros contables oficiales impresos (Diario, Mayor, Inventarios y Balances) llevarán el nombre legal de la empresa y su RTN en encabezado, conforme a prácticas locales.

Libros contables obligatorios: El sistema debe facilitar la impresión o exportación del Libro Diario y Libro Mayor oficiales. El Libro Diario listará cronológicamente cada asiento con número de asiento, fecha, cuentas involucradas y montos; el Libro Mayor presentará para cada cuenta su saldo inicial, movimientos de débitos/créditos y saldo final
asesorcontablehn.com
asesorcontablehn.com
. Asimismo, debe generarse el Libro de Inventarios y Balances al cierre del ejercicio, que típicamente incluye el balance general de cierre y la relación de inventarios valorizados. Estos libros pueden necesitar autorización de la SAR o un notario, por lo que el formato no debe ser alterable una vez emitido.

Moneda funcional: En Honduras la moneda funcional es normalmente Lempira (HNL), por lo que aunque el sistema soporte multi-moneda, deberá convertir todas las transacciones a Lempira para efectos contables oficiales. Es importante manejar una tabla de tasas de cambio diarias (por Banco Central) para registrar correctamente las operaciones en dólares u otra divisa y calcular diferencias cambiarias, ya que dichas diferencias deben contabilizarse (ganancia o pérdida cambiaria) según ocurran.

Impuestos y períodos fiscales: Contabilidad deberá reflejar correctamente el tratamiento de impuestos locales. Por ejemplo, el ISV (15%) cobrado en ventas se registra como pasivo fiscal y su pago periódico se contabiliza cuando se declara. Si existen impuestos municipales u otros (ej. impuesto sobre renta adelantado en pagos a cuenta), también se configurarán para su registro. Además, el sistema debe permitir ajustar el calendario fiscal si Honduras modifica fechas de presentación de declaraciones (p. ej. el pago de ISV dentro de los primeros 10 días del mes siguiente
asesorcontablehn.com
), asegurando alertas o cierres oportunos para obtener cifras precisas antes de esas fechas.

Módulo de Configuración

Entidades principales:

Empresa (datos de la compañía o compañías que usarán el sistema: código, razón social, nombre comercial, RTN, dirección, teléfono, correo, etc.).

Sucursales (establecimientos o puntos de emisión pertenecientes a la empresa, con sus direcciones y contactos. Cada sucursal puede tener rangos de factura propios autorizados por SAR).

Usuarios y Roles (catálogo de usuarios del sistema, sus credenciales/identificaciones y asignación de roles/permisos por módulo. Define qué módulos y funciones puede acceder cada rol, asegurando segregación de funciones).

Monedas (monedas soportadas, p. ej. Lempira, Dólar, con su código ISO, símbolo, número de decimales. Se marca la moneda base local y se permiten monedas extranjeras para transacciones multi-divisa).

Impuestos (catálogo de impuestos y tasas: define los impuestos aplicables – ISV u otros – con sus porcentajes y tipo: venta, compra, retención, exento, etc.. Puede incluir identificación de cuentas contables asociadas a cada impuesto para integración contable).

Series de documentos (tabla de numeraciones autorizadas para documentos fiscales: se configura por módulo y tipo de documento el prefijo, el número inicial, número máximo, fecha de expiración y el código de autorización (CAI) otorgado por SAR. Por ejemplo, serie “FAC-001” para facturas de venta de sucursal X con rango 000-001-01-00000001 a 000-001-01-00001000 y fecha límite).

Parámetros generales (opcional, tabla para otras configuraciones globales: ej. formato de número fiscal, porcentaje default de mora por retraso, próximas implementaciones, etc.).

Funciones clave:

Administración de entidades base: Permitir alta y mantenimiento de la información de la empresa y sus sucursales. Aquí se ingresan los datos fiscales de la empresa (RTN, dirección fiscal) que serán usados en todos los documentos. También se crean nuevos usuarios del sistema y se asignan roles o permisos a cada módulo, garantizando control de acceso.

Configuración fiscal del sistema: Ingreso de los impuestos con sus tasas vigentes (ej. 15% ISV general, 18% ISV para ciertos productos, 0% exento, retenciones de ISR/ISV aplicables, etc.). Estos impuestos se parametrizan una sola vez y luego son utilizados por Ventas y Compras al calcular totales. Igualmente, gestión de secuencias y CAI: antes de empezar a facturar, el usuario debe cargar en Configuración las series autorizadas por SAR (con su CAI, rango y vencimiento). El sistema entonces tomará de aquí el próximo número de factura válido cada vez que se emita un documento fiscal
solucionescmc.net
.

Parámetros contables y financieros: Definir elementos como el plan de cuentas inicial (puede importar un catálogo predefinido o crearlo manualmente), asignar cuentas contables predeterminadas a ciertas operaciones (por ejemplo, cuenta contable por defecto para clientes, proveedores, inventario, etc., si no se determinan dinámicamente). También permite configurar monedas y tasas de cambio (posiblemente integrando con un servicio o módulo para actualizar la tasa de cambio automáticamente
solucionescmc.net
).

Personalización y soporte de la normativa local: Adaptar el software a necesidades específicas de Honduras. Por ejemplo, carga de formatos de impresión homologados (diseños de factura con los campos que exige SAR), parámetros para cálculo de décimo cuarto mes u otras particularidades laborales (si extendiese a nómina), etc. Esta sección de Configuración sirve como base para que los módulos operativos funcionen conforme a la ley hondureña y las políticas internas de la empresa.

Mantenimiento de catálogos auxiliares: Crear y editar catálogos compartidos, como listas de bancos (nombres de bancos locales para cuentas bancarias), tipos de pago, formas de entrega, entre otros catálogos menores que utilizan varios módulos. Esto centraliza la información y evita inconsistencias (principio de una sola fuente de la verdad).

Relación con otros módulos:

Módulos operativos (Ventas, Compras, Inventarios): Todos consumen la información configurada aquí. Por ejemplo, Ventas y Compras leen las series de documentos para saber qué numeración y CAI usar en cada factura o nota de crédito. También utilizan las definiciones de impuestos para calcular totales correctamente (referenciando el porcentaje desde Configuración en vez de valores “quemados” en código). Inventarios utiliza las unidades de medida y posiblemente categorías definidas en Configuración.

Contabilidad: Toma del módulo de Configuración la estructura base del plan de cuentas (si se definió inicialmente aquí o importó) y los parámetros de moneda local. Además, si en Configuración se establecen enlaces de impuestos a cuentas contables (como el campo ledger_account_code en cada impuesto), Contabilidad las usa para generar asientos automáticos coherentes. Los cierres de periodos pueden estar protegidos por permisos definidos en Configuración (solo usuarios con rol “Contador” pueden cerrar mes, por ejemplo).

Administrativo (Tesorería): Usa los datos de cuentas bancarias de la empresa definidos en Configuración (banco, número de cuenta, moneda) para registrar pagos y cobros. Asimismo, cualquier nuevo usuario o cambio de permiso en Configuración impacta a qué funciones del módulo Administrativo están disponibles para cierto usuario (por ejemplo, solo el tesorero puede aprobar pagos mayores a cierto monto, etc., según configuración de roles).

Seguridad y auditoría: La Configuración es transversal: define los usuarios que operarán en cada módulo y sus permisos. Todos los demás módulos respetan estas definiciones (ejemplo: si un usuario no tiene permiso de eliminar facturas según su rol, el módulo Ventas hará cumplir esa restricción). También, si se activa un modo multi-compañía, Configuración administra qué datos pertenecen a cada empresa y garantiza aislamiento contable entre ellas.

Campos y consideraciones (Honduras):

En Empresa/Sucursal se almacena el RTN de la empresa y de cada sucursal (si la sucursal tiene un RTN específico, como en caso de sucursales de un grupo inscritas separadamente). Este dato es fundamental ya que debe imprimirse en todos los documentos fiscales emitidos
comunidad.sube.la
. Asimismo, la dirección de la casa matriz o sucursal de emisión, que también es requisito en la factura, se guarda aquí para ser utilizada por el sistema.

La tabla de series de documentos incluye campos para el CAI (código de autorización de impresión) otorgado por el SAR, el rango autorizado (desde-hasta) y la fecha límite de emisión para esa serie
comunidad.sube.la
. El sistema debe impedir emitir documentos fuera de su rango o vencidos, conforme a la regulación. Por ejemplo, si la fecha límite expira, el módulo Ventas ya no debe permitir facturar con esa serie hasta registrar una nueva autorización.

El catálogo de impuestos debe permitir configurar las tasas vigentes de ISV (actualmente 15% estándar, 18% especial) y marcarlos correctamente como IVA trasladado (ventas) o IVA acreditable (compras), etc. También se configuran aquí posibles exenciones especiales (ej. ciertas organizaciones exoneradas) o retenciones que la empresa deba practicar, de forma tal que los módulos de Ventas/Compras las apliquen automáticamente. Mantener estos valores actualizados en Configuración garantiza que cálculos de impuestos en todo el sistema estén alineados con la ley.

Usuarios y control de acceso: Si bien es técnico, es relevante mencionar que mantener usuarios autorizados asegura confidencialidad y confiabilidad en la información contable-fiscal. En el contexto hondureño, solo personal registrado debería emitir facturas fiscales. El sistema podría ligar cada factura con el usuario/vendedor que la emitió (almacenado en un campo de la factura) para trazabilidad, y esos usuarios se definen en Configuración.

Fuente: La estructura descrita está alineada con las obligaciones legales hondureñas vigentes, que requieren llevar libros de ventas, compras, diario, mayor, inventarios y balances correctamente autorizados
asesorcontablehn.com
, así como con los requisitos de facturación bajo el SAR (uso de RTN y CAI en documentos fiscales)
comunidad.sube.la
. Además, sigue lineamientos generales de sistemas ERP integrados y las mejores prácticas de normalización en bases de datos, asegurando integridad referencial entre módulos (por ejemplo, las facturas de venta referencian la tabla de clientes, las pólizas contables referencian las facturas origen, etc., con claves foráneas para mantener consistencia). Esto servirá de guía para el diseño e implementación del sistema contable-administrativo integrado, facilitando su desarrollo y cumplimiento