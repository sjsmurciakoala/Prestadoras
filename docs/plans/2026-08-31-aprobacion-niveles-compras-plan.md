# Aprobación por niveles en Compras — plan corregido

**Fecha:** 2026-08-31 · **Estado:** DISEÑO APROBADO EN SUS DECISIONES — sin código ni SQL derivado
**Alcance 1ª entrega:** Orden de compra (`alm_orden_compra`). El motor nace genérico para reusarse
después en facturas de compra, pagos a proveedor y requisiciones.

> Este documento corrige y reemplaza a `Plan_Aprobacion_por_Niveles_Compras.md` (borrador del usuario,
> escritorio). El diseño de fondo de aquel borrador se mantiene íntegro: **motor de aprobación basado
> en usuarios y reglas por monto, sin organigrama**. Lo que cambia es todo lo que asumía el modelo de
> datos de Centura/SIMAFI, que no es el del portal.

---

## 1. Resultado de la Fase 1 (análisis del flujo actual)

El borrador pedía, como primera fase, identificar cómo funcionan hoy los módulos. Ese análisis ya está
hecho y este es el resultado, verificado contra el código el 2026-08-31.

### 1.1 Lo que existe

| Pieza | Ubicación | Estado real |
|---|---|---|
| Orden de compra | `alm_orden_compra` / `_detalle` / `_correlativo` | En uso. Multitenant, correlativo por empresa |
| Estados de la O/C | `EstadosNumericos.cs:161` | 1 Borrador · 2 Aprobada · 3 Recibida parcial · 4 Cerrada · **5 Rechazada · 6 Cancelada** · 9 Anulada |
| Aprobación | `OrdenCompraService.AprobarAsync:292` | **Un solo escalón.** Valida Borrador + renglones, sella `aprobado_por`/`fecha_aprobacion` y **compromete presupuesto en la misma transacción** |
| Autorización de aprobar | `OrdenesCompraController.cs:153` | `[ModuleAuthorize(Compras, Edit)]` — **quien edita, aprueba**. No hay permiso propio |
| Trazabilidad de quién aprobó | `alm_orden_compra.aprobado_por` (VARCHAR 100) | Un solo nombre. Sin historia, sin niveles, sin motivo |
| Control presupuestario | `IPresupuestoCompromisoService` | Completo: comprometer / liberar / ajustar / devengar. Apagado por empresa (`cfg_presupuesto_control.modo = 0`) |
| Precedente de flujo | `alm_requisicion_hdr` | Borrador → En revisión → Aprobada / Rechazada, con **un solo** aprobador |
| Identidad de usuarios | ASP.NET Identity (`AspNetUsers`, schema `identity`) | Misma base física, **otro schema y otro DbContext** (`ApplicationDbContext` ≠ `SiadDbContext`) |
| Configuración por empresa+módulo | `cfg_presupuesto_control` | Patrón a replicar: modo numérico, semilla apagada, `PRIMARY KEY (company_id, modulo)` |
| Correo por empresa y área | Mantenimiento de correo / SendGrid | Existe. Las notificaciones **se enganchan, no se construyen** |

### 1.2 Los seis hallazgos que corrigen el borrador

**H1 — La tabla `USUARIOS` (`COD_USUARIO`, `NOMBRE`, `PASSWORD`, `ACTIVO`) no existe.**
Es el modelo de Centura/SIMAFI. El portal usa ASP.NET Identity y **todos los documentos guardan al
usuario como texto libre**: `alm_orden_compra.usuariocreacion` y `aprobado_por` reciben
`User.Identity.Name` (el email), normalizado por `ClasificacionNormalizer.Usuario` (solo `Trim`).
Consecuencias:

- La tabla de aprobadores guarda `user_name VARCHAR(256)`, **sin FK**: cruza schema y cruza contexto EF.
- La regla "no aprobar su propia orden" compara strings. **Hay que normalizar a minúsculas**, o la regla
  se evade con una diferencia de mayúsculas.
- La pantalla de configuración necesita un **endpoint de lookup de usuarios nuevo**: el único que existe
  (`UsuariosPortalController.cs:13`) está restringido a Super Administrador, y quien configure
  aprobaciones no será necesariamente superadmin.
- Los usuarios de Identity **no tienen `company_id` en tabla**, lo llevan en un claim. El lookup debe
  filtrar por el claim de empresa o la configuración de una empresa listará usuarios de otra.

**H2 — Solicitud de compra y cotización no existen.** No hay ninguna tabla ni servicio de ninguna de las
dos. El flujo real es: requisición de almacén (interna, pedir material de bodega) → **orden de compra** →
recepción / factura de proveedor → CxP → pago. Empezar por la O/C no es solo prudente: es la única
puerta que existe hoy.

