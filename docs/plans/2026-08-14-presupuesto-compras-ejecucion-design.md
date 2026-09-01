# Control de presupuesto en compras — afectación por ejecución (2026-08-14)

**Objetivo:** que la **recepción de la factura de compra** valide y consuma presupuesto, de modo que no se pueda registrar una compra que exceda lo presupuestado para la cuenta afectada.

**Modalidad elegida (usuario, 2026-08-14):** **ejecución**, no reserva. El presupuesto se afecta cuando la compra se ejecuta (se recibe la factura), **no** cuando se aprueba la orden de compra.

> **Estado: DISEÑO. Sin código ni SQL derivado.** Contiene decisiones abiertas (D1–D6) que requieren al contador o al usuario.

Antecedente que este documento revierte parcialmente: `docs/centura-flujos/README_orden_compra.md:30` y `:176` — **D-OC-5 / D-OC-b** (usuario, 2026-07-30): *"centro de costo por renglón, solo informativo, sin validar presupuesto"*. Esa decisión sigue vigente **para la O/C**; lo que aquí se agrega es el control en la **recepción**, que es un punto distinto. Ver §11.

---

## 1. Punto de partida: qué existe hoy

### 1.1 El presupuesto existe y funciona, pero compras no lo toca

| Pieza | Dónde | Estado |
|---|---|---|
| `pst_config_presupuesto_hdr` / `_dtl` | `SIAD.Core/Entities/`, PK compuesta `(company_id, id_presupuesto[, con_cuenta_code])` | En uso. Presupuestos **anuales** (`PRE-2025`, `PRE-2026`), `rango_periodo` fijo en 12 |
| Marca de cuenta presupuestable | `con_plan_cuentas.allows_budget` (checkbox "Presupuesto" en `PlanCuentaForm.razor:138`) | Existe. **Ningún script del repo la enciende** — verificar el dato real antes de asumir nada (§10) |
| Consumo real | `OrdenesPagoDirectoService.ApplyCompromisoPresupuestoAsync` (compromiso a proveedor) y `BanTransaccionesService` (créditos bancarios) | Las **dos únicas** rutas vivas |
| Compras | — | **Cero referencias a `pst_`** en `SIAD.Services/Almacen/`, `apc/Controllers/Almacen/`, `apc.Client/Pages/Almacen/` |

### 1.2 Los tres montos del modelo

- `dtl.valor_proyeccion` — lo presupuestado para esa cuenta. **Es el tope** contra el que se valida.
- `dtl.valor_real` — lo ejecutado. Es lo que se incrementa.
- `dtl.valor_disponible` — derivado: `MAX(valor_proyeccion − valor_real, 0)`.
- `hdr.valor_disponible` — derivado: `MAX(valor_global − Σ dtl.valor_real, 0)`. **Base distinta a la del detalle** (global vs proyección); no confundirlas.

**No existe "comprometido"**: el modelo no distingue reserva de ejecución. Por eso la modalidad "ejecución" encaja sin tocar el modelo; una reserva habría exigido una cuarta columna y un ciclo de liberación.

### 1.3 El flujo de recepción donde hay que engancharse

`RecepcionCompraService.CrearAsync` (`SIAD.Services/Almacen/RecepcionCompraService.cs:227-369`):

```
269  ── BEGIN (TransaccionAmbiente.IniciarAsync)
274     BloquearYLeerPendientesAsync      FOR UPDATE sobre renglones de la O/C
276     SiguienteNumeroAsync              FOR UPDATE sobre alm_compra_correlativo  ← serializa altas de la empresa
312     ArmarLineasYTotales               ← aquí ya existen sub_total, impuesto, total
322     SaveChangesAsync                  ← aquí ya existen cabecera.id y linea.id
329-347 Posteo al kardex por línea
354     GenerarCxp
359     CompraContabilidad.ContabilizarFacturaAsync
365     SaveChangesAsync
366  ── COMMIT
```

Tres hechos que condicionan el diseño:

