# RUNBOOK — Deploy único a producción (F1–F8 + L3)

**Fecha del documento:** 2026-07-07
**Estado:** DOCUMENTO EJECUTABLE — **nada se ejecuta contra producción (172.16.0.9) mientras se escribe esto.**
**Repo de trabajo:** `github.com/sjsmurciakoala/Prestadoras` (anidado en `E:\Koala\proyectos\HODSOFT_DEVEXPRESS\Prestadoras`).
**Alcance:** aplicar TODO lo que está en `main` y probado local contra `siad_v3_test` a producción, en **una sola ventana**.

Este runbook consolida:
- Plan maestro: [docs/plans/2026-07-02-plan-integracion-contable-comercial.md](plans/2026-07-02-plan-integracion-contable-comercial.md)
- Handoff F1–F8: [docs/handoff-integracion-contable-2026-07.md](handoff-integracion-contable-2026-07.md)
- Cutover del banco (ventana APARTE): [docs/f8-plan-cutover-ws-bancario.md](f8-plan-cutover-ws-bancario.md)

> **Regla de oro del documento:** cada paso trae su verificación. **No se pasa al siguiente paso hasta que la verificación del actual pasa.** Si una verificación falla, se detiene el deploy y se evalúa rollback (§9).

---

## 0. Mapa de lo que se despliega (y lo que NO)

| Componente | Qué es | Se despliega en esta ventana | Cómo |
|---|---|---|---|
| **BD `siad_v3`** | PostgreSQL prod 172.16.0.9 | **Sí** — 14 scripts DDL + correcciones de datos | psql |
| **`apc`** (portal) | Blazor + API + Identity | **Sí** | `publish-onprem.ps1 -Solo portal` |
| **`apc.MobileApi`** (L3) | API móvil REST de la app Flutter nueva | **Sí** | `publish-onprem.ps1 -Solo mobileapi` |
| **`apc.BancosWs`** (F8) | WS bancario SIMAFI (contrato congelado) | **Se publica e instala, SIN recibir tráfico** | `publish-onprem.ps1 -Solo bancosws` — el corte del canal es la **ventana aparte** (§10) |
| **`WSappLectores`** (WCF viejo) | WS Java-consumido por la app Android vieja | **NO SE TOCA NI SE PUBLICA** | — sigue vivo hasta su retiro en L12 |

> ⚠️ **Trampa de `publish-onprem.ps1`:** `-Solo todos` publica **portal + el WS WCF viejo (`WS_APC`)** y **NO** incluye `bancosws` ni `mobileapi` (están excluidos de "todos" a propósito). Por lo tanto **NO usar `-Solo todos`** en este deploy: republicaría el WCF viejo (que no se toca) y omitiría los dos hosts nuevos. **Publicar los tres net9 uno por uno:** `-Solo portal`, `-Solo mobileapi`, `-Solo bancosws`.

Objetos de un solo tenant hoy en prod: **1 empresa activa (`company_id = 2`)**, plan ERSAPS de 1,674 cuentas, piloto de 25 facturas / 27 clientes / 12 servicios / 1 NC / 4 ND.

---

## 1. Pre-deploy (antes de abrir la ventana)

### 1.1 Ventana y congelamiento
- [ ] **Ventana acordada** con operaciones/contador (fecha + hora + duración estimada). Sugerido: horario de baja actividad comercial (no hay lecturas de campo ni caja abierta).
- [ ] **Congelar cambios**: no más merges a `main`, no más scripts DDL nuevos, `main` en el commit exacto que se va a desplegar. Anotar el SHA desplegado en la bitácora.
- [ ] Confirmar que la suite verde corresponde a ese SHA: **172 passed / 2 skipped** (los 2 skips son preexistentes por datos del backup).
- [ ] Avisar a operaciones que durante la ventana **el portal estará caído** (iisreset).

### 1.2 Confirmar que el WS viejo queda intacto
- [ ] Verificar en el server que **`WSappLectores` sigue instalado y sirviendo** (la app Android de campo depende de él): `GET https://<host>/WS_APC.svc?wsdl` → WSDL visible **antes** de tocar nada.
- [ ] Confirmar por escrito que **este deploy no lo republica ni lo apaga**. Es la red de seguridad de los lectores (§9).