**H3 — El borrador se contradice entre su Fase 2 y su Fase 4.** La tabla de niveles define tramos
excluyentes (nivel 3 = 50,000.01–200,000) pero el ejemplo de L 75,000 exige niveles 1+2+3, que es
escalera acumulativa. Resuelto en **D1**.

**H4 — El control presupuestario no es el paso 9 del orden de implementación: ya existe y es
dependencia dura.** Aprobar hoy compromete presupuesto. Al partir la aprobación en N firmas hay que
decidir en cuál se compromete. Resuelto en **D2**.

**H5 — El `CHECK` de estados ya está ocupado.** `ck_alm_orden_compra_estado` fue ampliado por el script
del compromiso a `(1,2,3,4,5,6,9)`. Agregar el 7 exige un `DROP`/`ADD` del constraint, no un `CREATE`
limpio.

**H6 — El prefijo `COM_` no existe en la base.** Las convenciones vigentes son `cfg_` (configuración),
`alm_` (almacén/compras), `prv_`, `pst_`, `con_`, `th_`.

---

## 2. Decisiones tomadas (usuario, 2026-08-31)

| # | Decisión | Detalle |
|---|---|---|
| **D1** | **Escalera acumulativa** | Se exigen **todos** los niveles cuyo `monto_desde <= total de la orden`. L 75,000 con los tramos del ejemplo exige N1 + N2 + N3. **`monto_hasta` desaparece** del modelo: con escalera sobra, y dos columnas de rango generan huecos por redondeo y solapamientos que nada impide |
| **D1b** | **Dentro de un nivel, firma cualquiera** | Si el nivel tiene tres aprobadores, con que uno firme el nivel queda aprobado (opción A del borrador). Configurable a futuro, pero **no** en la 1ª entrega |
| **D2** | **El presupuesto se compromete en la PRIMERA firma** | La reserva nace con la firma del nivel 1, no al completar la escalera. Ver §5, que es la sección que más cambia por esta decisión |
| **D3** | **Aprobador por usuario o por rol** | La tabla admite las dos formas: `tipo = 1` usuario (`user_name`), `tipo = 2` rol de Identity. Así se configura nominalmente donde importa y por rol donde rota la gente |
| **D4** | **Devolver a borrador SIEMPRE pierde las firmas** | Sin excepción ni comparación de montos. Volver a borrador reinicia el flujo desde cero y libera el presupuesto comprometido |
| **D5** | **Nadie aprueba su propia orden, pero es configurable** | `cfg_aprobacion_control.permite_autoaprobacion` **nace en `false`**: quien figura en `usuariocreacion` no puede firmar ningún nivel de esa orden. Encendiéndola, sí puede firmar los niveles donde sea aprobador elegible. Es por empresa **y por documento**, así que se puede permitir en órdenes de compra y prohibir en pagos. Ver el riesgo R2 |

---

## 3. Modelo de datos

Cuatro tablas nuevas + un estado. Todas multitenant (`company_id`), con auditoría
(`usuariocreacion` / `fechacreacion` / `usuariomodificacion` / `fechamodificacion`) y FK compuestas
tenant-safe, según la convención del módulo.

### 3.1 `cfg_aprobacion_control` — interruptor por empresa y documento

```sql
CREATE TABLE IF NOT EXISTS public.cfg_aprobacion_control (
    company_id             BIGINT       NOT NULL,
    documento              VARCHAR(30)  NOT NULL,   -- COMPRAS_OC | COMPRAS_FACTURA | PROVEEDORES_PAGO | ALMACEN_REQUISICION
    modo                   SMALLINT     NOT NULL DEFAULT 0,     -- 0 Apagado · 1 Encendido
    permite_autoaprobacion BOOLEAN      NOT NULL DEFAULT false, -- D5
    -- auditoría …
    CONSTRAINT pk_cfg_aprobacion_control PRIMARY KEY (company_id, documento),
    CONSTRAINT ck_cfg_aprobacion_control_modo CHECK (modo IN (0, 1)),
    CONSTRAINT ck_cfg_aprobacion_control_doc  CHECK (documento IN
        ('COMPRAS_OC','COMPRAS_FACTURA','PROVEEDORES_PAGO','ALMACEN_REQUISICION'))
);
```

**Nace apagado para toda empresa**, igual que el control presupuestario: aplicar el script no cambia el
comportamiento de nadie y la suite de pruebas actual sigue verde.

### 3.2 `cfg_aprobacion_nivel` — la escalera (D1)