1. **Todo corre en una transacción real.** Cualquier `throw` entre 269 y 366 revierte kardex, CxP, correlativo, descarga de O/C y asiento. Fallar tarde no deja basura.
2. **No hay edición de recepción**, solo alta y anulación (`IRecepcionCompraService`, y el controlador no expone PUT/PATCH). Basta con **dos hooks**, no tres.
3. **La recepción puede ser directa**, sin O/C (`alm_compra_hdr.orden_compra_id` es nullable). Por eso el control **debe** estar en la recepción: si estuviera solo en la O/C se saltaría comprando directo.

---

## 2. Decisión central: ¿contra qué cuenta muerde?

Esta es la decisión que define todo lo demás, y hay una tensión real que conviene nombrar antes de resolverla.

El asiento actual de la factura (`CompraContabilidad.cs:59-98`) es:

```
DEBE   cuenta_inventario  (del tipo de artículo de cada renglón, agrupado por cuenta)
HABER  cuenta del proveedor  (por el total)
```

**Comprar inventario es un activo, no un gasto.** Presupuestariamente, el gasto ocurre cuando el material se descarga al consumo, no cuando entra a bodega. Si se controla el presupuesto en la compra, se está controlando *la adquisición*, no *el consumo*.

Además, hoy **no existe compra de gasto directo**: `alm_tipo_articulo.maneja_inventario` existe (`alm_tipo_articulo.cs:32`) pero `RecepcionCompraService` **no lo consulta en ninguna línea** — todo renglón se postea al kardex y `CompraContabilidad` exige `cuenta_inventario` para todos. Los 9 tipos están en `true`.

### Propuesta

**La cuenta presupuestaria de cada renglón es la misma cuenta que el asiento debita**, y solo participa si tiene `allows_budget = true`.

Ventajas:

- **Una sola fuente de verdad.** Presupuesto y contabilidad no pueden divergir: si el asiento debita una cuenta, el presupuesto se afecta en esa cuenta y por el mismo importe.
- **El contador decide el alcance sin tocar código**, marcando `allows_budget` en el plan de cuentas. Si marca las cuentas de inventario, la compra consume presupuesto. Si no las marca, la compra no consume nada y el control queda listo para cuando se enganche el descargo.
- Es exactamente el criterio que ya usa el compromiso a proveedor (`ApplyCompromisoPresupuestoAsync:4063`), así que no se introduce un segundo modelo mental.

Consecuencia que hay que aceptar explícitamente: **si se controla la compra, el descargo posterior no debe volver a consumir presupuesto** (sería doble conteo). Este diseño no engancha descargos; si más adelante se quiere mover el control al consumo, se apaga `allows_budget` en las cuentas de inventario, se prende en las de gasto y se engancha `DescargoDocumentoService` con el mismo servicio. → **D1**.

---

## 3. Diseño

### 3.1 Cuándo

En **`CrearAsync`, un solo punto: entre las líneas 354 y 359** (junto a `GenerarCxp`, antes del asiento contable).

Por qué ahí y no antes:
- `cabecera.id` y `linea.id` ya existen (SaveChanges de 322) → la bitácora puede referenciar el documento.
- Los totales ya están calculados (`ArmarLineasYTotales`, 312).
- Ya se tomó el candado de `alm_compra_correlativo` (276), que serializa altas concurrentes **de la misma empresa**.
- Fallar aquí es gratis: rollback completo.

No se hace un pre-chequeo temprano adicional. Duplicaría consultas para adelantar un mensaje de error que el usuario ve igual, y abre la puerta a que el pre-chequeo y la afectación definitiva discrepen.

### 3.2 Sobre qué monto

**Sobre la misma distribución por cuenta que produce el asiento**, incluido el remanente.

El asiento reparte así (`CompraContabilidad.cs:83-89`): suma `costo_posteo × cantidad` por cuenta, y el remanente contra el total de la factura —flete, otros gastos, descuento global e ISV no prorrateado— lo capitaliza en la cuenta de mayor valor. Resultado: `Σ DEBE = total de la factura`.