### 1.3 Backup completo de prod (obligatorio, es el punto de rollback)
```powershell
# desde Prestadoras/  — SOLO LECTURA sobre prod (pg_dump)
.\Database\backup_completo.ps1 -Origen prod
```
Esto genera `Database\Backups\siad_v3_172-16-0-9_<timestamp>.backup` (formato custom).

**Verificación:**
- [ ] El archivo `.backup` existe y su tamaño es coherente con el histórico (> los backups previos, no 0 bytes).
- [ ] Anotar la **ruta y timestamp exactos** del backup en la bitácora — es el artefacto de rollback (§9.1).
- [ ] **Guardar una copia fuera del server** (el rollback no puede depender de que el disco del server sobreviva).

### 1.4 Ensayo en staging (red de seguridad real — hacer ANTES de la ventana)
El verdadero gate de regresión **no es** correr tests en prod, es reproducir el deploy sobre una copia fresca de prod:
```powershell
# Restaurar el backup de 1.3 a una BD de staging (NO prod)
createdb -h localhost -U postgres siad_v3_staging
pg_restore -h localhost -U postgres -d siad_v3_staging --no-owner --no-privileges "<ruta-del-backup>.backup"
# Aplicar los 14 scripts del §2 en orden, luego:
$env:SIAD_TEST_DB = 'Host=localhost;Database=siad_v3_staging;Username=postgres;Password=<pass>'
dotnet test SIAD.Tests/SIAD.Tests.csproj
```
**Verificación:**
- [ ] Los 14 scripts aplican **sin error** sobre datos reales de prod (no solo sobre `siad_v3_test`).
- [ ] Suite completa **172 passed / 2 skipped**.
- [ ] `SELECT * FROM fn_con_verificar_saldo_cuenta(2);` → **0 filas** (0 divergencias) tras el rebuild de F6.
- [ ] Si algo falla aquí, **el deploy NO se agenda** hasta corregirlo. Este ensayo es lo que convierte la ventana de prod en mecánica.

### 1.5 Checklist de prerequisitos de acceso
- [ ] Acceso `psql` a `172.16.0.9 / siad_v3` con usuario con permisos DDL.
- [ ] Acceso RDP/copia al server IIS (para publicar y `iisreset`).
- [ ] Llaves reales del banco disponibles para §3.3 (desde **MySQL bdsimafi 172.16.0.3**, tabla `recolector`) — **nunca pasan por el repo**.
- [ ] Keystore de piloto del APK (`android/app/pilot-upload.jks`, fuera de git, resguardado) disponible para §6.
- [ ] Contador disponible/consultado para la **decisión de la partida retroactiva** (§5.5).

---

## 2. Scripts DDL — los 14, EN ESTE ORDEN EXACTO

Todos en `Database/ddl_v3/`, **idempotentes** (CREATE OR REPLACE / ON CONFLICT / IF NOT EXISTS). El orden importa: los scripts 11–14 vuelven a redefinir `sp_adm_calcular_factura_lectura` y el snapshot offline, cada uno **superseding** al anterior — aplicarlos fuera de orden deja la versión equivocada.

Aplicar cada uno con parada dura ante error:
```powershell
$env:PGPASSWORD='<pass>'
psql -v ON_ERROR_STOP=1 -h 172.16.0.9 -U postgres -d siad_v3 -f "Database/ddl_v3/<script>.sql"
```

> **NOTA CRÍTICA:** el viejo **`20260702_nc_nd_posteo_contable.sql` NO se aplica** — fue superseded por F5 (`20260704_ci_fase5_ncnd_posteo_config.sql`) y ya estaba revertido en prod. **No está en la lista de abajo. No aplicarlo.**

Tras cada script, correr su **verificación de humo SQL** (la función/tabla existe). El gate de regresión pesado (LecturaV3Tests) ya se cubrió en el ensayo de staging (§1.4); en prod la verificación por script es de existencia/estructura.