```sql
CREATE TABLE IF NOT EXISTS public.cfg_aprobacion_nivel (
    id                  SERIAL        PRIMARY KEY,
    company_id          BIGINT        NOT NULL,
    documento           VARCHAR(30)   NOT NULL,
    nivel               SMALLINT      NOT NULL,          -- 1..n, orden de firma
    descripcion         VARCHAR(100)  NOT NULL,
    monto_desde         NUMERIC(14,2) NOT NULL DEFAULT 0,
    activo              BOOLEAN       NOT NULL DEFAULT true,
    -- auditoría …
    CONSTRAINT uq_cfg_aprobacion_nivel UNIQUE (company_id, documento, nivel),
    CONSTRAINT uq_cfg_aprobacion_nivel_tenant UNIQUE (company_id, id),
    CONSTRAINT ck_cfg_aprobacion_nivel_nivel CHECK (nivel BETWEEN 1 AND 9),
    CONSTRAINT ck_cfg_aprobacion_nivel_monto CHECK (monto_desde >= 0)
);
```

Regla de negocio (validada en el servicio, no por constraint): **`monto_desde` debe crecer con el
nivel**. Un nivel 2 con umbral menor que el nivel 1 hace la escalera incoherente.

Ejemplo equivalente al del borrador:

| Nivel | Descripción | monto_desde | Aplica a |
|---:|---|---:|---|
| 1 | Aprobación Nivel 1 | 0.00 | toda orden |
| 2 | Aprobación Nivel 2 | 10,000.01 | ≥ 10,000.01 |
| 3 | Aprobación Nivel 3 | 50,000.01 | ≥ 50,000.01 |
| 4 | Aprobación Nivel 4 | 200,000.01 | ≥ 200,000.01 |

Una orden de L 75,000 exige niveles 1, 2 y 3. Una de L 5,000, solo el nivel 1.

### 3.3 `cfg_aprobacion_aprobador` — quién firma cada nivel (D3)

```sql
CREATE TABLE IF NOT EXISTS public.cfg_aprobacion_aprobador (
    id                  SERIAL        PRIMARY KEY,
    company_id          BIGINT        NOT NULL,
    nivel_id            INTEGER       NOT NULL,
    tipo                SMALLINT      NOT NULL,          -- 1 Usuario · 2 Rol
    valor               VARCHAR(256)  NOT NULL,          -- user_name (email, en minúsculas) o nombre del rol
    activo              BOOLEAN       NOT NULL DEFAULT true,
    -- auditoría …
    CONSTRAINT fk_cfg_aprobacion_aprobador_nivel
        FOREIGN KEY (company_id, nivel_id)
        REFERENCES public.cfg_aprobacion_nivel (company_id, id) ON DELETE CASCADE,
    CONSTRAINT uq_cfg_aprobacion_aprobador UNIQUE (company_id, nivel_id, tipo, valor),
    CONSTRAINT ck_cfg_aprobacion_aprobador_tipo CHECK (tipo IN (1, 2))
);
```

**Sin FK a `AspNetUsers`** (H1): otro schema, otro contexto EF, y el filtro multitenant no aplica a
Identity. `valor` se guarda **normalizado a minúsculas** para que la comparación de D5 y la elegibilidad
no dependan de cómo escribió el usuario su email al entrar.

### 3.4 `alm_orden_compra_aprobacion` — el flujo vivo (Fase 6 del borrador)

Una fila por nivel exigido, materializada al enviar la orden a aprobación.

```sql
CREATE TABLE IF NOT EXISTS public.alm_orden_compra_aprobacion (
    id                  SERIAL        PRIMARY KEY,
    company_id          BIGINT        NOT NULL,
    orden_compra_id     INTEGER       NOT NULL,
    nivel               SMALLINT      NOT NULL,
    descripcion         VARCHAR(100)  NOT NULL,          -- snapshot del nivel al momento del envío
    estado              SMALLINT      NOT NULL DEFAULT 1, -- 1 Bloqueado · 2 Pendiente · 3 Aprobado · 4 Rechazado
    usuario_firma       VARCHAR(256)  NULL,
    fecha_firma         TIMESTAMP     NULL,
    comentario          VARCHAR(500)  NULL,
    total_documento     NUMERIC(14,2) NOT NULL,          -- snapshot del total que se está aprobando
    CONSTRAINT fk_alm_oc_aprobacion_oc
        FOREIGN KEY (company_id, orden_compra_id)
        REFERENCES public.alm_orden_compra (company_id, id) ON DELETE CASCADE,
    CONSTRAINT uq_alm_oc_aprobacion UNIQUE (company_id, orden_compra_id, nivel),
    CONSTRAINT ck_alm_oc_aprobacion_estado CHECK (estado IN (1, 2, 3, 4))
);
```

