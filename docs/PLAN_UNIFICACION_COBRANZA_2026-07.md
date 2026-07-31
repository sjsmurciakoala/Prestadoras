# Plan: Unificación del proceso de cobro (motor único + modelo `adm_*` + estados numéricos)

Fecha: 2026-07-23 · Estado: **BORRADOR — pendiente de aprobación**
Complementa: [ESTADOS_DOCUMENTOS_COMERCIALES.md](ESTADOS_DOCUMENTOS_COMERCIALES.md), [ESTANDAR_ESTADOS_Y_FLUJO_CONTABLE.md](ESTANDAR_ESTADOS_Y_FLUJO_CONTABLE.md)

---

## 1. Problema (diagnóstico verificado en código y BD)

1. **7 escritores de `transaccion_abonado` sin lógica común**:
   caja-lectoras / posteo manual / misceláneos (3 métodos separados en
   `CaptacionPagosService`, tipo `201`), abono especial y recibo pendiente
   (`AbonoService`, tipo `202`, estados `C`/`P`), WS bancario
   (`sp_ban_ws_pagar`, `202`) y plan de pago (`CobranzaService`, `PLAN*` — que
   no son cobros sino cargos/traslados, ver §3.1). Captación y abonos
   especiales hacen **lo mismo** (aplicar pago a una factura); difieren solo en
   accidentes: total vs parcial, `caja_id` sí/no, reverso DELETE (201) vs
   marca `A` (202).
2. **No existe tabla puente pago↔factura**: la aplicación es UPDATE inline de
   `factura_detalle.montovalor_saldo` + `factura.estado`; el vínculo es `recibo` =
   `numrecibo` suelto, sin FK (y `numrecibo` sin UNIQUE).
3. **Estados a medias**: la migración a numéricos (2026-05) dejó infraestructura
   completa (catálogos, `estado_id`, triggers letra→id) pero el núcleo comercial
   sigue escribiendo/leyendo **letras**; `B`, `P` y el `A`=anulado de los
   pagos colapsan a `estado_id=1` (Activa). `tipotransaccion` es string libre.
   Nota: la letra `R` **no se escribe nunca** (el reverso del WS marca `A` +
   descripción `REVERSADO WS:`; lo "reversado" vive en `ban_ws_pago.status_id=2`)
   y existe una `N` fantasma que `CajaService` filtra pero nadie escribe (§10).
4. **No existe cobro de cuota de plan de pagos**: las cuotas nacen "Pendiente" y
   ningún código las actualiza jamás; caja no puede verlas porque no son facturas.
5. Los datos `SALDO_ANTERIOR` actuales son **de prueba** — la cartera real se
   migrará de SIMAFI en el cutover, lo que permite migrarla ya al modelo nuevo.

## 2. Objetivo

- **Un solo lugar para cobrar** (una pantalla, un motor, N canales).
- **Cobrar documentos, no saldos**: todo pago queda aplicado a documentos
  concretos vía tabla de aplicación con FKs reales.
- **Estados 100% numéricos** en el dominio comercial; las letras quedan como
  compatibilidad temporal y mueren al final.
- Contratos externos **intactos**: XML del banco (byte-exacto) y snapshot JSON de
  la app de lectores (`OFFLINE_SNAPSHOT_V3_2`).

## 3. Modelo destino (nuevas tablas, convención `adm_*`)

Todas tenant-scoped (`company_id`, `ICompanyScopedEntity`) salvo los catálogos
globales, siguiendo el patrón de `adm_condicion_lectura`/`_tipo`.

### 3.1 `adm_tipo_transaccion` — catálogo de tipos de movimiento (global)

Reemplaza el string libre `tipotransaccion`. Nota: existe una tabla legacy
`tipo_transaccion` (scaffold SIMAFI, vacía, sin uso) — se **elimina** en F7 para
no dejar dos catálogos.

| id | codigo | nombre | naturaleza | es_pago |
|---|---|---|---|---|
| 1 | `CARGO_SERVICIO` | Cargo de servicio facturado | D | no |
| 2 | `PAGO_CAJA` | Pago en caja (legacy `201`) | C | sí |
| 3 | `PAGO_BANCO` | Pago canal bancario (legacy `202` WS) | C | sí |
| 4 | `ABONO` | Abono parcial (legacy `202` caja) | C | sí |
| 5 | `NOTA_CREDITO` | Nota de crédito (legacy `205`) | C | no |
| 6 | `NOTA_DEBITO` | Nota de débito | D | no |
| 7 | `PLAN_TRASLADO` | Traslado a plan de pago (legacy `PLAN`) | C | no |
| 8 | `PLAN_PRIMA` | Prima de plan (legacy `PLAN-PR`) | D | no |
| 9 | `PLAN_CUOTA` | Cuota de plan (legacy `PLAN-CUOTA`) | D | no |
| 10 | `AJUSTE` | Ajuste manual | D/C | no |
| 11 | `SALDO_INICIAL` | Saldo inicial migrado | D | no |