| # | Script | Verificación de humo (SQL, debe devolver la fila / `t`) |
|---|---|---|
| 1 | `20260702_ci_fase1_integracion_config.sql` | `SELECT to_regclass('con_integracion_config'), to_regclass('con_integracion_cuenta'), to_regclass('con_partida_pendiente'), to_regprocedure('fn_con_resolver_cuenta(bigint,text,bigint,bigint,boolean)') IS NOT NULL;` |
| 2 | `20260703_ci_fase2_asientos_config.sql` | `SELECT to_regclass('con_integracion_asiento');` |
| 3 | `20260703_ci_fase3_lote_facturacion.sql` | `SELECT to_regclass('con_partida_factura'), to_regclass('con_lote_facturacion');` + `SELECT count(*) FROM information_schema.columns WHERE table_name='factura' AND column_name IN ('categoria_servicio_id','con_medicion');` (=2) + `SELECT tgname FROM pg_trigger WHERE tgname='trg_factura_snapshot_dimensional';` |
| 4 | `20260703_ci_fase4_captacion_config.sql` | `SELECT proname FROM pg_proc WHERE proname IN ('sp_con_generar_comprobante_config','sp_con_revertir_comprobante_config');` (2 filas) |
| 5 | `20260704_ci_fase5_ncnd_posteo_config.sql` | `SELECT proname FROM pg_proc WHERE proname='fn_con_lineas_nota';` + `SELECT count(*) FROM information_schema.columns WHERE table_name IN ('adm_nota_credito','adm_nota_debito') AND column_name='poliza_id';` (=2) |
| 6 | `20260704_ci_fase6_saldos_oficiales.sql` | `SELECT proname FROM pg_proc WHERE proname IN ('fn_con_saldo_libro','sp_con_reconstruir_saldo_cuenta','fn_con_verificar_saldo_cuenta');` (3 filas). **El script corre el rebuild solo.** → luego `SELECT * FROM fn_con_verificar_saldo_cuenta(2);` **= 0 filas** |
| 7 | `20260704_ci_fase7_periodo_cierre.sql` | `SELECT to_regclass('adm_periodo_comercial'), to_regclass('adm_periodo_comercial_ciclo');` + `SELECT proname FROM pg_proc WHERE proname='fn_adm_periodo_comercial_actual';` |
| 8 | `20260704_ci_fase8_ws_bancario.sql` | `SELECT to_regclass('ban_ws_credencial'), to_regclass('ban_ws_pago');` + `SELECT proname FROM pg_proc WHERE proname IN ('fn_ban_ws_pendientes','sp_ban_ws_pagar','sp_ban_ws_reversar');` (3 filas) |
| 9 | `20260705_al_fase3_mobileapi_lector.sql` | `SELECT to_regclass('adm_lector_credencial'), to_regclass('adm_lector_sesion');` |
| 10 | `20260706_al_condiciones_lectura.sql` | `SELECT to_regclass('adm_condicion_lectura_tipo'), to_regclass('adm_condicion_lectura');` + `SELECT character_maximum_length FROM information_schema.columns WHERE table_name='historicomedicion' AND column_name='condicion';` (=10) |
| 11 | `20260706_fix_resolver_medidos.sql` | `SELECT proname FROM pg_proc WHERE proname='sp_adm_calcular_factura_lectura';` (existe; el fix de medidos entra en el cuerpo) |
| 12 | `20260706_mora_en_snapshot_offline.sql` | `SELECT proname FROM pg_proc WHERE proname='sp_adm_generar_snapshot_offline_cliente_lectura';` (existe; bloque `mora` en el cuerpo) |
| 13 | `20260707_fix_saldo_cross_company_calcular.sql` | `sp_adm_calcular_factura_lectura` redefinido (fix saldo 2-arg company-scoped). Verificar: `SELECT pg_get_functiondef(p.oid) LIKE '%sp_obtener_cliente_saldo(%,%'  FROM pg_proc p WHERE proname='sp_adm_calcular_factura_lectura';` → `t` (usa el overload de 2 argumentos) |
| 14 | `20260707_snapshot_campos_piloto.sql` | `sp_adm_generar_snapshot_offline_cliente_lectura` redefinido. Verificar que el snapshot lleva los bloques nuevos: `SELECT pg_get_functiondef(p.oid) LIKE '%emisor%' AND pg_get_functiondef(p.oid) LIKE '%cliente_rtn%' AND pg_get_functiondef(p.oid) LIKE '%fecha_lectura_anterior%' FROM pg_proc p WHERE proname='sp_adm_generar_snapshot_offline_cliente_lectura';` → `t` |

**Verificación global tras los 14:**
- [ ] Ningún script abortó (`ON_ERROR_STOP=1` no disparó).
- [ ] `SELECT * FROM fn_con_verificar_saldo_cuenta(2);` → **0 divergencias** (el rebuild de F6 dejó el caché sano).
- [ ] La migración de F7 corrió sola: `SELECT count(*) FROM adm_periodo_comercial WHERE company_id=2;` > 0 (migró desde `historialmes`).