`total_documento` es snapshot y no un adorno: deja evidencia de **qué monto se firmó**, que es lo que
un auditor va a preguntar. Con D4 las firmas se borran al devolver, así que nunca hay firmas vivas
sobre un monto distinto; el snapshot queda en la bitácora.

### 3.5 `apr_bitacora` — historial append-only (Fase 7 del borrador)

```sql
CREATE TABLE IF NOT EXISTS public.apr_bitacora (
    id                  BIGSERIAL     PRIMARY KEY,
    company_id          BIGINT        NOT NULL,
    documento           VARCHAR(30)   NOT NULL,
    documento_id        BIGINT        NOT NULL,
    documento_numero    VARCHAR(40)   NULL,
    nivel               SMALLINT      NULL,              -- NULL en eventos del documento (envío, devolución)
    accion              VARCHAR(20)   NOT NULL,          -- ENVIADA | APROBADA | RECHAZADA | DEVUELTA | ANULADA | REINICIADA
    usuario             VARCHAR(256)  NOT NULL,
    fecha               TIMESTAMP     NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    comentario          VARCHAR(500)  NULL,
    total_documento     NUMERIC(14,2) NULL
);
CREATE INDEX IF NOT EXISTS ix_apr_bitacora_doc
    ON public.apr_bitacora (company_id, documento, documento_id, fecha);
```

**Nunca se actualiza ni se borra.** Es la fuente de verdad de auditoría; §3.4 es solo el estado
operativo para pintar la pantalla. Las firmas que D4 borra del flujo **siguen existiendo aquí**.

### 3.6 Estado nuevo en la orden

```sql
ALTER TABLE public.alm_orden_compra DROP CONSTRAINT IF EXISTS ck_alm_orden_compra_estado;
ALTER TABLE public.alm_orden_compra ADD  CONSTRAINT ck_alm_orden_compra_estado
    CHECK (estado IN (1, 2, 3, 4, 5, 6, 7, 9));
```

`7 = En aprobación`. Un solo estado, no dos: "enviada" y "pendiente" son lo mismo para el documento; lo
que cambia entre una y otra es en qué nivel va la escalera, y eso vive en §3.4. **"Devuelta" tampoco es
un estado**: devolver regresa la orden a `1 Borrador` y el evento queda en la bitácora.

---

## 4. Máquina de estados

```
1 Borrador ──enviar a aprobación──► 7 En aprobación ──firma del último nivel──► 2 Aprobada ──► recepción
     ▲                                    │
     └────────devolver (D4)───────────────┤
                                          └──rechazar──► 5 Rechazada
```

- **Borrador**: editable. Sin flujo, sin compromiso.
- **En aprobación**: **no editable** (es lo que da sentido a la firma). `ActualizarAsync` sigue
  exigiendo Borrador, sin cambios.
- **Aprobada / Recibida parcial / Cerrada / Cancelada / Anulada**: exactamente como hoy.
- **Rechazada**: terminal, con motivo obligatorio. Hoy `RechazarAsync` solo opera desde Borrador; pasa a
  operar también desde En aprobación, **y ahí sí tiene que liberar presupuesto** (§5).
- **Devolver**: solo desde En aprobación. Borra las filas de §3.4, libera presupuesto, deja rastro en
  §3.5 y devuelve la orden a Borrador.

---

## 5. Presupuesto: comprometer desde la primera firma (D2)

Esta es la sección que más cambia respecto del comportamiento actual, y la que hay que probar con más
cuidado.

**Verificado el 2026-08-31:** `sp_pst_comprometer_documento`
(`Database/2026-08-27_pst_compromiso_03_procedimientos.sql:163`) **no consulta el estado de la orden de
compra**. Solo mira `cfg_presupuesto_control.modo`, la idempotencia por documento y las líneas. Por lo
tanto **comprometer con la orden en estado 7 funciona sin tocar el SP**.

### 5.1 Matriz de eventos

| Evento | Estado resultante | Presupuesto |
|---|---|---|
| Enviar a aprobación | 7 | — nada |
| **Firma del nivel 1** | 7 | **`ComprometerOrdenCompraAsync`** (valida disponible, reserva, kardex) |
| Firma de niveles 2..n | 7 | — nada (la idempotencia del SP lo garantiza aunque se llame de más) |
| Firma del último nivel | **2 Aprobada** | — nada. Sella `aprobado_por` / `fecha_aprobacion` |
| **Rechazar** desde 7 | 5 | **`LiberarOrdenCompraAsync`** ← *comportamiento nuevo* |
| **Devolver** a borrador | 1 | **`LiberarOrdenCompraAsync`** ← *comportamiento nuevo* |
| Anular / Cancelar / Cerrar | 9 / 6 / 4 | Igual que hoy |

