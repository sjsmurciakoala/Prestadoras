# Existencia NEGATIVA en salidas de inventario — Plan corto por fases

Fecha: 2026-08-15 · Rama: `feat/almacen-integracion-contable`

Estado: **INICIATIVA COMPLETA (F0–F3 + F5)**, verificada. F0 aplicado al mirror; F1 motor + costeo +
fix reversa; F2 verificación de flujos; F3 aviso forzado a Negativa; F5 UI (interruptor de empresa
`/almacen/existencia-negativa` + override tri-estado en la ficha de bodega). **F4 DESCARTADO** por
decisión del usuario (2026-08-15): solo config, sin permiso. 22 pruebas nuevas, **suite total
769/48/0**; app compila y arranca limpia (DI de F5 verificado en el boot). **Sin commit; SRV pendiente.**

> Nota de implementación: el interruptor se resuelve **dentro del motor** (`InventarioPostingService.PermiteNegativoAsync`,
> lee `cfg_inventario_negativo` + `alm_bodega` por `_context`), en vez de inyectar un
> `INegativoInventarioConfigService` — menos ripple y es el estilo del motor. Ese servicio se crea en
> F5 para la pantalla (Obtener/Guardar).

---

## Requerimiento

En la operación real el almacenero **físicamente tiene el material** y lo entrega aunque el sistema
marque 0 (desfase físico vs. sistema). Hoy el motor **bloquea** cualquier salida que dejaría la
existencia en negativo, así que esa entrega no se puede registrar. Se quiere **permitir** el negativo
(para reflejar el físico y reconciliar después), pero **NO abierto para todos**: detrás de un
**interruptor** (config por empresa y/o bodega) o permiso.

## Contexto (hallazgos de la exploración)

- **Punto único de bloqueo.** El motor `InventarioPostingService.ValidarAsync`
  ([`InventarioPostingService.cs:334-395`](../../SIAD.Services/Almacen/InventarioPostingService.cs))
  lanza `InvalidOperationException` "…dejaría la existencia en negativo…" en **tres** ramas de salida:
  - `AjusteNegativo` (:334) — la usa **el ajuste directo** (`AjusteInventarioService`) **y el
    movimiento genérico de salida** (`MovimientoAlmacenService` mapea `clase SALIDA → AjusteNegativo`).
  - `SalidaDescargo` (:351) — la usa el **descargo** (`DescargoDocumentoService`).
  - `TrasladoSalida` (:375) — la usa el **traslado (envío)** (`TrasladoAlmacenService`).

  Es decir, los cuatro flujos del requerimiento (descargo, movimiento genérico de salida, ajuste
  negativo, traslado de salida) pasan por **estas tres ramas** — un solo lugar que aflojar.
- **El motor ya conoce empresa + bodega bajo candado.** Tras `BloquearArticuloBodegaAsync`
  (:255) tiene la `fila` con `company_id` y `bodega_id`; puede resolver el interruptor **sin confiar
  en el llamador** (mismo criterio por el que el `company_id` no viaja en el DTO).
- **El costeo de la salida NO se corrompe por sí solo.** `Calcular` para salidas (:509-519) devuelve
  el promedio **vigente sin moverlo** (una salida sale al promedio, no lo recalcula). El borde del
  promedio ponderado está en la **entrada siguiente** sobre existencia negativa (ver Decisión 5).
- **El aviso por cruce existe y es anti-spam.** `PostearAsync` (:95-99) marca `CruzoAlerta` solo al
  pasar de "en orden" a alerta (`severidadAntes is null && severidadDespues is not null`). Una caída
  **dentro** de alerta (bajo mínimo → negativo) hoy **no** re-avisa. Severidades en
  [`StockSeveridad`](../../SIAD.Core/DTOs/Almacen/StockSeveridad.cs) (`< 0 → Negativa`).
- **Patrón de config por empresa ya establecido:** `cfg_compra_isv`
  ([SQL](../../Database/2026-07-30_cfg_compra_isv.sql) · [`IsvCompraConfigService`](../../SIAD.Services/Almacen/IsvCompraConfigService.cs))
  — tabla `cfg_*` con PK = `company_id`, constante espejo del CHECK, servicio `Obtener/Guardar`, DTO.
  Es el molde exacto para el interruptor por empresa.
