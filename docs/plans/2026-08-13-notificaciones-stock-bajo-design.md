# Notificaciones por correo de stock bajo / en riesgo — Diseño

Fecha: 2026-08-13 · Rama: `feat/almacen-integracion-contable` · Estado: **propuesta (plan corto), sin implementar**

---

## Requerimiento

Enviar por **correo** avisos cuando el inventario esté bajo. Se apoya en la iniciativa de correo ya
terminada (conexión SendGrid + áreas de notificación + `ICorreoNotificador`): los avisos salen al
área **ALMACÉN** configurada en `/configuración/correo`.

## Decisiones tomadas (del usuario)

- **Dos disparadores:** (1) **por evento** al mover stock (una salida que deja un artículo bajo
  mínimo) y (2) **resumen diario automático**.
- **Niveles:** los **tres actuales** de la pantalla de alertas — `Existencia negativa`, `Sin stock`,
  `Bajo mínimo`. **No** se agrega el nivel "en riesgo" por `punto_reorden` (queda para después).
- **Mecanismo del diario (decisión del equipo):** `BackgroundService` dentro de la app, con la lógica
  del barrido detrás de un servicio reutilizable para poder dispararla también desde una Tarea de
  Windows si más adelante se prefiere. ⚠️ **En IIS el App Pool debe quedar `AlwaysRunning` con idle
  time-out = 0**, o el temporizador no corre cuando la app está dormida.

## Contexto (hallazgos de la exploración)

- **La detección ya existe y no se reinventa.** `ArticulosService.GetAlertasStockAsync`
  ([`ArticulosService.cs:339`](../../SIAD.Services/Almacen/ArticulosService.cs)) calcula las alertas
  **por bodega** (sobre `alm_articulo_bodega`): `existencia < 0` → NEGATIVA; `= 0` → SIN STOCK;
  `existencia < existencia_minima` (con mínimo > 0) → BAJO MÍNIMO. Constantes en `StockSeveridad`;
  DTO `AlertaStockDto`. La pantalla es `/almacen/alertas-stock`.
- **Umbrales por bodega ya modelados:** `alm_articulo_bodega` tiene `existencia`, `existencia_minima`,
  `existencia_maxima`, `punto_reorden`, `existencia_comprometida` ([`alm_articulo_bodega.cs`](../../SIAD.Core/Entities/alm_articulo_bodega.cs)).
- **Punto de enganche único para el evento:** `InventarioPostingService.PostearAsync`
  ([`InventarioPostingService.cs:33`](../../SIAD.Services/Almacen/InventarioPostingService.cs)) es el
  **motor único** que mueve existencia — conoce la existencia **antes** (`fila.existencia`) y la
  **después** (`existenciaResultante`, escrita en la línea 96) y devuelve `PosteoResultDto`. Todas las
  salidas pasan por aquí.
- **No hay infraestructura de tareas programadas** (ni Hangfire/Quartz/IHostedService). El diario
  introduce el primer `BackgroundService`.
- **El correo ya está listo:** `ICorreoNotificador.NotificarAreaAsync(tipo, asunto, html)` envía al
  área de la **empresa actual**; para el diario (sin sesión) hará falta una variante **cross-tenant**
  por `companyId`, análoga a `ResolverTransporteAsync` que ya existe para los correos de sistema.

---

## Núcleo compartido — armador del resumen

Servicio nuevo `SIAD.Services/Almacen/AlertasStockNotificador` (o método en un servicio de almacén):

- **Reutiliza** `GetAlertasStockAsync` (empresa en contexto) para obtener la lista de alertas.
- Arma un **resumen HTML** (tabla: código, descripción, bodega, existencia, mínimo, severidad; ordenado
  por severidad como ya hace el servicio). Encabezado con la fecha y los totales por severidad.
- Envía con `NotificarAreaAsync(TipoNotificacion.Almacen, asunto, html)`. Si el área no está
  configurada/activa → `Omitido` (no envía; seguro por defecto).
- Sin alertas → no envía (nada que reportar), salvo que se decida un "todo en orden" (fuera de alcance).

Este núcleo lo usan las dos fases.

## Fase 1 — Por evento (cruce bajo mínimo)

**Detección en el motor** ([`InventarioPostingService.PostearAsync`](../../SIAD.Services/Almacen/InventarioPostingService.cs)):
- Calcular el **cruce**: `cruzó = (existenciaAntes ≥ existencia_minima) && (existenciaResultante < existencia_minima)`
  con `existencia_minima > 0`; incluir también el paso a `0`/negativo. Solo cuando **cruza el umbral**,
  no en cada movimiento mientras siga bajo → evita spam.
