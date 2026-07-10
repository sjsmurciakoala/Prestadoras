# Estado general del proyecto — 2026-07-08

Documento de hilo: **dónde estamos, qué está hecho, qué falta.** Para que cualquier
sesión nueva retome sin perder contexto. Los detalles finos de cada fase están en
las memorias del proyecto (índice al final).

## 1. Los dos frentes de trabajo

### Frente A — Portal / contabilidad (repo `sjsmurciakoala/Prestadoras`)
Plan de integración contable-comercial **F1–F8 COMPLETO y mergeado a `main`**, más
follow-ups (condiciones, fix resolver medidos, mora en snapshot, campos de piloto).
Todo probado local contra `siad_v3_test`.

### Frente B — App de lectores (repo `sjsmurciakoala/app_lectores`, Flutter)
Plan **L0–L11 COMPLETO y mergeado**. Motor de factura V3 con paridad exacta,
impresión, sync. L12 (piloto) es lo único pendiente y depende de datos reales.

## 2. Deploy a 172.16.0.9 — HECHO (2026-07-08)

⚠️ **Aclaración crítica de contexto:** `172.16.0.9` es el servidor de **PRUEBAS**
(datos de prueba). La producción REAL vive en **SIMAFI, MySQL 172.16.0.3**. El
plan es adaptar los datos reales de SIMAFI a las tablas nuevas — no migrar por
migrar (eso, completo, es después). Para el demo del viernes se migran **2 ciclos**.

| Componente | Estado | Detalle |
|---|---|---|
| Portal `apc` | ✅ publicado | fix `PublishReadyToRun=false` en apc.Client (WASM); :80 |
| `apc.BancosWs` | ✅ instalado sin tráfico | `/simafi/api/heartbeat` = `ok ok`; :8087; cutover = ventana aparte |
| `apc.MobileApi.Lectores` | ✅ publicado | **`http://172.16.0.9:44817`**; sitio separado del MobileApi de órdenes de trabajo (no se pisó); firewall a acotar a VPN |
| BD `siad_v3` (14 scripts) | ✅ aplicados | F6=0 divergencias, F7 migró 4 períodos+5 ciclos, 10 lectores sembrados; backup pre-deploy en `Database/Backups/siad_v3_172-16-0-9_20260708_103855.backup` |
| APK piloto | ✅ firmado + instalado + verificado | flavor `pilot` → `172.16.0.9:44817`; `build/app/outputs/flutter-apk/app-pilot-release.apk`; login + diagnóstico 200 OK desde teléfono real (por VPN) |

**Ensayo de deploy validado antes:** los 14 scripts corren limpios sobre copia de
prod (172 tests, F6=0). Publish por servicio (`-Solo portal|bancosws|mobileapi`),
**nunca `-Solo todos`**.

## 3. Lo único que falta — la migración de 2 ciclos de SIMAFI (el trabajo del viernes)

**Meta (email de Cristian Pineda, visita viernes 10-jul):**
1. Migrar **2 ciclos** con sus clientes de facturación **con saldos al 31 de mayo**.
2. Facturar los 2 ciclos → verificar afectación de estado de cuenta + partida contable.
3. Simular pago completo de banco → verificar estado de cuenta + partida.
4. Verificar abono / recibo → estado de cuenta + partida.
5. (Aparte, otro contrato) Maestro de Artículos/Tipos/Grupo/Bodega — **no es este sistema**.

**El plan detallado está en:** [PLAN_MIGRACION_SIMAFI_2CICLOS.md](PLAN_MIGRACION_SIMAFI_2CICLOS.md)

## 4. Ventanas pendientes (después del viernes)
- **Cutover del banco** (`docs/f8-plan-cutover-ws-bancario.md`) — ventana aparte;
  exige toda la cartera facturando en SIAD.
- **Retiro de legacy** (historialmes, WS viejo, reglas/plantillas) — tras ≥1 mes estable.

## 5. Índice de memorias (contexto fino por tema)
`contabilidad-integracion-decision`, `ci-fase1..8`, `al-fase3-mobileapi`,
`al-condiciones-lectura`, `al-fix-resolver-medidos`, `al-mora-en-snapshot`,
`al-snapshot-campos-piloto`, `runbook-deploy-unico`, `app-lectores-plan-ejecucion`,
`al-fase12-apk-piloto`, **`simafi-migracion-comercial-2ciclos`** (la nueva),
`simafi-migracion-contabilidad-company2` (la contable, ya hecha).