- **Patrón de compuerta por permiso ya establecido:** el permiso
  `module.inventario.movimientos.autorizar_sensibles` se resuelve en el controller
  ([`MovimientosAlmacenController.cs:126`](../../apc/Controllers/Almacen/MovimientosAlmacenController.cs))
  como `bool` y baja al servicio (`CrearYPostearAsync(..., puedeUsarTiposSensibles, ...)`). Mismo
  patrón sirve para "quién puede dejar negativo".
- **Hallazgo a confirmar (posible defecto latente, PRE-existente):** revertir una **salida genérica**
  (`AjusteNegativo`, `documento_tipo = AJUSTE`) NO está cubierto por los tests de anulación
  ([`MovimientoAlmacenAnulacionTests`](../../SIAD.Tests/Almacen/MovimientoAlmacenAnulacionTests.cs) solo
  prueba revertir ENTRADA y VALOR). Como `EsReversaDeDevolucion` (:603) solo reconoce `Descargo` y
  `Traslado`, la reversa de un `AjusteNegativo` caería en la rama de **resta** (:555) en vez de
  **devolver** — restaría de nuevo. Se **confirma con un test** en F1 y, si aplica, se corrige. Importa
  porque al habilitar negativos en salidas genéricas su anulación se vuelve un camino real.

---

## Decisiones a resolver (con mi recomendación)

**1 · Dónde vive el interruptor.** → **Config en dos niveles, resuelto dentro del motor.**
   - **Empresa (interruptor maestro):** tabla nueva `cfg_inventario_negativo` (PK = `company_id`,
     patrón `cfg_compra_isv`), columna `permitir boolean NOT NULL DEFAULT false`. **Default OFF** =
     comportamiento actual → **despliegue seguro** (nadie nota el cambio hasta activarlo).
   - **Bodega (override opcional):** columna nueva `alm_bodega.permite_existencia_negativa boolean NULL`
     (tri-estado: `NULL` = hereda de la empresa, `true` = fuerza permitir, `false` = fuerza bloquear).
   - **Efectivo** = `override_bodega ?? interruptor_empresa`. Lo resuelve **el motor** con la `fila` ya
     bloqueada (chokepoint único, imposible de saltar desde un llamador).
   - **Permiso:** como **capa opcional aparte** (F4), no como el interruptor principal. El permiso
     responde "¿quién puede ejecutar una salida que cruza a negativo?" (per-usuario, claims), distinto
     de "¿está permitido aquí?" (config, per-empresa/bodega). Recomiendo incluirlo (la operación es
     sensible y da el "quién autorizó"), pero es separable y puede diferirse.

**2 · A qué flujos aplica.** → Las **tres ramas de salida** del motor (`AjusteNegativo`,
   `SalidaDescargo`, `TrasladoSalida`), que cubren descargo + movimiento genérico de salida + ajuste
   negativo + traslado de salida. **NO** se toca la guarda de la **reversa** (:434): revertir una
   ENTRADA a negativo es otra anomalía, fuera de alcance.

**3 · ¿Forzar aviso al cruzar a "Negativa" aunque ya estuviera en alerta?** → **Sí**, romper el
   anti-spam **solo** para el cruce a negativa: marcar cruce también cuando
   `severidadDespues == Negativa && severidadAntes != Negativa`. Es la anomalía exacta que introduce
   esta función; el resto del anti-spam se conserva.

**4 · Auditoría (quién autorizó).** → El asiento del kardex es **inmutable** y ya graba
   `existencia_resultante` (negativa) + `usuariocreacion` + `fecha` + `observacion`: **ese es el
   rastro**, sin cambio de esquema. Un reporte de reconciliación filtra `existencia_resultante < 0`.
   Si se adopta el permiso (F4), quien postea es quien está autorizado. (Se descarta agregar columna a
   `alm_kardex`: el ledger es inmutable y el dato ya se puede consultar.)