Todo evento que mueva presupuesto va **dentro de la misma transacción** que el cambio de estado, con
`TransaccionAmbiente`, como ya hace `AprobarAsync`.

### 5.2 Detalles que hay que respetar

- **`p_usuario_aprobo` pasa a ser el firmante del nivel 1** (quien reserva). Es correcto: es quien
  origina el compromiso. El rastro completo de la escalera vive en §3.4 y §3.5.
- **`alm_orden_compra.aprobado_por` se sella con el firmante del ÚLTIMO nivel**, no con el primero. Esa
  columna ya la consumen el PDF de la orden y los listados, y significa "quien dio la aprobación final";
  cambiarle el sentido rompería reportes existentes.
- **No hace falta `AjustarCompromisoOrdenCompraAsync`.** Con D4, la orden devuelta libera todo y, si se
  reedita, la nueva primera firma vuelve a comprometer el monto nuevo. El ajuste sigue sin llamador.
- **Modo apagado sigue siendo apagado**: si `cfg_presupuesto_control.modo = 0`, todos estos métodos son
  no-op, exactamente como hoy.

---

## 6. Motor de aprobación

Servicio genérico en `SIAD.Services/Aprobaciones/`, no lógica embutida en la O/C.

```csharp
public interface IAprobacionService
{
    Task<bool> RequiereAprobacionAsync(string documento, CancellationToken ct = default);
    Task<IReadOnlyList<NivelExigidoDto>> ResolverEscaleraAsync(string documento, decimal total, CancellationToken ct = default);
    Task IniciarAsync(string documento, long documentoId, string? numero, decimal total, string creadoPor, string user, CancellationToken ct = default);
    Task<FirmaResultadoDto> FirmarAsync(string documento, long documentoId, string? comentario, string user, CancellationToken ct = default);
    Task RechazarAsync(string documento, long documentoId, string motivo, string user, CancellationToken ct = default);
    Task ReiniciarAsync(string documento, long documentoId, string motivo, string user, CancellationToken ct = default); // devolver a borrador
    Task<IReadOnlyList<FlujoNivelDto>> ObtenerFlujoAsync(string documento, long documentoId, CancellationToken ct = default);
    Task<IReadOnlyList<PendienteAprobacionDto>> PendientesDeMiFirmaAsync(string documento, CancellationToken ct = default);
}
```

`FirmaResultadoDto` devuelve al menos: `NivelFirmado`, `EsPrimeraFirma` (dispara el compromiso),
`FlujoCompleto` (dispara la transición a Aprobada) y el nivel que queda pendiente.

### 6.1 Reglas del motor

1. **Elegibilidad (D3)**: el usuario puede firmar el nivel pendiente si está en
   `cfg_aprobacion_aprobador` activo de ese nivel **como usuario** (`tipo = 1`, comparando en
   minúsculas) **o como rol** (`tipo = 2` y el usuario tiene ese rol de Identity).
2. **Secuencia**: solo se puede firmar el nivel **pendiente**. Los superiores están bloqueados hasta que
   cierre el anterior. Un nivel bloqueado devuelve *"Este nivel todavía no está habilitado."*
3. **D5 — autoaprobación**: si `permite_autoaprobacion = false` (el valor de fábrica) y
   `lower(usuariocreacion) == lower(usuarioActual)`, se rechaza con *"No puede aprobar una orden que
   usted mismo creó."* Con la opción encendida, el creador firma como cualquier otro aprobador
   elegible. La regla se evalúa **por documento**, no globalmente.
4. **Una firma por usuario y documento**: quien ya firmó un nivel no puede firmar otro de la misma
   orden, aunque sea elegible. Separación de funciones.
5. **Sin escalera configurada**: si el documento tiene el control encendido pero ningún nivel activo
   aplica al monto, la orden **no puede enviarse a aprobación** — error explícito de configuración, no
   aprobación automática silenciosa.
6. **Concurrencia**: la firma toma `SELECT … FOR UPDATE` sobre la fila del flujo, para que dos
   aprobadores del mismo nivel pulsando a la vez no produzcan doble compromiso.

### 6.2 De dónde salen los roles del usuario

`SIAD.Services` no debe conocer `HttpContext`. Se introduce `ICurrentUserService`
(`UserName`, `Roles`), implementado en `apc` sobre `IHttpContextAccessor` y registrado junto a
`ICurrentCompanyService`. Es el mismo patrón de tenencia que ya usa el contexto.

### 6.3 Acceso a datos

Sin LINQ, por la regla del repo: funciones y vistas de Postgres consumidas con Dapper, o SP para las
transiciones. Como mínimo: `fn_apr_escalera(company_id, documento, total)`,
`fn_apr_puede_firmar(...)` y `vw_apr_pendientes`.