El presupuesto usa **esa misma distribución**. Consecuencia: se ejecuta presupuesto por el total de la factura, que es lo que efectivamente se va a pagar al proveedor.

Salvedad conocida: hoy el ISV se capitaliza siempre, incluso cuando `cfg_compra_isv` está en modo FISCAL (crédito fiscal). Es un pendiente ya registrado del asiento (`CompraContabilidad.cs:25-26`). El presupuesto **hereda esa imprecisión a propósito**: mantenerse pegado al asiento significa que cuando se separe el crédito fiscal, el presupuesto se corrige solo. → **D2**.

### 3.3 Con qué fecha

`alm_compra_hdr.fecha` (la fecha de la transacción), que es la misma que se pasa al asiento contable (`RecepcionCompraService.cs:362`). El presupuesto vigente se resuelve por `fecha_inicia <= fecha <= fecha_finaliza`.

En la **anulación**, la liberación usa la **fecha original de la factura**, no la de anulación — así se devuelve el monto al mismo presupuesto que lo consumió. Es el criterio que ya sigue el compromiso (`OrdenesPagoDirectoService.cs:3170`).

### 3.4 Con qué severidad

Tres modos por empresa y por módulo:

| Modo | Comportamiento |
|---|---|
| **0 — Apagado** | No consulta presupuesto. Comportamiento idéntico al de hoy. **Default.** |
| **1 — Advertencia** | Registra el consumo y deja pasar aunque exceda. Devuelve los avisos para mostrarlos al usuario. |
| **2 — Bloqueo** | Rechaza la recepción si excede el disponible de la cuenta. |

El modo Advertencia no es adorno: permite encender el control en producción, ver un mes de datos reales y detectar cuentas mal presupuestadas **sin bloquear la operación**. Sin él, el primer día de bloqueo se convierte en una fila de facturas que no se pueden registrar.

### 3.5 Qué pasa si la cuenta no tiene presupuesto

Se replica el criterio del compromiso, que es el correcto:

- Cuenta **sin** `allows_budget` → se ignora en silencio. No participa del control.
- Cuenta **con** `allows_budget` pero **sin** presupuesto vigente y aprobado a esa fecha → **error** en modo Bloqueo, **aviso** en modo Advertencia. Marcar una cuenta como presupuestable y no presupuestarla es un error de configuración, no un caso normal.
- En la **liberación** (anulación) no se exige que el presupuesto esté aprobado: se puede anular contra un presupuesto que ya se cerró.

---

## 4. Dónde vive la lógica: en la base de datos

**La afectación se implementa como procedimiento de Postgres, no como read-modify-write en C#.** Tres razones:

1. **La regla del repo.** `.github/skills/hodsoft-sin-linq/SKILL.md`: todo acceso a datos va por SP, función o vista; código nuevo con cero LINQ. El mecanismo actual del compromiso (`ApplyCompromisoPresupuestoAsync`) es LINQ + mutación de entidades EF — replicarlo tal cual sería código nuevo en violación directa.
2. **Concurrencia.** El mecanismo en C# **no toma ningún lock** sobre `pst_config_presupuesto_*` (verificado: sin `FOR UPDATE`, sin advisory lock, sin `xmin`). Se apoya solo en `IsolationLevel.Serializable`, y el `40001` resultante **no se maneja** en los caminos principales de OPD → un 500 crudo bajo concurrencia. En la base, un `SELECT ... FOR UPDATE` sobre el detalle resuelve el problema de raíz. Precedente en el propio repo: `fn_pst_afectar_saldo_real_credito` ya lo hace (`Database/ddl_v3/20260306_presupuesto_credito_allows_budget.sql:77`).
3. **Auditoría.** El SQL queda versionado en `Database/` y desplegable al SRV; la validación presupuestaria es exactamente el tipo de regla que el contador va a querer leer.

### 4.1 Objetos nuevos