**5 · Costeo / borde del promedio.** → La salida en sí es segura (no mueve el promedio). El riesgo es
   la **entrada siguiente sobre existencia negativa**: la fórmula ponderada (:502-505) con base
   negativa produce promedios **distorsionados o negativos**. Ejemplos con base `-5 @ 100`:
   entra `3 @ 120` → existencia `-2`, promedio `(-500+360)/(-2) = 70` (costo inventado);
   entra `10 @ 50` → existencia `8`, promedio `(-200… )` distorsionado.
   **Regla propuesta:** cuando `fila.existencia < 0`, la entrada **no pondera** contra la base
   negativa; `promedio_resultante = m.CostoUnitario` (el lote que llega re-establece el costo). Acota
   el promedio a un costo real y nunca lo deja ≤ 0. Aplica a `Compra`/`AjustePositivo`/`TrasladoEntrada`
   y a la rama de **devolución** de la reversa (:546-552). Es **política de costeo (D-costeo): a
   confirmar con el contador** — se documenta y se cubre con tests, pero el criterio lo valida él.

---

## Fases

> TDD en todo: primero el test que falla (rojo), luego el cambio mínimo (verde). Tras cada fase, la
> suite completa (`SIAD.Tests`) sin regresión. Sin commit hasta que el usuario lo pida.

### F0 — Estructura de BD (solo mirror `siad_v3_restore`)

Un único script `Database/2026-08-15_alm_existencia_negativa.sql`, **aditivo y reversible**, pasando
por la skill **guardia-estructura-bd** y registrado en **runbook-despliegue-srv** (SRV pendiente):
1. `CREATE TABLE cfg_inventario_negativo (company_id BIGINT PK, permitir BOOLEAN NOT NULL DEFAULT
   false, + auditoría usuariocreacion/…)`. Semilla idempotente: una fila `false` por empresa con
   artículos (`ON CONFLICT DO NOTHING`).
2. `ALTER TABLE alm_bodega ADD COLUMN permite_existencia_negativa BOOLEAN NULL` (sin default: NULL =
   hereda). Con `COMMENT` explicando el tri-estado.
3. Bloque de VERIFICACIÓN y ROLLBACK al pie (patrón `cfg_compra_isv`).

Entidad + mapeo: `cfg_inventario_negativo.cs` (`ICompanyScopedEntity`, PK = company_id) y su
`DbSet`/`modelBuilder` en [`SiadDbContext.Almacen.cs`](../../SIAD.Data/SiadDbContext.Almacen.cs);
agregar la propiedad nullable a la entidad `alm_bodega` + su `.Property(...)`. Constante no hace falta
(es un booleano, no un vocabulario con CHECK).

### F1 — Motor: aflojar las 3 guardas + resolución del interruptor + regla de costeo (TDD)

1. **Servicio de resolución** `INegativoInventarioConfigService.PermiteNegativoAsync(bodegaId, ct)`
   (impl. lee `alm_bodega.permite_existencia_negativa ?? cfg_inventario_negativo.permitir`; default
   `false` si no hay filas). Registrar en [`ServiceRegistration`](../../SIAD.Services/ServiceRegistration.cs).
   Inyectarlo en `InventarioPostingService`.
2. **Aflojar** las tres ramas: la comprobación `fila.existencia - m.Cantidad < 0` solo lanza si
   `!permiteNegativo`. Se resuelve una vez por posteo con la `fila` bloqueada (empresa + bodega).
   El resto de guardas (cantidad > 0, costo promedio > 0, documento) **se conservan**.
3. **Regla de costeo (Decisión 5):** en `Calcular`, ramas de entrada (`AjustePositivo`/`Compra`/
   `TrasladoEntrada`) y la devolución de la reversa: si `fila.existencia < 0` → `promedio = costo del
   lote` en vez de ponderar. Comentario documentando el borde y la decisión del contador.
4. **Confirmar el hallazgo de la reversa de salida genérica** con un test explícito (revertir un
   `AjusteNegativo` debe **sumar**). Si falla, extender `EsReversaDeDevolucion`/la clasificación para
   tratar la reversa de `AjusteNegativo` como devolución. (Si ya funciona, solo queda el test.)

**Tests** ([`InventarioPostingTests`](../../SIAD.Tests/Almacen/InventarioPostingTests.cs) + nuevo
`InventarioPostingNegativoTests`): interruptor OFF sigue bloqueando las 3 ramas; interruptor ON (por
empresa y por override de bodega, incl. `false` que fuerza bloqueo) permite la salida a negativo;
override de bodega gana sobre empresa; **entrada sobre existencia negativa** deja el promedio = costo
del lote (nunca negativo); reversa de una salida a negativo deja el par consistente.