---

## 7. API y permisos

| Verbo | Ruta | Permiso |
|---|---|---|
| POST | `/api/almacen/ordenes-compra/{id}/enviar-aprobacion` | `module.compras.edit` |
| POST | `/api/almacen/ordenes-compra/{id}/firmar` | `module.compras.ordenes.aprobar` |
| POST | `/api/almacen/ordenes-compra/{id}/devolver` | `module.compras.ordenes.aprobar` |
| POST | `/api/almacen/ordenes-compra/{id}/rechazar` *(existente, se amplía)* | `module.compras.ordenes.aprobar` |
| GET | `/api/almacen/ordenes-compra/{id}/aprobaciones` | `module.compras.view` |
| GET | `/api/almacen/ordenes-compra/pendientes-aprobacion` | `module.compras.ordenes.aprobar` |
| GET/PUT | `/api/configuracion/aprobaciones/*` | `module.configuracion.aprobaciones.view` / `.edit` |
| GET | `/api/configuracion/aprobaciones/usuarios` (lookup, H1) | `module.configuracion.aprobaciones.edit` |

**Un solo permiso de aprobación, no uno por nivel.** El permiso abre la bandeja y el botón; **quién
puede firmar qué nivel lo decide la tabla**, que es justamente lo que hace la configuración cambiable
sin desplegar. Los nombres nuevos se registran en `PermissionNames` y, los específicos, en
`PermissionEndpointCatalog`.

**Aprobar deja de ser `PermissionAction.Edit`.** Hoy cualquiera que edite compras aprueba
(`OrdenesCompraController.cs:153`); ese hueco se cierra en esta entrega.

---

## 8. Interfaz

1. **Configuración → Aprobaciones** (Fase 8 del borrador): selector de documento, grid de niveles
   (`nivel`, `descripción`, `monto_desde`, `activo`) y, por nivel, el grid de aprobadores con selector
   **Usuario / Rol**. Valida escalera creciente y avisa si un nivel queda sin aprobadores.
2. **Bandeja "Mis aprobaciones"**: órdenes pendientes de mi firma, con número, proveedor, total, nivel,
   días en espera y quién la creó. Es la pantalla que hace usable el módulo.
3. **Detalle de la orden**: línea de tiempo de la escalera (nivel, quién, cuándo, comentario) y botones
   Firmar / Rechazar / Devolver según elegibilidad.
4. **Listado de órdenes**: badge `En aprobación (2 de 3)` — descripción legible, nunca el código
   numérico.
5. **PDF de la orden**: bloque de firmas con nivel, nombre y fecha.

---

## 9. Riesgos

| # | Riesgo | Mitigación |
|---|---|---|
| **R1** | **D2 reserva presupuesto en órdenes que quizá nunca se aprueben.** Una orden estancada en el nivel 2 mantiene el disponible bloqueado indefinidamente | Bandeja con antigüedad + reporte de órdenes detenidas en aprobación. Caducidad automática: fuera de alcance, evaluable después con datos reales |
| **R2** | **D5 puede trabar una orden**: si el creador es el único aprobador elegible del nivel, nadie puede firmar | Tres capas: la configuración avisa al guardar un nivel con menos de dos aprobadores; el motor devuelve *"No hay aprobador elegible para este nivel"*, no un error genérico; y la empresa que no pueda separar funciones enciende `permite_autoaprobacion` |
| **R3** | Comparación de usuarios por string | Normalizar a minúsculas + `Trim` **al guardar y al comparar**, en configuración y en el motor |
| **R4** | El `CHECK` de estado ya fue ampliado (H5) | El script hace `DROP`/`ADD`, y se aplica primero al mirror |
| **R5** | Identity en otro schema/contexto | Sin FK. El lookup de usuarios filtra por el claim de empresa |
| **R6** | Las pruebas actuales asumen Borrador → Aprobada directo | El control nace apagado: con `modo = 0`, `AprobarAsync` se comporta **exactamente** como hoy. Las pruebas nuevas encienden el control explícitamente |
| **R7** | Ausencia por vacaciones bloquea la escalera | D1b (cualquiera del nivel firma) lo mitiga. Delegación temporal: fuera de alcance |

---

## 10. Fases