---

## 3. Correcciones de datos en prod

Estas NO son idempotentes de la misma forma: son limpiezas de datos reales del piloto. Cada una con su verificación.

### 3.1 `cont_account_id` huérfanos en `adm_servicio` (12 filas)
Los `adm_servicio.cont_account_id` de prod apuntan a ids que no existen en el plan cargado (huérfanos). El perfil ERSAPS resuelve por `code`, no por estos ids, pero hay que sanearlos para que el backfill quede consistente.

1. **Diagnóstico:**
   ```sql
   SELECT s.adm_servicio_id, s.cont_account_id
   FROM adm_servicio s
   LEFT JOIN con_plan_cuentas c ON c.account_id = s.cont_account_id AND c.company_id = s.company_id
   WHERE s.company_id = 2 AND s.cont_account_id IS NOT NULL AND c.account_id IS NULL;
   ```
2. Limpiar los ids huérfanos (poner NULL o el `account_id` correcto según el `code` ERSAPS acordado con el contador).
3. **Re-correr el backfill del script día-10**: `Database/ddl_v3/20260514_dia10_mapeo_cuentas_ersaps.sql` (mapea servicios ↔ cuentas por convención de código; idempotente).

**Verificación:** la consulta del paso 1 devuelve **0 filas**.

### 3.2 Clientes sin categoría (2–3)
El modo `POR_SERVICIO_CATEGORIA` y los reportes por categoría exigen `categoria_servicio_id`.

1. **Diagnóstico:**
   ```sql
   SELECT maestro_cliente_id, maestro_cliente_clave
   FROM cliente_maestro
   WHERE company_id = 2 AND categoria_servicio_id IS NULL;
   ```
2. Asignar la categoría correcta a cada uno (con el contador/operaciones).

**Verificación:** la consulta devuelve **0 filas**.

### 3.3 Llaves reales del banco en `ban_ws_credencial`
> **Las llaves NUNCA pasan por el repo.** Se leen del origen y se insertan directo en prod.

1. Origen — **MySQL bdsimafi 172.16.0.3** (solo lectura):
   ```sql
   SELECT idBancoWS, descripcion, llave, vigencia
   FROM recolector WHERE idBancoWS IS NOT NULL AND llave IS NOT NULL;
   ```
2. Destino — plantilla `INSERT ... ON CONFLICT` al final de `Database/ddl_v3/20260704_ci_fase8_ws_bancario.sql`, asignando `banco_cuenta_id` por recolector (cuenta contable definida con el área contable).

**Verificación:** `SELECT codigo_banco, activo FROM ban_ws_credencial WHERE company_id=2;` lista cada recolector activo esperado (001 Occidente, 002 Davivienda, 015 Banpaís, 028 Ficohsa, 051 Comixven, 777 Tigo Money, y los demás con llave activa).
> Nota: la **verificación de autenticación end-to-end** de las llaves (`GET /simafi/api/auth`) es parte del **cutover** (§10), no de esta ventana — el WS bancario no recibe tráfico todavía.

---

## 4. Configuración por pantalla (no SQL)

Se hace desde el portal ya publicado (§7) con un usuario SuperAdministrador, sobre la **empresa actual** (`TenantState.EnsureCompanyAsync()` — el controller solo acepta la empresa de los claims). Puede hacerse justo después de publicar el portal.

### 4.1 Perfil ERSAPS
- Pantalla **Contabilidad → Integración Contable** (`/contabilidad/empresas/integracion`), pestaña **Ventas/CxC** → botón **"Aplicar perfil ERSAPS"**.
- **Verificación:** `SELECT count(*) FROM con_integracion_cuenta WHERE company_id=2;` > 0 (matriz llena) y `SELECT * FROM fn_con_resolver_cuenta(2,'INGRESO',NULL,NULL,NULL);` resuelve sin `RAISE`.

### 4.2 Diarios y tipos por módulo (pestaña **Asientos**)
- Por cada módulo (`VENTAS, CAJA, BANCOS, NOTAS, MISCELANEOS, PROV`): diario (`journal_id`) + tipo de partida (`type_id`).
- **Verificación:** `SELECT modulo, journal_id, type_id FROM con_integracion_asiento WHERE company_id=2;` — todos los módulos que se van a activar tienen diario+tipo.

