# Carga inicial de existencias como movimiento de kardex — Diseño

Fecha: 2026-07-29 (rev. 3, tras verificación de hechos contra el código) · Rama: `Cambios_almacen2.0` · Estado: **Fases 1–7 implementadas en local** (sin commit). El costo de apertura depende de las decisiones D2/D4/D5/D6 del contador (§15); el mecanismo no queda bloqueado.

> **Estado de implementación (2026-07-31).** Fases 1 a 5 implementadas los días 2026-07-30. **Fases 6 y 7 implementadas el 2026-07-31** — van juntas por §13, y con el ajuste ya operativo desde la Fase 4. Suite de Almacén **144/144** contra el mirror. Faltan la Fase 0(b) (dimensionamiento que corre el usuario) y la Fase 8 (ejecución real del corte), más el SQL pendiente en el SRV.
>
> **Corrección de comportamiento hecha en la Fase 6:** `PostearAperturaAsync` ya **no** aplica el gate de `apertura_cerrada`. Lo aplicaba a todas las aperturas unitarias, lo que contradice §5.1 (`apertura_cerrada`) y §9: cerrar el corte habría dejado imposible dar de alta un artículo con existencia para siempre. La distinción par nuevo / preexistente no depende de la fecha sino del modo: `CargaInicialNueva` exige existencia previa 0, así que un par preexistente no puede colarse por esa vía. El gate sigue en `EjecutarLoteAsync` y `PostearConCostoManualAsync`, que son los del universo preexistente. Cubierto por `CargaInicialTests.AperturaUnitaria_ConElCorteCerrado_SigueFuncionandoParaParesNuevos` y `EjecutarLote_ConElCorteCerrado_Lanza`.
>
> **Desvío consciente en la UI de la Fase 7:** §5.11 pedía "grid editable de sin costo". Se implementó como una **tabla de captura** (no un `DxGrid`) debajo del resumen, porque el grid estándar del proyecto no es editable en línea y montar edición sobre él para una columna no compensa; el grid de pendientes sí sigue el estándar (`PageSize 15`, selector, `@ref` + Columnas, `LayoutAutoSaving/Loading`, `ToolbarTemplate` con contador, `DxToastProvider StickToViewport`).

> **Cambios de la revisión 3.** Se contrastaron ~79 afirmaciones del documento contra el árbol posterior al commit `40bd948`. Resultado: 6 refutadas y 7 con la cita corrida. Correcciones aplicadas, por gravedad:
> 1. **Decisión 12 / §5.10 — el argumento de seguridad de los permisos estaba invertido.** `ModuleAuthorize` hace *fallback* al permiso de módulo, así que un sub-recurso dentro de `inventario` es un **superconjunto** de `module.inventario.create`, no una restricción. Reescrito.
> 2. **Decisión 13 / §5.8 — la guarda tapaba una de DOS puertas.** La rama de reactivación de `AddAsync` es la segunda, hoy tapada *por accidente*; el propio §5.8 mandaba destaparla. Corregido.
> 3. **§5.7** — hoy son **cinco** llamadas al rollup, no cuatro (el commit de hoy añadió dos).
> 4. **§5.9** — el descuadre **no** se enciende para "todo artículo": existe un hueco silencioso cuando se filtra por bodega y no hay ubicación activa.
> 5. **§5.8 Camino A** — `CreateAsync` empieza en `:536`, no en `:633`; ahí va la transacción.
> 6. **§9 y §12** — el `[Range]` del DTO no protege al posteador, y la Fase 6 rompe cinco pruebas nominadas.

> **Cambios de la revisión 2** respecto de la propuesta original: la apertura retroactiva pasa de sumar a **reconciliar** (§5.3); `KardexService` gana un **punto de corte** para que el saldo corrido no mezcle el histórico SIMAFI (§5.9); el **documento de Ajuste** y la **reversa/reapertura** entran al alcance (§5.4, §5.5); la `fecha` del asiento distingue lote y alta unitaria (§5.3); el `uuid` de apertura gana un **discriminador de intento** (decisión 3); la política del ISV **sale** de este diseño (decisión 9); y el rollup se extrae a un servicio compartido que no pasa por la bitácora ni por `xmin` (§5.7).

## 1. Problema

Hoy la existencia de un artículo en una bodega se **teclea y se persiste directo**, sin dejar rastro documental:

- `ArticulosService.CreateAsync` crea el artículo con `existencia = ubicaciones.Sum(u => u.Existencia)` y `cantidad` igual ([SIAD.Services/Almacen/ArticulosService.cs:646-648](../../SIAD.Services/Almacen/ArticulosService.cs)) y una fila `alm_articulo_bodega` por ubicación con `existencia = u.Existencia` ([ArticulosService.cs:667](../../SIAD.Services/Almacen/ArticulosService.cs)). No abre transacción explícita: todo cuelga de un solo `SaveChangesAsync` ([ArticulosService.cs:683-684](../../SIAD.Services/Almacen/ArticulosService.cs)).
- `ArticuloUbicacionService.AddAsync` hace lo mismo al agregar una bodega — rama de fila nueva ([ArticuloUbicacionService.cs:134](../../SIAD.Services/Almacen/ArticuloUbicacionService.cs)) y rama de reactivación ([:106](../../SIAD.Services/Almacen/ArticuloUbicacionService.cs)) — y `UpdateAsync` la reescribe ([:190](../../SIAD.Services/Almacen/ArticuloUbicacionService.cs)); en los tres casos el rollup de cabecera se recalcula con `RecomputeArticuloAsync` ([:325-343](../../SIAD.Services/Almacen/ArticuloUbicacionService.cs)).
- **Ninguno de esos caminos escribe en `alm_kardex`.** Verificado: el único código del repo que inserta en esa tabla son dos tests (`SIAD.Tests/Almacen/KardexBodegaTests.cs:87` y `SIAD.Tests/Almacen/ArticuloDeleteGuardTests.cs:79`). En `SIAD.Services` la tabla solo se lee: `KardexService` y la guarda de cambio de tipo de `ArticulosService.cs:805`.

Tres consecuencias:

1. **La existencia no tiene respaldo.** No hay quién, cuándo, con qué documento ni a qué costo. Nadie puede auditar de dónde salió el número.
2. **El kardex nace descuadrado.** `KardexService` calcula un saldo corrido sobre todos los movimientos ([KardexService.cs:111-116](../../SIAD.Services/Almacen/KardexService.cs)) y lo compara contra `alm_articulo_bodega.existencia` ([:146-156](../../SIAD.Services/Almacen/KardexService.cs)); el DTO expone `SaldoDescuadrado` ([SIAD.Core/DTOs/Almacen/KardexArticuloDto.cs:47](../../SIAD.Core/DTOs/Almacen/KardexArticuloDto.cs)) y la pantalla pinta la tarjeta en amarillo y un aviso ([apc.Client/Pages/Almacen/KardexArticulo.razor:99](../../apc.Client/Pages/Almacen/KardexArticulo.razor), [:125-140](../../apc.Client/Pages/Almacen/KardexArticulo.razor)). El saldo del kardex arranca en un punto que nada explica.
3. **El costeo queda en cero.** `costo_promedio` y `ultimo_costo` de `alm_articulo_bodega` nacen en 0 por DEFAULT y están reservados al motor de posteo — los servicios los excluyen del DTO a propósito ([ArticuloUbicacionService.cs:110-112](../../SIAD.Services/Almacen/ArticuloUbicacionService.cs), [:194-195](../../SIAD.Services/Almacen/ArticuloUbicacionService.cs), [ArticulosService.cs:671-672](../../SIAD.Services/Almacen/ArticulosService.cs)). Como nadie los escribe, **la base de costeo real vale 0 en todo el inventario**. **⚠️ Precisado en rev.3:** no confundir con lo que ve el usuario — el KPI "Valor inventario" del maestro **no** usa `costo_promedio`, sino `Σ existencia × valor_unitario` de la cabecera ([ArticulosService.cs:64](../../SIAD.Services/Almacen/ArticulosService.cs)), es decir un precio de referencia heredado de SIMAFI. O sea: la pantalla muestra un número, pero **no hay costo promedio ponderado con el que valorar una salida ni con el que absorber una compra**. Ese es el argumento correcto, y sigue siendo válido.

Este diseño convierte esa inicialización en un **movimiento `CARGA_INICIAL` posteado en el kardex**, que además siembra el costo. Es la **Tarea 3.1** del plan del motor ([docs/plans/2026-07-14-motor-movimientos-almacen.md](2026-07-14-motor-movimientos-almacen.md), sección "FASE 3") y arrastra con ella la **Tarea 3.2** (cerrar la captura manual) y la **Tarea 3.3** (documento de Ajuste): si las tres no van juntas, o el kardex se vuelve a descuadrar con la primera alta posterior, o el módulo queda en solo lectura sin ninguna vía legítima de mover stock.

## 2. Alcance

Cubre:

- El asiento de apertura por par (artículo, bodega) y su siembra de `costo_promedio` / `ultimo_costo`.
- Los **dos caminos vivos** de captura: alta de artículo con bodegas y alta de bodega a un artículo existente.
- El cierre de la captura manual de `existencia` en esos dos caminos y en la edición de ubicación (**Tarea 3.2**).
- El **documento de Ajuste** (`AJUSTE`: entrada, salida y ajuste de valor), porque es el reemplazo legítimo de la captura manual que se cierra (**Tarea 3.3**). Sin él, la Fase 4 dejaría el almacén en solo lectura hasta que exista el módulo de compras.
- La **reversa** de un asiento y la **reapertura** de un par (§5.4): es lo que hace corregible un corte mal costeado.
- La **carga retroactiva** de los artículos históricos que ya tienen existencia sin kardex, en modo **reconciliación** (no suma).
- El **punto de corte en `KardexService`**: el saldo corrido se reinicia en el asiento `CARGA_INICIAL` del par, y el histórico SIMAFI anterior pasa a ser informativo (§5.9). Sin este cambio, postear la apertura **empeora** visiblemente la pantalla del kardex.
- La **ampliación de `KardexMovimientoDto`** con `DocumentoTipo`, `DocumentoId`, `ExistenciaResultante` y `CostoPromedioResultante`, para que la trazabilidad prometida llegue a la pantalla y no se quede en la BD.
- La **parametrización de la base del costo** de apertura.
- El motor mínimo de posteo, porque **no existe** y es dependencia dura (§5.3).
- El **rollup compartido** de cabecera, hoy encerrado en un método privado (§5.7).

**Fuera de alcance** — ver §14.

## 3. Decisiones tomadas

