# Configuración del tratamiento del ISV en compras — Diseño

Fecha: 2026-07-29 · Revisión: 2 (2026-07-30, tras crítica adversarial) · Rama: `Cambios_almacen2.0` · Estado: **propuesta; bloqueada por las decisiones D0–D11 del contador**

---

## Requerimiento

Hoy no está definido si el **ISV que la empresa paga a sus proveedores** al comprar inventario:

- **entra al costo** del artículo (el material vale L 115 si costó L 100 + 15 %), o
- se registra como **crédito fiscal** recuperable (el material vale L 100 y los L 15 son un derecho contra el SAR), o
- se **reparte** entre ambos (crédito fiscal parcial vía prorrata, cuando la empresa tiene ventas gravadas y exentas mezcladas).

La decisión es del contador, no del programador. Este diseño **no la toma**: construye la pantalla y el modelo para que el contador la declare, la fecha en que empieza a regir y la cuenta contable donde cae, y para que el sistema la respete sin que nadie toque código.

Es el bloqueo declarado del costeo de almacén: `costo_promedio` y `ultimo_costo` de `alm_articulo_bodega` hoy quedan en `0` porque nadie los escribe, y el motor de posteo no puede calcularlos sin saber qué es "costo".

---

## 1. Decisiones del contador (bloquean las fases 1, 2, 3 y 5)

Formato: la columna **Decisión** es la pregunta tal como se le hace al contador; la columna **Impacto** dice qué fase se hace u omite. La propuesta del equipo va entre paréntesis y **no es una conclusión**: nadie del equipo es contador ni fiscalista.

El texto íntegro y redactado para leer en voz alta está en el **Anexo A** al final del documento. Esta tabla es el índice con el impacto técnico de cada respuesta.

| # | Decisión | Impacto |
|---|---|---|
| **D0** | **¿La empresa factura hoy algo gravado con ISV?** Conexiones nuevas, instalaciones domiciliarias, venta de materiales a terceros, servicios de fontanería, alquileres, reconexiones cobradas. ¿Qué porcentaje de la facturación anual del año pasado representó eso? | **Es la pregunta previa a todo.** Si la respuesta es "nada, todo es exento", el modelo binario COSTO/CRÉDITO basta. Si hay ventas gravadas, aunque sean pocas, aparece el **ISV parcialmente acreditable (prorrata)** y el modelo necesita el tercer valor `PRORRATA` con su porcentaje. D1 **no se puede responder sin esto** |
| D1 | La venta de **agua potable y alcantarillado está exenta de ISV** por el Art. 15 de la Ley. Siendo así, y a la luz de lo que se responda en D0, el ISV que la empresa paga a sus proveedores (tubería, cloro, bombas, combustible): ¿lo declara como **crédito fiscal** y lo compensa en el formulario SAR 222, **lo lleva al costo** del material y al gasto, o lo **prorratea** entre ambos según la proporción de ventas gravadas? | Es **la** decisión operativa. Define el valor por defecto de la política, y si las fases 3 (cuenta de crédito fiscal) y 5 (libro de compras) se hacen o se omiten. (Propuesta del equipo, **solo si D0 = "todo exento"**: al costo, por la exención del Art. 15 — a confirmar) |
| **D2** | Si la respuesta a D1 es **crédito fiscal** (o prorrata): **le proponemos abrir la cuenta `11504010102 — ISV crédito fiscal`**, hermana de la que ya existe `11504010101 Pagos a cuentas SAR`, ambas colgando de `11504000000 Impuestos` ([`20260715_plan_cuentas_ersaps_faltantes_company2.sql:357-360`](../../Database/ddl_v3/20260715_plan_cuentas_ersaps_faltantes_company2.sql)). **¿La aprueba, o prefiere otro código/nombre de detalle?** | Define el script de plan de cuentas de la Fase 3 y la fila que se siembra en la matriz de integración. **Se pregunta con una cuenta concreta e imputable, no con códigos de agrupación**: `11504000000` es cuenta padre y la validación de la matriz la rechazaría por `allows_posting = false` |
| D3 | ¿El tratamiento es **el mismo para toda compra**, o cambia según a dónde va lo comprado? Tres casos distintos: material que entra a bodega (**inventario**), consumibles y servicios que se gastan de una vez —combustible, energía, reparaciones— (**gasto**), y compra de maquinaria o vehículos (**activo fijo**) | Define si la política se configura una sola vez por empresa o una vez por destino. (Propuesta: modelar los tres destinos desde el inicio, con la pantalla proponiendo el mismo valor para los tres) |
| D4 | ¿Hay artículos del almacén que **no pagan ISV** (exentos por ley) o que pagan la **tasa selectiva del 18 %** en lugar del 15 %? ¿Quién clasifica cada artículo y con qué criterio? | Define si se construye la clasificación fiscal por artículo (Fase 2) o si todo el almacén se compra a la tasa general |
| D5 | ¿El ISV de la factura de compra se **digita tal como viene** en el documento del proveedor, o el sistema lo **calcula** y el usuario solo confirma? Si difieren por centavos (el proveedor redondea por línea), ¿cuál manda? ¿A partir de qué diferencia quiere que el sistema **bloquee** el registro? | Define la captura en el alta de compra y la tolerancia. (Propuesta cerrada en §7.3: **manda siempre el ISV de la factura**, el sistema propone el calculado y bloquea si la diferencia excede **L 1.00** configurable) |
| D6 | ¿Desde qué **fecha** rige la política que se configure? ¿Aplica también a las compras ya registradas, o solo a las nuevas? Aviso técnico: el kardex es un libro **inmutable**, cambiar el costo de un movimiento ya registrado exige contra-asientos, no se puede "recalcular" | Define `vigencia_desde` del primer registro y si hay o no reproceso histórico. (Propuesta: rige desde la fecha que indique el contador, **sin efecto retroactivo**, pero con la regla de arranque de §9.0 para que no queden documentos viejos sin política) |
| **D7** | **Confirme o corrija** esta tabla de tasas del ISV, que el equipo trae ya investigada (código, tipo, porcentaje, decreto y fecha de vigencia). Hoy el catálogo del sistema trae 15 % general, 18 % selectiva, exento y exonerado, todas con vigencia inventada desde el 01/01/2010 ([`2026-07-14_cfg_impuestos.sql:123-125`](../../Database/2026-07-14_cfg_impuestos.sql)) | Sanea `cfg_impuesto_tasa`. **No es una pregunta abierta**: la investigación de La Gaceta / portal del SAR es tarea previa del equipo dentro de F0; al contador solo se le pide firmar o corregir |
| D8 | ¿La empresa tiene alguna **resolución de exoneración vigente** del SAR (proyectos, convenios, donaciones) que aplique a compras concretas? Si la tiene: ¿el mismo material se compra a veces exonerado y a veces gravado, según el proyecto? | `EXENTO` y `EXONERADO` se declaran en renglones distintos del formulario 222. **Si la respuesta es sí, la clasificación por artículo no alcanza** y hace falta el override por documento de §4.2 — F2 deja de ser opcional |
| D9 | ¿La empresa necesita del sistema el **libro/registro de compras** con el ISV pagado del mes, para respaldar la declaración? | Define si se hace la Fase 5 (`con_libro_iva`, hoy existe la entidad y la tabla pero ningún flujo las escribe) |
| **D10** | Cuando se **devuelve material a un proveedor** o él emite una **nota de crédito**: ¿el ISV se revierte en el mes de la devolución o se rehace la declaración del mes original? ¿La devolución sale de bodega al costo con que entró, o al promedio del momento? | Define la regla de reversión de §9.3 y si hay que rehacer el formulario 222. **Es el flujo que más rompe el promedio ponderado** y hoy no está diseñado en ninguna parte |
| **D11** | Si algún día se cambia de **crédito fiscal a costo**: la cuenta de ISV crédito fiscal queda con un **saldo acumulado que ya no se podrá compensar**. ¿Ese saldo se lleva a gasto del período, se capitaliza contra inventario, o se deja para arrastrar? El inventario que está en bodega ya se costeó **neto** y no puede absorberlo | Define el asiento de reclasificación y el reporte "saldo de ISV crédito fiscal a la fecha de corte" de la Fase 3. Ver §9.4 |

> **D1 es la misma pregunta pendiente en [`docs/plan_retenciones_compromisos_proveedores.md`](../plan_retenciones_compromisos_proveedores.md) (decisión D2 de ese plan).** Conviene llevarlas juntas al contador en una sola sesión: retenciones e ISV comparten base imponible y catálogo.

---

## 2. Decisiones de arquitectura (tomadas por el equipo — no dependen del contador)

| # | Decisión | Elección |
|---|---|---|
| A1 | Dónde vive el catálogo de impuestos y tasas | **Se reutiliza `cfg_impuesto` / `cfg_impuesto_tasa` tal como están.** No se crea catálogo nuevo, no se les agrega ninguna columna |
| A2 | Dónde vive la política (costo vs crédito fiscal vs prorrata) | **Tabla nueva tenant-scoped `cfg_impuesto_politica_compra`.** `cfg_impuesto*` es GLOBAL por diseño explícito ("la ley fija las tasas, no la empresa", [`2026-07-14_cfg_impuestos.sql:13-18`](../../Database/2026-07-14_cfg_impuestos.sql); [`SiadDbContext.Impuestos.cs:6-8`](../../SIAD.Data/SiadDbContext.Impuestos.cs)); meter ahí una política de empresa rompería la premisa del módulo |
| A3 | Dónde vive la cuenta contable del crédito fiscal | **Matriz de integración contable `con_integracion_cuenta`, con un uso nuevo `ISV_CREDITO_FISCAL`.** Es la única estructura del repo que guarda `account_id` con FK real, valida `allows_posting` y es tenant-scoped. **No se reutiliza el uso `ISV` existente**: apunta a un PASIVO (`21105010000` Impuestos por Pagar = ISV cobrado en ventas). **La escritura NO usa `GuardarAsync`** — ver A3.1 |
| **A3.1** | **Cómo se escribe esa fila desde la pantalla de impuestos** | **Método nuevo `UpsertCuentaUsoAsync(companyId, uso, accountId, usuario, ct)`.** Está **prohibido** que la pantalla de impuestos llame a `IIntegracionContableService.GuardarAsync` ([`IIntegracionContableService.cs:20-21`](../../SIAD.Services/Contabilidad/IIntegracionContableService.cs)): ese método recibe la matriz **completa** y **borra toda fila que no venga en el DTO** (`_context.con_integracion_cuentas.RemoveRange(eliminadas)`, [`IntegracionContableService.cs:204-205`](../../SIAD.Services/Contabilidad/IntegracionContableService.cs)). Guardar la cuenta de ISV desde `/mantenimientos/impuestos` con ese método **destruiría CXC, INGRESO, CAJA y el resto de la matriz de la empresa**. Ver §7.3 |
| **A4** | **Con qué fecha se resuelven la tasa y la política** | **Con `alm_compra.fecha_factura`** (fecha de emisión del documento del proveedor), **no con `alm_compra.fecha`** (fecha de recepción/registro). Ambas existen y son nullable ([`alm_compra.cs:21-22`](../../SIAD.Core/Entities/alm_compra.cs)). El SAR ancla el hecho generador a la emisión; el kardex fecha el movimiento por la recepción. Si `fecha_factura` es NULL se cae a `fecha` y **se registra advertencia en el resultado del resolver**. El contrato ya existe: `IImpuestosService.GetTasasVigentesAsync(fecha)` ([`IImpuestosService.cs:39`](../../SIAD.Services/Impuestos/IImpuestosService.cs)) |
| A5 | Qué pasa si no hay política vigente a la fecha | **Fail-closed**: excepción de configuración con mensaje al usuario. Nunca un default silencioso "al costo". Pero con **dos mensajes distintos** y la regla de arranque de §9.0, para no dejar inutilizables los documentos anteriores a la primera política |
| A6 | Retroactividad | La política **no reescribe el pasado**. El kardex es inmutable por trigger (`trg_alm_kardex_inmutable`, SQLSTATE `K0001`, [`2026-07-14_alm_fk_compuestas_tenant.sql:69`](../../Database/2026-07-14_alm_fk_compuestas_tenant.sql)); un cambio de política afecta a los documentos con fecha ≥ `vigencia_desde`. Única excepción: retroceder la `vigencia_desde` de la **primera** política, cuando no hay nada posteado antes (§9.0) |
| A7 | Granularidad de la política | Por **(empresa × impuesto × destino)** con vigencia. **No por artículo**: un mismo tubo no puede ser a la vez acreditable y no acreditable — lo que cambia por artículo es la **tasa**, no la política. Lo que sí puede cambiar por documento es la **tasa** (exoneración por resolución del SAR, D8): eso se resuelve con el override de §4.2, no con la política |
| A8 | Dónde se configura | **Ampliación de la pantalla existente** `/mantenimientos/impuestos`, no pantalla nueva. El contador entra a un solo lugar a ver impuesto, tasas y tratamiento. **Con distintivo de ámbito visible** (§8.1): esa pantalla mezcla un catálogo GLOBAL con una configuración POR EMPRESA |
| **A9** | **Cómo se deriva el destino de una línea de compra** | **De `alm_tipo_articulo.maneja_inventario`** del artículo de la línea ([`alm_tipo_articulo.cs:32`](../../SIAD.Core/Entities/alm_tipo_articulo.cs)): `true` → `INVENTARIO`, `false` → `GASTO`. **La resolución es POR LÍNEA, no por documento**: una misma factura puede traer tubería (inventario) y honorarios (gasto) |
| **A10** | **Vocabulario de la política** | **Los tres valores (`COSTO`, `CREDITO_FISCAL`, `PRORRATA`) van en el CHECK y en la constante desde el día uno**, junto con la columna `porcentaje_acreditable`. Mismo criterio que se aplicó a `destino`: modelarlo cuesta una columna y un CHECK; agregarlo después obliga a migrar filas y a ampliar un CHECK ya en producción. **La aritmética de la prorrata y su recálculo anual solo se implementan si D0 = "hay ventas gravadas"**; mientras tanto la pantalla no ofrece la opción |