### 4.3 Activar flags `activo_*` módulo por módulo
- En la misma pestaña Asientos, activar cada flujo cuando su asiento y matriz estén completos (activar de a uno y verificar, no todos de golpe).
- **Verificación por módulo:** emitir/registrar un movimiento de prueba del módulo recién activado y confirmar que postea (ver E2E §8). `SELECT activo_ventas, activo_caja, activo_bancos, activo_notas, activo_miscelaneos FROM con_integracion_config WHERE company_id=2;`
> ⚠️ **`activo_bancos`** puede quedar activado ahora (la config es necesaria antes del cutover del banco), pero el WS bancario **no recibe tráfico hasta §10**.

### 4.4 Catálogo de condiciones de lectura
- Pantalla **Facturación → Condiciones de lectura** (`/facturacion/condiciones-lectura`). El script F-L10 sembró el catálogo inicial por empresa; revisar/ajustar códigos y tipos (el `tipo` viene de la referencia global N/MIN/PND/PD).
- **Verificación:** `GET /api/condiciones` (con Bearer de un lector) devuelve el catálogo activo ordenado; en BD `SELECT codigo, tipo_codigo, activo FROM adm_condicion_lectura WHERE company_id=2 ORDER BY orden;`.

### 4.5 Decisión con el contador — partida retroactiva del piloto
- **Decisión requerida** (ver §5.5): partida retroactiva de las 25 facturas piloto **vs** corte desde julio.
- Según lo decidido, en §8 se corre (o no) el lote sobre el rango histórico.

---

## 5. Notas de decisión / precondiciones embebidas

### 5.5 Partida retroactiva de las 25 piloto vs corte desde julio
- **Retroactiva:** `sp_con_generar_partidas_facturacion(2, '<inicio-piloto>', '<hoy>', 'DIA')` — genera la contabilidad de las 25 facturas ya emitidas. Idempotente (correrlo dos veces no duplica; puente `con_partida_factura`).
- **Corte desde julio:** no se tocan las 25; la contabilidad arranca con la facturación nueva.
- **Dueño de la decisión:** el contador. Documentar la elección en la bitácora **antes** de correr §8.

---

## 6. Publish de los 3 servicios net9 (uno por uno)

Desde `Prestadoras/`. **No usar `-Solo todos`** (§0). Publicar, copiar la carpeta al app root de IIS correspondiente, y reciclar.

```powershell
.\publish-onprem.ps1 -Solo portal      # -> publish_<ts>\portal
.\publish-onprem.ps1 -Solo mobileapi   # -> publish_<ts>\mobileapi
.\publish-onprem.ps1 -Solo bancosws     # -> publish_<ts>\bancosws  (se instala; sin tráfico hasta §10)
```
Cada uno: copiar a su app root de IIS, ajustar `appsettings.json` del server (connection string `siad_v3` prod; `MobileApi:SesionHoras`; binding `/simafi/api` para bancosws), `iisreset` (o recycle del app pool de cada uno).

**Healthcheck por servicio (verificación):**
- [ ] **Portal:** `https://<host>/` → carga y **login OK**. (`SELECT 1` implícito: el portal conecta a la BD.)
- [ ] **MobileApi:** `https://<host-mobileapi>/swagger` carga **y** `GET /api/diagnostico` → 200.
- [ ] **BancosWs:** `GET https://<host-bancosws>/simafi/api/heartbeat` → **`ok ok`** (heartbeat responde; el servicio está arriba aunque no reciba tráfico del banco todavía).

---

## 7. App de lectores — distribución del APK del piloto

> El WS WCF viejo **sigue vivo** para la app Android Java; esto es la app **nueva** (Flutter) del piloto, que pega a `apc.MobileApi`.

- [ ] Firmar el APK de prod con el **keystore de piloto** (`android/app/pilot-upload.jks`, fuera de git, resguardado).
- [ ] El APK apunta a la URL de **`apc.MobileApi` de prod** (no a `siad_v3_test` ni al WCF viejo).
- [ ] Distribuir a los dispositivos del piloto.