| # | Decisión | Elección |
|---|---|---|
| 1 | ¿La existencia inicial se sigue capturando en el alta, o se mueve a un documento aparte? | **Se sigue capturando en el alta**, pero cambia de naturaleza: el campo deja de ser un valor persistido y pasa a ser la *cantidad* de un movimiento `CARGA_INICIAL` que se postea en la misma transacción. **No se crea tabla de documento nueva para la apertura.** |
| 2 | ¿Cuál es el "documento" de la apertura? | La propia fila `alm_articulo_bodega`: `documento_tipo = 'CARGA_INICIAL'`, `documento_id = alm_articulo_bodega.id`. Esa fila es única por `(company_id, articulo_id, bodega_id)` ([Database/2026-07-07_alm_articulo_bodega.sql:30](../../Database/2026-07-07_alm_articulo_bodega.sql)), así que sirve de clave documental sin inventar una tabla. |
| 3 | Idempotencia del uuid de apertura | `uuid = UUIDv5(ns_inventario, "CARGA_INICIAL\|{company_id}\|{articulo_id}\|{bodega_id}\|{intento}")`. **`intento` = 1 + número de aperturas de ese par que ya fueron revertidas** (§5.4). Es determinista: reintentar el mismo posteo recalcula el mismo `intento` (no hubo reversa nueva) y produce el mismo uuid, así que el índice único `uq_alm_kardex_company_uuid` lo absorbe; solo una reversa efectivamente posteada mueve el contador. El comentario de BD y el XML doc de la entidad, que hoy dicen que el uuid de `CARGA_INICIAL` sale de `(articulo_id, bodega_id)` ([Database/2026-07-14_alm_kardex_trazabilidad.sql](../../Database/2026-07-14_alm_kardex_trazabilidad.sql), `COMMENT ON COLUMN alm_kardex.uuid`; [SIAD.Core/Entities/alm_kardex.cs:45-52](../../SIAD.Core/Entities/alm_kardex.cs)), **se actualizan** en el script y en la entidad. |
| 4 | Cuántas aperturas por par artículo/bodega | **Exactamente una VIGENTE.** Vigente = un `CARGA_INICIAL` que no tiene su `REVERSA` posteada. Una apertura mal costeada se corrige con **reversa + nueva apertura** (`ReabrirAsync`, §5.4), atómica y permitida **solo si el par no tiene movimientos posteriores a la apertura**; si los tiene, la corrección es `AJUSTE` (§5.5). La guarda del posteador valida "no hay apertura **vigente**", no "no hay apertura nunca". |
| 5 | `tipo_transaccion` del asiento (hueco que el plan del motor no cerró) | **`'102'`** para la apertura, el código legacy de SIMAFI que ya significa *entrada / inventario inicial* ([SIAD.Core/DTOs/Almacen/TipoMovimientoKardex.cs:10-16](../../SIAD.Core/DTOs/Almacen/TipoMovimientoKardex.cs)); **`'103'`** para el ajuste; **`'202'`** para la reversa de una entrada. Cero cambios en el combo: `KardexService.GetTiposMovimientoAsync` lo arma con un `DISTINCT` sobre los valores existentes ([KardexService.cs:173-186](../../SIAD.Services/Almacen/KardexService.cs)) y los tres ya están en el histórico. **No** se agrega CHECK a `tipo_transaccion` (columna legacy de texto libre); la regla vive en una constante C#. La distinción real la lleva `documento_tipo`, que ahora sí llega a la pantalla (§5.9). |
| 6 | Apertura con costo 0 | **No se postea.** Se reporta y se resuelve con el usuario (regla del plan). No hay fallback silencioso. |
| 7 | Apertura con existencia negativa | **No se postea.** Se reporta como saneo previo obligatorio: una apertura negativa siembra un costeo imposible. El saneo se hace con el documento de `AJUSTE` (§5.5), que ya existe en esta entrega. |
| 8 | ¿La apertura genera partida contable? | **No en esta fase.** El plan del motor declara la contabilidad fuera de alcance y, sobre todo, el inventario que hoy figura en `alm_articulo_bodega` **ya viene del cierre de SIMAFI**: postear una partida lo duplicaría en el balance. La apertura es reconocimiento de un saldo ya contabilizado, no un hecho económico nuevo. Sujeto a confirmación (D3). |
| 9 | Dónde vive la política del ISV (al costo vs. crédito fiscal) | **Fuera de este diseño.** Va en el diseño de configuración de ISV, sobre la configuración fiscal/contable por empresa que ya existe (`con_empresa_configuracion`, `con_integracion_config`, `cfg_company` — las tres entidades están en `SIAD.Core/Entities/`). Meterla en una tabla de almacén obligaría a compras y contabilidad a depender del almacén para leer una política que no es de almacén. `alm_config_inventario` (§5.1) conserva **solo** lo que sí es de almacén: base del costo de apertura, declaración de base fiscal del corte, fecha de corte y cierre. Lo que el propio script de impuestos ya anticipó: las tasas son globales porque "la ley fija las tasas, no la empresa", y lo que es por empresa es qué tasa lleva cada artículo ([Database/2026-07-14_cfg_impuestos.sql:13-18](../../Database/2026-07-14_cfg_impuestos.sql)). |
| 10 | Retroactivo: ¿script SQL o servicio? | **Servicio**, ejecutado por lotes desde un endpoint one-shot. Un script SQL puro tendría que reimplementar UUIDv5 en plpgsql y duplicar la fórmula de costeo: dos implementaciones de la misma regla = divergencia garantizada. El SQL solo *selecciona* el universo (§6). |
| 11 | Fecha contable del asiento | **Depende del origen** (§5.3, campo `fecha`): (a) apertura del **lote retroactivo** → `fecha_corte_apertura`, parámetro obligatorio del proceso; (b) apertura **unitaria** de un par nuevo (alta de artículo o de bodega, Fases 4–5) → `DateOnly.FromDateTime(DateTime.Today)`. Fechar el alta de un artículo de seis meses después en la fecha de corte sería un anacronismo contable, y dejarla en NULL la esconde de la pantalla (ver §4). `fechacreacion` lleva la hora real del posteo, que es otra cosa. |
| 12 | Permiso del corte | **`POST cerrar` y `POST reabrir` van con `[ModuleAuthorize(PermissionModules.Configuracion)]` a secas.** Se agregan además `PermissionResources.Inventario.CargaInicial` / `.Ajustes` y sus constantes (hoy `PermissionResources` solo tiene `Ventas` y `Contabilidad`, y `PermissionNames.Inventario` solo los cuatro verbos de módulo, :153-158), más un array `Inventario` en [PermissionEndpointCatalog.cs](../../SIAD.Core/Constants/PermissionEndpointCatalog.cs) — hoy solo `Ventas` (:19) y `Contabilidad` (:517), agregados en `All` (:605). **⚠️ Corregido en rev.3:** el motivo original decía que el sub-recurso *impide* que un digitador con `module.inventario.create` valorice y cierre el corte. **Es falso.** [ModuleAuthorizeAttribute.cs:180-187](../../apc/Security/ModuleAuthorizeAttribute.cs) hace *fallback* explícito al permiso de módulo cuando el atributo lleva recurso, y `BuildPolicies` añade `BuildModulePermission` ([PermissionNames.cs:340-345](../../SIAD.Core/Constants/PermissionNames.cs)); la pantalla de roles ([RolesPortalForm.razor:265-269](../../apc.Client/Pages/Parametros/RolesPortalForm.razor)) también da por concedido el endpoint a quien tenga el permiso de módulo. Un sub-recurso de `inventario` es siempre un **superconjunto** de `module.inventario.create`, nunca una restricción. Para qué sirve entonces: para poder conceder permiso **fino** a quien *no* tiene el de módulo, y para que aparezca en la pantalla de roles. La **única** palanca que sí restringe con el `ModuleAuthorize` actual es sacar la acción del módulo `inventario` — de ahí Configuración. **Ojo:** `[ModuleAuthorize(Configuracion, "recurso")]` volvería a caer en `module.configuracion.create`; hay que usar el atributo **sin recurso**. |
| 13 | Filas de bodegas **inactivas** con existencia ≠ 0 | **El lote también las postea** (el universo de §6 ya no filtra `ab.activo`) y la guarda "no se puede devolver al rollup una fila con `existencia <> 0` sin apertura vigente" se aplica en **LAS DOS rutas que reactivan**. **⚠️ Corregido en rev.3:** la rev.2 solo contemplaba `ReactivarAsync` ([ArticuloUbicacionService.cs:278-300](../../SIAD.Services/Almacen/ArticuloUbicacionService.cs) ← [ArticulosController.cs:216](../../apc/Controllers/Almacen/ArticulosController.cs)). Hay una **segunda puerta**: la rama de reactivación de `AddAsync` (:98-123, `POST api/almacen/articulos/{articuloId}/ubicaciones`) pone `activo = true` en :100 y recomputa el rollup en :117. Hoy está tapada **por accidente**, porque :106 sobrescribe `existente.existencia` con el DTO (que lleva `[Range(0,…)]`) — y §5.8 manda quitar justamente esa escritura. Es decir: implementar §5.8 tal como estaba redactado **abriría** el agujero creyendo cerrarlo. La guarda se extrae a un método privado compartido y **ambos cambios van en el mismo commit**. Su conteo va en la Fase 0(b). |
| 14 | Rollup de cabecera | Se extrae a un servicio compartido `IArticuloRollupService` (§5.7) y se implementa **sin pasar por `SaveChanges`**, para no inundar la bitácora de maestros ni chocar contra el token `xmin` de `alm_articulo` durante el lote. |

> Las decisiones que dependen del contador están **al final**, en §15 (D1–D8), separadas a propósito de las que ya están tomadas.

## 4. Contexto (hallazgos verificados)

**La infraestructura del kardex ya está lista; falta el motor.**

- `alm_kardex` tiene las 8 columnas de trazabilidad (`uuid`, `documento_tipo`, `documento_id`, `bodega_destino_id`, `existencia_resultante`, `costo_promedio_resultante`, `usuariocreacion`, `fechacreacion`), el CHECK `ck_alm_kardex_documento_tipo` con **`CARGA_INICIAL` ya reservado** (vocabulario actual: `COMPRA, REQUISICION, DESCARGO, TRASLADO, AJUSTE, CARGA_INICIAL` — **sin `REVERSA`**), el índice único parcial `uq_alm_kardex_company_uuid`, `ix_alm_kardex_documento`, `ix_alm_kardex_saldo` sobre `(company_id, articulo_id, bodega_id, fecha, id)` y el trigger de inmutabilidad `trg_alm_kardex_inmutable` (BEFORE UPDATE OR DELETE, SQLSTATE `K0001`) — [Database/2026-07-14_alm_kardex_trazabilidad.sql](../../Database/2026-07-14_alm_kardex_trazabilidad.sql).
- Espejo en código: [SIAD.Core/Constants/TipoDocumentoInventario.cs:10-15](../../SIAD.Core/Constants/TipoDocumentoInventario.cs) (usar la constante, nunca el literal; `CargaInicial` en :15) y la entidad [SIAD.Core/Entities/alm_kardex.cs:45-77](../../SIAD.Core/Entities/alm_kardex.cs).
- **Ojo con los nombres**: el plan del motor pide `existencia_post` / `costo_promedio_post`; lo implementado se llama **`existencia_resultante` / `costo_promedio_resultante`**. Todo pseudocódigo copiado del plan no compila.
- **No existe columna `saldo`**: el saldo es corrido en memoria ([KardexService.cs:111-116](../../SIAD.Services/Almacen/KardexService.cs)) o snapshot en `existencia_resultante`, que hoy nadie escribe.
- `alm_kardex.fecha` es **`DateOnly?` nullable** ([alm_kardex.cs:23](../../SIAD.Core/Entities/alm_kardex.cs)). Un asiento con `fecha` NULL entra sin error, se ordena al principio (`OrderBy(k => k.fecha)`, [KardexService.cs:88](../../SIAD.Services/Almacen/KardexService.cs)) y **desaparece de cualquier filtro por rango** (`m.Fecha.HasValue`, [:123-131](../../SIAD.Services/Almacen/KardexService.cs)). De ahí la decisión 11.
- **No existe ningún SP ni función de posteo** de almacén en `Database/`. Las únicas funciones `alm_*` son los dos guardianes (`alm_kardex_inmutable`, `alm_documento_blindaje`). El posteo se implementa en C#.
- **No existe `IInventarioPostingService`** ni sus DTOs ni el enum `TipoMovimientoInventario`: la Fase 2 del plan del motor está sin escribir (§5.3 la cubre con el mínimo necesario).
- **No existe helper UUIDv5** en el repo, y .NET no lo trae de fábrica. Es pieza a escribir y a cubrir con vectores deterministas: de ella depende toda la idempotencia.
- `fechacreacion` de `alm_kardex` tiene DEFAULT en BD pero EF **no lo declara** ([SIAD.Data/SiadDbContext.Almacen.cs:145](../../SIAD.Data/SiadDbContext.Almacen.cs)) y no hay interceptor que lo estampe: EF mandará NULL explícito. El posteador setea `fechacreacion` y `usuariocreacion` a mano.
- Las FK de `alm_kardex` son **compuestas por tenant en la BD** (`(company_id, bodega_id)`, `(company_id, articulo_id)`, ambas `ON DELETE RESTRICT`) y **simples en EF** ([Database/2026-07-14_alm_fk_compuestas_tenant.sql:113-129](../../Database/2026-07-14_alm_fk_compuestas_tenant.sql) vs [SiadDbContext.Almacen.cs:147-161](../../SIAD.Data/SiadDbContext.Almacen.cs)). Un bug de tenant no lo atrapa EF: saldría como `23503` críptico. Se valida en el servicio.
- `alm_articulo` lleva **token de concurrencia optimista `xmin`** como propiedad sombra ([SiadDbContext.Almacen.cs:50-54](../../SIAD.Data/SiadDbContext.Almacen.cs)), así que cualquier UPDATE por `SaveChanges` sobre el maestro lleva `xmin` en el WHERE y puede lanzar `DbUpdateConcurrencyException`. `alm_articulo_bodega` **no** lo lleva.
- `alm_articulo` está en la **lista blanca de auditoría** de maestros ([SIAD.Core/Constants/AuditableMaestros.cs:22](../../SIAD.Core/Constants/AuditableMaestros.cs)) y lo captura el interceptor de `SaveChanges` ([SIAD.Services/Auditoria/BitacoraMaestrosInterceptor.cs:28-71](../../SIAD.Services/Auditoria/BitacoraMaestrosInterceptor.cs)), condicionado a `bitacora_maestro_config`. Cada recompute de cabecera hecho con `SaveChanges` produciría **una fila de bitácora por artículo tocado**.
- `RecomputeArticuloAsync` es **privado** de `ArticuloUbicacionService` ([:325-343](../../SIAD.Services/Almacen/ArticuloUbicacionService.cs)) y **no** está en `IArticuloUbicacionService`: un posteador nuevo no puede invocarlo. Además escribe tres campos, no uno: `existencia`, `existencia_minima` (Σ de mínimos de bodegas activas) y `cantidad = existencia` ([:339-341](../../SIAD.Services/Almacen/ArticuloUbicacionService.cs)). `ArticulosService` no tiene equivalente — por eso `CreateAsync` calcula el rollup a mano en :646-648.
- El patrón transaccional vigente del módulo (`IniciarTransaccionAsync` / `ConfirmarAsync`, [ArticuloUbicacionService.cs:312-319](../../SIAD.Services/Almacen/ArticuloUbicacionService.cs)) **reusa la transacción ambiente** y convierte el commit en no-op. Es lo que hace compatibles esos servicios con el fixture de tests, que envuelve cada prueba en `BEGIN … ROLLBACK` ([SIAD.Tests/Infrastructure/IntegrationTestBase.cs:22](../../SIAD.Tests/Infrastructure/IntegrationTestBase.cs), :30). Un servicio que abre transacción incondicionalmente queda fuera de cobertura — precedente documentado en [SIAD.Tests/Auditoria/ProveedorAuditTests.cs:80](../../SIAD.Tests/Auditoria/ProveedorAuditTests.cs).
- `KardexMovimientoDto` **no expone** `documento_tipo`, `documento_id`, `existencia_resultante` ni `costo_promedio_resultante` ([SIAD.Core/DTOs/Almacen/KardexMovimientoDto.cs](../../SIAD.Core/DTOs/Almacen/KardexMovimientoDto.cs)) y la proyección tampoco los trae ([KardexService.cs:90-107](../../SIAD.Services/Almacen/KardexService.cs)). Con `tipo_transaccion = '102'`, `TipoMovimientoKardex.Describir` rotula "Entrada" — idéntico a cualquier entrada del histórico SIMAFI.
- `KardexMovimientoDto.Saldo` es `decimal` **no nullable** y se renderiza con `@m.Saldo.ToString("N2")` en dos páginas ([apc.Client/Pages/Almacen/KardexArticulo.razor:244-248](../../apc.Client/Pages/Almacen/KardexArticulo.razor), [ArticuloMovimientosPanel.razor:148-152](../../apc.Client/Pages/Almacen/ArticuloMovimientosPanel.razor)): volverlo nullable obliga a tocar las dos.
- `ArticuloUbicacionDto.Existencia` ya tiene `[Range(0, …)]`, así que un negativo **no** puede llegar por el DTO ([SIAD.Core/DTOs/Almacen/ArticuloUbicacionDto.cs](../../SIAD.Core/DTOs/Almacen/ArticuloUbicacionDto.cs)); los negativos que existan vienen del histórico migrado.
- Los textos legacy `alm_kardex.linea` / `linea_desc` están **congelados** por la unificación línea→tipo ([docs/plans/2026-07-16-unificacion-linea-tipo-articulo-plan.md:22-24](2026-07-16-unificacion-linea-tipo-articulo-plan.md)): la apertura **no los puebla**.
- El backfill de `articulo_id` cubrió 47.203 de 47.215 filas; quedan **12 asientos huérfanos** sin `articulo_id` ni `codigo_articulo` ([Database/2026-07-14_alm_kardex_fk_articulo_restrict.sql:20-23](../../Database/2026-07-14_alm_kardex_fk_articulo_restrict.sql)). Cualquier invariante global tiene que excluirlos o reportará descuadre permanente.
- La existencia por bodega se backfilleó **desde la cabecera** hacia una bodega `PRIN` ([Database/2026-07-07_alm_articulo_bodega_backfill_existencia.sql:23-29](../../Database/2026-07-07_alm_articulo_bodega_backfill_existencia.sql)). Es el hecho que obliga a que la apertura retroactiva sea **reconciliación y no suma** (§5.3).
- **Los scripts de kardex/posteo no figuran en el runbook SRV vigente** ([Database/2026-07-23_runbook_despliegue_srv.md](../../Database/2026-07-23_runbook_despliegue_srv.md), cuyos pasos de almacén son los de `2026-07-16` y el `2026-07-29_alm_articulo_activo`). Presunción razonable: se aplicaron antes del 2026-07-23; el propio runbook advierte que no se verificó contra el servidor en vivo. **Confirmar antes de escribir una línea de motor** (Fase 0).