**a) Tipo compuesto de línea** (precedente: `tipo_linea_partida` de `sp_pst_aplicar_partida_presupuesto`)

```
pst_linea_afectacion ( con_cuenta_code VARCHAR(20), monto NUMERIC(18,4) )
```

**b) Bitácora de afectación — `pst_afectacion`**

Hoy **no hay ninguna trazabilidad**: `pst_config_presupuesto_dtl.valor_real` es un número acumulado sin historia, sin auditoría y sin referencia al documento que lo movió. Con un solo módulo consumiendo ya era incómodo; con dos es indefendible — nadie podría responder "¿por qué esta cuenta está al 90%?".

| Columna | Tipo | Nota |
|---|---|---|
| `id` | BIGSERIAL | |
| `company_id` | BIGINT NOT NULL | tenant |
| `id_presupuesto` | VARCHAR(10) NOT NULL | |
| `con_cuenta_code` | VARCHAR(20) NOT NULL | |
| `modulo` | VARCHAR(20) NOT NULL | `COMPRAS`, `PROV`, `BANCOS`… |
| `documento_tipo` | VARCHAR(20) NOT NULL | `FACTURA` |
| `documento_id` | BIGINT NOT NULL | `alm_compra_hdr.id` |
| `documento_numero` | VARCHAR(40) | número visible |
| `fecha` | DATE NOT NULL | la que resolvió el presupuesto |
| `monto` | NUMERIC(18,4) NOT NULL | **con signo**: + consume, − libera |
| `excedio` | BOOLEAN NOT NULL DEFAULT false | true si pasó en modo Advertencia |
| `usuario` | VARCHAR(50) | |
| `fecha_registro` | TIMESTAMP NOT NULL DEFAULT now() | |

Índice único de idempotencia: `(company_id, modulo, documento_tipo, documento_id, con_cuenta_code, signo(monto))` — un reintento no puede duplicar el consumo, y la anulación no puede liberar dos veces.

**c) Configuración del control — `cfg_presupuesto_control`**

`(company_id, modulo)` → `modo SMALLINT NOT NULL DEFAULT 0` (§3.4). Tabla propia y no una columna en `con_integracion_config` porque el control presupuestario no es integración contable, y porque permite encender compras hoy y descargos mañana de forma independiente.

**d) El procedimiento — `sp_pst_afectar_documento`**

```
sp_pst_afectar_documento(
    p_company_id      BIGINT,
    p_modulo          VARCHAR,
    p_documento_tipo  VARCHAR,
    p_documento_id    BIGINT,
    p_documento_numero VARCHAR,
    p_fecha           DATE,
    p_usuario         VARCHAR,
    p_direccion       SMALLINT,          -- +1 consume, -1 libera
    p_lineas          pst_linea_afectacion[]
) → tabla de avisos (cuenta, disponible, exceso)
```

Secuencia interna, por línea:

1. Leer el modo de `cfg_presupuesto_control`. Si es 0 → salir sin hacer nada.
2. Descartar la línea si la cuenta no tiene `allows_budget` en `con_plan_cuentas`.
3. `SELECT ... FROM pst_config_presupuesto_dtl d JOIN pst_config_presupuesto_hdr h ... WHERE company_id = ... AND upper(con_cuenta_code) = ... AND h.fecha_inicia <= p_fecha AND h.fecha_finaliza >= p_fecha AND (p_direccion < 0 OR h.estado_aprobado) ORDER BY h.fecha_inicia DESC, h.id_presupuesto DESC LIMIT 1 **FOR UPDATE OF d**`.
4. Si no hay fila → excepción (Bloqueo) o aviso (Advertencia).
5. `nuevo = valor_real + p_direccion * monto`. Si `p_direccion > 0` y `nuevo > valor_proyeccion` → excepción en modo Bloqueo; en Advertencia, aviso y `excedio = true`.
6. `UPDATE dtl SET valor_real = GREATEST(nuevo, 0), valor_disponible = GREATEST(valor_proyeccion − valor_real, 0)`.
7. `INSERT INTO pst_afectacion (...) ON CONFLICT DO NOTHING` — la idempotencia vive aquí.
8. Al final, recalcular `hdr.valor_disponible = GREATEST(valor_global − Σ dtl.valor_real, 0)` de los presupuestos tocados.