**Verificación:**
- [ ] Un dispositivo hace **login** contra `apc.MobileApi` de prod (`POST /api/lectores/login` → Bearer token).
- [ ] Descarga una ruta (`GET /api/rutas/.../snapshot/...`) y el `snapshot_json` trae los bloques nuevos: `emisor`, `cliente_rtn`, `fecha_lectura_anterior`, `mora` (aunque `mora.activo=false`). Esto se cierra en el E2E §8.

---

## 8. Verificación end-to-end post-deploy

Con todo publicado y configurado, ejercer un flujo real de cada capa (idealmente con datos de prueba acotados o en horario controlado):

1. **Lote de facturación:** correr `sp_con_generar_partidas_facturacion(2, <rango>, 'DIA')` (o desde la pantalla **Partidas de facturación** → preview → generar).
   - **Verificación:** el lote queda **balanceado** (Debe=Haber); `fn_con_verificar_saldo_cuenta(2)` = 0; correrlo dos veces **no duplica** (idempotencia por `con_partida_factura`).
2. **Cobro en caja:** registrar un cobro total (201) de una factura.
   - **Verificación:** postea automático (Caja / CxC abonados); la factura pasa a estado C; hay póliza asociada.
3. **Ciclo completo de la app nueva:** desde un dispositivo del piloto — **descarga de ruta → lectura → factura V3 → sync**.
   - **Verificación:** la factura se registró en `siad_v3` vía `apc.MobileApi` (`sp_lectura_v3`), con CAI/correlativo formal; el snapshot offline reprodujo el total (incluida mora si aplica) igual que el online.
4. **(Opcional / si se puede en shadow) Consulta del WS bancario:** `GET /simafi/api/consulta/servicios/<clave>?key=<llave>&banco=<banco>` (solo lectura) contra un cliente conocido.
   - **Verificación:** XML byte-compatible con el WS viejo. **Sin pagos ni reversiones** — eso es cutover (§10).

**Verificación de cierre de la ventana:**
- [ ] `SELECT * FROM fn_con_verificar_saldo_cuenta(2);` → 0 divergencias (final).
- [ ] Banner de avisos de períodos en el portal sin alertas rojas inesperadas.
- [ ] Los 3 healthchecks (§6) siguen verdes tras el tráfico de prueba.

---

## 9. Rollback por componente

Disparadores: verificación crítica falla, contabilidad no cuadra, el portal no levanta, o la app no puede sincronizar.

### 9.1 Base de datos
- **Restore del backup pre-deploy (§1.3)** con `Database/restore_bd.ps1` (modo "Reemplazar BD existente" solo si se decide sobrescribir `siad_v3`, o restaurar a `siad_v3_restore` y repuntar). Este backup es el único punto de verdad del estado previo.
- Los scripts DDL son idempotentes pero **no auto-revertibles**: el camino de rollback de esquema es el restore, no "des-aplicar" scripts.

### 9.2 Servicios
- **Portal / MobileApi / BancosWs:** repuntar el app root de IIS a la carpeta publicada anterior (conservar la versión previa antes de copiar la nueva) y `iisreset`. Si un servicio no levanta, apagar su app pool.
- **`apc.BancosWs`:** como no recibe tráfico en esta ventana, apagarlo no afecta al banco (el canal sigue en el WS SIMAFI viejo hasta el cutover).

### 9.3 Red de seguridad de los lectores
- **El WS WCF viejo (`WSappLectores`) queda intacto durante todo el deploy.** Si la app nueva del piloto falla, los lectores de campo siguen operando con la app Android vieja contra el WS viejo. No se apaga nada del canal viejo en esta ventana.

---

## 10. Cutover del banco — VENTANA APARTE, POSTERIOR

> **No se mezcla con este deploy.** El cutover del WS bancario es una ventana propia y posterior, con su propio runbook: **[docs/f8-plan-cutover-ws-bancario.md](f8-plan-cutover-ws-bancario.md)**. Aquí solo se referencia; **no ejecutar sus pasos en la ventana F1–F8+L3.**