### F2 — Cablear los flujos (verificación, poco o ningún cambio de código)

Como la resolución vive en el motor, `DescargoDocumentoService`, `MovimientoAlmacenService`,
`AjusteInventarioService` y `TrasladoAlmacenService` **no cambian su lógica de posteo**. Solo:
- Revisar que sus **validaciones de renglón propias** (mensajes "en el idioma del documento") no
  dupliquen un bloqueo de negativo por su cuenta (hoy no lo hacen: delegan al motor). 
- Tests de flujo (`DescargoFlujoTests`, `MovimientoAlmacenTests`, `TrasladoAlmacenTests`,
  `AjusteInventarioService`) confirmando que con el interruptor ON la salida a negativo se postea y el
  documento queda consistente.

### F3 — Aviso forzado al cruzar a "Negativa" (Decisión 3, TDD)

- En `PostearAsync`: `cruzoAlerta = (severidadAntes is null && severidadDespues is not null) ||
  (severidadDespues == Negativa && severidadAntes != Negativa)`.
- Los servicios ya propagan `CruzoAlerta` al notificador; no cambia el transporte.
- Tests en [`StockSeveridadTests`](../../SIAD.Tests/Almacen/StockSeveridadTests.cs) /
  `InventarioPostingTests`: caída `BajoMinimo → Negativa` y `SinStock → Negativa` **sí** marcan cruce;
  seguir cayendo dentro de negativo (`-2 → -5`) **no** re-marca.

### F4 — Compuerta por permiso (opcional; recomendada, separable)

- Permiso nuevo `module.inventario.existencia_negativa` en
  [`PermissionNames`](../../SIAD.Core/Constants/PermissionNames.cs) (+ policy). El controller lo
  resuelve como `bool negativoAutorizado` (patrón `PuedeUsarTiposSensibles`) y baja a los servicios de
  salida; estos pasan un `bool` al motor. Regla efectiva: negativo permitido **solo si**
  `config lo permite` **y** `negativoAutorizado`. Sin el permiso, aunque la config esté ON, la salida
  a negativo se rechaza con mensaje accionable.
- Tests en [`PermisosInventarioTests`](../../SIAD.Tests/Almacen/PermisosInventarioTests.cs).
- *Si se decide no incluir el permiso, se omite esta fase: la config sola ya cumple "no abierto para
  todos" (off por defecto, se activa por empresa/bodega deliberadamente).*

### F5 — UI de configuración + verificación en el portal

- **Config por empresa:** checkbox "Permitir existencia negativa en salidas" en la pantalla de
  configuración de almacén/inventario (DTO + `INegativoInventarioConfigService.Guardar`, patrón
  `IsvCompraConfig`). En español, con nota de que es una excepción operativa a reconciliar.
- **Override por bodega:** checkbox tri-estado (Hereda / Permitir / Bloquear) en el mantenimiento de
  `BodegaService` / pantalla de bodegas.
- Verificación con el flujo de preview del navegador: activar el interruptor, registrar un descargo que
  cruce a negativo, confirmar existencia negativa + aviso, y una entrada posterior con promedio sano.

---

## Alcance y riesgos

- **Fuera de alcance:** la guarda de la **reversa a negativo** de una entrada (:434) y la política de
  costeo de fondo (Fase B de [`2026-08-01-costeo-articulo-diseno.md`](2026-08-01-costeo-articulo-diseno.md),
  `costo_promedio_anterior`) — se referencian pero no se resuelven aquí.
- **Riesgo contable:** el borde del promedio (Decisión 5) es una **decisión de política** que el
  contador debe validar; el plan fija una regla defendible (promedio = costo del lote sobre base
  negativa) y la cubre con tests, pero no la da por cerrada.
- **Despliegue seguro por diseño:** todo nace **OFF**; sin activar el interruptor el sistema se comporta
  igual que hoy. SQL solo al **mirror**; SRV queda registrado en el runbook, pendiente de orden del
  usuario.