| Fase | Entregable | Verificación |
|---|---|---|
| **F1** ✅ | `Database/2026-08-31_apr_niveles_01_estructura.sql`: 5 tablas + estado 7 + 8 índices + semilla apagada, y su `Database/2026-08-31_runbook_despliegue_srv.md`. Las funciones (`fn_apr_escalera`, `fn_apr_puede_firmar`) se separan a un `02_funciones` que va con F2, siguiendo el patrón numerado del módulo de presupuesto | Script escrito el 2026-08-31. **Sin aplicar** en mirror ni SRV. ⚠️ Debe aplicarse **después** del paso 1 de la tanda del 2026-08-27: ambos reemplazan `ck_alm_orden_compra_estado` y el de aquella tanda no incluye el 7 |
| **F2** ✅ | `Database/2026-08-31_apr_niveles_02_funciones.sql` (`fn_apr_escalera`, `fn_apr_es_aprobador`, `fn_apr_oc_pendientes`) + `IAprobacionService`/`AprobacionService` + `ICurrentUserService`/`CurrentUserService` + DTOs + constantes + registro en DI | Hecho el 2026-08-31. Script aplicado al mirror; **17 pruebas nuevas** en `SIAD.Tests/Aprobaciones/AprobacionMotorTests.cs` (escalera, control apagado, apertura, firma por usuario y por rol, secuencia, D5 en ambos sentidos, doble firma, rechazo, devolución, bandeja). Suite completa: **918 verdes, 0 rojas** |
| **F3** ✅ | Enganche en la O/C: `EnviarAAprobacionAsync`, `FirmarAprobacionAsync`, `DevolverABorradorAsync`, `RechazarAsync` ampliado (opera desde el estado 7 y **libera presupuesto**), anulación con rastro en bitácora. Endpoints `enviar-aprobacion` / `firmar` / `devolver` / `aprobaciones` / `pendientes-aprobacion` y permiso propio `module.compras.ordenes.aprobar` | Hecho el 2026-08-31. **9 pruebas nuevas** en `SIAD.Tests/Aprobaciones/OrdenCompraAprobacionTests.cs`, empezando por la **no-regresión** con el control apagado. Suite completa: **927 verdes, 0 rojas** |
| **F4** ✅ | Pantalla `/configuracion/aprobaciones` (interruptor + escalera + aprobadores por usuario o rol, con advertencias de configuración incompleta), `IAprobacionConfigService`, `AprobacionesConfigController` con **lookup de usuarios filtrado por empresa** (el existente exigía Super Administrador), cliente HTTP, permiso `module.configuracion.aprobaciones.*` y entrada en el menú lateral | Hecho el 2026-08-31. Compila y el portal levanta; la ruta protegida redirige al login. **Falta la prueba logueada**: no se hace desde aquí porque exigiría credenciales |
| **F5** ✅ | Bandeja `/almacen/mis-aprobaciones` (con días en espera), línea de tiempo de firmas en el formulario de la orden, botones Enviar/Firmar/Devolver en lista y formulario, badge «En aprobación (2 de 3)», filtro por el estado nuevo, firmas reales en el PDF del comprobante, métodos nuevos en `OrdenesCompraClient` y dos endpoints de apoyo (`aprobacion-config`, `aprobacion-progreso`) | Hecho el 2026-08-31. Compila, suite en **927 verdes**, el portal levanta sin errores de consola. **Falta la prueba logueada** de las tres pantallas |
| **F6** ✅ | `IAprobacionNotificador`: aviso al nivel que queda pendiente (al enviar y tras cada firma) y al comprador cuando su orden queda aprobada, rechazada o devuelta. Se agregó `ICorreoNotificador.NotificarDestinatariosAsync` para poder escribirle a una persona y no solo a un área | Hecho el 2026-08-31. **2 pruebas nuevas** verifican que cada paso avisa a quien le toca; suite **929 verdes**. Los avisos van **después** del commit y no propagan errores: un correo caído no revierte una firma. **Limitación conocida:** a los aprobadores declarados por ROL no se les escribe nominalmente (sus miembros viven en Identity, otro contexto); les llega por la copia al área |
| **F7** ◐ | **Requisición: hecha.** `Database/2026-08-31_apr_niveles_03_requisicion.sql` (tabla gemela `alm_requisicion_aprobacion`) + motor parametrizado por documento (`Mapa`) + enganche en `RequisicionDocumentoService` (enviar a revisión abre la escalera; aprobar firma el nivel; solo la última firma aprueba) | Hecho el 2026-08-31. **5 pruebas nuevas**, incluida la no-regresión con el control apagado. Suite **934 verdes**. **Factura de compra y pago a proveedor quedan FUERA**: ver §14 |

**F1–F3 es la entrega mínima con valor**: el flujo completo funcionando por API, configurable en base de
datos. F4–F5 lo vuelven operable por el usuario final.

---

## 11. Fuera de alcance de esta entrega