**e) La distribución por cuenta — `fn_alm_compra_distribucion_cuentas(company_id, compra_hdr_id)`**

Devuelve `(con_cuenta_code, monto)` replicando exactamente la regla del asiento: `costo_posteo × cantidad` agrupado por `alm_tipo_articulo.cuenta_inventario`, más el remanente contra el total en la cuenta de mayor valor.

Nace para el presupuesto, pero su destino es ser **la fuente única de ambos**: en F3 `CompraContabilidad` pasa a consumirla, lo que de paso elimina su consulta N+1 por renglón (`CompraContabilidad.cs:65-75`) y su LINQ. Hasta entonces hay una duplicación deliberada de la regla en dos lugares, cubierta por un test que compara ambas salidas (§8).

### 4.2 El lado C#

Un servicio delgado, `SIAD.Services/Presupuesto/PresupuestoAfectacionService.cs` (+ interfaz), registrado en `ServiceRegistration.cs`. Invoca por Dapper sobre la conexión y transacción del `SiadDbContext` —el patrón de `CompraContabilidad.cs:43-44`— para quedar dentro de la transacción del alta. Sin LINQ, sin lógica de negocio: arma parámetros, llama, traduce el error de Postgres a `InvalidOperationException` con el mensaje al usuario, y devuelve los avisos.

```csharp
Task<IReadOnlyList<PresupuestoAvisoDto>> AfectarAsync(
    SiadDbContext context, long companyId, string modulo, string documentoTipo,
    long documentoId, string documentoNumero, DateOnly fecha, string usuario,
    short direccion, CancellationToken ct);
```

**No se toca `OrdenesPagoDirectoService` en esta entrega.** Migrarlo al servicio compartido es deseable (elimina la duplicación del criterio y le da bitácora y locks) pero es un archivo de 231 KB en un flujo crítico; va como fase opcional posterior con sus propios tests de regresión. → **D5**.

### 4.3 Los dos enganches

| Momento | Ubicación | Llamada |
|---|---|---|
| **Alta** | `RecepcionCompraService.CrearAsync`, entre 354 y 359 | `direccion: +1`, fecha = `cabecera.fecha` |
| **Anulación** | `AnularAsync`, entre 473 y 476 (junto a `AnularCxpDeFacturaAsync` y la reversa contable) | `direccion: −1`, fecha = **la original de la factura** |

Ambos dentro de la transacción existente. No hace falta un tercer hook porque no existe edición de recepción.

---

## 5. Interfaz de usuario

1. **Antes de guardar** — en `RecepcionCompraFormPage.razor`, un panel de disponible por cuenta afectada, consultado al cambiar los renglones. Ver el tope *después* del rechazo es la peor forma de enterarse.
2. **Al rechazar** — mensaje con cuenta (formateada con `IAccountFormatService`, como hace el compromiso), disponible y exceso: *"La compra excede el presupuesto disponible para la cuenta 5-01-02-001. Disponible: 12,340.00, requerido: 18,900.00."*
3. **En modo Advertencia** — toast no bloqueante y marca en la factura, para que el sobregiro quede visible y no silencioso.
4. **Consulta de ejecución** — hoy solo existe el grid maestro-detalle de `PresupuestoConfiguracionesList.razor` y el PDF `Rpt_Dev_Presupuesto`, ambos sin drill-down. Con `pst_afectacion` se puede por fin abrir una cuenta y ver **qué documentos la consumieron**. Es la entrega que vuelve auditable todo lo demás. → F4.
5. **Configuración** — pantalla del modo por módulo. Mientras no exista, se cambia por SQL.

---

## 6. Fases