**Sobre el módulo de impuestos** (relevante para §7): `cfg_impuesto` / `cfg_impuesto_tasa` existen, tienen CRUD completo, vigencias sin solape y `GetTasasVigentesAsync(fecha)` documentado como "lo que el motor de cálculo debe consultar". Pero **nadie los consume**, `alm_articulo` **no tiene columna de impuesto ni de tasa**, y el uso `'ISV'` de `con_integracion_cuenta` está sembrado a `21105010000 Impuestos por Pagar` — un **pasivo**, es decir ISV de ventas, no crédito fiscal de compras.

## 5. Arquitectura

### 5.1 Base de datos — `Database/2026-07-29_alm_carga_inicial.sql` (aditivo)

Estilo obligatorio del repo: encabezado con Fecha + **Regla DB Mirror**, bloque POR QUÉ, idempotencia (`IF NOT EXISTS` / `ON CONFLICT`), `BEGIN … COMMIT`, `COMMENT ON` en tabla y columnas, y bloque `VERIFICACION` comentado al final. Pasa por la skill **guardia-estructura-bd** (tarjeta **verde**: es aditivo; el único DROP es el del CHECK `ck_alm_kardex_documento_tipo` para ampliarlo a un superconjunto). **El usuario lo aplica**: mirror `siad_v3_restore` @localhost → SRV. Se registra en el runbook con la skill **runbook-despliegue-srv**.

**(a) Tabla nueva `alm_config_inventario`** — una fila por empresa, tenant-scoped:

| columna | tipo | nota |
|---|---|---|
| `company_id` | BIGINT PK NOT NULL | PK = la empresa. Implementa `ICompanyScopedEntity`. |
| `base_costo_apertura` | VARCHAR(20) NOT NULL DEFAULT `'VALOR_UNITARIO'` | CHECK ∈ {`VALOR_UNITARIO`, `MANUAL`}. De dónde sale el costo de apertura. |
| `costo_apertura_incluye_isv` | BOOLEAN NOT NULL DEFAULT `false` | Declara en qué base está el costo con el que se sembró la apertura (D2). Se congela al ejecutar el corte y se copia a la `observacion` de cada asiento. |
| `fecha_corte_apertura` | DATE NULL | Fecha contable del **lote** (D4). NULL = lote no ejecutado. **No** es la fecha de las aperturas unitarias (decisión 11). |
| `apertura_cerrada` | BOOLEAN NOT NULL DEFAULT `false` | `true` = el corte se dio por terminado; a partir de ahí **ninguna** apertura nueva se acepta para pares preexistentes. Los pares creados después siguen abriendo normalmente, y `ReabrirAsync` sigue disponible con permiso de Configuración. |
| auditoría | `usuariocreacion`, `fechacreacion`, `usuariomodificacion`, `fechamodificacion` | Patrón del repo. |

`isv_tratamiento` **no va aquí** (decisión 9). Semilla idempotente: una fila por cada `company_id` presente en `alm_articulo` (`ON CONFLICT DO NOTHING`).

Siguiendo el precedente de `cfg_impuesto_tasa`, en EF **no** se usa `HasDefaultValue` en los booleanos ni en los CHECK-eados, para que el INSERT lleve siempre el valor explícito ([SIAD.Data/SiadDbContext.Impuestos.cs:27-30](../../SIAD.Data/SiadDbContext.Impuestos.cs)).

**(b) Tabla nueva `alm_ajuste_inventario`** — el documento de la Tarea 3.3, plano (una fila por línea de ajuste; una cabecera con numeración formal puede venir después sin romper nada):

| columna | tipo | nota |
|---|---|---|
| `id` | SERIAL PK | `documento_id` del asiento. |
| `company_id` | BIGINT NOT NULL | tenant. |
| `articulo_id`, `bodega_id` | INT NOT NULL | FK **compuestas** `(company_id, articulo_id)` / `(company_id, bodega_id)`, `ON DELETE RESTRICT`, contra las claves alternas `uq_alm_articulo_company_id` / `uq_alm_bodega_company_id` que ya existen ([2026-07-14_alm_fk_compuestas_tenant.sql:105-106](../../Database/2026-07-14_alm_fk_compuestas_tenant.sql)). |
| `clase` | VARCHAR(10) NOT NULL | CHECK ∈ {`ENTRADA`, `SALIDA`, `VALOR`}. |
| `cantidad` | NUMERIC(15,2) NOT NULL DEFAULT 0 | CHECK: `> 0` en ENTRADA/SALIDA, `= 0` en VALOR. |
| `costo_unitario` | NUMERIC(12,4) NOT NULL DEFAULT 0 | CHECK `> 0` en ENTRADA y VALOR; en SALIDA lo fija el motor al promedio y se ignora. |
| `motivo` | VARCHAR(120) NOT NULL | CHECK `length(btrim(motivo)) > 0`. Texto libre; el catálogo de motivos es trabajo posterior (§14). |
| `observacion` | VARCHAR(254) NULL | |
| `posteado` | BOOLEAN NOT NULL DEFAULT false | Se marca en la misma transacción del posteo. |
| auditoría | `usuariocreacion`, `fechacreacion` | |

**(c) Ampliación del vocabulario de `documento_tipo`** — se agrega `REVERSA`:

```sql
ALTER TABLE alm_kardex DROP CONSTRAINT IF EXISTS ck_alm_kardex_documento_tipo;
ALTER TABLE alm_kardex ADD CONSTRAINT ck_alm_kardex_documento_tipo CHECK (
    documento_tipo IS NULL OR documento_tipo IN
    ('COMPRA','REQUISICION','DESCARGO','TRASLADO','AJUSTE','CARGA_INICIAL','REVERSA')
);
```

Es un **superconjunto** del CHECK vigente, así que el escaneo de validación no puede fallar si el CHECK actual está aplicado (lo confirma la Fase 0(a)). Va sin `NOT VALID`. Se sincroniza `TipoDocumentoInventario` (constante + array `Todos`).

**(d) Dos CHECK nuevos en `alm_kardex`, ambos `NOT VALID`**:

```sql
-- (1) uuid y documento_tipo son EL MISMO discriminador de "libro nuevo".
ALTER TABLE alm_kardex ADD CONSTRAINT ck_alm_kardex_libro_nuevo
    CHECK ((uuid IS NULL) = (documento_tipo IS NULL)) NOT VALID;

-- (2) todo asiento del libro nuevo tiene fecha contable.
ALTER TABLE alm_kardex ADD CONSTRAINT ck_alm_kardex_fecha_si_uuid
    CHECK (uuid IS NULL OR fecha IS NOT NULL) NOT VALID;
```

El primero cierra dos agujeros a la vez: un asiento nuevo **sin uuid** quedaría fuera de la idempotencia e indistinguible del histórico, y un asiento **con uuid pero sin `documento_tipo`** quedaría fuera de `ix_alm_kardex_carga_inicial` y de la guarda "ya tiene apertura" pero **sí** entraría en el invariante de §8 — dos definiciones distintas de "libro nuevo" conviviendo. Con el CHECK bidireccional, cualquiera de las dos columnas sirve como discriminador.

El segundo es la contraparte en BD de la decisión 11: la BD deja de aceptar en silencio un asiento del motor sin fecha contable.

**Por qué `NOT VALID`**: la afirmación "todo el histórico tiene `documento_tipo` NULL" es plausible (ningún código de producción escribe esa columna — verificado, §1) pero **no está comprobada contra el SRV**. Una sola fila incompatible —por una corrección manual pasada, por la escotilla de escape documentada en el propio script de trazabilidad, o por otro entorno— abortaría el `ALTER` en plena ventana de despliegue y con él toda la transacción del script. Con `NOT VALID` el `ALTER` es instantáneo y no escanea. La validación va como **paso propio del runbook**, después de los pre-chequeos de la Fase 0(b):

```sql
ALTER TABLE alm_kardex VALIDATE CONSTRAINT ck_alm_kardex_libro_nuevo;
ALTER TABLE alm_kardex VALIDATE CONSTRAINT ck_alm_kardex_fecha_si_uuid;
```

**(e) Índices parciales de apoyo**:

```sql
-- Reporte de pendientes y guarda "ya tiene apertura".
CREATE INDEX IF NOT EXISTS ix_alm_kardex_carga_inicial
    ON alm_kardex (company_id, articulo_id, bodega_id)
    WHERE documento_tipo = 'CARGA_INICIAL';

-- "¿este asiento ya fue revertido?" (§5.4) y cálculo del discriminador de intento.
CREATE INDEX IF NOT EXISTS ix_alm_kardex_reversa
    ON alm_kardex (company_id, documento_id)
    WHERE documento_tipo = 'REVERSA';
```

**(f) Comentarios que se actualizan** (la mecánica del uuid cambió, decisión 3): `COMMENT ON COLUMN alm_kardex.uuid` y `COMMENT ON COLUMN alm_kardex.documento_tipo` se reescriben para incluir el discriminador de intento y el valor `REVERSA`. El XML doc de [alm_kardex.cs:45-52](../../SIAD.Core/Entities/alm_kardex.cs) se actualiza en el mismo commit.

**(g) Lo que NO se toca**: `alm_kardex` no gana columnas nuevas (todo lo que la apertura necesita ya existe), `alm_articulo_bodega` no gana columnas, y `tipo_transaccion` sigue sin CHECK.

**Orden de aplicación**: este script depende de que ya estén aplicados `2026-07-09_alm_kardex_bodega_id.sql`, `2026-07-13_alm_kardex_articulo_id.sql` y los cuatro de `2026-07-14` (trazabilidad, ampliar precisiones, FK RESTRICT, FK compuestas). Esos scripts **dejan de ser re-ejecutables** una vez activo `trg_alm_kardex_inmutable`, porque hacen UPDATE de backfill y fallan con `K0001`.

### 5.2 Entidades y contexto

- `SIAD.Core/Entities/alm_config_inventario.cs` y `alm_ajuste_inventario.cs` — ambas implementan `ICompanyScopedEntity` (filtro global + stamping vía `SiadDbContext.Tenancy.cs`).
- Configuración fluent en el partial de almacén ([SIAD.Data/SiadDbContext.Almacen.cs](../../SIAD.Data/SiadDbContext.Almacen.cs)), nunca en el cuerpo scaffolded.
- `SIAD.Core/Constants/TipoTransaccionKardex.cs` (nuevo): `EntradaInventarioInicial = "102"`, `Ajuste = "103"`, `Salida = "202"`. Cierra el hueco del plan (decisión 5) y evita literales sueltos. El describidor de UI ya existe (`TipoMovimientoKardex.Describir`).
- `TipoDocumentoInventario` gana `Reversa = "REVERSA"` y entra en `Todos`.
- `SIAD.Core/Utils/UuidV5.cs` (nuevo): RFC 4122 v5 (SHA-1) determinista, con el namespace de inventario como constante (`UuidNamespaces.Inventario`, GUID fijo y documentado). Es la pieza de la que depende toda la idempotencia.

### 5.3 Motor mínimo de posteo — dependencia dura

**No se puede construir la carga inicial sin construir antes el motor.** Se implementa la Fase 2 del plan, **acotada a `CargaInicial`, `Ajuste*` y `Reversa`**, con los contratos completos para que las fases siguientes (compras, requisiciones, traslados) no tengan que refactorizarla:

- `SIAD.Core/DTOs/Almacen/TipoMovimientoInventario.cs` — enum del plan. En esta entrega se implementan `CargaInicial`, `AjustePositivo`, `AjusteNegativo`, `AjusteValor` y `Reversa`; el resto lanza `NotSupportedException` con mensaje explícito.
- `SIAD.Core/DTOs/Almacen/MovimientoInventarioDto.cs` y `PosteoResultDto.cs`.
- `SIAD.Services/Almacen/IInventarioPostingService.cs` + `InventarioPostingService.cs`, registrado en [SIAD.Services/ServiceRegistration.cs](../../SIAD.Services/ServiceRegistration.cs) junto a los demás servicios de almacén (:108-117).

**Los dos modos de `CargaInicial`.** Esta es la corrección central de la revisión 2, y la razón por la que el paso 5 ya no dice `+=`:

| modo | cuándo | existencia previa esperada | cantidad del asiento | efecto sobre `alm_articulo_bodega.existencia` |
|---|---|---|---|---|
| `AperturaNueva` | alta de artículo o de bodega (Fases 4–5); la fila acaba de nacer | **0** (se valida; si no lo es, error de negocio) | la tecleada por el usuario | `existencia := 0 + cantidad` |
| `AperturaReconciliacion` | lote retroactivo (Fase 6); la fila **ya tiene** la existencia backfilleada | la que tenga la fila, `<> 0` | **la existencia leída bajo `FOR UPDATE`**, no un parámetro externo | `existencia := existencia` (no cambia) |

En reconciliación el asiento **describe** lo que ya hay: `cantidad = ingresos = E`, `existencia_resultante = E`. Lo único que cambia en la fila es `costo_promedio` y `ultimo_costo`. Si el llamador pasa una cantidad distinta de la existencia leída, se rechaza: la reconciliación no acepta que le dicten la cifra.