### Por qué la política es por empresa y por destino, y no por artículo ni por tipo de artículo

El usuario es una **prestadora de servicios de agua en Honduras**. Su perfil fiscal es homogéneo pero **no necesariamente puro**:

- Casi **todo lo que vende** (agua potable, alcantarillado) es **exento** por el Art. 15.
- Casi **todo lo que compra** (tubería, accesorios, cloro y químicos, bombas, combustible, repuestos) es **gravado al 15 %**.
- **Pero el catálogo comercial ya tiene servicios que no son agua**: `CONEXION` e `INSTALACION` tienen cuenta de ingreso propia (`51301000000`) en la semilla de integración contable ([`20260702_ci_fase1_integracion_config.sql:313-352`](../../Database/ddl_v3/20260702_ci_fase1_integracion_config.sql)). **No consta si esos servicios se facturan gravados o exentos** — eso es exactamente lo que pregunta D0, y de ahí sale si hay o no prorrata.

De ahí que:

1. **Por empresa**: el sistema es multiempresa y cada prestadora puede tener situación fiscal distinta. La política depende de la empresa, no del artículo.
2. **Por destino**: lo que sí distingue la ley y la práctica contable es a dónde va la compra. El ISV de un material que se **capitaliza** en inventario y el de un **gasto** del período no terminan en el mismo lugar aunque la política sea la misma.
3. **Por impuesto**: la tabla se ancla a `cfg_impuesto.id` y no al literal `'ISV'`, para que mañana un impuesto nuevo se configure sin DDL.
4. **No por artículo**: sería la puerta a que dos artículos idénticos comprados el mismo día entren a bodega con bases de costo distintas y el promedio ponderado mezcle peras con manzanas. La dimensión del artículo es la **tasa** (gravado 15 / gravado 18 / exento / exonerado), y esa ya tiene su tabla.

---

## 3. Contexto (hallazgos de la exploración)

- **El módulo de impuestos existe completo y no lo consume nadie.** `cfg_impuesto` + `cfg_impuesto_tasa` ([`Database/2026-07-14_cfg_impuestos.sql`](../../Database/2026-07-14_cfg_impuestos.sql), 157 líneas), entidades, `ImpuestosService` con 13 operaciones, `ImpuestosController` (`api/impuestos`), cliente HTTP y la página `/mantenimientos/impuestos`. Un grep de `cfg_impuesto|IImpuestosService|ImpuestosClient` en todo el repo devuelve solo los archivos del propio módulo más los dos registros de DI. **Es un catálogo huérfano.**
- **Sus invariantes son buenos y hay que apoyarse en ellos**: `EXCLUDE USING gist` que impide vigencias solapadas del mismo código (`ex_cfg_impuesto_tasa_vigencia`, [`2026-07-14_cfg_impuestos.sql:99-105`](../../Database/2026-07-14_cfg_impuestos.sql)), CHECK de coherencia tipo↔porcentaje, y `CambiarTasaAsync` que cierra la vigencia vieja y abre la nueva en una transacción en vez de editar la fila. El histórico de tasas es inmutable por diseño.
- **Ese `EXCLUDE` tiene un defecto latente que este diseño NO debe copiar**: no lleva predicado `WHERE (activo)`, y su espejo en C# `ValidarSolapeAsync` tampoco filtra por `activo` ([`ImpuestosService.cs:515-552`](../../SIAD.Services/Impuestos/ImpuestosService.cs)). Consecuencia: una fila desactivada sigue ocupando su rango de fechas y bloquea la creación de la correcta. Ver §4.1.
- **`cfg_impuesto*` es GLOBAL, sin `company_id`, deliberadamente** ([`SiadDbContext.Impuestos.cs:6-8`](../../SIAD.Data/SiadDbContext.Impuestos.cs)). El propio script anticipó el hueco: *"Lo que SÍ es por empresa es qué tasa lleva cada artículo — eso vivirá en `alm_articulo`"* ([`2026-07-14_cfg_impuestos.sql:17-18`](../../Database/2026-07-14_cfg_impuestos.sql)). Esa columna **no existe**: `alm_articulo` no tiene ninguna referencia a impuesto ni a tasa ([`alm_articulo.cs:13-66`](../../SIAD.Core/Entities/alm_articulo.cs)).
- **El ISV no se aplica en ningún punto del flujo de compras.** Existen `alm_compra.impuesto` y `alm_compra.descuento` ([`alm_compra.cs:29-30`](../../SIAD.Core/Entities/alm_compra.cs), mapeadas con precisión en [`SiadDbContext.Almacen.cs:179-180`](../../SIAD.Data/SiadDbContext.Almacen.cs)), y `alm_requisicion.impuesto` / `impuesto_aplica` ([`SiadDbContext.Almacen.cs:234-237`](../../SIAD.Data/SiadDbContext.Almacen.cs)), heredadas de la migración SIMAFI, pero **ningún servicio C# las escribe ni las lee**. `ComprasService` es de solo consulta y él mismo lo declara ([`ComprasService.cs:7-10`](../../SIAD.Services/Almacen/ComprasService.cs)).
- **No existe noción de crédito fiscal en el repo.** Grep de `credito_fiscal|crédito fiscal|cuenta_isv`: cero implementaciones. Lo único parecido es el uso `ISV` de la matriz de integración, sembrado al **pasivo** `21105010000` "Impuestos por Pagar" — ISV cobrado en ventas, no crédito de compras — y **tampoco lo consume ningún flujo**.
- **El plan regulatorio ERSAPS no tiene dónde poner un crédito fiscal**: cero ocurrencias de "ISV" y de "crédito fiscal" en [`docs/regulatorio/manual_contabilidad_regulatoria.pdf.txt`](../regulatorio/manual_contabilidad_regulatoria.pdf.txt). **Pero el plan REAL de la empresa 2 sí tiene dónde colgarla**: `11504000000 Impuestos` ya tiene una hija imputable de nivel 6, `11504010101 Pagos a cuentas SAR` ([`20260715_plan_cuentas_ersaps_faltantes_company2.sql:357-360`](../../Database/ddl_v3/20260715_plan_cuentas_ersaps_faltantes_company2.sql)). De ahí sale la propuesta concreta de D2.
- **Las cuentas de inventario del plan real son de nivel 6, no de agrupación.** Lo imputable en la empresa 2 es `11401010101 Inv. Tubería y accesorios Agua Potable`, `11401010201 Inv. Tubería y accesorios Alc. Sanitario`, `11401020101 Inv. Producto quimico`, `11409010101 Inventario de materiales electricos`, `11409020101 Herramientas menores y otras` ([`20260715_plan_cuentas_ersaps_faltantes_company2.sql:309-351`](../../Database/ddl_v3/20260715_plan_cuentas_ersaps_faltantes_company2.sql)). **Los códigos de agrupación del manual regulatorio (`1.1.4.01`, `1.1.5.04`) no son posteables** y la validación de la matriz los rechazaría.
- **La matriz de integración contable sí sirve como base**: `con_integracion_cuenta` es tenant-scoped, guarda `account_id` con FK, tiene resolución por especificidad (`fn_con_resolver_cuenta`) y valida `allows_posting` en C# ([`IntegracionContableService.cs:340-509`](../../SIAD.Services/Contabilidad/IntegracionContableService.cs)). Agregar un uso obliga a sincronizar **dos** puntos: la constante `IntegracionContableUsos.Todos` ([`IntegracionContableDtos.cs:26-31`](../../SIAD.Core/DTOs/Contabilidad/IntegracionContableDtos.cs)), **de la que EF deriva el `HasCheckConstraint`** ([`SiadDbContext.IntegracionContable.cs:80-82`](../../SIAD.Data/SiadDbContext.IntegracionContable.cs)), y el CHECK del script SQL, **que es el único que llega a la base** porque el `SiadDbContext` no usa migraciones EF.
- **El vocabulario de módulos de asiento NO incluye almacén ni compras.** `con_integracion_asiento` tiene `CHECK (module IN ('VENTAS','CAJA','BANCOS','NOTAS','MISCELANEOS','PROV'))` ([`20260703_ci_fase2_asientos_config.sql:77-78`](../../Database/ddl_v3/20260703_ci_fase2_asientos_config.sql)), índice único `(company_id, module)`, **no tiene columna `document_type`**, y `IntegracionContableModulos.Todos` tampoco trae almacén ([`IntegracionContableDtos.cs:63-72`](../../SIAD.Core/DTOs/Contabilidad/IntegracionContableDtos.cs)). Cada módulo además tiene su flag `activo_*` en `con_integracion_config` mapeado a mano en `ObtenerActivo`/`AsignarActivo` ([`IntegracionContableService.cs:614-651`](../../SIAD.Services/Contabilidad/IntegracionContableService.cs)). **Postear un asiento de compra de almacén exige ampliar ese vocabulario** — es trabajo de la Fase 6, no un supuesto.
- **Las cuentas de almacén ya existen pero son código suelto y sin validar**: `alm_tipo_articulo.cuenta_inventario` y sus cuatro hermanas son `VARCHAR(20)` **sin FK y sin validación al guardar** ([`2026-07-13_alm_tipo_articulo_cuentas.sql:8-9,14-19`](../../Database/2026-07-13_alm_tipo_articulo_cuentas.sql); `TipoArticuloService` solo normaliza texto), y fueron **sembradas copiando `alm_linea.cuenta_contable` de SIMAFI** ([`2026-07-16_alm_tipo_articulo_seed_desde_lineas.sql:27-41`](../../Database/2026-07-16_alm_tipo_articulo_seed_desde_lineas.sql)) sin comprobar que esos códigos existan ni sean imputables en `con_plan_cuentas`. El artículo, en cambio, **sí** valida su cuenta (existe / imputable / activa, comparando sin separadores con `AccountCodeFormatter.Normalize`) en [`ArticulosService.cs:393-429`](../../SIAD.Services/Almacen/ArticulosService.cs) — ese es el patrón a seguir.
- **`maneja_inventario` existe y hoy está en `true` para los 9 tipos.** El seed del 2026-07-16 borró los 4 tipos genéricos —entre ellos "Servicios", el único con `maneja_inventario = false`— y sembró los 9 grupos todos en `true` ([`2026-07-16_alm_tipo_articulo_seed_desde_lineas.sql:6-13,27-41`](../../Database/2026-07-16_alm_tipo_articulo_seed_desde_lineas.sql)). La columna sigue en la entidad ([`alm_tipo_articulo.cs:32`](../../SIAD.Core/Entities/alm_tipo_articulo.cs)) y **es la que debe derivar el destino** (A9); crear un tipo "sin inventario" es deuda ya identificada del módulo.
- **El motor de posteo de inventario no existe.** Ningún código de producción inserta en `alm_kardex` (solo dos tests). La infraestructura sí está: columnas de trazabilidad, `uuid` determinista, CHECK de `documento_tipo` con `CARGA_INICIAL` reservado, trigger de inmutabilidad. El plan del motor ([`docs/plans/2026-07-14-motor-movimientos-almacen.md`](2026-07-14-motor-movimientos-almacen.md)) define `MovimientoInventarioDto.CostoUnitario` como un decimal de entrada **sin decir cómo se compone**, y declara la contabilidad fuera de alcance. Este diseño define **la componente ISV** de ese costo; fletes, seguros y aranceles siguen siendo un hueco abierto (ver Fuera de alcance).
- **No consta que el catálogo de impuestos esté aplicado en producción**: `2026-07-14_cfg_impuestos.sql` **no figura** en [`Database/2026-07-23_runbook_despliegue_srv.md`](../../Database/2026-07-23_runbook_despliegue_srv.md), y el propio runbook advierte que el estado no se verificó contra el servidor en vivo. Hay que confirmarlo antes de construir encima. El script requiere además la extensión `btree_gist`.