- Exponer el resultado en `PosteoResultDto` (p. ej. `CruzoBajoMinimo` + severidad resultante).
  **No se envía correo dentro del motor** (es por línea y dentro de la transacción).

**Envío en el servicio de documento** (el que postea todas las líneas de una salida/descargo,
p. ej. `DescargoDocumentoService` / `MovimientoAlmacenService`):
- Acumula las líneas que **cruzaron** durante el documento.
- **Después del commit**, si hubo cruces, arma **un solo correo** con esas líneas (no uno por línea) y
  llama al núcleo compartido. Tenant en contexto → empresa actual.
- Si el posteo se revierte, no se envía (por ser post-commit).

**Anti-spam:** el disparo por cruce + un correo por documento son el control. (Un cooldown por artículo
queda como refuerzo opcional, fuera de alcance de esta fase.)

## Fase 2 — Resumen diario (`BackgroundService`)

- `AlertasStockDiarioService : BackgroundService` con hora configurable
  (`Almacen:AlertasStock:HoraDiaria`, ej. `"07:00"`), calculando el próximo disparo.
- Al disparar: **por cada empresa** con el área ALMACÉN **activa** (consulta cross-tenant sobre
  `cfg_notificacion`), abrir un *scope* con contexto de esa empresa, calcular sus alertas y enviar el
  resumen.
- **Trabajo técnico grueso:** la iteración por empresa sin sesión. Dos piezas:
  1. Cómputo de alertas por empresa: un `DbContext` con tenant fijo a esa empresa (patrón de los tests /
     de `apc.BancosWs`), o un `GetAlertasStock` con filtro explícito.
  2. Envío por empresa: `NotificarAreaEmpresaAsync(companyId, tipo, asunto, html)` — variante
     cross-tenant del notificador (misma idea que `ResolverTransporteAsync`), que resuelve los
     destinatarios del área de **esa** empresa.
- La lógica del barrido vive en un servicio reutilizable (`EjecutarBarridoAsync`) para poder llamarla
  también desde un endpoint (Tarea de Windows) sin duplicar.

---

## Capas afectadas

- `SIAD.Services/Almacen/` — `AlertasStockNotificador` (núcleo), enganche en `InventarioPostingService`
  (`PosteoResultDto` + cálculo del cruce) y en el servicio de documento (acumular + enviar post-commit),
  `AlertasStockDiarioService` (BackgroundService) + su servicio de barrido.
- `SIAD.Services/Configuracion/` — `NotificarAreaEmpresaAsync` (cross-tenant) en `ICorreoNotificador` /
  `CorreoNotificador`, y un resolver de área por empresa (análogo a `ResolverTransporteAsync`).
- `apc/Program.cs` — `AddHostedService<AlertasStockDiarioService>()`.
- `apc/appsettings.json` — sección `Almacen:AlertasStock` (`HoraDiaria`; opcional `Activo`).
- DI en `ServiceRegistration.cs`.
- (Opcional) endpoint `POST api/almacen/alertas-stock/notificar` para disparo manual/Tarea de Windows.

## Pruebas (`SIAD.Tests`)

- **Cruce (unitario/integración):** antes ≥ mínimo y después < mínimo → marca cruce; ya estaba bajo y
  sigue bajo → **no** marca (anti-spam); entrada que sube por encima del mínimo → no marca.
- **Núcleo:** dado un conjunto de alertas, arma el HTML y llama al notificador con el área ALMACÉN
  (transporte mockeado con NSubstitute, como en `CorreoNotificadorTests`); sin alertas → no envía.
- **Documento:** una salida con varias líneas que cruzan → **un solo** envío tras el commit; posteo
  revertido → sin envío.
- **Diario (acotado):** selección de empresas con área ALMACÉN activa; envío por empresa (cross-tenant).

## Orden de trabajo

- **F0** — Núcleo compartido (armador de resumen + envío al área ALMACÉN) + tests.
- **F1** — Por evento: cruce en `InventarioPostingService` + envío post-commit en el servicio de
  documento + tests. *(Valor inmediato, tenant en contexto.)*
- **F2** — Resumen diario: `NotificarAreaEmpresaAsync` cross-tenant + `BackgroundService` + config +
  tests. Nota de despliegue del App Pool.

## Fuera de alcance

- Nivel **"En riesgo"** por `punto_reorden` (aviso temprano) — se puede añadir después extendiendo
  `StockSeveridad` y la consulta.
- Cooldown por artículo, correo "todo en orden", preferencias por usuario, y disparo por **Tarea de
  Windows** (se deja el servicio de barrido listo para ello, pero el endpoint/tarea no se implementan).