Un `+=` global sobre el universo de §6 dejaría `existencia = 2×E` en cada fila histórica, con `existencia_resultante` mintiendo lo mismo y el rollup de cabecera duplicado — y como `alm_kardex` es inmutable (`K0001`), **un lote ejecutado así no se puede deshacer con un UPDATE**. El plan del motor prescribe exactamente lo contrario: "cantidad = existencia actual … deja el kardex con un asiento de apertura que **cuadra** con los saldos" ([docs/plans/2026-07-14-motor-movimientos-almacen.md](2026-07-14-motor-movimientos-almacen.md), Tarea 3.1).

**Secuencia obligatoria de `PostearAsync`, toda dentro de una sola transacción** (es la del llamador si ya hay una abierta, siguiendo el patrón `IniciarTransaccionAsync` de [ArticuloUbicacionService.cs:312-319](../../SIAD.Services/Almacen/ArticuloUbicacionService.cs) — indispensable para que los tests puedan cubrirlo):

1. **Derivar el `uuid`** (decisión 3 para apertura; §5.4 para reversa; `AJUSTE\|{company}\|{ajuste_id}` para ajuste) y consultar `uq_alm_kardex_company_uuid`. Si ya existe → devolver el resultado previo, **no** duplicar.
2. **Bloquear la fila**: `SELECT … FROM alm_articulo_bodega WHERE company_id = {companyId} AND id = {id} FOR UPDATE` vía `FromSqlInterpolated`, con **`company_id` dentro del SQL crudo**, resuelto con `ICurrentCompanyService`. No basta con el filtro global: `alm_articulo_bodega` implementa `ICompanyScopedEntity` ([SIAD.Core/Entities/alm_articulo_bodega.cs:12](../../SIAD.Core/Entities/alm_articulo_bodega.cs)) y EF compone su filtro de tenant **por encima** del SQL crudo, de modo que el candado se tomaría antes de filtrar por empresa. El `FOR UPDATE` **no es opcional**: sin él dos posteos concurrentes calculan el mismo `existencia_resultante` y el mismo promedio, y el índice único no los detiene porque sus uuid difieren.
3. **Validar**: la fila existe; pertenece al tenant (`ICurrentCompanyService`, nunca el `companyId` del body); bodega y artículo del mismo tenant; **`fecha != null`** (decisión 11); reglas por tipo de movimiento —
   - `AperturaNueva`: existencia previa = 0, cantidad > 0, costo > 0, y el par **no tiene apertura vigente**;
   - `AperturaReconciliacion`: existencia previa ≠ 0 y > 0, costo > 0, y el par **no tiene apertura vigente**;
   - `AjustePositivo/Negativo`: cantidad > 0; en negativo, que la existencia resultante no quede negativa;
   - `AjusteValor`: cantidad = 0, costo > 0;
   - `Reversa`: el asiento a revertir existe, es del mismo tenant y **no fue revertido ya**.

   "Apertura vigente" = existe un `CARGA_INICIAL` del par **sin** su `REVERSA` posteada (decisión 4). La consulta va cubierta por `ix_alm_kardex_carga_inicial` + `ix_alm_kardex_reversa`.
4. **Calcular.** `CargaInicial` es ingreso sobre existencia 0 (modo nuevo) o siembra directa (modo reconciliación): en ambos cae en el borde documentado del plan, `costo_promedio = costo_entrada` (nunca dividir por cero) y `ultimo_costo = costo_entrada`. El resultado del cálculo es siempre el par `(existencia_resultante, costo_promedio_resultante)`.
5. **Aplicar sobre `alm_articulo_bodega`**: `existencia := existencia_resultante`, `costo_promedio := costo_promedio_resultante`, `ultimo_costo` cuando el movimiento lo fije. **Asignación, nunca `+=`**: el delta ya está dentro del cálculo del paso 4 y depende del tipo de movimiento. **No** toca `existencia_comprometida` ni `existencia_transito` (esas se derivan de documentos abiertos, son caché reconstruible).
6. **`Add(new alm_kardex{…})`** — nunca UPDATE, jamás tracking sobre una entidad de kardex (un `SaveChanges` con una propiedad modificada dispara UPDATE y revienta con `K0001`). Toda lectura de kardex, `AsNoTracking`.
7. **Rollup a `alm_articulo`** vía `IArticuloRollupService.RecomputeAsync` (§5.7), **fuera de `SaveChanges`**. En reconciliación el rollup no cambia nada (la existencia no se movió), pero se ejecuta igual: es idempotente y deja la cabecera cuadrada aunque venga descuadrada de antes.
8. **Un solo `SaveChangesAsync`** para las entidades trackeadas (kardex + fila de bodega + documento de ajuste). El rollup del paso 7 es un statement aparte, dentro de la misma transacción.

**Campos del asiento de apertura**:

| campo | valor |
|---|---|
| `company_id` | estampado por `SiadDbContext.Tenancy.cs` |
| `articulo_id`, `bodega_id` | los del par |
| `codigo_articulo` | snapshot del código actual (la referencia real es `articulo_id`) |
| `fecha` | **lote** → `alm_config_inventario.fecha_corte_apertura` (obligatoria, se valida no nula); **unitaria** → `DateOnly.FromDateTime(DateTime.Today)` |
| `tipo_transaccion` | `'102'` |
| `documento_tipo` | `TipoDocumentoInventario.CargaInicial` |
| `documento_id` | `alm_articulo_bodega.id` |
| `uuid` | UUIDv5 con discriminador de intento (decisión 3) |
| `cantidad`, `ingresos` | cantidad de apertura (= existencia leída, en reconciliación) |
| `salidas` | 0 |
| `valor_unitario` | costo de apertura |
| `total`, `debe` | cantidad × costo |
| `haber` | 0 |
| `existencia_resultante` | existencia **después** del asiento (en reconciliación, la misma que antes) |
| `costo_promedio_resultante` | costo promedio después |
| `descripcion` | `"Carga inicial de existencias"` |
| `observacion` | modo (`nueva` / `reconciliación`) + origen del costo (`valor_unitario` / `manual`) + base ISV declarada + intento |
| `cuenta_contable` | `alm_articulo.cuenta_contable` si la tiene (snapshot informativo) |
| `es_ajuste` | `false` |
| `usuariocreacion`, `fechacreacion` | **a mano** (EF no aplica el DEFAULT, §4) |
| `numero_documento`, `bodega`, `departamento*`, `linea*`, `barrio`, `bodega_destino_id` | NULL / sin poblar (legacy congelado) |

### 5.4 Reversa y reapertura — cómo se deshace un corte mal costeado

Sin esto el corte es un disparo único e irrevocable, y "reanudable e idempotente" **no** es lo mismo que reversible.

**Asiento de reversa**:

| campo | valor |
|---|---|
| `documento_tipo` | `'REVERSA'` (valor nuevo del CHECK, §5.1c) |
| `documento_id` | **`alm_kardex.id` del asiento revertido** (auto-referencia; `documento_id` es polimórfico y no tiene FK) |
| `uuid` | `UUIDv5(ns, "REVERSA\|{company_id}\|{kardex_id_revertido}")` → una sola reversa por asiento, idempotente por construcción |
| `tipo_transaccion` | `'202'` si el asiento revertido era entrada; `'102'` si era salida (espeja el signo) |
| `fecha` | **la misma del asiento revertido**: el corte es un punto cero y moverlo de fecha rompería el orden del libro. La fecha real de la corrección queda en `fechacreacion` |
| `cantidad`, `salidas` | los `ingresos` del asiento revertido (y viceversa) |
| `existencia_resultante` | existencia previa − cantidad revertida |
| `costo_promedio_resultante` | 0 cuando la reversa deja el par en existencia 0 (caso de la apertura); si quedara existencia, el promedio no se toca |
| `descripcion` | `"Reversa del asiento #<id>"` |
| `es_ajuste` | `false` |

**`ReabrirAsync(articuloId, bodegaId, nuevoCosto, motivo, usuario, ct)`** — reversa + nueva apertura **en la misma transacción**:

1. Localiza la apertura vigente del par. Si no hay, error de negocio.
2. **Rechaza si el par tiene movimientos posteriores a esa apertura en el libro nuevo** (`uuid IS NOT NULL` y `(fecha, id) > (fecha, id)` de la apertura). Con movimientos posteriores la corrección no es reapertura: es `AJUSTE` (§5.5), porque revertir el punto cero dejaría colgando todo lo que vino después.
3. Postea la `REVERSA` (el par queda en existencia 0 y costo 0).
4. Postea la nueva `CargaInicial`. Como ya hay **una** apertura revertida, `intento = 2`, el uuid es distinto y el índice único no estorba. La guarda del paso 3 de §5.3 se satisface porque no hay apertura *vigente*.
5. Un solo commit. Si algo falla, no queda ni la reversa ni la reapertura.

**Permiso**: Configuración (decisión 12). **Sigue disponible después de `apertura_cerrada = true`** — es la única vía de corrección y bloquearla dejaría el sistema sin salida. Cada reapertura queda en el kardex con su motivo, que es la auditoría.

### 5.5 Documento de Ajuste (Tarea 3.3) — dentro del alcance

Se construye en la **misma entrega** que el cierre de la captura manual. Si no, la Fase 4 dejaría el almacén sin **ninguna** forma de registrar una entrada o salida de stock hasta que exista el módulo de compras (Fase 7), y el mensaje de error de §9 apuntaría a un documento inexistente.

- Tabla `alm_ajuste_inventario` (§5.1b), una fila por línea, con `motivo` obligatorio.
- Servicio `IAjusteInventarioService` con `CrearYPostearAsync(dto, usuario, ct)`: inserta la fila, obtiene el id, postea el asiento vía `IInventarioPostingService` y marca `posteado = true`, todo en una transacción.
- Tres clases:
  - **ENTRADA** (`AjustePositivo`, `tipo_transaccion='103'`, `es_ajuste=true`): `existencia +`, recalcula promedio ponderado con el costo tecleado.
  - **SALIDA** (`AjusteNegativo`): `existencia −`, valorizada **al promedio vigente** (no al costo tecleado); no cambia el promedio. Es la vía para sanear negativos y para dejar en cero una bodega antes de deshabilitarla ([ArticuloUbicacionService.cs:251-257](../../SIAD.Services/Almacen/ArticuloUbicacionService.cs) ya lo exige y ya lo dice en su mensaje).
  - **VALOR** (`AjusteValor`): `cantidad = 0`, `ingresos = salidas = 0`; no mueve unidades, reescribe `costo_promedio` y `ultimo_costo`. `total` = diferencia de valorización. Es lo que permite corregir el costo de un par que **sí** tiene movimientos posteriores, sin tocar el invariante de unidades de §8.
- `documento_tipo = 'AJUSTE'`, `documento_id = alm_ajuste_inventario.id`, `uuid = UUIDv5(ns, "AJUSTE\|{company_id}\|{ajuste_id}")`.
- UI mínima: modal desde el tab de ubicaciones ("Registrar ajuste") + endpoint `POST api/almacen/ajustes`.

### 5.6 Servicio de carga inicial

`SIAD.Services/Almacen/CargaInicialInventarioService.cs` (`ICargaInicialInventarioService`), registrado en `ServiceRegistration.cs`:

- `PostearAperturaAsync(articuloId, bodegaId, cantidad, costo, usuario, ct)` — apertura unitaria en modo `AperturaNueva`; la usan los dos caminos de captura (§5.8). Fecha = hoy.
- `GetPendientesAsync(filtro, ct)` — universo de filas sin apertura vigente, clasificadas: **posteables**, **sin costo** (`valor_unitario = 0`), **negativas**, **artículo descontinuado**, **bodega inactiva**.
- `SimularLoteAsync(fechaCorte, ct)` — *dry-run*: cuántas filas, cuántas de cada clase, valor total que se sembraría, y cuántos artículos distintos tocaría el rollup. **No escribe nada.**
- `EjecutarLoteAsync(fechaCorte, tamañoLote, ct)` — modo `AperturaReconciliacion`; procesa por lotes (propuesta: 200 filas), **cada lote en su propia transacción**, reanudable e idempotente (repetirlo no duplica: el uuid manda). Devuelve posteadas / omitidas con motivo (incluido "omitida por concurrencia", §9). `fechaCorte` es obligatoria y se persiste en `alm_config_inventario.fecha_corte_apertura`.
- `PostearConCostoManualAsync(items, usuario, ct)` — flujo de excepción de los artículos sin costo: recibe `{articuloId, bodegaId, costo}` **por par**, no por artículo. El costo tecleado **no se guarda en ninguna tabla intermedia**: entra directo al asiento, que es el registro.
- `ReabrirAsync(...)` — §5.4.
- `CerrarAperturaAsync(usuario, ct)` — marca `apertura_cerrada = true` tras verificar el gate (§8).

### 5.7 Rollup compartido de cabecera

`RecomputeArticuloAsync` es privado y no está en la interfaz ([ArticuloUbicacionService.cs:325-343](../../SIAD.Services/Almacen/ArticuloUbicacionService.cs)): el posteador **no puede llamarlo**. Se extrae:

- `SIAD.Services/Almacen/IArticuloRollupService.cs` + `ArticuloRollupService.cs`, con `RecomputeAsync(int articuloId, CancellationToken ct)`, registrado en `ServiceRegistration.cs`.
- Consumidores: `ArticuloUbicacionService` (**las CINCO llamadas actuales**, no cuatro — el commit `40bd948` añadió las dos últimas), `ArticulosService.CreateAsync` (que hoy calcula el rollup a mano en :646-648) y `InventarioPostingService`. Las cinco, con su línea de hoy:

  | # | Origen | Línea |
  |---|---|---|
  | 1 | `AddAsync` — rama de reactivación | `:117` |
  | 2 | `AddAsync` — fila nueva | `:147` |
  | 3 | `UpdateAsync` | `:200` |
  | 4 | `DeshabilitarAsync` | `:273` |
  | 5 | `ReactivarAsync` | `:297` |

  No es un detalle cosmético: si el refactor migra cuatro y olvida una, ese camino deja `alm_articulo.existencia` desincronizado de la Σ de bodegas activas — exactamente la condición que detecta el filtro "Con descuadre" ([ArticulosService.cs:118](../../SIAD.Services/Almacen/ArticulosService.cs)) y que el commit de hoy buscaba cerrar. Los dos candidatos a quedar fuera son precisamente los nuevos (4 y 5), que son los que más mueven el rollup.