---

## 4. Modelo — qué configura el contador y dónde queda cada cosa

Cinco piezas, tres de ellas ya existentes:

| Qué | Dónde vive | Ámbito | Estado |
|---|---|---|---|
| El impuesto (ISV) y sus **tasas** con vigencia | `cfg_impuesto` / `cfg_impuesto_tasa` | **Global** (lo fija la ley) | **Ya existe.** Solo hay que sanear la semilla (D7) |
| La **política**: al costo / crédito fiscal / prorrata, por destino, con vigencia | `cfg_impuesto_politica_compra` (**tabla nueva**) | **Por empresa** | A construir — Fase 1 |
| La **cuenta** del crédito fiscal | `con_integracion_cuenta`, uso nuevo `ISV_CREDITO_FISCAL` | **Por empresa** | A construir — Fase 3 (solo si D1 ≠ costo puro) |
| La **cuenta de inventario** donde se capitaliza el costo | `alm_tipo_articulo.cuenta_inventario` | Por empresa | ⚠️ **Existe pero SIN VALIDAR — deuda.** Es `VARCHAR` sin FK, sin validación al guardar, sembrada copiando códigos de SIMAFI. **Si el código sembrado no existe o no es imputable en `con_plan_cuentas`, el asiento de §5 apunta a una cuenta inválida y el motor fallará al postear.** Se sanea y se valida en F1 (§7.5) |
| La **clasificación fiscal** de cada artículo (gravado 15 / 18 / exento / exonerado) | `alm_articulo.impuesto_tasa_id` → `alm_tipo_articulo.impuesto_tasa_id`, con override por documento si D8 = sí | Por empresa | A construir — Fase 2 (obligatoria si D8 = sí) |

### 4.1 Tabla nueva `cfg_impuesto_politica_compra`

Tenant-scoped (`ICompanyScopedEntity`). El prefijo `cfg_` marca "configuración"; lo que fija el ámbito es la columna `company_id` y el filtro global del contexto — precedente: `cfg_company`.

| Columna | Tipo | Nota |
|---|---|---|
| `id` | `SERIAL` PK | |
| `company_id` | `BIGINT NOT NULL` | Tenant. `ICompanyScopedEntity`: filtro global + stamping automáticos |
| `impuesto_id` | `INT NOT NULL` FK `cfg_impuesto(id)` RESTRICT | Se ancla al catálogo, no al literal `'ISV'` |
| `destino` | `VARCHAR(12) NOT NULL` | CHECK ∈ `INVENTARIO` \| `GASTO` \| `ACTIVO_FIJO` (D3) |
| `politica` | `VARCHAR(16) NOT NULL` | CHECK ∈ `COSTO` \| `CREDITO_FISCAL` \| `PRORRATA` (D0/D1) |
| **`porcentaje_acreditable`** | `NUMERIC(5,2) NULL` | **Solo y obligatoriamente cuando `politica = 'PRORRATA'`.** Porcentaje del ISV pagado que es acreditable; el resto va al costo. Rango 0–100 |
| `vigencia_desde` | `DATE NOT NULL` | Se resuelve por **fecha de la factura del proveedor** (A4, D6) |
| `vigencia_hasta` | `DATE NULL` | `NULL` = vigente indefinidamente |
| `base_legal` | `VARCHAR(250) NULL` | Artículo, decreto o resolución que la sustenta — lo escribe el contador |
| `activo` | `BOOLEAN NOT NULL DEFAULT true` | Soft-delete |
| auditoría | `usuariocreacion`, `fechacreacion`, `usuariomodificacion`, `fechamodificacion` | Mismo bloque que `cfg_impuesto` |

Constraints:

- `ck_cfg_imp_pol_compra_destino` — `destino IN ('INVENTARIO','GASTO','ACTIVO_FIJO')`.
- `ck_cfg_imp_pol_compra_politica` — `politica IN ('COSTO','CREDITO_FISCAL','PRORRATA')`.
- **`ck_cfg_imp_pol_compra_prorrata`** — `(politica = 'PRORRATA') = (porcentaje_acreditable IS NOT NULL)` **AND** `(porcentaje_acreditable IS NULL OR porcentaje_acreditable BETWEEN 0 AND 100)`. Es el espejo de la coherencia tipo↔porcentaje que ya usa `cfg_impuesto_tasa`: el porcentaje existe si y solo si la política es prorrata.
- `ck_cfg_imp_pol_compra_vigencia` — `vigencia_hasta IS NULL OR vigencia_hasta >= vigencia_desde`.
- **`ex_cfg_imp_pol_compra_vig`** — el invariante central:

  ```sql
  EXCLUDE USING gist (
      company_id  WITH =,
      impuesto_id WITH =,
      destino     WITH =,
      daterange(vigencia_desde, COALESCE(vigencia_hasta,'infinity'::date), '[]') WITH &&
  ) WHERE (activo)
  ```

  Nunca puede haber dos políticas **activas** del mismo impuesto y destino pisándose en el tiempo, o "¿al costo o crédito el 3 de marzo?" deja de tener respuesta única. **El predicado `WHERE (activo)` es obligatorio y es la diferencia con `ex_cfg_impuesto_tasa_vigencia`**: esa tabla no lo lleva y por eso arrastra un defecto latente. Aquí `activo` se vende como funcionalidad (`DeactivatePoliticaCompraAsync`, botón en pantalla), así que **sin el predicado el flujo de corrección más obvio quedaría sin salida**: el contador configura mal una política abierta (COSTO desde 01/01/2026, `vigencia_hasta` NULL), la desactiva, intenta crear la correcta para el mismo rango y la BD la rechaza con `23P01` porque la fila inactiva sigue ocupando el `daterange`. Con el predicado, desactivar y recrear funciona. El `COMMENT ON CONSTRAINT` debe explicar exactamente esto.
- **Sin índice adicional.** El propio `EXCLUDE USING gist` crea el índice sobre `(company_id, impuesto_id, destino, daterange)` que sirve para la consulta del resolver. Un índice B-tree parcial extra sería una estructura más que mantener sin beneficio medible en una tabla que tendrá unidades de filas por empresa — a ese tamaño el planificador hará seq scan de todas formas.

**La cuenta contable no es columna de esta tabla** (decisión A3): vive en `con_integracion_cuenta` bajo el uso `ISV_CREDITO_FISCAL`, con FK real a `con_plan_cuentas` y validación de `allows_posting`. La pantalla la muestra y la edita **delegando en el método `UpsertCuentaUsoAsync` de A3.1 — nunca en `GuardarAsync`**; el contador no percibe que son dos tablas.

**El espejo en C# también filtra por `activo`.** `ValidarSolapeAsync` de la política debe llevar `.Where(p => p.activo)`, cosa que su análogo de tasas no hace ([`ImpuestosService.cs:515-552`](../../SIAD.Services/Impuestos/ImpuestosService.cs)). Si el espejo no filtra y la BD sí, el servicio rechazaría con mensaje humano un alta que la base habría aceptado.

### 4.2 Clasificación fiscal por artículo y override por documento (Fase 2)

- `alm_articulo.impuesto_tasa_id INT NULL` FK `cfg_impuesto_tasa(id)` RESTRICT — la columna que el script de impuestos anticipó y nunca se creó.
- `alm_tipo_articulo.impuesto_tasa_id INT NULL` FK igual — **valor por defecto por tipo**, para no clasificar 5.000 artículos a mano.
- **Override por documento (condicionado a D8):** una exoneración del SAR se otorga **sobre una compra o un proyecto concreto, no sobre el artículo**. El mismo tubo se compra exonerado dentro del convenio y gravado fuera de él. Por eso la cadena tiene un nivel más arriba del artículo:

  **`tasa del documento (override manual + número de resolución del SAR)` → `artículo` → `tipo de artículo` → error de configuración** (fail-closed, A5).

  El override se captura en la línea de compra junto con el campo `resolucion_exoneracion` (texto), que después alimenta el renglón de EXONERADO del formulario 222 (F5).

- **Si D8 = sí, la Fase 2 NO se puede omitir** y el override es obligatorio; la clasificación por artículo sola no sirve. **Si D8 = no**, se declara `EXONERADO` fuera de alcance de esta iteración y se dice en §14.
- No hay cuarto nivel ni default implícito al 15 %.

### 4.3 Compras de gasto vs compras de inventario — el destino se deriva POR LÍNEA

Son caminos distintos y el diseño los separa explícitamente:

- **Línea de inventario** (el tipo del artículo tiene `maneja_inventario = true`; entra a bodega, hay kardex): el ISV, según la política, **se capitaliza en el costo unitario** que recibe el motor de posteo, **se separa** y el costo entra neto, o **se reparte** (prorrata).
- **Línea de gasto** (el tipo del artículo tiene `maneja_inventario = false`, o la línea no lleva artículo — combustible consumido, servicios, reparaciones): no hay costo que capitalizar. Con política `COSTO`, el ISV **engrosa la cuenta de gasto**; con `CREDITO_FISCAL`, va a la cuenta de crédito fiscal; con `PRORRATA`, se reparte.
- **Línea de activo fijo**: mismo razonamiento que inventario, pero el destino del débito es la cuenta del activo. Se modela el destino aunque el módulo de activo fijo no consuma la política todavía.

**Cómo se determina el destino (A9):** de `alm_tipo_articulo.maneja_inventario` del artículo de la línea, **no** de la mera presencia de líneas de artículo en el documento. Decir "una compra con líneas de artículo es INVENTARIO" sería falso en el propio modelo de almacén: existen —y volverán a existir, es deuda declarada del módulo— tipos de artículo que no manejan existencia, y a esos les correspondería `GASTO`. **Una misma factura puede mezclar ambos destinos**, así que el resolver se invoca **por línea**, no por documento. `ACTIVO_FIJO` no se deriva automáticamente: lo marca el usuario en la línea (o lo pone el módulo de activo fijo cuando exista).

Mientras el módulo de compras no exista, solo `INVENTARIO` tiene consumidor real.

---

## 5. Efecto de cada política — ejemplos numéricos

> ⚠️ **Los asientos de esta sección son ILUSTRATIVOS: muestran el efecto contable esperado, NO una configuración que hoy exista.** `con_integracion_asiento` no tiene módulo de almacén ni de compras (su CHECK admite solo `VENTAS, CAJA, BANCOS, NOTAS, MISCELANEOS, PROV`, [`20260703_ci_fase2_asientos_config.sql:77-78`](../../Database/ddl_v3/20260703_ci_fase2_asientos_config.sql)) y **no tiene columna `document_type`**. Postear estos asientos exige antes el trabajo de la **Fase 6** (ampliar el vocabulario). Lo que sí queda definido y consumible en la Fase 1 es **el costo unitario y el desglose del impuesto**, no la partida.

