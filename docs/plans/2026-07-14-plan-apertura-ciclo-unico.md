# Plan — Apertura única de ciclo + calendario de facturación + retiro del legacy

**Fecha:** 2026-07-14 · **Estado:** EJECUTADO (5 de 5 fases implementadas, 2026-07-15)
**Contexto previo:** F7 (períodos comercial/contable, PR #8), app lectores L3 (apc.MobileApi, PR #10),
deploy completo a 172.16.0.9 (2026-07-08).

## 0. Estado de ejecución (2026-07-15)

| Fase | PR | Rama | Estado |
|---|---|---|---|
| A — Calendario de facturación | #22 | feat/calendario-facturacion | ✔ implementada; carga real de SIMAFI (2,541 filas) incluida |
| B — Apertura única e integral | #23 | feat/apertura-ciclo-integral | ✔ implementada; validada por el usuario en pantalla y con la copia de 0.9 |
| C — Eliminar Auxiliar de Lectura | #24 | feat/eliminar-auxiliar-lectura | ✔ implementada (−1,959 líneas); planilla vive en Períodos comerciales |
| E — Rutas por catálogo en Lectores | #25 | feat/lectores-ruta-catalogo | ✔ implementada |
| D — Retiro del legacy | #26 | feat/retiro-legacy-historialmes | ✔ implementada (ver hallazgos abajo) |

**Ramas apiladas: mergear en orden #22 → #23 → #24 → #25 → #26.**
Los 5 DDL (2 de A, 1 de B, 1 de D + el de Almacén de julio) van a 172.16.0.9 en la
próxima ventana de deploy, junto con el publish del portal y de apc.MobileApi.

**Hallazgos del gate D0 (2026-07-15, IIS de AGUAST140/172.16.0.9):**
- El WS WCF viejo (APCService.svc, app Java) **no está publicado** en 0.9 — el gate
  de "tráfico cero" quedó resuelto por inexistencia: nadie lee `historialmes`.
- Sitios reales: portal:80, apc.BancosWs:8087, apc.MobileApi.Lectores:44817 (la API
  nueva, swagger verificado) y apc.MobileApi:8086 = **backend de la app de órdenes de
  trabajo** (apc-AppMiTrabajo, com.apc.ordenes_trabajo_apc) — ajeno a este plan, no se toca.
- `usuarioapc` quedó **fuera** del DDL de la Fase D a propósito: se retira en la
  ventana de ops SOLO tras confirmar que el backend de órdenes de trabajo no la usa.
- Validación integral con copia real de 0.9 (`siad_v3_copia09` local): cierre de junio +
  `sp_adm_periodo_ciclo_abrir(2026,7,'19')` → roll-over de 473 clientes, 5 rutas todas
  con lector, fecha límite 27-jul del calendario. Backup completo:
  `Database/Backups/siad_v3_09_2026-07-15.backup`.

---

## 1. Problema

Hoy existen **dos caminos** para abrir un ciclo de facturación:

| Camino | Dónde | Comportamiento |
|---|---|---|
| "Formal" | Períodos comerciales (`sp_adm_periodo_comercial_abrir`) | Valida secuencia (mes anterior cerrado), **no** genera planilla de lectura |
| "Operativo" | Auxiliar de Lectura → Generar Período (`AuxiliarLecturaService.GenerarPeriodoAsync`) | **Sin validaciones** (`p_validar_secuencia=false`), cierra el ciclo abierto vigente **a escondidas y sin checklist**, genera la planilla en `historicomedicion`, puede reabrir meses cerrados |

Además:

- `calendariopro` (el calendario de facturación por ciclo) está **vacío en SIAD** → las facturas
  V3 salen con `fechavence` NULL. En SIMAFI (MySQL 172.16.0.3) está vivo y poblado:
  2,541 filas (2016–2026), 21 ciclos × 12 meses/año, fechas escalonadas
  (`fechaal`, `fechalec`, `fechafac`, `fecharefac`, `fechavence`, `diasvence`).
  SIMAFI opera **un ciclo abierto a la vez**, rodando por el mes según ese calendario
  (verificado 2026-07-14: ciclos 1–10 de julio cerrados, 11 abierto = lo que dice el calendario).
- El legacy (`historialmes` espejo, WS WCF viejo, app Java, `usuarioapc`,
  fallback `sp_informacion_ciclo`) **ya no se usa** (app Flutter + apc.MobileApi en producción)
  pero sigue montado.
- En la pantalla Lectores, la ruta del lector es un **texto libre** sin validar contra el
  catálogo `rutas`.

## 2. Decisiones (confirmadas con el usuario 2026-07-14)

| # | Decisión |
|---|---|
| D1 | **Un solo camino**: "Abrir ciclo" vive únicamente en Períodos comerciales y **siempre prepara la lectura** (genera la planilla). Desaparece la vía del Auxiliar |
| D2 | **La pantalla Auxiliar de Lectura se elimina completa** (incl. carga masiva). La consulta de planilla del ciclo pasa a Períodos comerciales |
| D3 | **El legacy se retira**: espejo `historialmes`, triggers, fallback `sp_informacion_ciclo`, WS WCF viejo, `usuarioapc` — previa verificación de tráfico cero |
| D4 | **Calendario de facturación** se copia de SIMAFI (solo el catálogo de fechas — NO es la migración comercial, que sigue su propio plan) y se administra en el portal con vista de calendario mensual (`DxScheduler` + `DxSchedulerMonthView`, confirmado en docs DevExpress vía dxdocs) |
| D5 | Se permiten **varios ciclos abiertos en paralelo** (recuperar atrasos), pero abrir uno nuevo con otro abierto genera **aviso visible** — nunca cierre silencioso. Cerrar es siempre explícito con validación de rutas (ya existe, F7) |
| D6 | Sin cambios en la app Flutter ni en el modelo lector→ruta→ciclo: al lector se le asigna **ruta**; el ciclo se deriva del período abierto |

## 3. Fases

Cada fase = 1 PR contra `main` (repo `sjsmurciakoala/Prestadoras`). DDLs timestamped en
`Database/ddl_v3/`, idempotentes. Orden: **A → B → C → D → E** (B depende de A;
C depende de B; D y E son independientes entre sí pero van después de C).

### Fase A — Calendario de facturación

**DDL `20260714_calendario_facturacion.sql`:**
1. `ALTER TABLE calendariopro`: agregar `company_id` (regla del repo: toda tabla funcional es
   multitenant) + PK/UK tenant-safe (`company_id, ano, mes, ciclo`) + backfill `company_id=2`.
   Registrar la entidad como `ICompanyScopedEntity` (re-scaffold parcial o partial manual).
2. Repunte de los SPs que la leen — **único cambio: filtro por company_id**:
   - `sp_lectura_v3` (versión vigente: `20260709_fix_sp_lectura_v3_montovalor_saldo.sql`)
   - `sp_adm_calcular_factura_lectura` (versión vigente: `20260707_fix_saldo_cross_company_calcular.sql`)
3. Carga desde SIMAFI: export de `bdsimafi.calendariopro` (2,541 filas) →
   `INSERT ... ON CONFLICT DO UPDATE` con `company_id=2`. Guardar el script de carga en
   `Database/ddl_v3/` (datos inline; es catálogo chico).

**Backend:** DTOs `SIAD.Core/DTOs/Facturacion/`, servicio `SIAD.Services/Facturacion/CalendarioFacturacionService`
(CRUD por año + "copiar año anterior" con desplazamiento de fechas), controller
`apc/Controllers/Facturacion/` con `[ModuleAuthorize]`, permisos en `PermissionNames` +
`PermissionEndpointCatalog`.

**UI `apc.Client/Pages/Facturacion/CalendarioFacturacion.razor`:**
- **Vista calendario mensual** con `DxScheduler` + `DxSchedulerMonthView`: cada día muestra los
  ciclos que leen/facturan/vencen ese día (colores por tipo de fecha). Solo lectura.
- Grid editable (estándar `siad-grid.css`) por año/mes/ciclo + botón "Copiar año anterior".
- Antes de tocar cualquier API DevExpress: consultar `dxdocs` (regla del repo).

**Tests:** SIAD.Tests — carga idempotente, `fechavence` ya no NULL al facturar con calendario presente.

### Fase B — Apertura única e integral de ciclo

**DDL `2026xxxx_sp_adm_periodo_ciclo_abrir.sql`:**

`sp_adm_periodo_ciclo_abrir(p_company_id, p_anio, p_mes, p_ciclo, p_usuario) RETURNS jsonb` —
una sola transacción:
1. Valida secuencia: mes calendario anterior cerrado si existe (**sin** parámetro de bypass).
2. No reabre períodos/ciclos cerrados (`PERIODO_CERRADO`/`CICLO_CERRADO`, igual que F7).
3. Crea/reutiliza `adm_periodo_comercial` + crea `adm_periodo_comercial_ciclo` con
   `fecha_limite` desde `calendariopro.fechalec/fechafac` (fallback: fin de mes + aviso).
4. **Genera la planilla** en `historicomedicion` (lógica migrada de `GenerarPeriodoAsync` C# → SQL):
   roll-over del mismo ciclo del mes anterior (`lect_act`→`lect_ant`, `consumo`→`consumoant`,
   usuario NULL) o, si no hay historia, alta desde `cliente_maestro` activos del ciclo
   (ruta del indicativo, medidor del detalle). Idempotente: si el ciclo ya tiene planilla del
   mes, no duplica.
5. Devuelve resumen jsonb: `clientes_planilla`, `rutas` (con lector asignado sí/no, cruzando
   `adm_lector_credencial` activos por ruta), `avisos[]`
   (`SIN_CALENDARIO`, `OTRO_CICLO_ABIERTO`, `RUTAS_SIN_LECTOR`).

`sp_adm_periodo_ciclo_deshacer(p_company_id, p_periodo_ciclo_id, p_usuario)`:
solo si el ciclo tiene 0 lecturas registradas (`usuario IS NULL` en toda la planilla) y
0 facturas del mes/ciclo → borra planilla + ciclo (+ período si queda vacío).
Reemplaza a `EliminarPeriodoAsync`.

**Retiro en el mismo DDL:** `DROP FUNCTION sp_adm_periodo_comercial_abrir` (el SP viejo con
`p_validar_secuencia`) una vez repuntado el servicio.

**Backend:** `PeriodoComercialService`: `AbrirAsync` pasa a llamar el SP nuevo y devuelve el
resumen; `DeshacerAperturaAsync` nuevo. Controller + client + DTOs del resumen.

**UI Períodos comerciales:**
- Popup "Abrir ciclo": propone año/mes/ciclo según el calendario y la fecha de hoy
  (el ciclo cuya `fechalec` es la más próxima), muestra preview (clientes, rutas, avisos)
  → confirmar → resumen del resultado.
- Acción "Deshacer apertura" en la fila del ciclo (con confirmación; deshabilitada si hay
  lecturas o facturas).
- **Planilla del ciclo** (viene de D2/Fase C): al expandir un ciclo, grid de su planilla
  (`historicomedicion` del año/mes/ciclo): clave, cliente, ruta, lect. anterior/actual,
  consumo, condición, usuario — con filtro "solo pendientes". Reusa el `SearchPagedAsync`
  actual del auxiliar (se muda a `PeriodoComercialService` o servicio propio).

**Tests:** apertura feliz, secuencia violada, ciclo cerrado no reabre, planilla roll-over,
planilla desde cliente_maestro, idempotencia, deshacer con/sin lecturas, avisos.

### Fase C — Eliminar Auxiliar de Lectura

1. Borrar `apc.Client/Pages/Auxiliares/AuxiliarLecturaIndex.razor` (+ .razor.cs/.razor.css si
   existen) y su entrada en `NavMenu`.
2. Borrar `apc.Client/Services/AuxiliarLectura/AuxiliarLecturaClient.cs` y su registro en
   `CommonServices.cs`.
3. Borrar `apc/Controllers/AuxiliarLecturaController.cs` (todos los endpoints
   `/api/auxiliarlectura*`), `SIAD.Services/AuxiliarLectura/*` completo
   (Search* se muda en Fase B), DTOs que queden huérfanos, permisos y entradas de
   `PermissionEndpointCatalog` del recurso auxiliar.
4. `dotnet build` + tests; buscar referencias huérfanas (`Grep AuxiliarLectura`).

### Fase D — Retiro del legacy (ventana corta en 0.9)

**Gate D0 — verificación, antes de borrar nada:**
- Tráfico del WS WCF viejo en 0.9: revisar logs de descarga
  (`SP_GENERAR_LOG_APP_CICLO` / tabla de log) y logs IIS de los últimos días → debe ser cero.
- Confirmar con el usuario que ningún teléfono quedó con la app Java.

**D1 — código:** quitar el fallback `sp_informacion_ciclo` de
`LectoresMobileService.GetCicloAsync` (queda solo el CTE V3).

**D2 — DDL `2026xxxx_retiro_historialmes.sql`:**
- `DROP TRIGGER` espejo sobre `adm_periodo_comercial(_ciclo)` +
  `DROP FUNCTION fn_adm_periodo_ciclo_espejo_sync / *_espejo_trigger`.
- `DROP FUNCTION sp_informacion_ciclo`.
- Backup de `historialmes` (dump) → `DROP TABLE historialmes`.
- Quitar la entidad del scaffold/contexto si está mapeada.

**D3 — ops (fuera del repo):** apagar el sitio del WS WCF viejo en el servidor; retirar
`usuarioapc` (backup previo). Coordinado con el siguiente deploy.

### Fase E — Pantalla Lectores: ruta desde catálogo

1. En `LectoresCredencialList.razor`: reemplazar el `DxTextBox` de "Ruta (libro)" por
   `DxComboBox` alimentado del catálogo `rutas` de la empresa (código + descripción),
   con búsqueda. Consultar `dxdocs` para la API del combo.
2. Validación server-side: la ruta debe existir en el catálogo al crear/editar credencial.
3. (Ya cubierto por Fase B) El resumen de apertura muestra rutas sin lector.

## 4. Riesgos y mitigaciones

| Riesgo | Mitigación |
|---|---|
| Mover la generación de planilla C#→SQL cambia comportamiento | Tests de paridad en SIAD.Tests comparando contra el resultado del flujo viejo antes de borrarlo (Fase B se mergea antes que C) |
| `calendariopro` multitenant rompe SPs que la leen | Solo 2 SPs la leen; repunte incluido en el mismo DDL; tests de facturación existentes (172 verdes) deben seguir pasando |
| Algún teléfono viejo con app Java | Gate D0 con logs; el drop de `historialmes` lleva backup y va en DDL separado (rollback = restaurar dump + recrear triggers desde el DDL de F7) |
| El demo/operación usa el Auxiliar mientras tanto | Fases B y C van en PRs separados: primero existe el camino nuevo, después se quita el viejo |
| Fechas del calendario 2026 incompletas en SIMAFI (cargado hasta julio) | La pantalla de Fase A permite completar ago–dic 2026 con "copiar mes/año anterior" |

## 5. Deploy

Por el runbook vigente (`docs/RUNBOOK_DEPLOY_2026-07.md`): DDLs en orden de timestamp,
publish del portal (`publish-onprem.ps1`, nunca `-Solo todos`) + `mobileapi` cuando entre D1.
La Fase D exige su gate de verificación y backup dentro de la misma ventana.