- **Escribe tres campos**, igual que hoy: `existencia` = Σ de bodegas **activas**, `existencia_minima` = Σ de mínimos de bodegas activas, y `cantidad = existencia`. Esto importa para §5.8: al quitar la línea 648 de `CreateAsync`, `cantidad` queda en 0 y es el rollup quien la fija; por eso `CreateAsync` llama al rollup **siempre** al final, haya o no posteo.
- **Se implementa sin `SaveChanges`**, con `ExecuteUpdateAsync` sobre `_context.alm_articulos.Where(a => a.id == articuloId)` (el filtro global de tenant sí se aplica a `ExecuteUpdate`, que es una consulta LINQ). Dos razones:
  1. **Bitácora**: `alm_articulo` está en la lista blanca de auditoría ([AuditableMaestros.cs:22](../../SIAD.Core/Constants/AuditableMaestros.cs)) y el interceptor captura las entidades `Modified` en `SavingChanges` ([BitacoraMaestrosInterceptor.cs:40-71](../../SIAD.Services/Auditoria/BitacoraMaestrosInterceptor.cs)). Un lote de miles de pares generaría miles de filas de auditoría de algo que no es una edición de maestro sino un efecto de posteo. `ExecuteUpdateAsync` no pasa por `SaveChanges` y el interceptor no lo ve.
  2. **Concurrencia**: `alm_articulo` lleva token `xmin` ([SiadDbContext.Almacen.cs:50-54](../../SIAD.Data/SiadDbContext.Almacen.cs)); un UPDATE por `SaveChanges` durante el lote choca con cualquier usuario que guarde ese artículo en paralelo. `ExecuteUpdateAsync` no incluye el token.
- **Si EF no traduce la subconsulta agregada** dentro de `SetProperty`, el fallback es `ExecuteSqlInterpolated` con el `UPDATE … FROM (SELECT … GROUP BY …)` explícito **y `company_id` en el WHERE**: el SQL crudo de `ExecuteSql*` **no** pasa por el filtro global de tenant. Esa condición se prueba con un test de tenant.
- El comportamiento observable no cambia: el detector de descuadre del maestro sigue midiendo lo mismo ([ArticulosService.cs:66](../../SIAD.Services/Almacen/ArticulosService.cs), [:118](../../SIAD.Services/Almacen/ArticulosService.cs)).

### 5.8 Los dos caminos de captura — qué cambia exactamente

**Camino A — `ArticulosService.CreateAsync`** ([:536-688](../../SIAD.Services/Almacen/ArticulosService.cs) — **⚠️ corregido en rev.3**: la rev.2 citaba `:633-688`, pero `:633` es solo el `var entity = new alm_articulo`. El método **arranca en `:536`**, y entre `:536` y `:632` viven las validaciones que deciden si el artículo maneja inventario: `ValidarTipoArticuloAsync` (:547), rechazo de ubicaciones si no maneja inventario (:563-571), exigencia de al menos una bodega sin repetir (:575-588) y validación de bodegas activas (:613-624). Quien implemente leyendo solo `633-688` abrirá la transacción **después** de consultas ya hechas y se saltará el caso del tipo sin inventario):

- Las filas `alm_articulo_bodega` se crean con **`existencia = 0`** (se quita `existencia = u.Existencia` de la línea 667). `existencia_minima`, `existencia_maxima` y `punto_reorden` **siguen siendo configuración tecleada**: no cambian.
- La cabecera nace con `existencia = 0` y `cantidad = 0` (se quitan las líneas 646 y 648); `existencia_minima` puede seguir calculándose en línea (647) o dejarse al rollup — se deja al rollup, por coherencia.
- El método pasa a abrir **transacción explícita al inicio real (~`:536`, antes de las validaciones), no en `:633`**, reusando el patrón `IniciarTransaccionAsync` / `ConfirmarAsync` de `ArticuloUbicacionService` ([:312-319](../../SIAD.Services/Almacen/ArticuloUbicacionService.cs)) para no romper el fixture de tests. Ese patrón son dos métodos `private` de una clase `sealed` y **no** están en `IArticuloUbicacionService`: hay que **extraerlos a un helper compartido** en `SIAD.Services/Almacen` (encaja junto al `IArticuloRollupService` de §5.7) en vez de copiarlos en tres servicios.
- `PostearAperturaAsync` solo aplica cuando **`manejaInventario == true`**: un tipo sin inventario (ej. Servicios) no lleva bodegas ni kardex y no debe generar apertura.
- Tras el `SaveChanges` que asigna ids, por cada ubicación con `Existencia > 0` se llama `PostearAperturaAsync` **dentro de la misma transacción**. Si una falla, no se crea el artículo: todo o nada.
- Al final se llama **siempre** al rollup (§5.7), haya o no posteo, para que `cantidad` y `existencia_minima` queden correctas.

**Camino B — `ArticuloUbicacionService.AddAsync`** ([:72-153](../../SIAD.Services/Almacen/ArticuloUbicacionService.cs)):

- Rama **fila nueva** (:125): nace con `existencia = 0` y, si `dto.Existencia > 0`, se postea la apertura en la misma transacción.
- Rama **reactivación** (:98, `existente is not null`): **deja de escribir `existente.existencia`** (:106). Motivo: `DeshabilitarAsync` ya exige que la bodega esté en cero para poder deshabilitar ([:251-257](../../SIAD.Services/Almacen/ArticuloUbicacionService.cs)), así que una fila deshabilitada *después* de esa guarda viene en 0; y si el par ya tuvo apertura vigente, un segundo `CARGA_INICIAL` la rechaza la guarda del paso 3. Al reactivar, la existencia se conserva y el stock vuelve a entrar por **ajuste**, compra o traslado. La UI muestra el campo de solo lectura en ese caso.
- **Guarda de reactivación en LAS DOS rutas** (decisión 13): rechazar devolver al rollup una fila con `existencia <> 0` sin apertura vigente, con mensaje que remite al corte o al ajuste. Simétrica a la de `DeshabilitarAsync`. **⚠️ Corregido en rev.3:** no basta con `ReactivarAsync`. Al quitar la escritura de `:106` (viñeta anterior) se **destapa** la rama de reactivación de `AddAsync`, que hasta hoy quedaba a salvo solo porque sobrescribía la existencia con el DTO. Extraer la guarda a un método privado compartido y aplicarla en:
  1. `ReactivarAsync` (:278-300)
  2. la rama de reactivación de `AddAsync` (:98-123), justo antes de poner `activo = true` (:100)

  Los dos cambios —quitar la escritura y poner la guarda— **deben ir en el mismo commit**; separarlos deja una ventana en la que el stock vuelve al rollup sin asiento.

**`UpdateAsync`** ([:190](../../SIAD.Services/Almacen/ArticuloUbicacionService.cs)): `existencia` **deja de escribirse desde el DTO** y pasa al bloque de campos del motor. Es la Tarea 3.2; sin ella, la carga inicial caduca sola con la primera edición.

**DTO**: en `ArticuloUbicacionDto`, `Existencia` cambia de semántica (cantidad de apertura al crear; solo lectura después) y se agrega **`CostoApertura`** (`decimal`, obligatorio y > 0 cuando `Existencia > 0`, ignorado en cualquier otro caso). El bloque de comentario "Campos del motor de movimientos (Fase 2): SOLO LECTURA" se amplía para incluir `Existencia`.

### 5.9 `KardexService`: el punto de corte en la pantalla

**Sin este cambio, postear la apertura empeora la pantalla del kardex.** Hoy el saldo corrido suma **todos** los movimientos del par sin filtrar por `uuid` ni por `documento_tipo` ([KardexService.cs:76-84](../../SIAD.Services/Almacen/KardexService.cs) para el universo, [:111-116](../../SIAD.Services/Almacen/KardexService.cs) para el bucle). Los ~47,2k asientos migrados de SIMAFI entran en ese saldo. Al postear la apertura, `SaldoCalculado` pasaría a ser *histórico + apertura*, nunca igual a `ExistenciaBodega`, y `SaldoDescuadrado` ([KardexArticuloDto.cs:47](../../SIAD.Core/DTOs/Almacen/KardexArticuloDto.cs)) se encendería, pintando el aviso de [KardexArticulo.razor:125-142](../../apc.Client/Pages/Almacen/KardexArticulo.razor). Además, el `existencia_resultante` que escribe el motor contradiría el saldo que dibuja la UI.

**⚠️ Corregido en rev.3 — no es "todo artículo con histórico".** Hay un caso en que el aviso **no** se enciende por descuadrado que esté el saldo: `SaldoDescuadrado` exige `ExistenciaComparable.HasValue` ([KardexArticuloDto.cs:47](../../SIAD.Core/DTOs/Almacen/KardexArticuloDto.cs)), y `KardexService` devuelve `null` cuando **hay bodega filtrada y no existe fila `alm_articulo_bodega` ACTIVA** para ese par ([KardexService.cs:146-155](../../SIAD.Services/Almacen/KardexService.cs); el predicado incluye `u.activo`). Es una mordaza deliberada del commit de hoy (comentario en `:144-145`): sin cifra comparable no se afirma descuadre. Así que el argumento de urgencia de §5.9 aplica a **(a)** sin bodega filtrada y **(b)** con bodega filtrada y fila activa. Queda una **decisión abierta**: qué debe mostrar la pantalla para un par con apertura posteada pero **ubicación inactiva** — que es justo el universo de la decisión 13. El punto de corte se sigue necesitando; lo que se retira es el "todo artículo".

**Regla nueva** (`GetByArticuloAsync`):

1. La proyección trae también `documento_tipo`, `documento_id`, `existencia_resultante` y `costo_promedio_resultante`.
2. Para cada par `(articulo_id, bodega_id)` presente en el resultado se localiza su **asiento de corte**: el `CARGA_INICIAL` **vigente** de menor `(fecha, id)`. Si el par no tiene apertura, no hay corte y su saldo se comporta como hoy (compatibilidad hacia atrás, indispensable durante la transición).
3. El saldo corrido **arranca en 0 en el asiento de corte** de cada par. Los movimientos anteriores al corte de su par se devuelven con `Saldo = null` y `EsPreCorte = true`: son histórico informativo.
4. `SaldoCalculado` = Σ (ingresos − salidas) de los movimientos **no** pre-corte. Es lo que se compara contra `ExistenciaBodega` / `ExistenciaRegistrada`, y por construcción cuadra si el motor está bien.
5. Un par cuya apertura fue revertida y re-posteada toma como corte **la apertura vigente**: la apertura revertida y su `REVERSA` quedan del lado pre-corte, y el saldo no las cuenta dos veces.

**Cambios de contrato**:

- `KardexMovimientoDto`: `Saldo` pasa de `decimal` a `decimal?`; se agregan `DocumentoTipo`, `DocumentoId`, `ExistenciaResultante`, `CostoPromedioResultante` y `EsPreCorte`.
- Las dos páginas que renderizan `@m.Saldo.ToString("N2")` se ajustan a nullable: [KardexArticulo.razor:244-248](../../apc.Client/Pages/Almacen/KardexArticulo.razor) y [ArticuloMovimientosPanel.razor:148-152](../../apc.Client/Pages/Almacen/ArticuloMovimientosPanel.razor) muestran "—" en las filas pre-corte.
- La fila de la apertura se rotula visualmente como la línea de corte (*"Saldo inicial al &lt;fecha de corte&gt;"*) y las filas pre-corte se atenúan.
- `TotalIngresos` / `TotalSalidas` siguen sumando sobre la lista **filtrada** (comportamiento actual, [:167-168](../../SIAD.Services/Almacen/KardexService.cs)); se documenta que pueden incluir histórico pre-corte si el usuario no filtra por fecha.

### 5.10 API, cliente y permisos

- Endpoints existentes que cambian de comportamiento (sin cambiar de ruta ni de contrato HTTP): `POST api/almacen/articulos` y `POST|PUT api/almacen/articulos/{articuloId}/ubicaciones[/{id}]`, más `POST .../ubicaciones/{id}/reactivar` que gana la guarda ([apc/Controllers/Almacen/ArticulosController.cs:167-228](../../apc/Controllers/Almacen/ArticulosController.cs)).
- Controlador nuevo `apc/Controllers/Almacen/CargaInicialController.cs` — `[Route("api/almacen/carga-inicial")]`:
  - `GET pendientes` (filtros: bodega, clase de excepción) → `module.inventario.carga_inicial.view`
  - `GET simular?fechaCorte=` → `…carga_inicial.view`
  - `POST ejecutar` (body: fecha de corte, tamaño de lote) → `…carga_inicial.create`
  - `POST costo-manual` (body: lista de `{articuloId, bodegaId, costo}`) → `…carga_inicial.create`
  - `POST cerrar` → **`module.configuracion.create`**
  - `POST reabrir` → **`module.configuracion.create`**
  - `GET config` / `PUT config` → `module.configuracion.{view,edit}`