**Compra:** 10 tubos de PVC 4" a L 100.00 c/u = **L 1,000.00** + **ISV 15 % = L 150.00** → total factura **L 1,150.00**. Sin descuento. Bodega con existencia previa 0. Artículo del tipo cuyo `cuenta_inventario` resuelve a `11401010101 Inv. Tubería y accesorios Agua Potable` (cuenta imputable real del plan de la empresa 2).

### Política A — AL COSTO (el ISV no es recuperable)

**Lo que entra al inventario**

| Dato | Valor |
|---|---|
| Costo unitario que recibe el motor | **L 115.0000** (1,150 ÷ 10) |
| `alm_kardex.ingresos` / `valor_unitario` / `total` | 10.00 / 115.0000 / 1,150.0000 |
| `alm_articulo_bodega.costo_promedio` / `ultimo_costo` | **115.0000** / 115.0000 |
| Valor del inventario tras la compra | **L 1,150.00** |

**Vista previa contable** (ilustrativa — ver aviso arriba)

| Cuenta | Debe | Haber |
|---|---:|---:|
| `11401010101` Inv. Tubería y accesorios Agua Potable — la que traiga `alm_tipo_articulo.cuenta_inventario` del tipo del artículo | 1,150.00 | |
| Cuentas por pagar proveedores | | 1,150.00 |

### Política B — CRÉDITO FISCAL (el ISV es recuperable)

**Lo que entra al inventario**

| Dato | Valor |
|---|---|
| Costo unitario que recibe el motor | **L 100.0000** (1,000 ÷ 10) |
| `alm_kardex.ingresos` / `valor_unitario` / `total` | 10.00 / 100.0000 / 1,000.0000 |
| `alm_articulo_bodega.costo_promedio` / `ultimo_costo` | **100.0000** / 100.0000 |
| Valor del inventario tras la compra | **L 1,000.00** |

**Vista previa contable** (ilustrativa)

| Cuenta | Debe | Haber |
|---|---:|---:|
| `11401010101` Inv. Tubería y accesorios Agua Potable | 1,000.00 | |
| `11504010102` ISV crédito fiscal — cuenta **propuesta** en **D2** (activo, imputable, hermana de `11504010101 Pagos a cuentas SAR`) | 150.00 | |
| Cuentas por pagar proveedores | | 1,150.00 |

### Política C — PRORRATA (crédito fiscal parcial, solo si D0 = hay ventas gravadas)

Con `porcentaje_acreditable = 20 %`: de los L 150.00 de ISV, L 30.00 son crédito y L 120.00 van al costo.

| Dato | Valor |
|---|---|
| Costo unitario que recibe el motor | **L 112.0000** ((1,000 + 120) ÷ 10) |
| ISV acreditable | L 30.00 |
| ISV capitalizado en el costo | L 120.00 |
| Valor del inventario tras la compra | **L 1,120.00** |

**Consecuencia que hay que decirle al contador:** bajo prorrata el costo unitario del inventario depende de un porcentaje **que se ajusta a fin de año** cuando se conoce la proporción real de ventas gravadas. Ese ajuste **no reprocesa el kardex** (es inmutable): se resuelve con un **asiento de ajuste** por la diferencia, y el promedio ponderado del artículo conserva el porcentaje provisional con el que se costeó. Es una limitación estructural, no un defecto.

### Ejemplo con descuento (la base imponible no es el precio bruto)

**Compra:** 10 tubos a L 100.00 = L 1,000.00 bruto, **descuento L 100.00** → base imponible **L 900.00**, ISV 15 % = **L 135.00**, total factura **L 1,035.00**.

| | Base | ISV | Costo unitario (COSTO) | Costo unitario (CRÉDITO) |
|---|---:|---:|---:|---:|
| Con descuento | 900.00 | 135.00 | **103.5000** | **90.0000** |

La base imponible del ISV en Honduras es **precio menos descuentos**, y `alm_compra.descuento` ya existe en el modelo ([`alm_compra.cs:30`](../../SIAD.Core/Entities/alm_compra.cs), [`SiadDbContext.Almacen.cs:180`](../../SIAD.Data/SiadDbContext.Almacen.cs)). Por eso la firma del resolver recibe **bruto y descuento por separado** (§7.4) y no un `montoBase` ya calculado por el llamador: si cada llamador lo calcula a su manera, la base del impuesto deja de ser reproducible.

### El efecto no se agota en la compra: se arrastra a cada salida

Descargo posterior de **4 tubos** (ejemplo sin descuento), valorizado al promedio (las salidas nunca alteran el promedio, solo lo consumen):

| | Política A (al costo) | Política B (crédito fiscal) | Diferencia |
|---|---:|---:|---:|
| Costo del descargo (al gasto del período) | **L 460.00** | **L 400.00** | L 60.00 |
| Inventario remanente (6 unidades) | **L 690.00** | **L 600.00** | L 90.00 |

Y en una **línea de gasto** (combustible por L 1,000 + L 150 de ISV, artículo de tipo sin inventario o sin artículo):

| | Debe | Haber |
|---|---|---|
| **A — al costo** | Gasto combustibles y lubricantes **1,150.00** | CxP 1,150.00 |
| **B — crédito fiscal** | Gasto combustibles y lubricantes 1,000.00 + ISV crédito fiscal 150.00 | CxP 1,150.00 |

**Conclusión operativa:** bajo la política B (y bajo C), el sistema **debe** tener una cuenta de activo donde acumular el ISV pagado y, tarde o temprano, un libro de compras que lo respalde ante el SAR (D9). Bajo la política A no se necesita ninguna cuenta nueva y el ISV desaparece dentro del costo — más simple, pero **irreversible en el histórico**: los costos ya posteados no se pueden reabrir sin contra-asientos.

### Advertencia sobre la base de costo mezclada

La carga inicial del inventario se costeará con `alm_articulo.valor_unitario`, **cuya base fiscal nadie conoce** (viene de SIMAFI: no consta si esos valores incluyen ISV o no). Si la apertura entra con una base y las compras posteriores con otra, el promedio ponderado móvil mezcla dos bases distintas y queda **silenciosamente mal** — no hay forma de detectarlo después. La respuesta a D1 debe fijar también la base de la apertura; queda anotado como dependencia dura del diseño de carga inicial.

---

## 6. Base de datos — `Database/2026-07-29_isv_compras_politica.sql` (aditivo)

Un solo script, aditivo, **no toca ninguna tabla existente** salvo el uso nuevo de la matriz:

1. `CREATE EXTENSION IF NOT EXISTS btree_gist;` (idempotente; ya la exige el script de impuestos).
2. `CREATE TABLE IF NOT EXISTS cfg_impuesto_politica_compra (...)` con los CHECK (incluido `ck_cfg_imp_pol_compra_prorrata`) y el `EXCLUDE ... WHERE (activo)` de §4.1. **Sin índice B-tree adicional** (el gist del EXCLUDE ya cubre la consulta del resolver).
3. `COMMENT ON TABLE` / `COMMENT ON COLUMN` / `COMMENT ON CONSTRAINT` explicando **por qué** existe cada invariante (estilo del script de impuestos). El comentario del `EXCLUDE` debe decir explícitamente **por qué lleva `WHERE (activo)`**: para que desactivar una política mal configurada y recrearla en el mismo rango de fechas funcione.
4. Ampliación del CHECK de la matriz: `ALTER TABLE con_integracion_cuenta DROP CONSTRAINT IF EXISTS ck_con_integracion_cuenta_uso;` + `ADD CONSTRAINT ... CHECK (uso IN (..., 'ISV_CREDITO_FISCAL'))` con la lista completa. **Debe quedar idéntico** a lo que EF reconstruye desde `IntegracionContableUsos.Todos` ([`SiadDbContext.IntegracionContable.cs:80-82`](../../SIAD.Data/SiadDbContext.IntegracionContable.cs)).
5. **Sin semilla de política.** Sembrar `COSTO` o `CREDITO_FISCAL` por defecto sería tomar la decisión del contador por él. La tabla nace vacía y el sistema falla cerrado hasta que él configure (A5).

Estilo obligatorio del script: encabezado con **Fecha** + **Regla DB Mirror** (`aplicar también en siad_v3_restore @localhost`), bloque **POR QUÉ**, idempotencia (`IF NOT EXISTS` / `ON CONFLICT`), `BEGIN … COMMIT`, y bloque **VERIFICACIÓN** comentado al final. La verificación debe incluir **tres pruebas**:

- el `EXCLUDE` rechaza dos políticas activas solapadas (debe FALLAR con `23P01`);
- desactivar una política y crear otra con el mismo rango **debe FUNCIONAR** (es la prueba del predicado `WHERE (activo)`);
- `politica = 'PRORRATA'` sin `porcentaje_acreditable` debe FALLAR.

Pasa por la skill **guardia-estructura-bd** (tarjeta verde, aditivo) y se registra en el runbook SRV vigente vía la skill **runbook-despliegue-srv**. **El usuario lo aplica**: mirror `siad_v3_restore` primero, luego SRV.

**Prerrequisito verificable antes de aplicar:** que `2026-07-14_cfg_impuestos.sql` esté aplicado en el destino. La consulta «¿ya aplicado?» del paso: `SELECT to_regclass('public.cfg_impuesto_tasa');` — si devuelve NULL, aplicar primero el script de impuestos.

**Consulta de saneo obligatoria en el runbook** (deuda de `alm_tipo_articulo.cuenta_inventario`, §4 y §7.5). Debe devolver **cero filas** antes de encender el simulador y el motor:

```sql
SELECT t.codigo, t.nombre, t.cuenta_inventario
FROM alm_tipo_articulo t
LEFT JOIN con_plan_cuentas c
       ON c.code = replace(replace(t.cuenta_inventario, '-', ''), '.', '')
      AND c.company_id = t.company_id
WHERE t.cuenta_inventario IS NOT NULL
  AND (c.account_id IS NULL OR NOT c.allows_posting);
```

Si D1 ≠ costo puro, un **segundo script** (Fase 3) crea `11504010102 ISV crédito fiscal` en `con_plan_cuentas` según lo que apruebe D2 y siembra la fila de la matriz para la empresa. Va aparte porque depende de una decisión distinta.

Si el contador aprueba postear el asiento de compra desde almacén, un **tercer script** (Fase 6) amplía `ck_con_integracion_asiento_module` y agrega `con_integracion_config.activo_almacen`.

---

## 7. Capas afectadas

### 7.1 Entidades y contexto

- `SIAD.Core/Entities/cfg_impuesto_politica_compra.cs` — implementa `ICompanyScopedEntity` (filtro tenant y stamping vía `SiadDbContext.Tenancy.cs`), navegación a `cfg_impuesto`, `vigencia_desde`/`vigencia_hasta` como `DateOnly`/`DateOnly?`, `porcentaje_acreditable` como `decimal?`.
- `SIAD.Data/SiadDbContext.Impuestos.cs` — nuevo `DbSet` y su bloque en `ConfigureImpuestosModel`. **Replicar el patrón documentado del módulo**: no declarar `HasDefaultValue` en `activo` ni en campos con default de BD, o un alta inactiva nacería activa ([`SiadDbContext.Impuestos.cs:27-30`](../../SIAD.Data/SiadDbContext.Impuestos.cs)). FK con `DeleteBehavior.Restrict`. `porcentaje_acreditable` con `HasPrecision(5,2)`.
- `SIAD.Core/DTOs/Contabilidad/IntegracionContableDtos.cs` — agregar `IsvCreditoFiscal = "ISV_CREDITO_FISCAL"` a `IntegracionContableUsos` **y a `Todos`** (el `HasCheckConstraint` de EF se genera desde ahí). No va en `Dimensionables` ni en `GeneralesRequeridos`.

### 7.2 Constantes

- `SIAD.Core/Constants/PoliticaImpuestoCompra.cs` — `Costo`, `CreditoFiscal`, `Prorrata`, `Todas`, `EsValida()`, `ExigePorcentaje(politica)`. Espejo del CHECK.
- `SIAD.Core/Constants/DestinoCompra.cs` — `Inventario`, `Gasto`, `ActivoFijo`, `Todos`, `EsValido()`. Espejo del CHECK.