Recordatorios clave de esa ventana (ver el documento para el detalle):
- **Restricción dura:** el corte del canal **solo puede ocurrir cuando TODA la cartera esté facturando en SIAD**. SIMAFI comercial sigue vivo y facturando (período 2026/06) y 6+ recolectores pagan HOY por el WS viejo. Cortar antes = "No existe registro" para los abonados aún facturados en SIMAFI = canal de recaudación roto. **No hay fallback dual** (un solo sistema de registro por diseño).
- Precondiciones del cutover que **este** deploy deja listas: scripts F1–F8 aplicados, portal publicado, `activo_bancos=true` + asiento `BANCOS` + matriz CXC/BANCO_DEFAULT, `apc.BancosWs` instalado y con heartbeat, llaves migradas en `ban_ws_credencial`.
- El cutover añade: captura real del XML del WS viejo para confirmar el orden alfabético de elementos, migración/verificación de llaves con `GET /auth`, repunte de IP/DNS/proxy del GlassFish viejo → `apc.BancosWs`, congelamiento de reversiones cruzadas, monitoreo intensivo, y plan de rollback repuntando al WS viejo.

---

## 11. Secuencia global recomendada (qué agrupar en cada ventana)

```
┌─ VENTANA 1 — Deploy único (este runbook) ──────────────────────────────┐
│  1. Pre-deploy: backup prod + ensayo staging + congelar (§1)            │
│  2. 14 scripts DDL en orden, con humo por script (§2)                   │
│  3. Correcciones de datos (§3)                                          │
│  4. Publish portal + mobileapi + bancosws(instala, sin tráfico) (§6)    │
│  5. Config por pantalla: ERSAPS, asientos, flags, condiciones (§4)      │
│  6. Distribución APK piloto → apc.MobileApi prod (§7)                   │
│  7. E2E: lote + caja + app nueva + (shadow) consulta banco (§8)         │
│     → WS WCF viejo INTACTO toda la ventana (red de seguridad)           │
└────────────────────────────────────────────────────────────────────────┘
              │  (estabilización: operar, conciliar, verificar saldos)
              ▼
┌─ VENTANA 2 — Cutover del banco (docs/f8-plan-cutover-ws-bancario.md) ───┐
│  Solo cuando TODA la cartera facture en SIAD.                           │
│  Repunte del canal SIMAFI viejo → apc.BancosWs; monitoreo intensivo;    │
│  rollback repuntando al WS viejo (que sigue vivo).                      │
└────────────────────────────────────────────────────────────────────────┘
              │  (≥ 1 mes estable)
              ▼
┌─ VENTANA 3+ — Retiros de legacy (regla espejo, decisión aparte) ────────┐
│  Tras ≥1 mes estable:                                                   │
│   • Retiro del WS bancario SIMAFI viejo + acceso MySQL del canal.       │
│   • Retiro físico de con_regla_integracion / con_plantilla_partida_* /  │
│     historialmes y su trigger espejo (post-F7 estable).                 │
│   • Retiro del WS WCF de lectores (WSappLectores) — cuando L12 retire   │
│     la app Android vieja.                                               │
└────────────────────────────────────────────────────────────────────────┘
```

**Reglas de agrupación:**
- **Todo lo de la Ventana 1 va junto**: los 3 servicios net9 y los 14 scripts comparten esquema y contrato (el portal y la MobileApi leen las tablas nuevas; publicarlos por separado en distintas ventanas dejaría código nuevo contra esquema viejo o al revés).
- **El cutover bancario exige ventana propia** por la regla "un solo sistema de registro" y su precondición de cartera completa — no se puede meter en la ventana de deploy.
- **Los retiros de legacy son irreversibles** y esperan ≥1 mes de estabilidad; nunca en la misma ventana que introduce lo nuevo.

---

## 12. Bitácora de la ventana (llenar al ejecutar)

| Ítem | Valor |
|---|---|
| SHA de `main` desplegado | |
| Ruta+timestamp del backup pre-deploy (§1.3) | |
| Resultado ensayo staging (§1.4) | passed/skipped: __ / __ ; divergencias F6: __ |
| 14 scripts aplicados sin error (§2) | sí / no |
| `fn_con_verificar_saldo_cuenta(2)` tras F6 y al cierre | 0 / __ |
| Huérfanos `cont_account_id` tras §3.1 | 0 / __ |
| Clientes sin categoría tras §3.2 | 0 / __ |
| Recolectores en `ban_ws_credencial` (§3.3) | |
| Decisión partida piloto (§5.5) | retroactiva / corte-julio |
| Healthchecks §6 (portal / mobileapi / bancosws) | OK / OK / OK |
| E2E §8 (lote / caja / app / consulta banco) | |
| WS WCF viejo verificado intacto (§1.2) | sí |
| Incidencias / rollback ejecutado | |
```