| Fase | Alcance | Entregable |
|---|---|---|
| **F0 — Base de datos** | `pst_linea_afectacion`, `pst_afectacion`, `cfg_presupuesto_control`, `sp_pst_afectar_documento`, `fn_alm_compra_distribucion_cuentas` | Scripts en `Database/` + registro en el runbook SRV (skill `runbook-despliegue-srv`) |
| **F1 — Servicio + enganche** | `IPresupuestoAfectacionService` + los dos hooks en `RecepcionCompraService` | Servicio, DI, tests |
| **F2 — UI de la recepción** | Panel de disponible, mensajes de rechazo, avisos | `RecepcionCompraFormPage.razor` |
| **F3 — Unificación con el asiento** | `CompraContabilidad` consume `fn_alm_compra_distribucion_cuentas`; se elimina el N+1 y la duplicación de la regla | Refactor + test de equivalencia |
| **F4 — Consulta de ejecución** | Drill-down por cuenta sobre `pst_afectacion`; pantalla de configuración del modo | Pantalla + reporte |
| **F5 — Opcional** | Migrar `OrdenesPagoDirectoService` al servicio compartido (bitácora + locks para el compromiso) | Refactor + regresión |

F0 y F1 son el mínimo funcional. F2 debería acompañarlas: bloquear sin mostrar el disponible es hostil.

---

## 7. Despliegue seguro

- **Default apagado** (`modo = 0`): aplicar los scripts y publicar el binario no cambia el comportamiento de nadie.
- Encender exige tres cosas, en este orden: (1) cuentas con `allows_budget = true`, (2) presupuesto vigente y **aprobado** que cubra esas cuentas en la fecha de operación, (3) `cfg_presupuesto_control.modo = 1` (advertencia) y, tras validar un período, `= 2`.
- Los scripts van **con binario**: el SP y el servicio se despliegan en la misma ventana.

---

## 8. Pruebas (`SIAD.Tests/Almacen/`, patrón `BEGIN … ROLLBACK`)

1. Modo 0 → la recepción no consulta presupuesto y `pst_afectacion` queda vacía (no-regresión: el comportamiento de hoy).
2. Cuenta sin `allows_budget` → se ignora, la recepción pasa.
3. Modo 2, dentro del disponible → `valor_real` sube por el total de la factura, `valor_disponible` baja, una fila en `pst_afectacion` por cuenta.
4. Modo 2, excede → la recepción falla **y no queda nada**: ni kardex, ni CxP, ni correlativo consumido, ni asiento (verifica el rollback completo).
5. Modo 1, excede → pasa, `excedio = true`, avisos devueltos.
6. Cuenta con `allows_budget` y sin presupuesto vigente → falla en modo 2, avisa en modo 1.
7. Anulación → libera exactamente lo consumido, contra el presupuesto de la fecha original; `valor_real` vuelve al valor previo.
8. Anular dos veces / reintentar el alta con el mismo `uuid` → no duplica (idempotencia por el índice único).
9. Compra directa (sin O/C) → afecta igual.
10. **Equivalencia**: `fn_alm_compra_distribucion_cuentas` devuelve exactamente las mismas cuentas y montos que el DEBE del asiento de `CompraContabilidad`. Vigila la duplicación de §4.1(e) hasta F3.
11. Concurrencia: dos recepciones simultáneas contra la misma cuenta con disponible para una sola → una pasa, la otra falla con mensaje de negocio (no un 500).

---

## 9. Decisiones abiertas