Ambas siguen el patrón de `TipoImpuestoTasa` y `TipoDocumentoInventario`: **agregar un valor obliga a tocar constante + CHECK**, y eso queda escrito en el comentario del archivo.

### 7.3 Servicios de impuestos y de integración contable

- **`IImpuestosService` / `ImpuestosService`** (ampliar, no duplicar): `GetPoliticasCompraAsync(filtro)`, `CreatePoliticaCompraAsync`, `UpdatePoliticaCompraAsync`, `DeactivatePoliticaCompraAsync`, y `CambiarPoliticaCompraAsync` — análogo a `CambiarTasaAsync`: **cierra la vigencia de la política actual y abre la sucesora en una transacción**, nunca edita la fila vigente. Se replican en C# los invariantes de BD (`ValidarSolapeAsync` **filtrando por `activo`**, `ValidarRangoVigencia`, `ValidarProrrata`) y se traduce SQLSTATE a mensaje humano en `TraducirErrorDeBd` (`23P01` → *"Ya existe una política ACTIVA para ese impuesto y destino en ese rango de fechas. Si la anterior quedó mal, desactívela primero."*).

- **`IIntegracionContableService.UpsertCuentaUsoAsync` (método NUEVO, obligatorio)**:

  ```csharp
  Task UpsertCuentaUsoAsync(long companyId, string uso, long accountId, string usuario, CancellationToken ct = default);
  ```

  **Toca UNA fila** (`con_integracion_cuenta` con ese `uso` y dimensiones nulas): la actualiza si existe, la inserta si no. **Nunca ejecuta `RemoveRange`.** Valida con el **mismo criterio** que `ValidarInternoAsync` —la cuenta existe en el plan de la empresa, `allows_posting = true`, y el `uso` está en `IntegracionContableUsos.Todos`— pero **no puede reutilizar ese método tal cual**: `ValidarInternoAsync` es `private` y recibe la matriz completa (`IntegracionContableDto`) ([`IntegracionContableService.cs:340`](../../SIAD.Services/Contabilidad/IntegracionContableService.cs)). Se extrae la validación de cuenta a un helper privado compartido por ambos caminos, para que no haya dos criterios divergentes.

  **Prohibición explícita del diseño:** la pantalla de impuestos **no llama a `GuardarAsync`** bajo ninguna circunstancia. `GuardarAsync` recibe la matriz completa y borra lo que no venga en el DTO ([`IIntegracionContableService.cs:20-21`](../../SIAD.Services/Contabilidad/IIntegracionContableService.cs); [`IntegracionContableService.cs:204-205`](../../SIAD.Services/Contabilidad/IntegracionContableService.cs)); llamarlo desde una pantalla que solo conoce una fila **borraría CXC, INGRESO, CAJA, BANCO_DEFAULT y toda la configuración contable de la empresa**, en silencio y sin error. Es el riesgo más destructivo de todo el diseño y por eso tiene test propio (§12, test 18).

  Endpoint: `PUT api/integracion-contable/cuentas/{uso}` en el controlador de integración contable, con el `[ModuleAuthorize]` que ya tenga esa clase.

### 7.4 El resolver — punto único de cálculo

**`SIAD.Services/Impuestos/IImpuestoCompraResolver.cs`** (servicio nuevo, de solo lectura) — **el punto único que consumirá el motor de posteo**:

```csharp
ResolucionIsvDto Resolver(
    DateOnly fechaFactura,          // A4: fecha de emisión del proveedor
    DateOnly? fechaRecepcion,       // solo para advertir si se cayó a ella
    int? articuloId,
    int? tipoArticuloId,
    string destino,                 // derivado de maneja_inventario (A9), por LÍNEA
    decimal montoBruto,
    decimal descuento,              // base = bruto - descuento
    decimal? montoIsvFactura,
    int? tasaIdOverride,            // D8: exoneración por documento
    string? resolucionExoneracion);
```

Devuelve: `TasaId`, `TipoTasa` (GRAVADO/EXENTO/EXONERADO), `Porcentaje`, `Politica`, `PorcentajeAcreditable?`, `MontoBase`, `MontoIsvCalculado`, `MontoIsvAplicado`, `MontoIsvAcreditable`, `MontoIsvCapitalizado`, **`CostoUnitarioResultante`**, `CuentaCreditoFiscalId?` y `Advertencias` (lista). Es la única pieza que sabe sumar o no sumar el impuesto al costo.

Registrado en [`ServiceRegistration.cs`](../../SIAD.Services/ServiceRegistration.cs) junto a `IImpuestosService`.

**Reglas de cálculo (cierran D5, que el texto dejaba abierta):**

1. **Base imponible** = `montoBruto − descuento`. Nunca el bruto.
2. **Fecha** = `fechaFactura`; si viene NULL, se cae a `fechaRecepcion` y **se agrega advertencia** al resultado (A4).
3. **Cuál ISV manda cuando difieren**: **manda siempre el de la factura del proveedor** (`montoIsvFactura`), en las tres políticas.
   - Bajo `COSTO` porque es el **desembolso real** y es exactamente lo que se capitaliza en el inventario.
   - Bajo `CREDITO_FISCAL` y `PRORRATA` porque es **lo que se declara** ante el SAR: el crédito no puede exceder el impuesto documentado.
   - El sistema **propone** el calculado y **bloquea** el registro si `|factura − calculado| >` **tolerancia**, con valor por defecto **L 1.00** (no L 0.05: el proveedor redondea por línea y una factura de veinte renglones acumula centavos legítimamente). La tolerancia es configurable y su valor final lo fija D5.
   - Si `montoIsvFactura` es NULL, se usa el calculado.
4. **Bajo `COSTO`, la diferencia entre el ISV de la factura y el calculado entra al costo** y por tanto al promedio ponderado. Es correcto y es deliberado: el inventario vale lo que se pagó por él.
5. **Redondeo**: el importe del impuesto a **2 decimales**; el costo unitario a **4** (precisión de `alm_kardex.valor_unitario` y de `alm_articulo_bodega.costo_promedio`). **El `total` manda y el `valor_unitario` se deriva**, no al revés.
6. **Sin política vigente**: excepción de configuración, con **dos mensajes distintos** según el caso (§9.0).
7. **Tasa `EXENTO` o `EXONERADO`**: impuesto 0 y costo = base, sea cual sea la política. La política solo decide qué hacer con un impuesto que existe.

### 7.5 Validación de cuentas contables (deuda que se salda en F1)

- **Cuenta de crédito fiscal**: se valida con el criterio de la matriz (existe / `allows_posting` / del tenant), a través de `UpsertCuentaUsoAsync`. Esa es **la razón de no guardar la cuenta como texto suelto** al estilo de `alm_tipo_articulo`.
- **Cuenta de inventario del tipo de artículo**: hoy `TipoArticuloService` **solo normaliza texto** y acepta cualquier código. Se le aplica la **misma** `ValidarCuentaContableAsync` que ya usa `ArticulosService` —existe en el plan de la empresa, es imputable, está activa, comparando sin separadores con `AccountCodeFormatter.Normalize`— ([`ArticulosService.cs:393-429`](../../SIAD.Services/Almacen/ArticulosService.cs)), más la consulta de saneo del §6 en el runbook.
- **El simulador de §8 se niega a mostrar la vista previa contable si la cuenta del tipo de artículo no resuelve**, con el mensaje *"El tipo de artículo «X» tiene configurada la cuenta NNN, que no existe o no es imputable en el plan de cuentas. Corríjala en Mantenimientos → Tipos de artículo antes de configurar el ISV."* Mostrar un asiento contra una cuenta inválida sería peor que no mostrarlo: el contador lo aprobaría y el motor fallaría al postear.

### 7.6 API y cliente

- `apc/Controllers/Impuestos/ImpuestosController.cs` — endpoints nuevos bajo `api/impuestos`: `GET politicas-compra`, `POST politicas-compra`, `PUT politicas-compra/{id}`, `POST politicas-compra/{id}/desactivar`, `POST politicas-compra/cambiar`, y `GET politicas-compra/vigente?fecha=&destino=`. Hereda `[ModuleAuthorize(PermissionModules.Configuracion)]` de la clase ([`ImpuestosController.cs:23`](../../apc/Controllers/Impuestos/ImpuestosController.cs)): `GET→View`, `POST→Create`, `PUT→Edit`. **No hacen falta entradas nuevas** en `PermissionNames` ni en `PermissionEndpointCatalog` — el fallback de módulo los cubre. Ver §8.1 sobre el problema de ámbito que comparte ese permiso.
- `apc.Client/Services/Impuestos/ImpuestosClient.cs` — métodos gemelos con los helpers `*WithAuthCheck`. Ya está registrado en [`CommonServices.cs`](../../apc.Client/CommonServices.cs) (seguro en ambos hosts).
- DTOs en `SIAD.Core/DTOs/Impuestos/`: `PoliticaImpuestoCompraDto` (con `IValidatableObject`, `EsAbierta`, `EsVigenteHoy`, igual que `ImpuestoTasaDto`), `PoliticaImpuestoCompraFilterDto`, `CambiarPoliticaCompraDto`, `ResolucionIsvDto`.

---

## 8. La pantalla del contador

**Ubicación:** ampliación de `apc.Client/Pages/Mantenimientos/Impuestos.razor` (`/mantenimientos/impuestos`, ítem `mant-impuestos` del menú lateral). Hoy es maestro–detalle impuesto→tasas; se le agrega una **tercera sección**, *"Tratamiento en compras"*, que carga al seleccionar un impuesto. Un solo lugar para todo lo fiscal.

### 8.1 Distintivo de ámbito (obligatorio) — global vs por empresa

Esta pantalla mezcla **dos ámbitos incompatibles** y el usuario no tiene hoy forma de distinguirlos:

- Las secciones **Impuesto** y **Tasas** editan `cfg_impuesto` / `cfg_impuesto_tasa`, que son **GLOBALES: sin `company_id`, compartidas por todas las empresas del sistema** ([`SiadDbContext.Impuestos.cs:6-8`](../../SIAD.Data/SiadDbContext.Impuestos.cs)).
- La sección **Tratamiento en compras** edita `cfg_impuesto_politica_compra`, que es **por empresa**.

Ambas quedan bajo el mismo permiso `module.configuracion` ([`ImpuestosController.cs:23`](../../apc/Controllers/Impuestos/ImpuestosController.cs)). El riesgo es concreto y multitenant: el contador de la empresa 2 entra "a cambiar su ISV", usa **Cambiar tasa por decreto** y **modifica la tasa de todas las empresas del sistema**.

Mitigaciones, todas en F1:

1. **Badge de ámbito visible** sobre cada sección: *"⚠ Catálogo GENERAL — aplica a TODAS las empresas del sistema"* sobre Impuesto y Tasas; *"Solo &lt;nombre de la empresa actual&gt;"* sobre Tratamiento en compras.
2. **Confirmación explícita** al usar *Cambiar tasa por decreto*, con el texto de que el cambio afecta a todas las empresas y no solo a la actual.
3. **Evaluar exigir `RoleNames.SuperAdministrador`** para las secciones globales (impuesto y tasas), dejando `module.configuracion` para la política por empresa. Es un cambio de permisos y por eso se evalúa, no se da por hecho: hay que confirmar que quien administra hoy el catálogo tenga ese rol.

### 8.2 Campos