- Controlador nuevo `apc/Controllers/Almacen/AjustesInventarioController.cs` — `[Route("api/almacen/ajustes")]`, `POST` con `module.inventario.ajustes.create`.
- **Permisos (decisión 12)**: se agregan `PermissionResources.Inventario` (clase nueva) con `CargaInicial = "carga_inicial"` y `Ajustes = "ajustes"`; las constantes `module.inventario.carga_inicial.*` y `module.inventario.ajustes.*` en `PermissionNames.Inventario`; sus entradas en `Policies`; y un array `Inventario` en `PermissionEndpointCatalog` incorporado a `All` ([:605](../../SIAD.Core/Constants/PermissionEndpointCatalog.cs)).

  **⚠️ Corregido en rev.3 (dos errores de la rev.2):**
  1. *"Sin la entrada en el catálogo, `ModuleAuthorize` no puede derivar el recurso desde la ruta"* — **falso**. El atributo no menciona `PermissionEndpointCatalog` en ninguna línea: deriva el recurso en `ResolveEndpointResource` (:198-217) a partir del parámetro del atributo y `RoutePattern.RawText`. Lo que el catálogo sí alimenta es `PermissionNames.All` (:264), `BuildPolicies` (:338-354) y la pantalla de roles. **Consecuencia real de omitirlo**: el permiso no entra en `All`, así que `RolesPortalController.cs:212` lo rechaza al guardarlo en un rol y `DatabaseInitializer.cs:30` no lo siembra; tampoco habría policy registrada. Resultado: el permiso fino sería **inasignable** — que es motivo suficiente, pero es otro motivo.
  2. **Los permisos cortos no son los que genera el catálogo.** Con el patrón `Permission = BuildPermission(Module, Resource, Action)` y `Resource = "{opción}__{ruta_normalizada}"` ([PermissionEndpointCatalog.cs:15](../../SIAD.Core/Constants/PermissionEndpointCatalog.cs)), el permiso real de `GET api/almacen/carga-inicial/pendientes` sería `module.inventario.carga_inicial__almacen_carga_inicial_pendientes.view`. El corto (`module.inventario.carga_inicial.view`) es el permiso **de opción**, que actúa como fallback de recurso base. Si se quieren las constantes cortas como claims asignables hay que declararlas **a mano** en `PermissionNames.Inventario` y agregarlas a `BuildAll` (:236-261) y a `Policies` (:310-335): el `foreach` del catálogo (:338-354) no las crea solo.
- `apc.Client/Services/Almacen/CargaInicialClient.cs` y `AjusteInventarioClient.cs`, registrados en [apc.Client/CommonServices.cs](../../apc.Client/CommonServices.cs) (seguro en ambos hosts), usando los helpers `*WithAuthCheck` de `HttpClientExtensions`.

### 5.11 UI

- `ArticuloUbicacionesTab.razor` y `ArticuloForm.razor`: junto a "Existencia" aparece **"Costo de apertura"** (`DxSpinEdit`), obligatorio cuando la existencia es > 0 y visible **solo en el alta** del par artículo/bodega. En edición y en reactivación, ambos campos van de solo lectura con la leyenda *"La existencia se mueve con documentos de inventario"* y un botón **"Registrar ajuste"** que abre el modal de §5.5.
- Página nueva `apc.Client/Pages/Almacen/CargaInicialInventario.razor` — ruta `/almacen/carga-inicial`, ítem en [apc.Client/Layout/Navigation/SidebarNavigationDefinition.cs](../../apc.Client/Layout/Navigation/SidebarNavigationDefinition.cs) (el menú real; `NavMenu.razor` está muerto). Tres bloques: **resumen del dry-run**, **grid de pendientes** con las cinco clases, y **grid editable de "sin costo"** para capturar el costo por par y postear. Botones: *Simular*, *Ejecutar corte*, *Cerrar apertura* (con confirmación explícita).
- `KardexArticulo.razor`: columna **"Documento"** (`DocumentoTipo`), filas pre-corte atenuadas con `Saldo` vacío, y la fila de apertura rotulada como corte. Sin esto, el problema 1 de §1 queda resuelto en la BD pero no en la pantalla.
- Sigue el **estándar de grid** (`siad-grid.css` + referencia `ClientesList.razor`): `PageSize 15`, selector de página, `@ref` + botón "Columnas", `LayoutAutoSaving/Loading`, `ToolbarTemplate` con contador. `DxToastProvider` con `StickToViewport="true"`.
- **Obligatorio**: consultar el MCP `dxdocs` antes de tocar cualquier API de componente DevExpress.

## 6. Carga retroactiva del histórico

Es el mismo servicio, en modo lote y en **modo reconciliación** (decisión 10 + §5.3). No hay script de posteo: hay un **script de medición** y un **proceso reanudable**.

**Selección del universo** (SQL a alto nivel; la consulta real vive en el servicio, con el tenant resuelto por `ICurrentCompanyService`):

```sql
SELECT ab.id AS ubicacion_id, ab.articulo_id, ab.bodega_id, ab.activo AS ubicacion_activa,
       ab.existencia, a.valor_unitario, a.activo AS articulo_activo
FROM   alm_articulo_bodega ab
JOIN   alm_articulo a ON a.company_id = ab.company_id AND a.id = ab.articulo_id
WHERE  ab.company_id = :company
  AND  ab.existencia <> 0
  -- Sin apertura VIGENTE: existe CARGA_INICIAL y NO existe su REVERSA.
  AND  NOT EXISTS (
         SELECT 1 FROM alm_kardex k
         WHERE  k.company_id     = ab.company_id
           AND  k.articulo_id    = ab.articulo_id
           AND  k.bodega_id      = ab.bodega_id
           AND  k.documento_tipo = 'CARGA_INICIAL'
           AND  NOT EXISTS (SELECT 1 FROM alm_kardex r
                            WHERE r.company_id     = k.company_id
                              AND r.documento_tipo = 'REVERSA'
                              AND r.documento_id   = k.id))
ORDER BY ab.articulo_id, ab.bodega_id;
```

Nótese que **no** se filtra `ab.activo`: las filas de bodegas inactivas con existencia también se postean (decisión 13). Cubierta por `ix_alm_kardex_carga_inicial` y `ix_alm_kardex_reversa` (§5.1e).

Clasificación de cada fila:

| clase | criterio | acción |
|---|---|---|
| Posteable | `existencia > 0` y `valor_unitario > 0` | Se postea en modo reconciliación con costo = `valor_unitario` (o el manual si `base_costo_apertura = MANUAL`). |
| Sin costo | `existencia > 0` y `valor_unitario = 0` | **No se postea.** Va al flujo de excepción (D5). |
| Negativa | `existencia < 0` | **No se postea.** Saneo previo obligatorio con `AJUSTE` de entrada (D6). |
| Descontinuado | artículo con `activo = false` pero con existencia | **Sí se postea**: el soft-delete del maestro no borra stock. Se reporta aparte para que el usuario decida si primero lo descarga. |
| Bodega inactiva | `ab.activo = false` con existencia ≠ 0 | **Sí se postea** (decisión 13). Queda fuera del rollup de cabecera mientras siga inactiva, pero con asiento; y `ReactivarAsync` ya no puede devolverla al rollup sin respaldo. |

**Propiedades del proceso**: por lotes de N con transacción propia (no una transacción de horas sobre miles de filas); **idempotente** por uuid, así que se puede cortar y reanudar; el `usuariocreacion` de los asientos retroactivos es el usuario que ejecuta, y `observacion` deja constancia del modo, del origen del costo y de la base ISV declarada. La auditoría del corte **es el propio kardex**: no hace falta tabla de lote.

**El rollup no se dispara por fila**: en reconciliación la existencia no cambia, así que `RecomputeAsync` se invoca una vez por artículo al cerrar cada lote (idempotente), no una vez por par. Aun así se cuenta en la Fase 0(b) cuántos artículos distintos toca, para dimensionar el impacto (§5.7).

**Nota sobre el histórico SIMAFI**: los ~47,2k movimientos migrados tienen `uuid` NULL y `documento_tipo` NULL. **No se tocan y no cuentan** para el saldo del motor ni para la pantalla a partir del corte (§5.9). El asiento de apertura es el **punto cero** del nuevo libro; el histórico queda como consulta.

## 7. Parametrización del costo de apertura

El costo sale de `alm_config_inventario.base_costo_apertura`:

- `VALOR_UNITARIO` (default) → `alm_articulo.valor_unitario`, tal como manda el plan del motor.
- `MANUAL` → costo tecleado **por par artículo/bodega**, no por artículo (es lo que `PostearConCostoManualAsync(items)` permite por firma).

Y se **declara la base fiscal** en `costo_apertura_incluye_isv` (D2), que se congela al ejecutar el corte y queda escrito en la `observacion` de cada asiento. Eso es lo que evita el error silencioso: dentro de un año se puede saber con qué base se sembró el promedio.

**Asunción explícita del corte**: con `base_costo_apertura = VALOR_UNITARIO`, **todas las bodegas de un artículo nacen con el mismo costo promedio**, porque `alm_articulo.valor_unitario` es único por artículo mientras `alm_articulo_bodega.costo_promedio` es por bodega ([Database/2026-07-13_alm_articulo_bodega_comprometido_transito_costos.sql](../../Database/2026-07-13_alm_articulo_bodega_comprometido_transito_costos.sql), `NUMERIC(12,4)`). Un artículo repartido en tres bodegas nacerá idéntico en las tres, sin que eso refleje ninguna realidad de compra. Las diferencias reales por bodega se corrigen con `AJUSTE` de valor (§5.5) o capturando en modo `MANUAL`. D5 solo cubre el caso `valor_unitario = 0`, no este.

**La decisión D1 (ISV al costo vs. crédito fiscal) no bloquea la carga inicial** porque la apertura no genera partida (decisión 8) y la política del ISV salió de este diseño (decisión 9). Lo que D1 fija es el `costo_entrada` de las **compras** (Fase 7). Qué cambia con cada respuesta, para que el diseño de compras lo tenga escrito:

| | **D1 = AL COSTO** | **D1 = CRÉDITO FISCAL** |
|---|---|---|
| `costo_entrada` de una recepción de compra | precio + ISV | precio **sin** ISV |
| Base que debe tener la apertura para ser coherente | **con** ISV → si `valor_unitario` está sin ISV (D2), hay que re-expresarlo antes del corte, o el promedio mezcla bases | **sin** ISV → si `valor_unitario` está con ISV, hay que des-impuestarlo (`monto / (1 + tasa vigente)`) antes del corte |
| Cuenta contable nueva | **ninguna**: el ISV entra al débito de la cuenta de inventario | **sí**: cuenta de ACTIVO que **no existe** en el plan ERSAPS (D7) + uso nuevo (p. ej. `ISV_CREDITO_COMPRAS`) en `con_integracion_cuenta`. **No** reutilizar el uso `ISV` actual: apunta a `21105010000` *Impuestos por Pagar*, un pasivo de ventas |
| Piezas adicionales a construir | ninguna | enlace **artículo → tasa** (`alm_articulo` no tiene columna de impuesto hoy) + escritura de `con_libro_iva` (existe la tabla, nadie la escribe) |
| Sincronización obligatoria si se agrega el uso | — | tres puntos a la vez: CHECK en `Database/*.sql`, `IntegracionContableUsos.Todos` y `SiadDbContext.IntegracionContable.cs` |

**Si el corte ya se ejecutó y la respuesta a D1/D2 obliga a cambiar la base**, la corrección **no es un UPDATE**: el trigger `K0001` lo impide. Es **`ReabrirAsync`** (reversa + nueva apertura, §5.4) para los pares sin movimientos posteriores, y **`AJUSTE` de valor** (§5.5) para los que ya los tengan. Ambas vías existen en esta entrega — pero conviene igual tener D2 respondida **antes** de la Fase 6: reabrir miles de pares es un proceso, no un clic.

## 8. Invariantes, descuadre y soft-delete

**Qué se preserva**:

- El detector de descuadre del maestro (`existencia != Σ ubicaciones activas`, [ArticulosService.cs:66](../../SIAD.Services/Almacen/ArticulosService.cs) y [:118](../../SIAD.Services/Almacen/ArticulosService.cs)) sigue igual: el posteo escribe la fila de bodega y **luego** recomputa la cabecera, así que el rollup queda cuadrado por construcción. La carga inicial **elimina la causa principal** del descuadre, que era escribir existencia sin movimiento.
- Soft-delete de ubicación: `DeshabilitarAsync` sigue exigiendo bodega en cero, y ahora `ReactivarAsync` tiene su guarda simétrica (decisión 13). Soft-delete de artículo (`alm_articulo.activo`): descontinuar **no** genera movimiento ni toca el kardex; la guarda de `ArticulosService.cs:805` (no se puede pasar a "sin inventario" con asientos) tampoco cambia.
- Concurrencia optimista `xmin` del maestro: intacta para el usuario. El lote no la usa porque el rollup ya no pasa por `SaveChanges` (§5.7).

**La comparación de `KardexService` como prueba de aceptación**: **solo después** del cambio de §5.9. Tal como está hoy el servicio, `SaldoCalculado` suma también el histórico SIMAFI y nunca igualaría a `ExistenciaBodega` para un artículo con histórico. Con el punto de corte implementado, `SaldoDescuadrado == false` para todo par con apertura vigente **sí** es la prueba de aceptación natural, y se incluye en el smoke de §11.

**Invariante de cierre corregido.** El del plan del motor está mal: compara `alm_articulo_bodega.existencia` contra `SUM(ingresos - salidas)` **sin filtrar `company_id` ni excluir el histórico SIMAFI**, y como la existencia por bodega se backfilleó desde la cabecera ([2026-07-07_alm_articulo_bodega_backfill_existencia.sql:23-29](../../Database/2026-07-07_alm_articulo_bodega_backfill_existencia.sql)), devolvería descuadre masivo aunque el motor fuera perfecto. El invariante correcto:

```sql
-- Cero filas = motor cuadrado. Por tenant, SOLO sobre el libro nuevo, sin filtrar activo
-- (las bodegas inactivas con existencia también tienen apertura, decisión 13).
SELECT ab.articulo_id, ab.bodega_id, ab.existencia, COALESCE(SUM(k.ingresos - k.salidas), 0)
FROM   alm_articulo_bodega ab
LEFT JOIN alm_kardex k
       ON k.company_id  = ab.company_id
      AND k.articulo_id = ab.articulo_id
      AND k.bodega_id   = ab.bodega_id
      AND k.uuid IS NOT NULL              -- libro nuevo; excluye SIMAFI y los 12 huérfanos
WHERE ab.company_id = :company
GROUP BY ab.articulo_id, ab.bodega_id, ab.existencia
HAVING ab.existencia <> COALESCE(SUM(k.ingresos - k.salidas), 0);
```

Con `ck_alm_kardex_libro_nuevo` validado (§5.1d), `k.uuid IS NOT NULL` y `k.documento_tipo IS NOT NULL` son **equivalentes**: no hay dos definiciones de "libro nuevo" conviviendo. El asiento de `REVERSA` entra en la suma con su signo, así que un par revertido y reabierto sigue cuadrando.

**Gate de cierre de fase** — sin esto **no se avanza a compras**:

```sql
-- Debe dar 0. Sin filtrar activo, por la misma razón que el invariante.
SELECT count(*) FROM alm_articulo_bodega
WHERE company_id = :company AND existencia <> 0 AND costo_promedio = 0;
```

## 9. Manejo de errores

- **Apertura duplicada** (uuid ya existe) → **no es error**: se devuelve la existente. Idempotencia por diseño.
- **Costo 0 o negativo** con cantidad > 0 → error de negocio (400): *"El artículo no tiene costo de apertura. Capture el costo antes de inicializar la existencia."* Nunca 500.
- **Cantidad negativa** → error de negocio: *"La existencia inicial no puede ser negativa. Regístrela con un ajuste de entrada antes del corte."*
- **Modo `AperturaNueva` con existencia previa ≠ 0** → error de negocio: *"La bodega ya tiene existencia registrada; esta apertura sumaría por segunda vez."* Es el cinturón de seguridad contra la duplicación de §5.3.
- **Modo `AperturaReconciliacion` con cantidad distinta de la existencia leída** → error de negocio: la reconciliación describe la existencia, no la dicta.
- **`fecha` nula** → error de negocio: *"El asiento de inventario requiere fecha contable. Configure la fecha de corte antes de ejecutar el lote."* La BD lo respalda con `ck_alm_kardex_fecha_si_uuid`.
- **Par con apertura vigente** que se intenta re-inicializar → error de negocio: *"Esta bodega ya tiene carga inicial. Use un ajuste de inventario para corregir la existencia, o reabra la apertura si aún no tiene movimientos."* Ambos documentos existen en esta entrega (§5.4, §5.5).
- **`ReabrirAsync` con movimientos posteriores** → error de negocio: *"La bodega ya tiene movimientos posteriores a la apertura. Corrija con un ajuste de inventario."*
- **`apertura_cerrada = true`** y llega una apertura para un par **preexistente** → rechazo; para un par **nuevo** (artículo dado de alta después del corte) → se acepta, es el flujo normal; `ReabrirAsync` → se acepta con permiso de Configuración.
- **Reactivar una fila con existencia sin apertura** (las DOS rutas de la decisión 13: `ReactivarAsync` y la rama de reactivación de `AddAsync`) → error de negocio: *"No se puede reactivar la ubicación: tiene existencia sin respaldo en el kardex. Regístrela con la carga inicial o con un ajuste."*
- **Cantidad negativa** → rechazo **en el servicio**, no en el DTO. **⚠️ Corregido en rev.3:** el `[Range(0,…)]` de `ArticuloUbicacionDto.Existencia` (:42-43) solo actúa en la capa HTTP (`[ApiController]` + `ModelState`, [ArticulosController.cs:174](../../apc/Controllers/Almacen/ArticulosController.cs) y :189). Ni `AddAsync` ni `UpdateAsync` validan `dto.Existencia >= 0`, así que **una llamada interna —como el propio posteador o el lote de carga inicial— puede pasar negativos**. La regla vive en `CargaInicialInventarioService` / el posteador. El test `ArticuloUbicacionTests.cs:425-444` ya documenta que hoy una negativa solo entra por SQL directo.
- **`DbUpdateConcurrencyException`** → durante `EjecutarLoteAsync` la fila se marca **"omitida por concurrencia"** en el resultado y el proceso continúa: el lote es idempotente y la reprocesa la siguiente corrida. No aborta el lote completo. Con el rollup por `ExecuteUpdateAsync` (§5.7) este caso debería ser residual, pero se maneja igual porque el `FOR UPDATE` bloquea `alm_articulo_bodega`, no `alm_articulo`.
- **Violación de FK `23503`** en el INSERT del kardex → casi siempre es bug de tenant (EF mapea las FK como simples, la BD las tiene compuestas con `company_id`). Se valida el tenant **antes** de postear; el mensaje al usuario es genérico y el detalle va al log.
- **`K0001`** (intento de UPDATE/DELETE sobre kardex) → bug de programación, no de usuario: se loguea con el mensaje del trigger íntegro.

## 10. Fases de implementación

| # | Fase | Contenido | Bloqueada por |
|---|---|---|---|
| **0** | **Verificación previa** (sin código) | **(a)** Confirmar en SRV que los seis scripts de infraestructura (2026-07-09, 2026-07-13, cuatro de 2026-07-14) **están aplicados** — no figuran en el runbook vigente. **(b)** Medir y pre-chequear (§11). **Ese conteo decide si el corte es one-shot o un proyecto de captura de costos.** | — |
| **1** | **Motor mínimo + rollup compartido** (TDD) | `UuidV5` + namespace, `TipoMovimientoInventario`, `MovimientoInventarioDto`, `PosteoResultDto`, `IArticuloRollupService` (§5.7), `IInventarioPostingService` con `CargaInicial` (dos modos), `Ajuste*` y `Reversa`: idempotencia, `FOR UPDATE` con tenant, costeo borde, snapshots resultantes, rollup. | — |
| **2** | **Base de datos** | `Database/2026-07-29_alm_carga_inicial.sql` + entidades `alm_config_inventario` y `alm_ajuste_inventario` + fluent config + actualización de los `COMMENT` y del XML doc del uuid + registro en el runbook SRV (skill `runbook-despliegue-srv`), incluido el paso propio de `VALIDATE CONSTRAINT`. Aplica **el usuario**: mirror → SRV. | Fase 0(a) |
| **3** | **Permisos** | `PermissionResources.Inventario`, constantes `module.inventario.{carga_inicial,ajustes}.*`, entradas en `Policies` y array `Inventario` en `PermissionEndpointCatalog` + `All`. Va **antes** que la API para que ningún endpoint nazca con el permiso equivocado. | — |
| **4** | **Servicios + API** | `CargaInicialInventarioService` (unitaria, pendientes, simular, lote, costo manual, reabrir, cerrar), `AjusteInventarioService`, controladores, clientes HTTP. | 1, 2, 3 |
| **5** | **Kardex con punto de corte** | `KardexMovimientoDto` ampliado, `KardexService` con reinicio del saldo en la apertura, ajuste de las dos páginas a `Saldo` nullable, columna "Documento". | 1 |
| **6** | **Cierre de la captura manual + Ajuste en UI** | `existencia` deja de escribirse desde el DTO en `CreateAsync`, `AddAsync` (ambas ramas) y `UpdateAsync`; **guarda de reactivación en las DOS rutas** (decisión 13) **en el mismo commit** que quita la escritura de `:106`; campo `CostoApertura`; modal "Registrar ajuste". Reescribe **cinco pruebas nominadas** (§12). **Sale en el mismo binario que la Fase 7**, y no antes de que el Ajuste esté operativo, o el módulo queda en solo lectura. | 4, 5 |
| **7** | **UI del corte** | Campo "Costo de apertura" en el tab de ubicaciones + pantalla `/almacen/carga-inicial` + ítem de menú. | 4, 6 |
| **8** | **Ejecución del corte** | Respaldo previo (§11) → saneo de negativas con `AJUSTE` y captura de costos faltantes (D5/D6) → dry-run → ejecutar en **mirror primero**, verificar el invariante de §8, luego SRV en ventana de bajo uso y con el maestro de artículos cerrado a edición → `CerrarAperturaAsync`. **Gate**: cero filas con `existencia <> 0 AND costo_promedio = 0`. | 7, D2, D4, D5, D6 |
| **9** | *(otro diseño)* | Compras: recepción, ISV, costeo real, integración contable. | **D1**, D7, D8, y la Fase 8 cerrada |

**El orden 8 → 9 no se puede invertir.** Con `costo_promedio = 0` y una compra que entre primero, el promedio se corrompe: 100 unidades a 0 + 10 a L.50 = **L.4,55**, basura irrecuperable (ejemplo del propio plan del motor).

## 11. Checklist de despliegue por fase (regla del proyecto)

- [ ] **Pre-chequeos de la Fase 0(b)** corridos por el usuario en el mirror y en SRV (yo no me conecto a ninguna BD por iniciativa propia):
  ```sql
  -- Deben dar 0 los tres primeros; si no, los CHECK de §5.1d no validan.
  SELECT count(*) FROM alm_kardex WHERE documento_tipo IS NOT NULL AND uuid IS NULL;
  SELECT count(*) FROM alm_kardex WHERE uuid IS NOT NULL AND documento_tipo IS NULL;
  SELECT count(*) FROM alm_kardex WHERE uuid IS NOT NULL AND fecha IS NULL;
  -- Dimensionamiento del corte:
  SELECT count(*) FROM alm_articulo_bodega WHERE existencia <> 0;
  SELECT count(*) FROM alm_articulo_bodega ab JOIN alm_articulo a ON a.id = ab.articulo_id
   WHERE ab.existencia <> 0 AND a.valor_unitario = 0;                       -- sin costo
  SELECT count(*) FROM alm_articulo_bodega WHERE existencia < 0;            -- negativas
  SELECT count(*) FROM alm_articulo_bodega ab JOIN alm_articulo a ON a.id = ab.articulo_id
   WHERE ab.existencia <> 0 AND a.activo = false;                           -- descontinuados
  SELECT count(*) FROM alm_articulo_bodega WHERE activo = false AND existencia <> 0; -- bodega inactiva
  SELECT count(DISTINCT articulo_id) FROM alm_articulo_bodega WHERE existencia <> 0; -- volumen de rollup/bitácora
  -- ¿La bitácora de alm_articulo está habilitada? (dimensiona el riesgo de §5.7)
  SELECT * FROM bitacora_maestro_config WHERE tabla = 'alm_articulo';
  ```
- [ ] Script timestamped en `Database/` con el estilo obligatorio (Regla DB Mirror, POR QUÉ, idempotencia, `BEGIN/COMMIT`, `COMMENT ON`, bloque VERIFICACION que incluya los tres primeros conteos).
- [ ] Paso registrado en `Database/2026-07-23_runbook_despliegue_srv.md` (o el runbook vigente) con su consulta «¿ya aplicado?», **más un paso propio** para los dos `VALIDATE CONSTRAINT`.
- [ ] **Mirror primero** (`siad_v3_restore` @localhost), nunca directo a SRV.
- [ ] **Respaldo previo obligatorio antes de `EjecutarLoteAsync` en SRV** (el kardex es inmutable: si el lote sale mal, el único camino es restaurar):
  ```
  pg_dump -t alm_kardex -t alm_articulo_bodega -t alm_articulo -Fc -f pre_corte_<fecha>.dump "$SRV"
  ```
- [ ] **Maestro de artículos cerrado a edición** durante la ventana del corte (evita el ruido de concurrencia de §9).
- [ ] `dotnet build HODSOFT_DEVEXPRESS.sln` sin errores.
- [ ] `dotnet test SIAD.Tests/SIAD.Tests.csproj` con `SIAD_TEST_DB` apuntando a una BD **de prueba**.
- [ ] Invariante de §8 en cero + gate de cierre en cero.
- [ ] **Prueba manual de reanudabilidad** en el mirror (guion): ejecutar el lote con 1.000 filas y `tamañoLote = 200`; matar el proceso tras el segundo lote; verificar que hay 400 asientos posteados y commiteados; relanzar; verificar que el total es 1.000 y **no** 1.400, y que el invariante de §8 da cero. Esta prueba no puede automatizarse (§12).
- [ ] **Prueba manual de concurrencia** del `FOR UPDATE` en el mirror: dos sesiones posteando el mismo par a la vez; la segunda espera y luego encuentra la apertura vigente.
- [ ] Smoke logueado: alta de artículo con existencia → el kardex muestra el movimiento de apertura con `Documento = CARGA_INICIAL`, el costo promedio deja de ser 0, y la tarjeta de saldo **no** queda en amarillo (`SaldoDescuadrado = false`).

## 12. Impacto en pruebas

Nuevos en `SIAD.Tests/Almacen/`:

- **`UuidV5Tests.cs`** — vectores deterministas conocidos (RFC 4122) + estabilidad de la derivación de apertura + **el discriminador de intento**: mismo par sin reversas → mismo uuid; con una reversa posteada → uuid distinto. **Es la prueba más importante del lote**: si el uuid deja de ser determinista, la idempotencia desaparece sin que nada falle.
- **`CargaInicialTests.cs`** — la apertura postea kardex con `documento_tipo='CARGA_INICIAL'`, `tipo_transaccion='102'`, `documento_id = ubicacion.id`; siembra `costo_promedio = ultimo_costo = costo`; escribe `existencia_resultante` y `costo_promedio_resultante`; repetir el posteo **no** duplica; costo 0 rechazado; cantidad negativa rechazada; segunda apertura vigente del mismo par rechazada; rollup de cabecera = Σ bodegas **activas** (y `cantidad` = `existencia`); el asiento **no** puebla `linea`/`linea_desc`; tenant: no se puede postear contra bodega de otra empresa. Casos nuevos obligatorios:
  - `AperturaRetroactiva_NoDuplicaExistencia` — fila con existencia 500 → tras postear en modo reconciliación **sigue en 500**, el asiento dice `ingresos = 500` y `existencia_resultante = 500`, y la cabecera no se duplica.
  - `AperturaNueva_ParConExistenciaPrevia_Rechazada` — el cinturón de seguridad de §9.
  - `AperturaUnitaria_FechaEsHoy_NoLaDeCorte`.
  - `Apertura_SinFecha_Rechazada`.
- **`ReversaReaperturaTests.cs`** — `Reabrir_RevierteYVuelveAPostear` (existencia y costo finales correctos, dos asientos nuevos, el par queda con una sola apertura vigente); `Reabrir_ConMovimientosPosteriores_Rechazado`; `Reversa_EsIdempotente` (repetirla no duplica); `Reversa_NoSeAplicaDosVecesAlMismoAsiento`.
- **`AjusteInventarioTests.cs`** — entrada, salida (valorizada al promedio, que no cambia), ajuste de valor (`ingresos = salidas = 0`, promedio nuevo, existencia intacta); motivo obligatorio; salida que dejaría negativo rechazada.
- **`KardexCorteTests.cs`** — `SaldoCorrido_ReiniciaEnCargaInicial` (par con histórico SIMAFI + apertura → `SaldoCalculado == ExistenciaBodega` y `SaldoDescuadrado == false`); `MovimientosPreCorte_TienenSaldoNull`; **regresión** `SinCargaInicial_SaldoSigueSiendoElHistoricoCompleto` (comportamiento actual intacto durante la transición); `ParReabierto_ElCorteEsLaAperturaVigente`.
- **`CargaInicialLoteTests.cs`** — cubre **solo** clasificación del universo (posteable / sin costo / negativa / descontinuado / bodega inactiva) e **idempotencia por uuid dentro de una transacción**.