Columnas: `id smallint PK`, `codigo varchar UNIQUE`, `nombre`, `naturaleza
char(1)`, `es_pago bool`, `activo bool`, auditoría. Tabla de mapeo legacy
(`codigo_legacy` → id) para el backfill: `201→2`, `202→3|4` (según
`trans_aplicar LIKE 'WSBANCO:%'`), `205→5`, `PLAN→7`, `PLAN-PR→8`,
`PLAN-CUOTA→9`, `SALDO_ANTERIOR→11`, códigos de servicio→1.

### 3.2 Catálogo de estados de pago — `adm_estado_pago` (global)

Separado del de documentos (raíz de la ambigüedad actual de la letra `A`):

| id | codigo | nombre | equivale hoy a |
|---|---|---|---|
| 1 | `APLICADO` | Pago aplicado/vigente | `C` en 201/202 |
| 2 | `PENDIENTE` | Recibo generado sin pagar | `P` |
| 3 | `ANULADO` | Anulado desde caja | `A` en 202 |
| 4 | `REVERSADO` | Reversado por canal externo | `A` con `trans_aplicar LIKE 'WSBANCO:%'` (la letra `R` no se usa hoy) |

### 3.3 `cfg_estado_documento_comercial` — se completa (tabla existente)

Se agrega `4 | B | Parcialmente abonada` y se actualiza
`fn_estado_documento_comercial_id_from_codigo` (`B→4`). **Ojo con el ELSE**: la
función la comparten los triggers de `factura` y de `transaccion_abonado`, y
durante el dual-write (F2–F6) los pagos legacy siguen entrando con `P`/`A` — un
WARNING global dispararía en cada abono legítimo. Decisión por tabla: en
`factura` el ELSE deja de colapsar a 1 y lanza WARNING (solo faltaba `B`); el
trigger de `transaccion_abonado` pasa a mapear las letras de pago a
`estado_pago_id` y deja de pasarlas por el catálogo de documentos.
`EstadoDocumentoComercial` (C#) gana `ParcialmenteAbonada = 4`.

### 3.4 `adm_pago` — cabecera del cobro (el "recibo" como entidad)

```
id bigint PK, company_id FK, numero_recibo varchar(30) NOT NULL,
cliente_clave, fecha, canal_id (caja|banco|app),
tipo_transaccion_id FK adm_tipo_transaccion, estado_id FK adm_estado_pago,
monto_total, forma_pago (EFECTIVO|BANCO), banco_cuenta_id NULL,
ban_kardex_id NULL, sesion_caja_id FK sesion_caja (NOT NULL para canal caja),
poliza_id NULL (comprobante contable), referencia_externa NULL (idempotencia:
referencia bancaria / uuid app), usuario, creado/actualizado.
UNIQUE (company_id, numero_recibo).
UNIQUE (company_id, referencia_externa) WHERE referencia_externa IS NOT NULL.
```

`numero_recibo` es el folio visible del recibo (ver §3.8) — corrige el pecado
original de `factura.numrecibo`: secuencia **global** compartida entre empresas
y **sin UNIQUE** (documentado en el propio `sp_ban_ws_reversar` y en
BUGS_MOTOR_FACTURACION_2026-05-13 §353).

### 3.5 `adm_pago_aplicacion` — el puente pago↔documento (lo que hoy no existe)

```
id bigint PK, company_id, pago_id FK adm_pago,
documento_tipo smallint (1=factura, 2=cuota_plan, 3=nota_debito, ...),
factura_id FK factura NULL, factura_detalle_id FK factura_detalle NULL,
plan_cuota_id FK cln_plan_pago_dtl NULL,
monto_aplicado numeric NOT NULL CHECK (> 0).
CHECK: exactamente una referencia de documento poblada según documento_tipo.
```

Invariante: `SUM(monto_aplicado) por pago = adm_pago.monto_total`.

### 3.6 Saldo por documentos

`sp_obtener_cliente_saldo` pasa a calcular:
`SUM(factura.saldototal WHERE estado_id IN (1,4))` + cuotas de plan pendientes +
ND pendientes − créditos no aplicados. La vista-parche
`vw_transaccion_abonado_vigente` se retira en F7.

### 3.7 `transaccion_abonado` — transición y destino

- F2–F6: **dual-write** — el motor único escribe `adm_pago(_aplicacion)` **y** la
  fila legacy en `transaccion_abonado` (con `tipo_transaccion_id` y letra), para
  que contabilidad, reportes y arqueo sigan cuadrando sin cambios simultáneos.
- F7: se corta el dual-write; la tabla queda **solo-lectura** (histórico del
  estado de cuenta viejo). No se elimina.

### 3.8 Numeración del recibo — `adm_documento_secuencia` (SIN CAI)

**Hoy:** el número de recibo es `factura.numrecibo`, un identity global de BD
(secuencia única compartida entre empresas, arranca en 3,075,052, sin UNIQUE).
El recibo PDF de abono imprime ese número; los misceláneos (`tipofactura='R'`)
se numeran igual y **no llevan CAI** (`numfactura` vacío).

**Decisión SAR:** el recibo de cobro **no es documento fiscal CAI**. Fundamento
(todo ya modelado en el repo):

1. El cobro de una factura ya emitida (que sí llevó CAI) no vuelve a devengar el
   hecho generador — el CAI ampara la venta, no su cobranza.
2. El catálogo `cfg_tipo_documento_fiscal` no tipifica "recibo de cobro"; los
   tipos 5 (HON) y 10 (REC servicio público) son comprobantes de **venta**.
3. El propio repo ya apunta en esa dirección en contabilidad: `cfg_document_type`
   CAJA/ABO está sembrado con `requires_cai = false` **explícito**, y VENTAS/REC
   queda en `false` por default de columna (su seed no incluye la columna);
   NC/ND en cambio llevan `requires_cai = true` explícito. Aparte — mecanismo
   independiente que apunta igual — los SPs de NC/ND validan que el CAI sea del
   tipo fiscal correcto (`CAI_TIPO_INCORRECTO` contra `tipo_documento_fiscal_id`
   6/7), no contra `requires_cai`.
4. Un CAI ampara un único tipo de documento (regla SAR modelada en
   PLAN_SAR_COMPLIANCE §117); numerar recibos con CAI consumiría rangos
   autorizados sin base y obligaría a anular recibos por NC en vez del
   `estado_id → ANULADO` del motor.

**Qué sigue usando CAI (sin cambios):** factura (FAC), NC y ND, con su flujo
actual (`adm_cai_facturacion` → correlativo `EEE-PPP-TD-NNNNNNNN`, bloques
offline para la app, `adm_cai_correlativo_emitido`). Nota aparte: la decisión
de PLAN_SAR_COMPLIANCE §232 quedó ✅ **confirmada por el contador (2026-07-27):
el ciclo de agua usará tipo 10 "Recibo de servicio público" en lugar de
factura** — sigue siendo un tema de **facturación** (rangos CAI del tipo 10),
no de cobro, y su implementación queda fuera de este plan.

**Modelo nuevo:** `adm_documento_secuencia` — serie administrable por empresa
(patrón calcado de `adm_codigo_cliente_config` + `fn_adm_siguiente_codigo_cliente`,
el mejor precedente del repo: `UPDATE … RETURNING` atómico y auto-correctivo):

```
adm_documento_secuencia:
  id PK, company_id FK, tipo_documento varchar (ej. 'RECIBO_PAGO'),
  canal_id NULL (serie por canal/caja opcional), prefijo varchar (ej. 'REC-'),
  longitud_padding int (ej. 8), valor_actual bigint, activo bool, auditoría.
  UNIQUE (company_id, tipo_documento, canal_id).

fn_adm_siguiente_correlativo_documento(company_id, tipo_documento, canal_id)
  → 'REC-00000013'  (consumo atómico UPDATE…RETURNING)
```

- **Continuidad estricta** (confirmada con el contador 2026-07-26 y ya
  garantizada por construcción): el folio se consume DENTRO de la transacción
  del cobro — si el cobro falla, el rollback devuelve el folio y no queda
  hueco. Costo: dos cobros simultáneos de la misma empresa se serializan un
  instante en la fila de la serie (imperceptible al volumen de APC).
- Migración: `valor_actual` inicial = `MAX(factura.numrecibo)` por empresa; el
  recibo PDF pasa a imprimir `adm_pago.numero_recibo`.
- La misma tabla reemplaza los correlativos `MAX+1` con carrera de
  `CobranzaService` (planes D6, notas y cartas de cobro) y `CorteMasivoService`
  — se migran a la función atómica en F6 como limpieza.

### 3.9 Resumen del modelo de estados final (todo numérico)

| Ámbito | Catálogo | Valores | Letras hoy |
|---|---|---|---|
| Documentos (factura, NC/ND origen, cuota de plan) | `cfg_estado_documento_comercial` (existente, se completa) | 1 Activa · 2 Cobrada/Compensada · 3 Anulada · **4 Parcial** | `A`/`C`/`N`/`B` |
| Pagos (`adm_pago`) | `adm_estado_pago` (nueva) | 1 Aplicado · 2 Pendiente · 3 Anulado · 4 Reversado | `C`/`P`/`A` de 201/202 (+ marker `WSBANCO:`) |
| Tipos de movimiento | `adm_tipo_transaccion` (nueva) | ids 1–11 (§3.1) | `201`/`202`/`205`/`PLAN*`/servicios |
| Documento fiscal NC/ND | `cfg_estado_documento_fiscal` (existente, ya numérico) | 1 Emitida · 2 Aplicada · 3 Anulada · 4 Pendiente | — |
| Pago WS bancario | `ban_ws_pago.status_id` (existente, ya numérico) | 1 Aplicado · 2 Reversado | — |

Dos catálogos separados para documentos vs pagos **a propósito**: la raíz del
desorden actual es que un solo catálogo intentó servir a ambos y la letra `A`
terminó significando "activo" en cargos y "anulado" en pagos. Las letras quedan
como columnas de compatibilidad hasta F7 y ahí mueren.

## 4. El motor único de cobro

`SIAD.Services/Cobros/CobroService.cs` (nuevo módulo), método central:

```
RegistrarCobroAsync(CobroCrearDto {
  Canal, ClienteClave, Aplicaciones[] { DocumentoTipo, DocumentoId, Monto },
  FormaPago, BancoCuentaId?, ReferenciaExterna?, ReciboPendienteId?
}) → CobroResultadoDto { PagoId, PolizaId, NuevoSaldo }
```

Reglas únicas (hoy divergentes):

| Regla | Comportamiento único |
|---|---|
| Parcialidad | Siempre permitida; total = parcial con monto = saldo. Factura → `estado_id 4` (parcial) o `2` (saldada) |
| Aplicación | FIFO por antigüedad dentro del documento; multi-documento permitido (patrón WS) |
| Sesión de caja | **Obligatoria** para canal caja (error si no hay sesión ABIERTA); siempre puebla `sesion_caja_id`. Hoy NO se valida en ningún camino: captación asigna `caja_id` best-effort (null si no hay sesión y el cobro procede) y abonos fija `caja_id = null` literal |
| Idempotencia | Advisory lock + UNIQUE `referencia_externa` (patrón WS) para todos los canales |
| Reverso | **Nunca DELETE**: `estado_id → ANULADO/REVERSADO` + restitución de saldos + reversión de póliza. Un solo método `ReversarCobroAsync` |
| Contabilidad | Un solo generador (config F4 contable existente): documento `REC`, módulo `CAJA`; banco → kardex `DEP` + referencia |
| Efectos | Si saldo llega a 0: cancelar órdenes de corte (lógica actual, un solo lugar) |

Los canales son adaptadores delgados sobre el motor:
- **Caja (UI única)** → llama `CobroService` directo.
- **WS bancario** → `sp_ban_ws_pagar/reversar` se reescriben por dentro para
  escribir el modelo nuevo (misma firma, mismo contrato XML, mismos golden).
- **Futuro** (app, kioscos, pasarelas) → mismo motor.

## 5. La pantalla única de caja

**"Única" = un solo módulo/motor, NO una sola caja.** La empresa opera con
**varias cajas físicas simultáneas** (`adm_caja`, agregado en F2): cada cajero
abre su sesión EN una caja concreta (una sesión ABIERTA por caja, índice único
parcial), el arqueo sale por caja, y `adm_pago.sesion_caja_id` amarra cada
cobro transitivamente a su caja física. La UI de apertura (F3) exige elegir
caja; el mantenimiento de cajas es parte del módulo.

Nueva `apc.Client/Pages/Facturacion/Caja/CajaCobro.razor` (`/facturacion/caja/cobro`):

1. Buscar cliente (clave/nombre/RTN) **o** documento (N° factura/recibo).
2. Grid de **documentos pendientes** del cliente (facturas A/parciales, cuotas de
   plan vencidas, ND) con saldo por documento — reemplaza "cargar saldos".
3. Selección de documentos + monto (default: total; editable para parcial).
4. Forma de pago (efectivo/banco con permiso `AbonoBanco` actual).
5. Confirmar → recibo PDF (plantilla `Rpt_Dev_Recibo_Abono` unificada).

Sub-vistas del mismo módulo: **Recibos pendientes** (generar/cobrar/anular),
**Reversos** (con motivo, auditados), **Cobros del día / arqueo** (todos los
canales de la sesión), **Consulta** (la actual de abonos especiales, generalizada).

### Pantallas que se retiran (redirect + aviso durante 1 fase, luego borrado)

Carpeta real: `apc.Client/Pages/Facturacion/CaptacionPagos/`. Son **5 páginas
ruteables** (con `@page`) más sus componentes embebidos (sin ruta propia):

| Ruta (`@page`) | Página | Componentes embebidos que arrastra |
|---|---|---|
| `/facturacion/captacion/caja` | `Caja.razor` | `PosteoLectoras.razor`, `PosteoManual.razor`, `PosteoMiscelaneos.razor` |
| `/facturacion/captacion/abonos-especiales` | `AbonosEspeciales.razor` | `PosteoAbonos.razor` |
| `/facturacion/captacion/abonos-especiales/consulta` | `AbonosEspecialesConsulta.razor` | — |
| `/facturacion/captacion/abono-especial/{ClienteId}` | `AbonoEspecial.razor` | `GenerarReciboAbono.razor` |
| `/facturacion/captacion/reverso` | `Reverso.razor` | — |

También se borra `CaptacionPagosIndex.razor` (huérfano: referencia tabs
`Posteo*Tab` cuyos archivos ya no existen).

`/facturacion/cobranza` **se queda** (gestión: planes, cartera, acciones, cortes),
pero su pestaña de planes gana el ciclo de vida de cuotas (F6).

## 6. Fases

Cada fase = 1 PR contra `origin/main` + scripts DDL timestampeados en `Database/`
+ tests. El sistema opera normal durante toda la transición (dual-write).

### F1 — Catálogos y estados numéricos completos (4–5 días)
- DDL: `adm_tipo_transaccion` + seed + mapeo legacy; `adm_estado_pago`;
  `cfg_estado_documento_comercial` += `B(4)`; fix de la función del trigger;
  `transaccion_abonado.tipo_transaccion_id` (FK NULL) + backfill;
  `transaccion_abonado.estado_pago_id` (FK NULL) + backfill de 201/202
  (`C→1`, `P→2`, `A→3|4` según origen WS).
- `vw_transaccion_abonado_vigente` reescrita sobre ids (comportamiento idéntico
  — verificado por `SaldoVigenciaTests` ampliados).
- C#: `EstadosNumericos.cs` ampliado; entidades `factura`/`transaccion_abonado`
  exponen `estado_id`/`tipo_transaccion_id`/`estado_pago_id`. Incluye poner al
  día la entidad EF `transaccion_abonado`, hoy desfasada de la BD (le falta
  `estado_id`, que existe en BD desde 2026-05; `caja_id` vive en un partial).
- Aceptación: suite completa verde; saldos idénticos antes/después (query de
  auditoría por cliente).

### F2 — Modelo `adm_pago` + motor único con dual-write (6–8 días)
- DDL: `adm_pago`, `adm_pago_aplicacion`, `adm_documento_secuencia` +
  `fn_adm_siguiente_correlativo_documento` (seed serie RECIBO_PAGO por empresa,
  `valor_actual = MAX(numrecibo)`), índices tenant-safe.
- `CobroService` + `ICobroService` + DTOs + controller `api/cobros`
  (`[ModuleAuthorize(Ventas, Caja)]`) + registro DI.
- **F2b (PR aparte, inmediato)**: los 4 caminos C# actuales pasan a delegar en
  el motor (fachadas temporales, sin tocar UI aún; deben reproducir los payloads
  anónimos que la UI consume — PolizaStatus/PolizaEstado/BanKardexId — y
  conservar el reverso legacy para pagos pre-F2, cuyo documentId contable era
  offset+numrecibo y no existe en adm_pago). Reverso unificado post-fachada (se
  elimina el DELETE físico para todo cobro nuevo).
- Aceptación F2: tests nuevos del motor (aplicación total/parcial/multi-factura,
  FIFO por línea, idempotencia, reverso sin DELETE, sesión obligatoria, folio).
  Aceptación F2b: los tests existentes de caja/abonos verdes vía fachada.

### F3 — Pantalla única de caja (5–6 días)
- `CajaCobro.razor` + sub-vistas + `CobrosClient`; estándar de grid del repo.
- Retiro de las pantallas viejas (redirect en la primera release).
- Permisos: se reutilizan `PermissionNames.Ventas.Caja.*`; alta de endpoints
  nuevos en `PermissionEndpointCatalog`.
- Aceptación: E2E manual local (cobro total, parcial, banco, recibo pendiente,
  reverso, arqueo) + build verde.

### F4 — Saldo por documentos + lectores de saldo (5–8 días)
- `sp_obtener_cliente_saldo` v2: suma documentos pendientes (misma firma).
- Estado de cuenta (`ClientesServices`) y pantallas de cobranza leen documentos;
  desglose por servicio desde `factura_detalle` + `adm_pago_aplicacion`
  (la tabla `adm_desglose_abono_porcentaje` sigue para presentación).
- ~10 funciones `rep_*` actualizadas (misma salida, fuente nueva).
- Snapshot offline: sin cambio de contrato (el SP de saldo mantiene firma).
- Aceptación: `SnapshotMoraTests`/`SnapshotCamposPilotoTests` verdes sin tocar;
  reportes comparados contra la versión anterior con data de prueba.

### F5 — WS bancario sobre el modelo nuevo (3–5 días)
- `sp_ban_ws_pagar/reversar` reescritos por dentro: escriben `adm_pago` +
  aplicaciones (+ fila legacy hasta F7). `ban_ws_pago` se conserva como bitácora
  del canal (ya es numérica).
- Aceptación: **los 12 fixtures golden pasan sin modificación** (11 XML +
  `filtro-no-autorizado.txt`); `BancosWsSqlTests` ajustados a las escrituras
  nuevas.

### F6 — Cuotas de plan cobrables (3–5 días)
- `cln_plan_pago_dtl` gana `estado_id` + saldo de cuota; la cuota aparece como
  documento cobrable en la caja única (`documento_tipo=2`).
- Al aplicar pago: cuota → pagada; plan → completado al saldar la última.
- Se eliminan los movimientos `PLAN-CUOTA` sueltos para planes nuevos (el plan
  vive en sus tablas propias; el traslado de deuda se hace por aplicación de
  documentos, no por asientos de saldo).
- Aceptación: E2E crear plan → cobrar cuotas en caja → plan completado.

### F7 — Corte y limpieza (3–4 días + ventana de deploy)
- Corte del dual-write; `transaccion_abonado` a solo-lectura (revocar INSERT/
  UPDATE salvo rol de migración); retiro de `vw_transaccion_abonado_vigente`.
- DROP de los SPs legacy huérfanos (`sp_registrar_posteo_manual`,
  `sp_reversar_posteo_manual`, `sp_actualizar_factura_pago`,
  `sp_actualizar_detalle_posteomanual`, `sp_actualizar_detalle_posteolectora`)
  y de la tabla vacía `tipo_transaccion` (scaffold SIMAFI; su retiro arrastra
  entidad EF + `DbSet tipo_transaccions` + configuración en `SiadDbContext`,
  no solo el DDL). Cuidado: `fn_getclientesaldos_posteomanual` (definida en el
  mismo archivo fuente que esos SPs) está **viva** y NO se dropea. El DROP de
  `sp_registrar_posteo_manual` habilita además limpiar el overload DEPRECATED
  de 1 argumento de `sp_obtener_cliente_saldo` (cross-company) — se suma a esta
  limpieza.
- Borrado definitivo de páginas/servicios viejos (`AbonoService`,
  `CaptacionPagosService` — lo vigente ya vive en `CobroService`).
- ~~Migración de cartera real SIMAFI como documentos `SALDO_INICIAL`~~
  ⚠️ **SUPERSEDED (2026-07-28)** — ver abajo.
- Actualizar [ESTADOS_DOCUMENTOS_COMERCIALES.md](ESTADOS_DOCUMENTOS_COMERCIALES.md).

#### La cartera SIMAFI ya no se migra como `SALDO_INICIAL`

Decisión del usuario del 2026-07-28, documentada en
[PLAN_MIGRACION_SIMAFI_TOTAL_2026-07.md](PLAN_MIGRACION_SIMAFI_TOTAL_2026-07.md):
se migra **todo el histórico con los códigos y la numeración originales**, sin
documentos sintéticos ni marcas de migración. Eso **anula** el `SALDO_INICIAL`
por cliente/período que este plan asumía (§9.5) y entrega bastante más:

**Ya hecho y validado en local (M6 aprobada, 2026-07-29 — ver
[M6_VALIDACION_MIGRACION_SIMAFI_2026-07.md](M6_VALIDACION_MIGRACION_SIMAFI_2026-07.md)):**

| | |
|---|---|
| Clientes | 25,934 |
| Facturas (numeración original SIMAFI) | 3,896,909 |
| Líneas de detalle | 9,331,049 |
| Movimientos del libro | 12,173,095 |
| Pagos en `adm_pago` | 2,837,660 |
| Aplicaciones en `adm_pago_aplicacion` | 9,393,969 |
| **Saldo por cliente vs SIMAFI** | **25,530 de 25,530 exactos (L 48,858,786.58)** |

Puntos que tocan a este plan:

- **La cartera nace en el modelo nuevo**: la migración escribe `adm_pago` +
  `adm_pago_aplicacion` (§3.4/§3.5), no el camino legacy. Los
  `tipo_transaccion_id` siguen el mapeo de §3.1.
- **`transaccion_abonado` recibió los 12.2M de movimientos históricos**, coherente
  con §3.7 (queda como histórico solo-lectura, no se elimina).
- **`SALDO_INICIAL` (tipo 11) no se usa**: no hay saldo de apertura porque está
  la historia completa desde 2005.
- **Los pagos migrados no llevan folio de `adm_documento_secuencia`**: su
  `numero_recibo` es el id del movimiento original (el recibo de SIMAFI se repite
  entre pagos y la columna es única). La serie se resembró por encima de todo lo
  usado — `Database/2026-07-29_m3e_resembrar_secuencia_recibo.sql`.
- **Los 124 documentos del piloto de julio se borraron** (122 con CAI):
  duplicaban meses que SIMAFI también tiene. Esto ejecuta de hecho el
  `wipe_transaccional` que §8 anticipaba como riesgo.
- **Pendiente sin bloquear**: M5 — las 25,900 NC, 740 convenios y 15,851
  descuentos de adulto mayor están migrados como créditos aplicados pero aún no
  como `adm_nota_credito` ni `cln_plan_pago_*`. No mueve ningún saldo.

**Total estimado: 30–40 días hábiles ≈ 6–8 semanas** (ritmo por PRs como la
integración contable F1–F8), incluyendo pruebas y ventanas de deploy a 0.9.

## 7. Matriz de impacto — QUÉ AFECTAMOS

### Se crea
`adm_tipo_transaccion`, `adm_estado_pago`, `adm_pago`, `adm_pago_aplicacion`;
`CobroService`/`ICobroService`/DTOs/`CobrosController`/`CobrosClient`;
`CajaCobro.razor` + sub-vistas.

### Se modifica
| Qué | Cambio | Fase |
|---|---|---|
| `cfg_estado_documento_comercial` + trigger fn | +`B(4)`; ELSE deja de colapsar | F1 |
| `transaccion_abonado` | +2 columnas FK (transición); luego solo-lectura | F1/F7 |
| `factura`/`factura_detalle` | sin DDL; `estado_id` pasa a ser la fuente | F2+ |
| `sp_obtener_cliente_saldo` | misma firma, fuente = documentos | F4 |
| `sp_lectura_v3` | cargo con `tipo_transaccion_id`; sin cambio de contrato | F1 |
| SPs NC/ND | escriben `tipo_transaccion_id` (ya escriben ids de estado) | F1 |
| `sp_ban_ws_pagar/reversar` | internos al modelo nuevo; firma y XML intactos | F5 |
| Funciones `rep_*` (~10) | misma salida, fuente nueva | F4 |
| `ClientesServices` (estado de cuenta/desglose), `CobranzaService`, `CorteMasivoService` | leen documentos / motor | F4 |
| `CaptacionPagosService.ListarArqueos*` (el arqueo real vive ahí, no en `CajaService`) y `CajaService.ObtenerResumenAsync` (resumen por sesión) | absorbidos por la caja única | F3 |
| `cln_plan_pago_hdr/dtl` | +`estado_id`, ciclo de vida real | F6 |
| `PermissionEndpointCatalog` | endpoints `api/cobros` | F3 |
| Tests (~24 guardianes en `SaldoVigencia`, `SaldoCrossCompany`, `BancosWsSql`, caja) | reescritos/ampliados + suite nueva del motor | F1–F5 |

### Se retira
Pantallas de captación y abonos especiales (5 páginas ruteables + 5 componentes
embebidos + `CaptacionPagosIndex.razor` huérfano + clientes HTTP),
`AbonoService` + `CaptacionPagosService` (absorbidos), `vw_transaccion_abonado_vigente`,
5 SPs legacy huérfanos, tabla vacía `tipo_transaccion`, escrituras `PLAN-*`.

### NO se toca (blindado)
- **Contrato XML del banco**: byte-exacto, golden files sin modificar (el
  `<estado>` va vacío por diseño; los `<detalle>` siguen saliendo de
  `factura_detalle`).
- **App de lectores**: cero cambios de APK; el snapshot JSON mantiene contrato
  (`saldo_anterior_total`, `mora`, `saldos_por_servicio` — misma forma, el SP de
  saldo conserva su firma).
- **Motor tarifario y `sp_lectura_v3` en su lógica de cálculo** (solo gana el id
  de tipo en el INSERT).
- **Contabilidad core** (motor de comprobantes por config F1–F8): se consume tal
  cual desde el motor único; los asientos siguen cuadrando durante el dual-write.
- **Identity/permisos base**, multi-tenancy, NC/ND y CAI (ya numéricos).

## 8. Riesgos y mitigaciones

| Riesgo | Mitigación |
|---|---|
| Desalineación saldo legacy vs nuevo durante dual-write | Query de auditoría por cliente (SUM legacy vs SUM documentos) corriendo en cada fase; corte F7 solo con diff = 0 |
| Reportes con salida distinta | Comparación automatizada de las 10 funciones `rep_*` antes/después con la data de prueba |
| WS bancario en producción | F5 se valida con los golden + réplica del caso real; deploy en ventana propia como F8 |
| Regresión en app de lectores | `SnapshotMoraTests`/`SnapshotCamposPilotoTests` intocados deben pasar verbatim |
| Datos de prueba contaminados (`SALDO_ANTERIOR`, 0998) | ✅ **RESUELTO en local (2026-07-29)**: la migración total borró los 124 documentos del piloto y cargó la cartera real completa sobre el modelo nuevo. Falta replicarlo en 0.9 cuando se decida el despliegue |

## 9. Decisiones que este plan asume (confirmar al aprobar)

1. Convención `adm_*` para las tablas nuevas (incl. `adm_estado_pago`, aunque los
   catálogos de estado previos son `cfg_*`). ✔ indicado por el usuario.
2. Se **reescriben por dentro** los SPs del WS bancario (no se crea un canal
   paralelo).
3. `transaccion_abonado` queda como histórico solo-lectura, **no se elimina**.
4. ✅ **CONFIRMADO (contador, 2026-07-26)**: documento contable único `REC`
   (módulo VENTAS) para todo cobro del motor — desaparece la distinción
   REC/ABO. Implementado en `CobroService.ResolverDocumentoContable`; los ABO
   históricos no se tocan y se reversan por su camino legacy.
5. ~~La cartera real SIMAFI se migrará como documentos `SALDO_INICIAL` por
   cliente/período~~ ⚠️ **ANULADO (usuario, 2026-07-28)**: se migra **todo el
   histórico con códigos y numeración originales**, sin documentos sintéticos.
   Ya ejecutado y validado en local — ver F7 §"La cartera SIMAFI ya no se migra
   como `SALDO_INICIAL`".
6. **El recibo de cobro NO lleva CAI** (§3.8): folio interno por empresa vía
   `adm_documento_secuencia`, único por `(company_id, numero_recibo)`. El CAI
   sigue intacto para FAC/NC/ND.
7. ✅ **CONFIRMADO (contador, 2026-07-26)**: folio con **continuidad estricta**
   — ya garantizada por construcción (§3.8): el folio se consume dentro de la
   transacción del cobro y el rollback lo devuelve.
8. ✅ **CONFIRMADO (contador, 2026-07-26)**: los misceláneos que son **venta
   nueva se emiten como factura con CAI** (tipo fiscal correcto, serie CAI
   propia); el recibo interno queda solo para el COBRO de documentos ya
   emitidos. Diseño de detalle en F3: la pantalla de venta miscelánea emite la
   factura (con CAI) y la caja única la cobra como cualquier factura — corrige
   de raíz la inconsistencia actual (`tipofactura='R'` sin CAI con
   `tipo_documento_fiscal_id` default 1).

## 10. Hallazgos anexos (fuera de alcance, registrar como correcciones aparte)

- **Dos catálogos de estado de CAI contradictorios**: `cfg_cai_estado`
  (1 DISPONIBLE … 5 ANULADA) vs `cfg_estado_cai` (1 VIGENTE … 5 SUSPENDIDO).
  La FK de `adm_cai_facturacion.estado_id` apunta a `cfg_estado_cai`, pero
  `CaiTarifarioService` razona con la semántica del otro; los filtros de emisión
  (`estado_id=1`) coinciden por casualidad. Unificar en un solo catálogo.
- **Entidad EF `adm_cai_facturacion` desactualizada** (sin `establecimiento_codigo`,
  `correlativo_actual`, `estado_id`) — el módulo opera por Dapper.
- **`factura.numdei`** es duplicado de `numfactura`, ya marcado para eliminar en
  BUGS_MOTOR_FACTURACION §296.
- **Estado `'N'` fantasma en pagos**: `CajaService` filtra `estado != 'N'` pero
  ninguna ruta lo escribe (era la propuesta de normalizar el reverso a `N` de
  ESTADOS_DOCUMENTOS_COMERCIALES que quedó a medias) — el filtro es letra
  muerta; muere con el motor único.
- **Correlativos con carrera — detalle**: los 4 generadores (`CobranzaService`
  planes/notas/cartas, `CorteMasivoService`) no usan `MAX()` SQL sino top-1 por
  orden descendente de string (rompe si cambia el ancho `D6`); y el advisory
  lock de `CorteMasivoService` protege `orden_trabajo.orden_numero` pero se
  toma *después* de generar el correlativo del header, que queda sin proteger.
  Todos migran a `fn_adm_siguiente_correlativo_documento` en F6 (§3.8).