| Campo | Control | Regla |
|---|---|---|
| Impuesto | (contexto, no editable) | El seleccionado en el grid maestro |
| **Destino de la compra** | `DxComboBox` — *Inventario (entra a bodega)* / *Gasto (se consume)* / *Activo fijo* | Obligatorio |
| **Tratamiento** | `DxRadioGroup`, redactado en lenguaje de contador:<br>○ **Al costo** — *"El ISV pagado aumenta el valor del material. No se recupera."*<br>○ **Crédito fiscal** — *"El ISV pagado es un derecho contra el SAR. El material entra a su valor sin impuesto."*<br>○ **Crédito parcial (prorrata)** — *"Solo una parte del ISV se recupera; el resto aumenta el valor del material."* | Obligatorio. **La tercera opción solo se muestra si D0 = la empresa tiene ventas gravadas** |
| **% acreditable** | `DxSpinEdit` (0–100, 2 decimales) | **Visible y obligatorio solo si** Tratamiento = Crédito parcial. Espejo de `ck_cfg_imp_pol_compra_prorrata` |
| **Cuenta del crédito fiscal** | `DxComboBox` del plan de cuentas (solo imputables y activas) | **Visible y obligatoria si** Tratamiento ∈ {Crédito fiscal, Crédito parcial}. Escribe la fila `ISV_CREDITO_FISCAL` de la matriz **vía `UpsertCuentaUsoAsync`, nunca vía `GuardarAsync`** (A3.1) |
| **Vigente desde** | `DxDateEdit` | Obligatorio. Regla de arranque de §9.0 para la primera política. Se advierte si es anterior a documentos ya posteados |
| **Vigente hasta** | `DxDateEdit` | Vacío = vigente indefinidamente |
| **Base legal / observación** | `DxTextBox` (250) | Opcional; el contador anota el artículo o la resolución que la sustenta |
| Activo | `DxCheckBox` | Soft-delete. Ver flujo de corrección en §8.4 |

### 8.3 Vista previa del efecto (antes llamada "simulador")

Un panel de solo lectura, siempre visible bajo el formulario: el contador teclea un **monto de compra bruto y un descuento**, y el sistema muestra, con la tasa vigente y la política elegida, **cómo queda el costo del artículo y cómo quedaría el asiento**. Es la forma de que apruebe la configuración viendo el resultado, no leyendo un manual.

**Rótulos obligatorios del panel:**

- **"Vista previa contable — ilustrativa. No es configuración de asientos."** El posteo automático de compras de almacén **no existe** todavía: `con_integracion_asiento` no admite un módulo de almacén ni de compras (§3, Fase 6). Lo que la Fase 1 sí deja funcionando y consumible es el **costo unitario y el desglose del impuesto**.
- Si la cuenta de inventario del tipo de artículo no resuelve contra `con_plan_cuentas`, **el panel no muestra el asiento** y explica por qué (§7.5).

### 8.4 Validaciones, avisos y flujo de corrección

- **Solape de vigencias**: lo impide el `EXCLUDE ... WHERE (activo)` en BD y se anticipa en el servicio con mensaje humano.
- **Flujo de corrección de una política mal configurada** (posible gracias al predicado `WHERE (activo)`): *desactivar* la política errónea con el botón Activo/Desactivar y **volver a crearla con las mismas fechas**. La BD lo acepta porque el `EXCLUDE` solo mira filas activas. El servicio debe filtrar igual (`ValidarSolapeAsync` con `.Where(p => p.activo)`), o rechazaría con mensaje humano algo que la base habría permitido. **Este flujo tiene test propio** (§12, test 14).
- **Cambio de política** (no es corrección, es sucesión): no se edita la fila vigente. Botón **"Cambiar tratamiento"** (mismo patrón que "Cambiar tasa por decreto"): pide *último día del tratamiento actual* + *nuevo tratamiento* + *motivo*, y hace cierre + alta en una transacción. Banner didáctico: *"No edite el tratamiento vigente: eso reescribiría el pasado. Use Cambiar tratamiento para que quede el histórico."*
- **Aviso al cambiar de CRÉDITO FISCAL a COSTO**: recordatorio de que la cuenta de crédito fiscal quedará con un saldo por reclasificar (§9.4, D11), con enlace al reporte de saldo.
- **Aviso de irreversibilidad**: al guardar una `vigencia_desde` anterior a la fecha de un documento ya posteado, confirmación explícita con el texto *"Las compras ya registradas conservan el costo con que se postearon. El kardex no se reescribe."*
- Si Tratamiento ∈ {Crédito fiscal, Crédito parcial} y no hay cuenta configurada → **error bloqueante**, no advertencia (mismo criterio que la validación de la matriz).

### 8.5 Deuda de UI que se salda de paso

La pantalla incumple hoy el estándar de grid: `PageSize="10"` en los dos grids (el estándar es 15), sin selector de página, sin `@ref` ni botón "Columnas" (`ShowColumnChooser`), sin `LayoutAutoSaving`/`LayoutAutoLoading`, y su `DxToastProvider` no lleva `StickToViewport="true"` ([`Impuestos.razor:16,36,158`](../../apc.Client/Pages/Mantenimientos/Impuestos.razor)). Mantenimientos **no está** en las exclusiones del estándar. Se migra en el mismo golpe, referencia `ClientesList.razor` y `siad-grid.css`.

> DevExpress: consultar el MCP `dxdocs` (`devexpress_docs_search` → `devexpress_docs_get_content`) antes de tocar cualquier API de componente. Obligatorio por `CLAUDE.md`.

---

## 9. Vigencia, retroactividad y los casos que rompen el promedio

### 9.0 Regla de arranque: la primera política nunca puede dejar documentos huérfanos

El fail-closed de A5 combinado con "rige desde la fecha que indique el contador" tiene un efecto que hay que neutralizar: si el contador configura `vigencia_desde = 01/09/2026`, **queda bloqueado el registro de cualquier factura de compra de agosto**, y recibir la factura del proveedor con semanas de retraso es la práctica normal.

Reglas:

1. **La PRIMERA política de cada `(impuesto, destino)` debe abrirse con `vigencia_desde` ≤ la fecha del documento más antiguo posteable.** La pantalla **propone por defecto** la fecha del asiento de apertura del inventario, y si no la hay, `2010-01-01` — exactamente el criterio con el que el catálogo de impuestos resolvió el mismo problema: *"Se usa una ventana amplia para que ningún documento existente quede sin tasa aplicable"* ([`2026-07-14_cfg_impuestos.sql:123-125`](../../Database/2026-07-14_cfg_impuestos.sql)).
2. **El fail-closed distingue dos situaciones, con mensajes distintos:**
   - *No hay ninguna política configurada* → **"No hay política de ISV configurada para compras de inventario. Configúrela en Mantenimientos → Impuestos."**
   - *Hay políticas, pero la más antigua empieza después del documento* → **"La política de ISV más antigua para compras de inventario empieza el DD/MM/AAAA y este documento es del DD/MM/AAAA. Retroceda la fecha 'Vigente desde' de la primera política en Mantenimientos → Impuestos."**
3. **Retroceder la `vigencia_desde` de la primera política es la ÚNICA edición retroactiva permitida**, y solo mientras no haya nada posteado antes de esa fecha. No reescribe ningún costo porque no hay costos que reescribir.

### 9.1 Las tres reglas generales

1. **La política se resuelve por la fecha de la factura del proveedor** (A4), nunca por "la política actual". Es el mismo principio que ya sostiene `cfg_impuesto_tasa` y `GetTasasVigentesAsync(fecha)`: reconstruir un documento de marzo debe dar lo que regía en marzo.
2. **Un cambio de política no es retroactivo.** Los movimientos ya posteados conservan su costo. No es una preferencia de diseño: `alm_kardex` es inmutable por trigger y cualquier `UPDATE` revienta con `K0001`. Corregir el pasado solo es posible con contra-asientos (reversa) y una nueva entrada — procedimiento que define el diseño del motor, no este.
3. **El histórico de políticas es intacto**: cambiar significa cerrar la vigencia de una y abrir la siguiente, nunca editar. Por eso hay `EXCLUDE` y `CambiarPoliticaCompraAsync`, y por eso la pantalla desaconseja la edición directa.

### 9.2 Dos fechas, dos usos

Una factura emitida el 30/06 y recibida el 05/07, con cambio de política al 01/07, **se resuelve con la política de junio** (la de `fecha_factura`). El **asiento del kardex puede llevar la fecha de recepción** (`alm_compra.fecha`, cuando el material entró físicamente a bodega) aunque el impuesto se haya resuelto con `fecha_factura`. No es una inconsistencia: son dos hechos distintos con dos fechas distintas, y el diseño lo declara explícitamente para que nadie lo "arregle" después. Tiene test propio (§12, test 16).

### 9.3 Devoluciones al proveedor y notas de crédito (D10)

Es el flujo que más rompe el promedio ponderado y hasta esta revisión no estaba diseñado en ninguna parte. Regla técnica del sistema (la parte fiscal la fija D10):

1. **La devolución es un movimiento NUEVO del kardex** —una salida valorizada al promedio vigente—, jamás un `UPDATE` del movimiento original (imposible: `K0001`).
2. **El ISV se revierte con la política vigente a la fecha de la COMPRA original**, no a la fecha de la devolución. Si no fuera así, un cambio de política dejaría el ISV revertido en una cuenta distinta de aquella en la que entró, y la cuenta de crédito fiscal nunca cerraría.
3. Bajo `CREDITO_FISCAL` la devolución genera un **contra-asiento** que acredita la cuenta de ISV crédito fiscal y **afecta el renglón del formulario 222 del mes** — si ese mes ya se declaró, hay que decidir si se rectifica (eso es **D10**).
4. Bajo `COSTO` la devolución sale al promedio del momento, que puede no coincidir con el costo de entrada de esa compra: **la diferencia es un ajuste al costo, no un error**. Es inherente al promedio ponderado móvil.
5. El resolver expone las mismas cifras para la devolución que para la compra, invocado con la fecha de la factura original.

### 9.4 Saldo remanente al cambiar de política (D11)

Cambiar de política no solo afecta a los documentos futuros: **deja un saldo contable colgando**. Las dos direcciones no son simétricas:

**(a) COSTO → CRÉDITO FISCAL.** El ISV ya capitalizado en las existencias que quedan en bodega **se pierde como crédito**: nunca fue registrado como derecho contra el SAR y no se puede recuperar retroactivamente. No hay asiento. Las existencias viejas simplemente valen más que las nuevas y el promedio ponderado los mezcla (§9.5). No hace falta decisión del contador, solo advertirlo.

**(b) CRÉDITO FISCAL → COSTO.** El caso problemático. La cuenta de activo *ISV crédito fiscal* queda con un **saldo acumulado que ya no se podrá compensar**, y **el inventario que está en bodega no puede absorberlo**: se costeó neto y el kardex es inmutable. Ese saldo hay que **reclasificarlo por asiento manual** a la fecha de corte, contra gasto del período o contra inventario. **Cuál de las dos es decisión del contador — es exactamente D11.** El sistema aporta:

- Un **reporte "Saldo de ISV crédito fiscal a la fecha de corte"** (Fase 3): saldo de la cuenta `ISV_CREDITO_FISCAL` a una fecha, con el detalle de los documentos que lo formaron.
- El **aviso en pantalla** al registrar el cambio de tratamiento (§8.4).
- **No** genera el asiento automáticamente: reclasificar contra gasto o contra inventario tiene consecuencias fiscales distintas y el sistema no está en posición de elegir.

### 9.5 Cambio a mitad de ejercicio

Consecuencia práctica que hay que decirle al contador (D6): **cambiar la política a mitad de ejercicio deja el promedio ponderado de cada artículo con dos bases de costo mezcladas**. No es un error del sistema, es la naturaleza del promedio móvil. Si se quiere una frontera limpia, lo correcto es cambiar la política al inicio de un ejercicio.

---

## 10. Fases