| # | Decisión | Recomendación |
|---|---|---|
| **D1** | ¿El control muerde en la **compra** (cuenta de inventario) o en el **consumo** (descargo, cuenta de gasto)? Si es en la compra, el descargo no debe volver a consumir. | Compra, como pidió el usuario. Se implementa de forma que mover el control al consumo después sea configuración (`allows_budget`) + un enganche, no rediseño. **Confirmar con el contador** cómo está armado el presupuesto real de MERENDON: si sus cuentas presupuestadas son de gasto y no de inventario, marcar `allows_budget` no alcanzará y hay que revisar D3. |
| **D2** | ¿El presupuesto se ejecuta por el **total** de la factura (con ISV capitalizado, flete y otros gastos) o solo por la base? | Por el total, pegado al asiento. Si el contador quiere excluir el ISV de crédito fiscal, se resuelve al separar el crédito fiscal en el asiento y el presupuesto se corrige solo. |
| **D3** | ¿Hace falta **compra de gasto directo** (renglones que no entran a inventario)? Hoy no existe: `maneja_inventario` está en `true` en los 9 tipos y la recepción no lo consulta. | Fuera de alcance de esta entrega, pero es el desbloqueo natural si D1 apunta a cuentas de gasto. Ya hay diseño previo en `docs/plans/2026-07-29-configuracion-isv-compras-design.md` (F4). |
| **D4** | ¿Control por **centro de costo** además de por cuenta? | **No.** El modelo de presupuesto no tiene eje de centro de costo (`pst_config_presupuesto_dtl` solo lleva `con_cuenta_code`), el `centro_costo` de la O/C es texto libre de 40 chars sin lookup, y no se copia a `alm_compra`. Agregarlo es un proyecto aparte: catálogo real + eje nuevo en presupuesto + propagación O/C → factura. |
| **D5** | ¿Migrar el compromiso a proveedor al servicio compartido? | Sí, pero como F5. Deja al compromiso sin bitácora ni locks mientras tanto — un hueco preexistente, no uno nuevo. |
| **D6** | ¿Permiso para **sobrepasar** el presupuesto (un rol que fuerce la compra en modo Bloqueo)? | Solo si el negocio lo pide. Añade un permiso en `PermissionNames` y un flag en el DTO; el sobregiro autorizado quedaría en `pst_afectacion` con `excedio = true`. |

---

## 10. Riesgos

1. **`allows_budget` puede estar apagado en todo el plan de cuentas.** Ningún script del repo lo enciende, el default es `false` y la importación masiva del plan lo apaga explícitamente (`ContabilidadCatalogosService.cs:1444`). Si es así, el control es un no-op silencioso: no falla, simplemente no hace nada. **Verificar el dato en el servidor antes de dar por funcionando el mecanismo** — incluido el del compromiso a proveedor, que podría llevar meses sin controlar nada.
2. **Presupuestos solo anuales.** `rango_periodo` está forzado a 12 y la vigencia se resuelve por rango de fechas. No hay control mensual ni trimestral; pedirlo implica agregar el eje de período al modelo.
3. **Duplicación temporal de la regla de distribución** entre `fn_alm_compra_distribucion_cuentas` y `CompraContabilidad`, hasta F3. Mitigada por el test de equivalencia (§8.10).
4. **Los scripts de presupuesto (pasos 14/15/18 del runbook) están pendientes en el SRV** (`Database/2026-07-30_pendientes_srv.md:54-55`). Este diseño no depende de `sp_pst_aplicar_partida_presupuesto` —que sigue siendo código muerto—, pero sí de que las tablas multitenant estén al día en el servidor.
5. **Reversa contra un presupuesto ya cerrado**: anular una factura del año anterior libera contra el presupuesto de esa fecha, que puede estar cerrado. El diseño lo permite a propósito (no exige aprobado en la liberación); si el contador prefiere bloquearlo, es una condición más en el SP.

---

## 11. Sobre D-OC-b

La decisión del 2026-07-30 fue **no validar presupuesto en la orden de compra**, y **sigue vigente**: este diseño no toca `OrdenCompraService.AprobarAsync`, la O/C se aprueba sin consultar presupuesto y el `centro_costo` sigue siendo informativo.

Lo que cambia es que aparece un control en un punto que aquella decisión no cubría —la recepción—, que es además el único punto que no se puede saltar (la compra directa no pasa por O/C). Conviene actualizar `README_orden_compra.md` cuando esto se implemente, para que la nota de D-OC-5 no se lea como "compras no controla presupuesto".