Modificar los existentes. **⚠️ Ampliado en rev.3:** la rev.2 decía genéricamente "modificar `ArticuloUbicacionTests` y `ArticulosValorYDescuadreTests`, con lo que **se subestimaba el costo de la Fase 6**. Hoy `SIAD.Tests/Almacen` tiene 6 archivos y 56 `[SkippableFact]`, **ninguno** de carga inicial ni de posteo. La Fase 6 (dejar de escribir `existencia` desde el DTO) rompe **al menos estas cinco**, nominadas:

| Archivo | Prueba | Por qué rompe |
|---|---|---|
| `ArticuloUbicacionTests.cs:331` | `Create_ConUbicaciones_PrimeraPrincipalYSumaExistencia` | siembra existencia por el DTO |
| `ArticuloUbicacionTests.cs:364` | `Rollup_ExistenciaYMinimoSonSumaDeBodegas` | ídem |
| `ArticuloUbicacionTests.cs:516` | `Escrituras_ReusanLaTransaccionAmbiente_YDejanLaCabeceraCuadrada` | ídem |
| `ArticulosValorYDescuadreTests.cs:143` | `ConDescuadre_FiltraYCuentaElArticuloDesincronizado` | su helper crea el artículo con existencia por DTO |
| `ArticulosValorYDescuadreTests.cs:208` | (caso de descuadre / ubicación inactiva) | ídem |

**Decisión a tomar antes de la Fase 6**: si esas pruebas pasan a sembrar existencia vía `PostearAperturaAsync` (más realista, acopla los tests al motor) o por SQL directo — ya existe el helper `SeedUbicacionDirectaAsync` ([ArticuloUbicacionTests.cs:437](../../SIAD.Tests/Almacen/ArticuloUbicacionTests.cs)), que es el camino de menor fricción para las que solo necesitan un estado inicial.

- `ArticuloUbicacionTests.cs` — `AddAsync`/`UpdateAsync` ya **no** escriben existencia desde el DTO; reactivar no re-abre; **ambas** rutas de reactivación rechazan una fila con existencia sin apertura (decisión 13).
- `ArticulosValorYDescuadreTests.cs` — el rollup ahora nace del posteo y del servicio compartido, no del DTO.
- `KardexBodegaTests.cs` — sigue insertando kardex a mano para simular el histórico ([:87](../../SIAD.Tests/Almacen/KardexBodegaTests.cs)); esos inserts no ponen `documento_tipo` **ni** `uuid`, así que `ck_alm_kardex_libro_nuevo` (`(uuid IS NULL) = (documento_tipo IS NULL)`) los deja pasar. Se verifica explícitamente.
- `ArticuloDeleteGuardTests.cs` — igual: su insert de [:79](../../SIAD.Tests/Almacen/ArticuloDeleteGuardTests.cs) tampoco pone ninguna de las dos columnas.

**Caveats conocidos, declarados y no disimulados**:

- Cada test corre dentro de la transacción del fixture ([SIAD.Tests/Infrastructure/IntegrationTestBase.cs:22](../../SIAD.Tests/Infrastructure/IntegrationTestBase.cs), :30) y el patrón `IniciarTransaccionAsync` devuelve `null` cuando ya hay una ambiente, con el commit convertido en no-op ([ArticuloUbicacionService.cs:312-319](../../SIAD.Services/Almacen/ArticuloUbicacionService.cs)). Por tanto **dentro del test no hay lotes independientes ni commits reales**: `CargaInicialLoteTests` **no** puede probar la reanudabilidad ni el "cada lote en su propia transacción". Se verifican con la **prueba manual** de §11, con guion escrito. No se promete cobertura automatizada de un comportamiento que el fixture anula.
- Por la misma razón, la prueba de concurrencia real del `FOR UPDATE` (dos conexiones simultáneas) tampoco encaja en el patrón; va también como prueba manual en el mirror.
- Sin `SIAD_TEST_DB` todos quedan `Skipped`; los tests nuevos requieren además que el script de §5.1 esté aplicado a la BD de prueba.

## 13. Riesgos

- **Las Fases 6 y 7 deben desplegarse juntas, y no antes de que el Ajuste esté operativo.** Si se cierra la captura manual sin el documento de ajuste, el módulo queda en solo lectura hasta la Fase 9. Si se hace la carga inicial pero no se cierra la captura manual, el kardex se descuadra con la primera alta de ubicación posterior y todo el trabajo caduca solo.
- **No está verificado que la infraestructura de posteo exista en producción.** Los seis scripts de kardex no figuran en el runbook vigente y el propio runbook advierte que su estado no se comprobó contra el servidor en vivo. Construir encima sin confirmarlo es apostar.
- **El corte se puede deshacer, pero no gratis.** `ReabrirAsync` y el `AJUSTE` de valor cubren la corrección par por par; lo que **no** existe es un "deshacer el lote" masivo. Por eso el respaldo `pg_dump` previo es obligatorio (§11) y el dry-run no es opcional.
- **El costo de apertura es lo que más probablemente salga mal.** No se corrige editando (`K0001`); se corrige reabriendo o ajustando, y ambas cosas dejan rastro. Tener D2 respondida antes del corte ahorra ese trabajo.
- **Volumen desconocido de artículos sin costo.** No hay dato en el repo sobre cuántos tienen `valor_unitario = 0` (no me conecté a ninguna BD). Si son cientos, la Fase 8 deja de ser un one-shot y se vuelve un proyecto de captura de costos con el contador.
- **Base de costo inconsistente entre apertura y compras** (D2). Si la apertura entra en una base y las compras en otra, el promedio ponderado mezcla las dos y queda mal **sin que nada lo detecte**: el kardex cuadra en unidades y miente en dinero. Mitigación parcial: `costo_apertura_incluye_isv` queda escrito en cada `observacion`.
- **Todas las bodegas de un artículo nacen al mismo costo** con `base_costo_apertura = VALOR_UNITARIO` (§7). Es una simplificación consciente, no un descuido.
- **`valor_unitario`, `total`, `debe` y `haber` son NOT NULL DEFAULT 0**: un posteador incompleto puede escribir cantidades correctas con importes en cero y nadie se quejará. Cubierto por los tests de §12, no por la BD.
- **Riesgo de tracking accidental sobre `alm_kardex`**: cualquier lectura con tracking + modificación produce un UPDATE que revienta con `K0001` y un mensaje que no parece un bug de código. Regla: `AsNoTracking` siempre, `Add` nunca `Update`.
- **La bitácora de maestros puede inundarse** si alguien reintroduce el rollup por `SaveChanges` (§5.7). Conviene un comentario grande en `ArticuloRollupService` explicando por qué usa `ExecuteUpdateAsync`.
- **12 asientos huérfanos** sin `articulo_id` ni `codigo_articulo`: quedan fuera de toda consulta por artículo. Cualquier conteo global tiene que excluirlos explícitamente (el invariante de §8 ya lo hace vía `uuid IS NOT NULL`).
- **`documento_tipo`/`documento_id` no tienen FK real** (referencia polimórfica), y con `REVERSA` el `documento_id` apunta a la propia `alm_kardex`. Si alguien borrara la fila `alm_articulo_bodega`, el asiento de apertura quedaría apuntando al vacío. Mitigación: la FK compuesta del kardex hacia `alm_articulo` es `RESTRICT` ([2026-07-14_alm_fk_compuestas_tenant.sql:126-129](../../Database/2026-07-14_alm_fk_compuestas_tenant.sql)), lo que impide borrar un artículo con asientos y, por transitividad, que la cascada de `alm_articulo_bodega` (`ON DELETE CASCADE`, :137-140) llegue a dispararse. Conviene verificarlo en el mirror antes del corte.
- **Divergencia EF ↔ BD**: EF mapea las FK del kardex como simples y la BD las tiene compuestas por tenant. Como el contexto SIAD no usa migraciones, la BD manda; pero los errores de tenant salen como `23503` crípticos en tiempo de INSERT.

## 14. Fuera de alcance (explícito)

- **Compras, requisiciones, descargos y traslados** (Fases 4-6 del plan del motor). Este diseño implementa el motor para `CargaInicial`, `Ajuste*` y `Reversa`; el resto de los tipos lanza `NotSupportedException`.
- **Tabla `alm_traslado`** (Tarea 1.3 del plan): no existe, y este diseño no la crea ni la asume.
- **Cabecera y numeración formal del documento de Ajuste**, y **catálogo de motivos**: el ajuste es plano y el motivo es texto libre. Ampliarlo después no rompe nada.
- **Integración contable de inventario**: ninguna partida en `con_partida_hdr/dtl`, ningún uso nuevo en `con_integracion_cuenta`, ninguna columna `alm_kardex.poliza_id`. Sujeto a D3.
- **Política del ISV** (al costo vs. crédito fiscal) y su cuenta: van en el diseño de configuración de ISV, sobre la configuración fiscal/contable por empresa ya existente (decisión 9).
- **Enlace artículo → tasa fiscal** (`alm_articulo` no tiene columna de impuesto) y escritura de `con_libro_iva`. Van con la Fase 9 y dependen de D1.
- **Deshacer el lote completo** en un paso: la vía es el respaldo `pg_dump` previo, o la corrección par por par con reapertura/ajuste.
- **Migración del histórico SIMAFI al vocabulario nuevo**: los ~47,2k asientos migrados quedan con `uuid`/`documento_tipo` NULL, como consulta pre-corte. No se re-enlazan.
- **Migración de la pantalla de Impuestos al estándar de grid** y sus tests faltantes: deuda conocida, otro trabajo.

## 15. Decisiones pendientes del contador

Bloquean el **costo** y la **contabilidad**, no el mecanismo. Numeradas para poder citarlas en el resto del documento.

| # | Decisión | Qué bloquea |
|---|---|---|
| **D1** | **ISV de compras**: ¿va **al costo** del artículo o es **crédito fiscal** recuperable? Ojo: agua potable y alcantarillado son EXENTOS por Art. 15, y quien vende exento normalmente no acredita el ISV de sus compras — pero eso lo confirma el contador, no este documento. | La **Fase 9 (compras)**, NO la carga inicial. Ver la tabla de §7 para qué cambia con cada respuesta. |
| **D2** | **Base de `alm_articulo.valor_unitario`**: ¿está expresado **con** ISV incluido o **sin** ISV? | El **costo** de la apertura (Fase 8), no su mecánica. Si la apertura y las compras futuras usan bases distintas, el promedio ponderado mezcla dos bases y queda mal **en silencio**. Propuesta del equipo: **sin ISV**, y se declara `costo_apertura_incluye_isv = false`. |
| **D3** | ¿El inventario actual **ya está reconocido en el balance** (viene del cierre de SIMAFI)? | Si se mantiene la decisión 8 (sin partida) o si la apertura debe generar comprobante contra patrimonio / ajuste de resultados de ejercicios anteriores. Propuesta: **ya está reconocido, no se postea partida**. |
| **D4** | **Fecha de corte** del asiento de apertura del lote. | Parámetro obligatorio de `EjecutarLoteAsync` (§5.6). Propuesta: la fecha de puesta en marcha del módulo, posterior al último movimiento migrado de SIMAFI. No aplica a las aperturas unitarias, que se fechan el día del alta (decisión 11). |
| **D5** | Criterio de **costo para los artículos con `valor_unitario = 0`**: ¿última compra, avalúo, costo de reposición? | El flujo de excepción de §5.6/§6. Sin respuesta, esos artículos quedan **fuera** del inventario valorizado y el gate de cierre de fase no cierra. |
| **D6** | **Existencias negativas**: ¿se sanean a cero con un ajuste de entrada, o se investigan una por una? | Si el corte se ejecuta completo o queda con excepciones abiertas. El documento de `AJUSTE` (§5.5) ya provee la herramienta para cualquiera de las dos respuestas. |
| **D7** | Si D1 = **crédito fiscal**: ¿en qué cuenta? El plan regulatorio ERSAPS **no tiene** ninguna cuenta de ISV por cobrar ni de crédito fiscal (0 ocurrencias de "ISV" en el manual); lo más cercano es 1.1.5.04 *Impuestos* (gastos pagados por adelantado) y 2.1.1.05.01 *Impuestos por Pagar* (pasivo). | La Fase 9. Exigiría cuenta nueva en el plan + uso nuevo en `con_integracion_cuenta`. |
| **D8** | ¿Las tasas semilla de `cfg_impuesto_tasa` (ISV 15 / 18 / EXENTO / EXONERADO, todas con vigencia desde 2010-01-01) son correctas y desde qué decreto rige cada una? | La Fase 9. El propio script advierte que deben ser validadas por el contador ([Database/2026-07-14_cfg_impuestos.sql:28-31](../../Database/2026-07-14_cfg_impuestos.sql)). |

> Por regla del proyecto no me conecto a ninguna BD por iniciativa propia: los conteos y pre-chequeos que exige la Fase 0 (§11) los corre el usuario.

## 16. Preguntas abiertas del equipo

Distintas de D1–D8: no dependen del contador sino de una decisión interna.

1. ¿Se puede ejecutar el corte **por empresa** de forma independiente, o todas las empresas cortan el mismo día? (El diseño soporta lo primero: `alm_config_inventario` es por empresa.)
2. ¿El `AJUSTE` necesita **aprobación** de un segundo usuario, o basta con el permiso y la bitácora del kardex? Hoy se diseñó sin flujo de aprobación.
3. `alm_compra.impuesto` y `alm_requisicion.impuesto` / `impuesto_aplica` existen (heredadas de SIMAFI) y **nadie las escribe ni las lee**. ¿Se rehabilitan en la Fase 9 o se declaran formalmente legado muerto? Son un atractor de errores: alguien puede asumir que "el ISV ya está".
4. Tras el corte, ¿conviene un **reporte de valorización** por bodega (`existencia × costo_promedio`) en esta entrega, o espera a la Fase 9 como propone el plan del motor? El dato ya queda disponible al terminar la Fase 8.