| Fase | Qué | Depende de |
|---|---|---|
| **F0** | **Sesión con el contador.** Llevar D0–D11 (más la D2 del plan de retenciones, que es la misma pregunta). **Tarea previa del equipo, no del contador:** investigar en La Gaceta / portal del SAR las fechas y números de decreto de cada tasa, y llevar la tabla ya armada para que él solo confirme o corrija (D7). Sanear después `cfg_impuesto_tasa` con lo confirmado. Sin código | Nada. **Es lo primero** |
| **F1** | **Política de ISV en compras.** Script `Database/2026-07-29_isv_compras_politica.sql` (con `EXCLUDE ... WHERE (activo)`), entidad, contexto, constantes, DTOs, ampliación de `ImpuestosService`, endpoints, cliente, sección de pantalla + vista previa + **badges de ámbito (§8.1)**, migración de la pantalla al estándar de grid. **Además: validación de `alm_tipo_articulo.cuenta_inventario` con `ValidarCuentaContableAsync` (§7.5) y consulta de saneo en el runbook** | D0, D1, D3, D6 |
| **F2** | **Clasificación fiscal por artículo y override por documento.** `alm_articulo.impuesto_tasa_id` + `alm_tipo_articulo.impuesto_tasa_id` + cadena de resolución + combos. **Si D8 = sí, incluye el override por documento con número de resolución y NO se puede omitir** | D4, D8. Se **omite** solo si toda compra es gravada al 15 % **y** D8 = no |
| **F3** | **Cuenta de crédito fiscal.** Cuenta nueva en `con_plan_cuentas` según D2 (script aparte), uso `ISV_CREDITO_FISCAL` sincronizado en los **dos** puntos, **método `UpsertCuentaUsoAsync` + su endpoint (A3.1)**, regla de validación (*si hay política CREDITO_FISCAL o PRORRATA activa, faltar la cuenta es ERROR*), y **reporte "Saldo de ISV crédito fiscal a la fecha de corte" (§9.4, D11)** | **Solo si D1 ≠ costo puro.** Depende de D2 y D11 |
| **F4** | **Consumo real.** `IImpuestoCompraResolver` conectado al alta de compra y al motor de posteo: el `CostoUnitario` que recibe `MovimientoInventarioDto` sale de aquí y de ningún otro lado. Incluye la derivación del destino por línea desde `maneja_inventario` (A9) | F1 (+F2 si aplica) y **el motor de posteo, que no existe** |
| **F5** | **Libro de compras.** Escritura de `con_libro_iva` (`iva_type` = compras, `third_party_id` = proveedor, base / exento / exonerado / tasa / impuesto) y reporte mensual para la declaración | **Solo si D9 = sí.** Requiere F3 y F4 |
| **F6** | **Vocabulario contable del asiento de almacén** — trabajo que el diseño original daba por hecho y **no existe**. Tres piezas: (1) script SQL que amplía `ck_con_integracion_asiento_module` con `'ALMACEN'` (o `'COMPRAS'`, a decidir con el módulo de compras); (2) la constante en `IntegracionContableModulos` ([`IntegracionContableDtos.cs:63-72`](../../SIAD.Core/DTOs/Contabilidad/IntegracionContableDtos.cs)), de donde EF deriva su `HasCheckConstraint` ([`SiadDbContext.IntegracionContable.cs:110-112`](../../SIAD.Data/SiadDbContext.IntegracionContable.cs)); (3) el flag `con_integracion_config.activo_almacen` y su mapeo en `ObtenerActivo`/`AsignarActivo` ([`IntegracionContableService.cs:614-651`](../../SIAD.Services/Contabilidad/IntegracionContableService.cs)). Sin esto, la vista previa de §5 **no se puede configurar ni postear** | F4 y el diseño contable del motor de posteo |

**Orden:** F0 → F1 → (F2 si D4/D8) → (F3 si D1 ≠ costo) → F4 → (F5 si D9) → F6.

**F1 se puede construir y desplegar antes de que exista el motor de posteo**: es configuración, no cálculo. Esa es la razón de partir el trabajo así — desbloquea al contador hoy y deja el consumo listo para cuando el motor se escriba. Lo que **no** se puede hacer antes de F6 es prometer que el asiento se postea solo.

### Checklist de despliegue por fase (regla del proyecto)

- [ ] Script timestamped en `Database/` (idempotente).
- [ ] Registro en el runbook SRV vigente (skill `runbook-despliegue-srv`), **incluida la consulta de saneo de `cuenta_inventario`**.
- [ ] Aprobación previa por la skill `guardia-estructura-bd`.
- [ ] Aplicado primero en el mirror `siad_v3_restore` — **el usuario aplica en SRV**.
- [ ] `dotnet build HODSOFT_DEVEXPRESS.sln` sin errores.
- [ ] Tests de integración (`SIAD_TEST_DB`) verdes, incluidos los nuevos.
- [ ] Smoke logueado en `/mantenimientos/impuestos`.

---

## 11. Riesgos

- **Escribir la cuenta de crédito fiscal con `GuardarAsync` destruiría la matriz contable de la empresa.** Ese método recibe la matriz completa y borra toda fila ausente del DTO ([`IntegracionContableService.cs:204-205`](../../SIAD.Services/Contabilidad/IntegracionContableService.cs)); una pantalla que solo conoce una fila borraría CXC, INGRESO, CAJA y BANCO_DEFAULT en silencio. **Mitigación: `UpsertCuentaUsoAsync` (A3.1), prohibición explícita en el diseño y test 18.** Es el riesgo más destructivo del documento.
- **La decisión bloquea el costeo entero, no solo esta pantalla.** Mientras D0/D1 no estén respondidas, el motor de posteo no puede calcular `costo_promedio` ni `ultimo_costo`, y la carga inicial del inventario no debería correr.
- **Un modelo binario habría sido un error si la empresa tiene ventas gravadas.** El catálogo comercial ya tiene `CONEXION` e `INSTALACION` con cuenta de ingreso propia ([`20260702_ci_fase1_integracion_config.sql:313-352`](../../Database/ddl_v3/20260702_ci_fase1_integracion_config.sql)); si esos servicios se facturan gravados, hay prorrata. Mitigación: D0 y el tercer valor `PRORRATA` en el vocabulario desde el día uno (A10).
- **Soft-delete + `EXCLUDE` sin predicado dejaría la pantalla sin salida.** Mitigación: `WHERE (activo)` en el `EXCLUDE` y en su espejo C# (§4.1), más test 14. Ojo: `cfg_impuesto_tasa` **sí** tiene ese defecto latente hoy ([`2026-07-14_cfg_impuestos.sql:99-105`](../../Database/2026-07-14_cfg_impuestos.sql)); vale la pena corregirlo cuando se toque.
- **`alm_tipo_articulo.cuenta_inventario` puede apuntar a cuentas inexistentes o no imputables.** Es `VARCHAR` sin FK, sin validación y sembrada copiando códigos de SIMAFI. Es la cuenta que la vista previa debita y la que el motor usará. Mitigación: validación en F1 + consulta de saneo obligatoria + el simulador se niega a mostrar el asiento (§7.5).
- **El asiento de compra de almacén no se puede postear todavía.** `con_integracion_asiento` no admite ese módulo. Mitigación: rótulo explícito en la vista previa y Fase 6 con el trabajo real.
- **La pantalla mezcla ámbito global y ámbito por empresa bajo el mismo permiso.** Un contador puede cambiar la tasa de todas las empresas creyendo cambiar la suya. Mitigación: §8.1.
- **Base de costo mezclada en la apertura.** `alm_articulo.valor_unitario` viene de SIMAFI sin que conste si incluye ISV. Si la apertura y las compras usan bases distintas, el promedio queda mal **en silencio**. Debe fijarse la base de la apertura en la misma sesión de D1.
- **Reutilizar el uso `ISV` de la matriz sería un error contable.** Apunta a `21105010000` "Impuestos por Pagar" (PASIVO = ISV cobrado en ventas). Meter ahí un crédito fiscal registraría un activo recuperable en una cuenta de pasivo. Por eso el uso nuevo.
- **Desalineación del CHECK de usos.** El de la BD y el que EF reconstruye desde `IntegracionContableUsos.Todos` deben quedar idénticos — **son dos puntos, no tres**: la constante (de la que EF deriva el `HasCheckConstraint`) y el CHECK del script SQL, que es **el único que llega a la base**, porque el `SiadDbContext` no tiene migraciones EF. El riesgo real es acotado: la discrepancia se manifestaría como un rechazo de EF en memoria, no como corrupción de datos.
- **Devoluciones y notas de crédito no diseñadas hasta esta revisión.** Ahora tienen regla (§9.3) y pregunta (D10), pero no implementación: hasta F4/F5 el sistema no las procesa.
- **El catálogo de impuestos no tiene un solo test.** `CambiarTasaAsync` (transacción de dos pasos) y el `EXCLUDE` gist son exactamente el tipo de lógica que se rompe callada, y ahora el costeo va a depender de ellos.
- **No consta que `cfg_impuesto*` exista en producción**: el script no figura en el runbook SRV y el runbook advierte que no se verificó contra el servidor en vivo. Además requiere `btree_gist`; si la extensión no está en SRV, el `EXCLUDE` falla al crearse.
- **Tasas semilla sin validar** (vigencia inventada desde 2010-01-01). Costear con ellas produce números defendibles solo por casualidad — de ahí D7 como parte de F0.
- **`alm_compra.impuesto`, `alm_compra.descuento` y `alm_requisicion.impuesto`/`impuesto_aplica` existen y nadie las usa.** Son un atractor de errores: alguien puede asumir que "el ISV ya está en compras". Al construir F4 hay que decidir explícitamente si se rehabilitan o se marcan como legado muerto.
- **Redondeo.** `porcentaje` es `NUMERIC(5,2)`, el importe del ISV se lleva a 2 decimales y el costo unitario a 4. Con política `COSTO`, dividir el total con impuesto entre la cantidad puede dejar centavos de diferencia entre `cantidad × valor_unitario` y `total`. Regla fijada en §7.4 (el `total` manda) y cubierta con test.
- **La pantalla de configuración es de bajo tráfico y alto impacto**: se toca una vez y define el costo de todo el inventario. Merece confirmación explícita al guardar y bitácora — evaluar si `cfg_impuesto_politica_compra` entra en `bitacora_maestro_config` (interceptor de auditoría de maestros ya implementado).

---

## 12. Pruebas

`SIAD.Tests` contra Postgres real (`SIAD_TEST_DB`), cada test dentro de `BEGIN … ROLLBACK`. Archivo nuevo `SIAD.Tests/Impuestos/PoliticaIsvCompraTests.cs`:

**Invariantes de BD**
1. El `EXCLUDE` rechaza dos políticas **activas** del mismo `(company, impuesto, destino)` con vigencias solapadas → SQLSTATE `23P01`.
2. Dos empresas **sí** pueden tener políticas distintas para el mismo impuesto, destino y fechas (el `company_id` está en el `EXCLUDE`).
3. El CHECK rechaza `politica` y `destino` fuera del vocabulario.
4. `vigencia_hasta < vigencia_desde` es rechazada.
5. `politica='PRORRATA'` sin `porcentaje_acreditable` es rechazada; `politica='COSTO'` **con** `porcentaje_acreditable` también; `porcentaje_acreditable = 150` es rechazada.

**Resolución**
6. Con dos políticas consecutivas (COSTO hasta el 30/06, CREDITO_FISCAL desde el 01/07), un documento del 15/06 resuelve **COSTO** y uno del 15/07 **CREDITO_FISCAL**. *No* devuelve la vigente hoy.
7. Sin ninguna política configurada → excepción de configuración con el mensaje "no hay política configurada", **no** un default silencioso.
8. Con políticas que empiezan después del documento → excepción con el **mensaje distinto** de §9.0 (indica la fecha de la política más antigua y cómo corregirla).
9. Artículo con tasa `EXENTO` → impuesto 0 y costo = base, sea cual sea la política.
10. Cadena de resolución de tasa: override de documento gana sobre artículo; artículo sin tasa toma la del tipo; ni override ni artículo ni tipo → excepción.

**Aritmética (los números de §5)**
11. `COSTO`: base 1,000 + 15 % sobre 10 unidades → `CostoUnitario = 115.0000`, impuesto 150.00.
12. `CREDITO_FISCAL`: mismo insumo → `CostoUnitario = 100.0000`, impuesto 150.00, cuenta de crédito resuelta.
13. **Descuento**: bruto 1,000 − descuento 100 → base 900.00, ISV 135.00, `CostoUnitario` 103.5000 (COSTO) / 90.0000 (CRÉDITO).
14. **Desactivar y recrear**: crear una política, desactivarla, y crear otra con **vigencias idénticas** → **NO** lanza `23P01`. Es la prueba del predicado `WHERE (activo)` y del flujo de corrección de §8.4.
15. **ISV de factura vs calculado**: factura 150.07 vs calculado 150.00 con política `COSTO` → manda el de la factura: `CostoUnitarioResultante = 115.0070` y `total` del kardex 1,150.07. Con diferencia de L 5.00 (> tolerancia L 1.00) → bloquea con excepción.
16. **Dos fechas**: factura emitida el 30/06 y recibida el 05/07, con cambio de política al 01/07 → resuelve con la política de **junio** (`fecha_factura`). Con `fecha_factura` NULL → cae a `fecha` y devuelve advertencia.
17. **Destino por línea**: artículo cuyo tipo tiene `maneja_inventario = false` → destino `GASTO`; artículo con `true` → `INVENTARIO`. Un documento con ambas líneas resuelve **dos destinos distintos** y puede aplicar dos políticas distintas.
18. **`UpsertCuentaUsoAsync` no destruye la matriz**: con CXC, INGRESO, CAJA y BANCO_DEFAULT ya configurados, guardar la cuenta de `ISV_CREDITO_FISCAL` desde `/mantenimientos/impuestos` **no altera ni elimina** ninguna de esas filas. Contraprueba documentada del riesgo de `GuardarAsync`.
19. **Prorrata** (solo si D0 = sí): `PRORRATA` al 20 % sobre base 1,000 + ISV 150 → acreditable 30.00, capitalizado 120.00, `CostoUnitario = 112.0000`.
20. **Devolución**: compra del 15/06 bajo COSTO, cambio de política al 01/07, devolución el 10/07 → el ISV se revierte con la política de **junio** (la de la compra original), no con la de julio.