- Delegación / suplencia de aprobadores.
- Solicitud de compra y cotización (no existen en el sistema; son módulos nuevos, no aprobación).
- Caducidad automática de órdenes estancadas (R1).
- Jerarquía organizacional (cargos, departamentos, jefaturas). El borrador acierta al posponerla: el
  motor no cambia cuando llegue, solo se agrega un tipo de aprobador `tipo = 3` (por cargo) a §3.3.
- **Factura de compra y pago a proveedor** — ver §14, que explica por qué no son un enganche.

---

## 14. Por qué la factura de compra y el pago NO son un enganche (hallazgo 2026-08-31)

F7 se planteó como "extender el motor a tres documentos más". Al implementarla, solo la
**requisición** resultó ser un enganche real. Los otros dos exigen un cambio del modelo del
documento, no del motor:

| Documento | Estados hoy | Por qué no basta con enganchar |
|---|---|---|
| Factura de compra (`alm_compra_hdr`) | 1 Registrada · 9 Anulada | **Nace posteada**: el mismo `BEGIN…COMMIT` mueve el kardex, crea la CxP y asienta la partida. No hay un estado previo que aprobar |
| Pago a proveedor (`alm_compra_cxp_abono`) | Se registra y postea de una | El abono mueve banco, partida y retenciones en un solo commit. No existe un "pago solicitado" que espere autorización |

Aprobar cualquiera de los dos obliga a **separar capturar de postear**: un estado borrador que no
toque inventario ni contabilidad, y todo el posteo movido a la aprobación final. Eso es una fase
propia, con su diseño, su decisión de negocio y su impacto en cierres contables.

**Dónde está el valor.** Si se hace uno, el más rentable es el **pago**: una autorización por monto
antes de que salga el dinero es exactamente lo que un control de este tipo debe frenar. La factura
es un registro de algo que ya ocurrió (la mercadería llegó), así que aprobarla después de recibirla
aporta bastante menos.

---

## 15. Cambio de regla (2026-09-01): la aprobación NO es en cascada

El usuario pidió reemplazar la escalera acumulativa por **autorización por límite**. Es un cambio
de negocio, no un ajuste: **revierte D1 y D1b**.

### Antes (D1) y ahora

| | Escalera acumulativa (hasta 2026-08-31) | Límite de autorización (desde 2026-09-01) |
|---|---|---|
| Qué declara el nivel | `monto_desde`: **a partir de** qué monto se exige | `monto_hasta`: **hasta** cuánto puede autorizar |
| Una orden de 75,000 | Exige firmas de los niveles 1, 2 y 3, **en orden** | La aprueba **de una firma** quien llegue a 75,000 |
| Si el nivel 1 no ha firmado | El 2 está bloqueado | Irrelevante: no hay secuencia |
| Reparto del monto | — | No se reparte: una sola persona lo autoriza entero |
| Nadie alcanza el monto | No podía pasar (el nivel más bajo siempre entraba) | El documento **queda pendiente** y la pantalla lo dice |

### Decisiones de esta vuelta

- **D6 — Sin cascada.** Un tramo es una capacidad, no un escalón. Quien pertenece a un tramo cuyo
  límite cubre el total aprueba directamente. El `nivel` (1..9) solo ordena los tramos de menor a
  mayor capacidad, para poder decir cuál es el más bajo que cubre un monto.
- **D7 — `NULL` = sin tope**, para el tramo que autoriza cualquier monto sin escribir 999,999,999.
- **D8 — Los límites crecen con el nivel**, validado al guardar: es lo que da sentido a «el tramo
  más bajo que cubre este monto», que es lo que se muestra y lo que se registra.
- **D9 — Rechazar exige la misma capacidad que aprobar.** Quien no podría autorizar el monto
  tampoco puede tumbar el documento.
- **D10 — Sin aprobador capaz no se bloquea el envío.** El documento entra igual y se queda
  esperando, con el aviso a la vista en el listado y en la ficha. Bloquear escondería el problema.
- **D11 — Devolver a borrador solo mientras espera.** Como autorizar y aprobar son el mismo acto,
  una vez aprobado se sale anulando o cancelando, no devolviendo.

### Registro de la autorización (lo que pidió el requerimiento)

Queda en `apr_bitacora` y en la tabla de flujo del documento: **usuario** que autorizó, **tramo y
límite utilizados**, **monto aprobado**, **fecha y hora**, **estado anterior y nuevo** y la
**observación** cuando la hay.

### Qué NO cambió

El interruptor por empresa y documento, los aprobadores por usuario o por rol, D2 (el presupuesto
se compromete al autorizar), D4 (devolver borra la autorización), D5 (nadie autoriza lo suyo, con
su interruptor), los correos, el PDF y el permiso propio de aprobar.
