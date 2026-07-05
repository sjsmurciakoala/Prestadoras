# Handoff — Integración Contable ↔ Comercial (F1–F8, julio 2026)

**Fecha:** 2026-07-04 · **Estado:** plan completo, TODO en local — **NADA desplegado a producción**
**Para:** quien tome el repo viniendo del código anterior a estos cambios.

Este documento resume TODO lo que se construyó entre el 2026-07-02 y el 2026-07-04 sobre
el plan [docs/plans/2026-07-02-plan-integracion-contable-comercial.md](plans/2026-07-02-plan-integracion-contable-comercial.md).
Leé el plan primero: las decisiones D1–D10 de su §2 están acordadas con el cliente y **no se reabren**.

---

## 0. Lo primero: el repo donde vive esto (IMPORTANTE)

Hay **dos repos git anidados** y es la primera confusión de todo el que llega:

| Repo | Raíz | Remoto | Qué es |
|---|---|---|---|
| Padre | `E:\Koala\proyectos\HODSOFT_DEVEXPRESS` | `github.com/koala-outsourcing/HODSOFT_DEVEXPRESS` | El repo histórico del equipo (GitHub Desktop). **Todo este trabajo NO está ahí.** |
| Anidado | `E:\Koala\proyectos\HODSOFT_DEVEXPRESS\Prestadoras` | `github.com/sjsmurciakoala/Prestadoras` (privado) | **Acá vive TODO el trabajo F1–F8**, en `main`, con un PR por fase (#1–#9), cada uno con code-review multi-agente y correcciones aplicadas. |

Cualquier comando git corrido DENTRO de `Prestadoras/` opera el repo anidado. Para traerte el trabajo:
`git clone https://github.com/sjsmurciakoala/Prestadoras.git` (pedir acceso al repo privado), o pedir
que se transfiera a la org.

**Sincronizar con el repo padre es una tarea pendiente y deliberadamente NO hecha**: el padre
tiene su propia historia de los mismos archivos y ~1,400 archivos de drift. No hagas merge a ciegas.

---

## 1. Qué problema resuelve todo esto (en un párrafo)

Antes de estos cambios, ningún movimiento comercial generaba contabilidad de forma confiable:
`sp_lectura_v3` no postea (decisión vieja), NC/ND posteaban con una plantilla gruesa (revertida en
prod), y la configuración contable eran reglas/plantillas deprecadas. Ahora **todos los flujos**
(facturación por lote manual, caja, abonos, misceláneos, bancos, NC/ND y el WS bancario) generan
partidas analíticas conforme ERSAPS desde **una sola configuración por empresa**, posteando
exclusivamente por el **motor único** (`sp_con_postear_poliza` — D1, intocable).

---

## 2. Fase por fase — qué hay y dónde

Scripts SQL (todos en `Database/ddl_v3/`, idempotentes, **este es el orden de aplicación**):

```
20260702_ci_fase1_integracion_config.sql
20260703_ci_fase2_asientos_config.sql
20260703_ci_fase3_lote_facturacion.sql
20260703_ci_fase4_captacion_config.sql
20260704_ci_fase5_ncnd_posteo_config.sql
20260704_ci_fase6_saldos_oficiales.sql
20260704_ci_fase7_periodo_cierre.sql
20260704_ci_fase8_ws_bancario.sql
```

### F1 — Modelo de configuración (PR #1 vía F2, base)
- **Tablas**: `con_integracion_config` (cabecera por empresa: modos de granularidad
  GENERAL / POR_SERVICIO / POR_SERVICIO_CATEGORIA para ventas y CxC, `encolar_sin_periodo`,
  flags `activo_*` por módulo), `con_integracion_cuenta` (matriz uso × servicio × categoría ×
  medición → `account_id`; unicidad tenant-safe por índice de expresiones con comodines NULL),
  `con_partida_pendiente` (cola de regularización, estados 1=PENDIENTE 2=PROCESADA 3=DESCARTADA).
- **Funciones**: `fn_con_resolver_cuenta` (fallback "lo más específico gana" → servicio → general;
  RAISE si no resuelve), `fn_con_cuenta_posteable`, `sp_con_aplicar_perfil_integracion(company,'ERSAPS')`
  — auto-llenado por convención de códigos del plan regulatorio: ingreso `5.1.XXX` ↔ CxC abonados
  `11301010XXX` ↔ previsión `11303010XXX`. Resuelve SIEMPRE por `con_plan_cuentas.code`
  (los `adm_servicio.cont_account_id` de prod están HUÉRFANOS — ver §6 pendientes).
- Usos de cuenta: CXC, INGRESO, CAJA, BANCO_DEFAULT, ISV, DESCUENTO, RECARGO_MORA,
  PREVISION_INCOBRABLE, GASTO_INCOBRABLE, RESULTADO_EJERCICIO, RESULTADO_ACUMULADO,
  DEVOLUCION_NC (opcional — sin fila = la NC espeja la factura), TRANSITORIA.

### F2 — Pantalla de configuración (PR #1)
- Página `apc.Client/Pages/Contabilidad/Empresas/IntegracionContable.razor`
  (`/contabilidad/empresas/integracion`, menú Contabilidad → "Integración Contable").
  Pestañas: Cuentas generales / Ventas-CxC (matriz inline + botón "Aplicar perfil ERSAPS") /
  Notas C/D / Asientos (diario + tipo de partida + activación por módulo).
- **Tabla** `con_integracion_asiento` (empresa × módulo → `journal_id`, `type_id`; FKs compuestas
  tenant-safe). **Vocabulario de módulos** (constantes `IntegracionContableModulos` en SIAD.Core,
  fuente única de los CHECK): `VENTAS, CAJA, BANCOS, NOTAS, MISCELANEOS, PROV`.
- Backend: `IIntegracionContableService`, controller `api/contabilidad/integracion`, cliente HTTP,
  permisos en `PermissionEndpointCatalog` (sección Contabilidad, recurso `integracion`).
- La pantalla opera sobre la **empresa actual** (`TenantState.EnsureCompanyAsync()`); el controller
  solo acepta la empresa de los claims.

### F3 — Lote manual de partidas de facturación (PR #3)
- **Snapshot dimensional en factura**: columnas `factura.categoria_servicio_id` y `con_medicion`
  + backfill + trigger `trg_factura_snapshot_dimensional` BEFORE INSERT (desvío documentado del
  plan: mismo efecto que tocar `sp_lectura_v3`, que sigue INTACTO y sin postear — D5).
- **SP** `sp_con_generar_partidas_facturacion(company, desde, hasta, DIA|PERIODO)`: facturas de
  lectura sin partida (excluye anuladas) → Debe CxC / Haber Ingresos analíticos → postea por el
  motor. Idempotente vía puente `con_partida_factura` (`poliza_id NULL` = procesada sin efecto).
  Sin período abierto: encola (dedup, `intentos++`) o rechaza según config. Pendientes se
  resuelven por **cobertura** del rango de su payload.
- **Compartidos que las demás fases reusan**: `fn_con_siguiente_poliza` (numeración del motor:
  `empresa-YYYY-MM-corr` con `pg_advisory_xact_lock`), `fn_con_periodo_abierto`,
  `fn_con_resolver_cuenta_modo`, `fn_con_candidatas/lineas_lote_facturacion` (fuente única
  preview = lote), historial `con_lote_facturacion`.
- Página `PartidasFacturacion.razor` (`/contabilidad/partidas-facturacion`): preview → confirmación
  → generar → historial → reproceso de pendientes. Permiso propio `module.contabilidad.lotefacturacion.*`.

### F4 — Captación / abonos / misceláneos por config (PR #4)
- `CaptacionPagosService`, `AbonoService`, `FacturacionMiscelaneosService` y `BanTransaccionesService`
  resuelven cuentas vía la config (adiós plantillas/reglas — que NO se droppearon, solo dejaron de
  consultarse). Nuevos `sp_con_generar_comprobante_config` / `sp_con_revertir_comprobante_config`
  (comprobante armado desde la config, sin plantilla). Contrapartida bancaria: `BANCO_DEFAULT`
  como fallback; la cuenta por `ban_cuenta` se mantiene.
- Los flags `activo_*` de `con_integracion_config` controlan cada flujo; pantalla `CuentasAbonos`
  absorbida por la de Integración Contable.

### F5 — NC/ND con posteo analítico (PR #5)
- Script que **SUPERSEDE** al viejo `20260702_nc_nd_posteo_contable.sql` (versión plantilla,
  revertida en prod — ya no se necesita aplicar). Los SPs de emisión arman la **partida espejo**
  de la factura origen (`fn_con_lineas_nota`, prorrateo por restos mayores, `DEVOLUCION_NC` por
  línea si está configurada) en la MISMA transacción. `poliza_id` poblado en
  `adm_nota_credito/debito`. Respeta `activo_notas` y `encolar_sin_periodo`.

### F6 — Saldos oficiales + balance de comprobación (PR #6)
- `sp_con_reconstruir_saldo_cuenta` (reconstrucción total de `con_saldo_cuenta` desde
  `con_partida_dtl` — corrige la inconsistencia acumulada por las remigraciones SIMAFI),
  `fn_con_saldo_libro` (fuente única de cálculo) y `fn_con_verificar_saldo_cuenta` (reconciliación
  caché vs libro). Balance de comprobación **híbrido**: períodos cerrados leen del caché, abiertos
  calculan en vivo. Controller `SaldosContablesController`.
- **Regla operativa**: cualquier remigración SIMAFI futura debe terminar con la reconstrucción.

### F7 — Períodos comercial/contable + cierres (PR #8) — la fase más grande
- **Reemplaza a `historialmes`** como fuente del mes comercial: tablas `adm_periodo_comercial` +
  `adm_periodo_comercial_ciclo` (multitenant, estados numéricos), `fn_adm_periodo_comercial_actual`,
  `sp_adm_periodo_comercial_abrir/cerrar`, checklist (`fn_adm_periodo_comercial_checklist`).
  **Trigger espejo** mantiene `historialmes` sincronizado durante la transición (los consumidores
  legacy siguen funcionando; retiro físico solo tras ≥1 mes estable). Migración de datos incluida.
- Repunte de consumidores: `sp_adm_calcular_factura_lectura` y el SP de GetCiclo del WS de
  lectores (SOLO SQL — la app Android y el WS NO cambiaron, D9) + servicios C#.
- **Cierre contable** con checklist (`fn_con_checklist_cierre_periodo`, `sp_con_periodo_precerrar/
  cerrar/reabrir`): facturas con partida, sin borradores, caja posteada, cola vacía.
- **Avisos en el layout**: `AvisosPeriodosBanner.razor` + `PeriodosAvisosController` (mes vencido
  sin cerrar, facturas sin partida, período inexistente, desfase comercial-contable configurable).
- Página `PeriodosComerciales.razor`. `LecturaV3Tests` (motor tarifario) quedó en verde sin
  cambios de comportamiento — era el riesgo mayor de la fase.

### F8 — WS bancario SIMAFI (PR #9)
- **Proyecto nuevo `apc.BancosWs`** (ASP.NET Core net9, SIN Identity/Blazor): servicio separado
  para deploy y ciclo de vida independientes del portal. Controllers del contrato CONGELADO
  (D8 — el banco ya consume este contrato en prod contra el WS SIMAFI viejo): genkey, consulta,
  pago, reversión y **heartbeat**, XML byte-compatible. Auth por `banco`+`key` contra
  `ban_ws_credencial` (middleware propio + `BancosWsCurrentCompanyService` resuelve el tenant
  desde la credencial).
- SQL: `ban_ws_credencial`, `ban_ws_pago` (idempotencia por referencia), `fn_ban_ws_cliente/
  pendientes`, `sp_ban_ws_pagar` (FIFO, parcial = abono, `ban_kardex` + partida por config),
  `sp_ban_ws_reversar`.
- **El contrato extraído está documentado en [docs/f8-contrato-ws-bancario.md](f8-contrato-ws-bancario.md)**
  (fuente: código Java del WS viejo en `E:\Koala\proyectos\SIMAFI_WS` + colecciones Postman).
  Golden files del contrato en `SIAD.Tests/GoldenFiles/BancosWs/`.
- **Plan de cutover** (documento, NO ejecutado): [docs/f8-plan-cutover-ws-bancario.md](f8-plan-cutover-ws-bancario.md).
  ⚠️ Restricción de negocio: mientras SIMAFI siga facturando, el WS nuevo no puede responder con la
  cartera de SIAD — el cutover exige que la cartera viva ya esté en SIAD.

### Transversales (fuera del plan, salieron de los reviews)
- **Migración de alertas** (PR #2): `DxAlert` NUNCA existió en DevExpress Blazor — 194 usos en 93
  páginas se migraron: feedback transitorio → `DxToast` (`DxToastProvider` global en
  `MainLayout.razor` + `IToastNotificationService`), avisos permanentes → `<div class="alert alert-*">`.
  Para páginas nuevas: ese es el patrón.
- **`ICompanyAccessValidator`** (PR #7): unifica el chequeo tenant de los controllers (había 6
  copias, una débil en bancos). Usalo — no copies el método viejo.
- **DevExpress es 25.2.4** (los CLAUDE.md decían 25.1.7). MCP de documentación DevExpress
  configurado en `.mcp.json` (`dxdocs`) — consultalo antes de usar cualquier API DX.

---

## 3. Cómo montar el entorno local

1. **Restaurar la BD de pruebas** (PostgreSQL local, usuario `postgres`):
   ```powershell
   # backup fresco de prod incluido en el repo de trabajo:
   # Database/Backups/siad_v3_prod_20260702.backup
   .\Database\restore_bd.ps1   # o pg_restore manual a una BD "siad_v3_test"
   ```
2. **Aplicar los 8 scripts** de `Database/ddl_v3/` en el orden de la lista del §2
   (`psql -v ON_ERROR_STOP=1 -f <script>` con cada uno). El viejo
   `20260702_nc_nd_posteo_contable.sql` ya NO se aplica (superseded por F5).
3. **Correr los tests** (135 en total; cada uno en BEGIN…ROLLBACK, la BD queda limpia):
   ```powershell
   $env:SIAD_TEST_DB = 'Host=localhost;Database=siad_v3_test;Username=postgres;Password=<pass>'
   dotnet test SIAD.Tests/SIAD.Tests.csproj
   ```
4. **Correr el portal**: `dotnet run --project apc/apc.csproj` (apunta la conexión de
   `apc/appsettings.Development.json` a tu BD local). Usuario de demo del seed:
   `admin@siad-demo.com` / `Admin123@` (SuperAdministrador).
5. **Correr el WS bancario**: `dotnet run --project apc.BancosWs/apc.BancosWs.csproj`
   (servicio aparte; ver su `appsettings.Development.json`).
6. Para probar el flujo contable completo: pantalla Integración Contable → "Aplicar perfil ERSAPS"
   → pestaña Asientos (diario + tipo por módulo, activar flags) → Partidas de facturación → preview
   → generar.

## 4. Cómo verificar que todo está sano

- `dotnet build HODSOFT_DEVEXPRESS.sln` → 0 errores.
- Suite completa → **133 passed / 2 skipped** (los 2 skips son por datos del backup: no hay config
  de mora activa, y un test cross-tenant que requiere multiempresa).
- Los tests cubren: resolución/fallback/perfil (F1), asientos tenant-safe (F2), lote balanceado/
  idempotente/encolado (F3), captación por config (F4), NC/ND espejo (F5), reconstrucción/
  reconciliación de saldos (F6), períodos y cierres + regresión del tarifario (F7), y golden files
  + pagos FIFO/idempotentes del WS (F8).

## 5. Reglas que NO se negocian al seguir trabajando

1. **Solo el motor único escribe `con_saldo_cuenta`** (`sp_con_postear_poliza` / `sp_con_revertir_poliza`).
2. **Multitenancy**: `company_id` en toda tabla funcional, tenant por claims/`ICurrentCompanyService`
   (en BancosWs, por credencial), FKs compuestas tenant-safe, nunca confiar en un companyId del request.
3. **Cambios de BD** = scripts timestamped idempotentes en `Database/ddl_v3/` (no EF migrations).
4. **Estados numéricos** — no agregar estados en letras.
5. **Contrato del WS bancario CONGELADO** — cualquier cambio de respuesta rompe al banco.
6. **No revivir** `con_regla_integracion` ni `con_plantilla_partida_*` (deprecadas; se retiran
   físicamente tras ≥1 mes estable post-deploy, junto con `historialmes`).
7. **Nada se aplica a producción (172.16.0.9) fuera de la ventana de deploy acordada.**

## 6. Pendientes conocidos (para la ventana de deploy única F1–F8)

- Aplicar los 8 scripts en orden + publish del portal Y de `apc.BancosWs`.
- **Datos en prod**: 12 `adm_servicio.cont_account_id` huérfanos (re-correr el backfill del script
  día-10 tras limpiar ids); clientes sin categoría (2–3); cargar las llaves reales del banco en
  `ban_ws_credencial`.
- **Config en prod** (vía pantalla, no SQL): perfil ERSAPS + diarios/tipos por módulo + activar
  flags módulo por módulo.
- **Decisión con el contador**: partida retroactiva de las 25 facturas piloto vs corte desde julio.
- **Cutover del banco**: ventana aparte, posterior; ver docs/f8-plan-cutover-ws-bancario.md y la
  restricción de cartera del §2-F8.
- Sincronización con el repo padre de la org (decidir estrategia con el equipo).

## 7. Referencias

- Plan maestro: `docs/plans/2026-07-02-plan-integracion-contable-comercial.md`
- Contrato WS: `docs/f8-contrato-ws-bancario.md` · Cutover: `docs/f8-plan-cutover-ws-bancario.md`
- WS SIMAFI viejo (fuente Java + Postman): `E:\Koala\proyectos\SIMAFI_WS` (solo lectura)
- Manual regulatorio: `docs/regulatorio/manual_contabilidad_regulatoria.pdf.txt`
- PRs #1–#9 en `github.com/sjsmurciakoala/Prestadoras` — cada uno documenta su fase y su review.
- Backup para desarrollo: `Database/backup_completo.ps1` (ver ese script para generar/enviar la BD).