**Transaccionalidad y tenancy**
21. `CambiarPoliticaCompraAsync` cierra la vigente y abre la sucesora; si el alta falla, el cierre se revierte (patrón de `CambiarTasaAsync`).
22. La empresa A no ve ni puede modificar la política de la empresa B (filtro global).

**Deuda que se salda**: agregar de paso los tests que hoy faltan del catálogo — `ex_cfg_impuesto_tasa_vigencia` y `CambiarTasaAsync` — porque el costeo pasará a depender de ellos.

---

## 13. Preguntas abiertas y lo no verificable desde el repo

**Confirmado en código:** todo lo citado en §2, §3, §4, §7, §8 y §10 (rutas y líneas).

**Pendiente (requiere al contador o acceso a BD — no verificable en fuentes):**

1. La respuesta a D0–D11 (Anexo A).
2. Si `2026-07-14_cfg_impuestos.sql` está aplicado en el SRV de producción y si `btree_gist` está instalado allí.
3. Cuántos artículos tienen hoy `valor_unitario = 0` (determina si la carga inicial es un one-shot o exige captura manual masiva previa) y si esos valores incluyen o no ISV.
4. Si la empresa está inscrita como responsable de ISV y qué declara hoy en el formulario 222.
5. **Cuántos tipos de artículo tienen hoy `cuenta_inventario` apuntando a una cuenta inexistente o no imputable** — es la consulta de saneo del §6, y determina si F1 arrastra trabajo de corrección de datos.
6. Si los servicios comerciales `CONEXION` e `INSTALACION` se facturan **gravados o exentos** hoy (entra directamente en D0).

> Por regla del proyecto, no me conecto a ninguna base de datos por iniciativa propia. Los puntos 2, 3 y 5 se confirman cuando el usuario lo autorice; el 1, el 4 y el 6 solo los responde el contador.

---

## 14. Qué NO hacer (errores que este diseño evita a propósito)

1. **No** agregar columnas de política ni de cuenta a `cfg_impuesto` / `cfg_impuesto_tasa`: son globales por decisión explícita y documentada.
2. **No** reutilizar el uso `ISV` de la matriz para el crédito fiscal: es una cuenta de pasivo.
3. **No** llamar a `IIntegracionContableService.GuardarAsync` desde la pantalla de impuestos ni desde ninguna pantalla que conozca solo una parte de la matriz: **borra lo que no venga en el DTO**. Usar `UpsertCuentaUsoAsync`.
4. **No** guardar la cuenta contable como texto suelto sin FK ni validación, como hace hoy `alm_tipo_articulo` — y no dar por buena esa cuenta sin sanearla antes.
5. **No** consultar "la tasa actual" ni "la política actual": siempre por **`fecha_factura`** del documento.
6. **No** derivar el destino de la mera presencia de líneas de artículo: se deriva de `maneja_inventario`, **por línea**.
7. **No** presentar la vista previa contable como configuración de asientos: `con_integracion_asiento` no admite todavía un módulo de almacén.
8. **No** poner un `EXCLUDE` de vigencias sin `WHERE (activo)` en una tabla con soft-delete.
9. **No** sembrar una política por defecto en el script: sería decidir por el contador.
10. **No** dejar que la UI escriba costos: `costo_promedio` y `ultimo_costo` son de escritura exclusiva del motor de posteo, y los servicios ya los excluyen del DTO a propósito.
11. **No** hardcodear 15 % en ningún punto del código.
12. **No** encender el motor de costeo antes de que F0 y F1 estén cerradas.

---

## Fuera de alcance (explícito)

- **El motor de posteo de inventario** y el asiento contable de la compra. Este diseño define **la componente ISV del costo unitario**; quién lo escribe en `alm_kardex` y qué partida genera es el plan del motor ([`2026-07-14-motor-movimientos-almacen.md`](2026-07-14-motor-movimientos-almacen.md), fases 2 y 4) y su diseño contable aparte.
- **Fletes, seguros, aranceles y demás costos accesorios de adquisición.** El costo de adquisición de NIC 2 / Sección 13 los incluye, y **este diseño no los cubre**: `CostoUnitario` sigue siendo un hueco abierto en esa dimensión. Corregir aquí la afirmación de la versión anterior de que este documento "llena exactamente ese hueco" — llena **una parte** de él.
- **`EXONERADO` por resolución del SAR**, si D8 = no. Si D8 = sí, entra en F2 con el override por documento de §4.2.
- **El recálculo anual de la prorrata** (ajuste de fin de ejercicio contra el porcentaje provisional): se declara la consecuencia en §5, pero el asiento de ajuste es trabajo del diseño contable del motor.
- **El alta de compras** (`alm_compra` hoy es solo lectura): capturar la factura del proveedor, sus líneas, su descuento y su ISV es otro trabajo.
- **La carga inicial del inventario** (asiento de apertura): diseño propio; este documento solo le fija la dependencia de la base de costo.
- **Retenciones** (ISR, anticipo, ISV retenido): [`docs/plan_retenciones_compromisos_proveedores.md`](../plan_retenciones_compromisos_proveedores.md). Comparten D1 y el catálogo, pero son flujos distintos.
- **ISV en ventas**: la facturación de agua y alcantarillado es exenta y hoy no calcula ISV; no se toca. **Salvo que D0 revele que hay servicios gravados**, en cuyo caso hace falta un diseño propio de ISV en ventas — este documento solo cubre compras.
- **Activo fijo**: el destino se modela, pero ningún módulo lo consume todavía.
- **Declaración electrónica ante el SAR**: fuera del sistema.

---

## Anexo A — Cuestionario para la sesión con el contador (F0)

Doce preguntas, en el orden en que conviene hacerlas. Todas están redactadas para decidirse en una reunión, sin necesidad de consultar el sistema. Las marcadas **[bloqueante]** impiden avanzar con código.

**D0 — ¿Facturamos algo gravado? [bloqueante — es la primera]**
¿La empresa factura hoy algún concepto **gravado con ISV**? Piense en conexiones nuevas, instalaciones domiciliarias, reconexiones cobradas, venta de materiales a terceros, servicios de fontanería, alquileres. ¿Aproximadamente qué porcentaje de la facturación del año pasado representaron?
*Por qué se pregunta:* si todo es exento, el ISV que pagamos a proveedores solo puede ir al costo o perderse. Si hay ventas gravadas, aunque sean pocas, aparece el **crédito fiscal parcial (prorrata)** y el sistema tiene que llevar un porcentaje.

**D1 — ¿Al costo, crédito fiscal o prorrata? [bloqueante]**
La venta de agua potable y alcantarillado está exenta por el Art. 15. Con eso y con lo que acaba de responder en D0: el ISV que pagamos a proveedores (tubería, cloro, bombas, combustible), ¿lo declaramos como **crédito fiscal** y lo compensamos en el formulario 222, lo llevamos **al costo** del material y al gasto, o lo **prorrateamos**?

**D2 — ¿Qué cuenta usamos para el ISV pagado?**
Si la respuesta a D1 no es "todo al costo": le proponemos abrir la cuenta **`11504010102 — ISV crédito fiscal`**, hermana de la que ya existe `11504010101 Pagos a cuentas SAR`, ambas bajo `11504000000 Impuestos`. ¿La aprueba, o prefiere otro código y nombre?
*Nota técnica:* tiene que ser una cuenta **de detalle** (imputable). Las cuentas de agrupación como `11504000000` el sistema las rechaza.

**D3 — ¿El mismo tratamiento para toda compra?**
¿El tratamiento es igual para todo, o cambia según a dónde va lo comprado? Distinguimos tres casos: material que entra a bodega (**inventario**), consumibles y servicios que se gastan de una vez (**gasto**), y compra de maquinaria o vehículos (**activo fijo**).

**D4 — ¿Todo se compra al 15 %?**
¿Hay artículos del almacén que **no pagan ISV** o que pagan la **tasa selectiva del 18 %**? Si los hay, ¿quién clasifica cada artículo y con qué criterio?

**D5 — ¿El ISV lo digitamos o lo calcula el sistema?**
¿El ISV de la factura de compra se digita **tal como viene** en el documento del proveedor, o el sistema lo calcula y el usuario solo confirma? Si difieren por centavos porque el proveedor redondea línea por línea: proponemos que **mande siempre el de la factura** y que el sistema bloquee si la diferencia pasa de **L 1.00**. ¿Le parece bien esa tolerancia?

**D6 — ¿Desde cuándo rige? [bloqueante]**
¿Desde qué fecha rige lo que configuremos? ¿Aplica también a las compras ya registradas o solo a las nuevas?
*Aviso técnico:* el kardex es un libro **inmutable**. Cambiar el costo de un movimiento ya registrado exige contra-asientos; no se puede "recalcular".
*Nota:* para que ninguna factura vieja quede bloqueada, la primera política se abrirá con fecha amplia hacia atrás. No cambia ningún costo, solo evita que el sistema rechace documentos anteriores.

**D7 — Confirme o corrija estas tasas**
*(El equipo lleva la tabla ya investigada: código, tipo, porcentaje, número de decreto y fecha de vigencia.)* Le pedimos únicamente que **confirme o corrija**. Hoy el sistema tiene 15 % general, 18 % selectiva, exento y exonerado, todas con una fecha de vigencia puesta a ojo (01/01/2010) que hay que reemplazar por la real.

**D8 — ¿Tenemos exoneraciones vigentes?**
¿La empresa tiene alguna **resolución de exoneración vigente** del SAR — proyectos, convenios, donaciones — que aplique a compras concretas? Si la tiene: ¿el mismo material se compra a veces exonerado (dentro del convenio) y a veces gravado (fuera de él)?

**D9 — ¿Necesita el libro de compras del sistema?**
¿Necesita que el sistema produzca el **libro/registro de compras** con el ISV pagado del mes, para respaldar la declaración?

**D10 — Devoluciones y notas de crédito de proveedor**
Cuando devolvemos material a un proveedor o él emite una nota de crédito: ¿el ISV se revierte **en el mes de la devolución**, o hay que **rehacer la declaración del mes original**? ¿La devolución debe salir de bodega al costo con que entró, o al promedio del momento?

**D11 — Qué hacer con el saldo si algún día cambiamos de crédito fiscal a costo**
Si en el futuro se cambia de crédito fiscal a costo, la cuenta de ISV crédito fiscal queda con un **saldo acumulado que ya no se podrá compensar**. El inventario que está en bodega ya se costeó neto y no puede absorberlo. ¿Ese saldo se lleva a **gasto del período**, se **capitaliza contra inventario**, o se deja para arrastrar?
*El sistema le dará un reporte con ese saldo a la fecha de corte, pero el asiento de reclasificación lo hace usted: tiene consecuencias fiscales distintas según la opción.*

---

**Extra para la misma sesión:** la decisión D2 del plan de retenciones ([`docs/plan_retenciones_compromisos_proveedores.md`](../plan_retenciones_compromisos_proveedores.md)) es la misma pregunta que D1 vista desde el otro lado. Conviene resolverlas juntas: retenciones e ISV comparten base imponible y catálogo.
