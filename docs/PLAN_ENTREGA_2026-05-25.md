# Plan de entrega — Deadline 2026-05-25

**Hoy:** 2026-05-14 (Sprint 3 día 2: bugs colaterales cerrados, motor único pendiente)
**Deadline:** 2026-05-25 (11 días corridos)
**Objetivo:** dejar el sistema APC completo, **legalmente emitible al cliente** (CAI conforme SAR Acuerdo 481-2017), listo para operación real.

---

## 🔄 Actualización 2026-05-05 — Sprint 1 cerrado + replanteo regulatorio

### Sprint 1 ejecutado (Fases A→I)

✅ **Corte legacy completo en los 3 repos**:
- Fases A (sp_medidores_por_ruta_ws), B (12 endpoints WS), C+D (entidades EF + 21 controllers/services + 24 vistas Blazor), E (csproj), F (app Android), G (mover SQL legacy), H (pestaña Configuración Tarifas), I (vistas Servicios/Conceptos/MiApp Config + clientes huérfanos + combo Letras + sidebar correcto).
- 0 errors compilando los 3 binarios.
- Pendiente operativo: deploy a PROD, anular factura duplicada, decisión sobre `ReglasIntegracion` (resuelta: se quita).

### Replanteo Sprint 2 + Sprint 3 con regulatorio

Tras revisar **manual contabilidad ERSAPS** y **Plan de Arbitrios PC 2026** + validar SAR (Acuerdo 481-2017 + reformas), se identificaron **5 gaps rojos bloqueantes legales** y otros 10 importantes (ver §Gaps SAR/ERSAPS).

**Decisión 2026-05-05:**
- Entrega 25-may **debe** ser legalmente emitible (SAR completo).
- Reportería ERSAPS por categoría queda **post-25** (no bloquea).
- Estados numéricos pasan a **Sprint 2** (más temprano = menos riesgo).
- Motor unificado de facturación (lectura + miscelaneos) entra en **Sprint 3**.
- `adm_*` para ciclos/medidor/ruta y otros refactors estructurales → **post-25**.
- Botón generar partidas contables + admin reglas → **post-25** (decisión diferida).

---

## 🚨 Estado al iniciar el plan (2026-04-28)

### Lo que está roto AHORA mismo
1. **`sp_medidores_por_ruta_ws`** — JOIN-ea tablas dropeadas (`servicios_roles_ws`, `configuracion_tasas*`). El app no puede descargar rutas hasta reescribirlo.
2. **Portal Blazor no compila** — entidades EF mapean tablas que ya no existen.
3. **9 endpoints del WS** devuelven HTTP 500 (`/Tarifa`, `/TarifaContador`, `/GetCobrosAdicionales(V2)`, `/CondicionesLectura`, `/Informativos`, `/ActualizarLecturaV2`, `/GetCAI`, `/ServiciosApp`).
4. **4 páginas del portal crashean** (`/tarifas-base`, `/tarifas-contador`, `/letras`, `/mi-app/configuracion`).

### Lo que está bien
- Stack V3 desplegado en PROD (15 tablas `adm_*`, 10 SP V3).
- Datos de prueba: 25 clientes en 5 rutas (`01001..01005`), 5 lectores configurados, 5 bloques CAI.
- E2E validado: 2 facturas reales creadas vía app el 2026-04-26.

---

## 📋 Documentos de referencia (leer primero al retomar)

1. **[AUDITORIA_CORTE_LEGACY_2026-04-28.md](AUDITORIA_CORTE_LEGACY_2026-04-28.md)** — inventario completo de archivos rotos por tabla dropeada.
2. **[PLAN_PRUEBAS_2026-04-27.md](PLAN_PRUEBAS_2026-04-27.md)** — guion de pruebas multi-usuario con asignación rutas/lectores/clientes.
3. **[task_corte_legacy.md](task_corte_legacy.md)** — alcance original del corte.
4. **[task_reglas_pendientes.md](task_reglas_pendientes.md)** — reglas de motor pendientes (tercera edad, exoneraciones, anulación).
5. **[task_cai.md](task_cai.md)** — flujo CAI/correlativo offline.
6. **[ESTADO_CORTE_LECTURA_V3_2026-04-17.md](ESTADO_CORTE_LECTURA_V3_2026-04-17.md)** — estado consolidado por componente.

---

## 🗓️ Cronograma sugerido (3 sprints de ~9 días cada uno)

### Sprint 1 — Desbloqueo + corte legacy (2026-04-28 → 2026-05-06)

**Objetivo:** que el sistema vuelva a compilar y operar limpio sin legacy.

| Día | Tarea | Esfuerzo |
|---|---|---|
| 1 | **Fase A** — Reescribir `sp_medidores_por_ruta_ws` sin JOINs legacy + aplicar PROD | 1 h |
| 1-2 | **Fase B** — Eliminar 9 endpoints WCF del WS + recompilar `WS_APC.dll` + redeploy IIS | 3 h |
| 2-3 | **Fase C** — Borrar Controllers (`TarifasBaseController`, `TarifasContadorController`, `LetrasController`), Services y entidades EF (`tarifas_catalogo`, `tarifas_contador`, `configuracion_tasa`, `configuracion_tasas_detalle`, `servicios_roles_ws`, `condicion_lectura`, `informativo`, `configuracion_app_lectura_medidore`, `letra`) | 4 h |
| 3-4 | **Fase D** — Borrar páginas Blazor (`/tarifas-base`, `/tarifas-contador`, `/letras`, `/mi-app/configuracion`) + sidebar items + clientes HTTP | 3 h |
| 4-5 | **Fase E** — Regenerar `SiadDbContextModelSnapshot.cs` + crear migración `CleanupLegacyTables` | 2 h |
| 5-6 | **Fase F** — Limpiar `Utilidades.java`, `UtilidadesBD.java`, tablas SQLite locales del app + recompilar APK + reinstalar | 3 h |
| 6 | **Fase G** — Mover scripts SQL históricos a `Database/historico_legacy/` + drop `fn_generar_codigo_cliente()` | 1 h |
| 7-8 | **Smoke E2E** completo con los 5 lectores en sus 5 rutas + verificar creación de facturas | 4 h |
| 9 | Buffer / contingencias / commit de Sprint 1 | — |

**Salida del Sprint 1:** sistema sin legacy, compila limpio, lectores facturando.

---

### Sprint 2 — Estados numéricos + saldo único + SAR mínimo + multi-tenancy (2026-05-07 → 2026-05-15) [REPLANTEADO 2026-05-05 + multi-tenancy 2026-05-07]

**Objetivo:** dejar las decisiones arquitectónicas tomadas (estados, saldo, SAR mínimo, multi-empresa real en facturación legacy) antes de tocar UI.

| Día | Tarea | Estado |
|---|---|:--:|
| 1 (anticipado 05-may) | **Investigación saldo cliente** — fuente única: `transaccion_abonado.saldo_detalle`. Reporte: [REPORTE_SALDO_CLIENTE_2026-05-05.md](REPORTE_SALDO_CLIENTE_2026-05-05.md) | ✅ |
| 2 (anticipado 06-may) | **Diseño SAR-compliant v2** — [PLAN_SAR_COMPLIANCE_2026-05-06.md](PLAN_SAR_COMPLIANCE_2026-05-06.md) con feedback multi-empresa aplicado | ✅ |
| 2-3 (07-may) | **DDL SAR fase 1+2+3 aplicado en PROD**: catálogos (cfg_tipo_documento_fiscal, cfg_motivo_anulacion, cfg_estado_documento_fiscal) + adm_establecimiento + ALTER adm_cai_facturacion + ALTER factura + fn_adm_validar_cai_emitible | ✅ |
| 3 (07-may) | **Multi-tenancy en 5 tablas legacy** (decisión "A" usuario 07-may): factura, factura_detalle, transaccion_abonado, historicomedicion, maestro_medidor con `company_id NOT NULL` + FK + backfill. Simplificar adm_establecimiento (DROP rtn_emisor + razon_social, viven en cfg_company). FK compuesta factura → adm_establecimiento. | ✅ |
| 3 (07-may) | **Bug `sp_obtener_cliente_saldo`** + ajuste `sp_lectura_v3` con `company_id` en INSERTs + `sp_adm_calcular_factura_lectura` con filtro `company_id` en historicomedicion (2 queries) | ✅ |
| 4 (07-may) | **Scaffold EF** de 5 tablas alteradas + 5 entidades nuevas + `SiadDbContext.SarCompliance.cs` (DbSets + ConfigureSarComplianceModel con HasKey, ToTable, FK compuesta). DEFAULT 1 en `factura.tipo_documento_fiscal_id` y `adm_cai_facturacion.tipo_documento_fiscal_id`. | ✅ |
| 4 (07-may) | **13 INSERTs vivos via EF patcheados con company_id**: AuxiliarLecturaService (2), MedidoresService (1), NotasCreditoDebitoService (1), CobranzaService (3), CaptacionPagosService (3), FacturacionMiscelaneosService (3). Inyección de `ICurrentCompanyService` donde faltaba. | ✅ |
| 4 (07-may) | **Vista para administrar adm_establecimiento**: slice completo (DTO + Service + Controller + Cliente HTTP + Razor + DI + sidebar entry) en `/establecimientos` | ✅ |
| 5-6 (07-may) | **Migración estados string → numérico (CONVIVENCIA)**: 6 catálogos `cfg_estado_*` aplicados; ALTER 7 tablas con `*_id smallint NOT NULL` + backfill; `sp_lectura_v3` escribe `estado` + `estado_id` en paralelo; **6 triggers BD** para auto-sincronizar `*_id` cuando C# escribe la columna string. Constantes C# tipadas en `SIAD.Core/Constants/EstadosNumericos.cs`. **Migración del código C#/Java a leer `*_id` queda diferida a post-25** (las strings siguen vivas, sin riesgo). | ✅ |
| 7 (08-may) | **CAI offline UI completa** — combo Estado, badges clickables por estado, ColumnChooser nativo DevExpress, search con Enter, filtros por estado, prefijo `EEE-PPP-TD-NNNNNNNN` preview, leyenda fiscal SAR de 3 líneas autogenerada, anular/reactivar rápido en grid, **Vencida read-only** (UI + service), validación backend para extender fecha al editar Vencida. | ✅ |
| 7 (08-may) | **CAI estado_id lookup numérico** — `cfg_cai_estado` (5 estados: DISPONIBLE/EN_USO/VIGENTE/VENCIDA/ANULADA) + `adm_cai_facturacion.estado_id smallint` + backfill + reglas de defensa profunda en SELECT y write (ANULADA gana, fecha vencida fuerza VENCIDA). Endpoint `PATCH /cais/{id}/estado` para toggle rápido. | ✅ |
| 7 (08-may) | **Decisión arquitectónica 2026-05-08: mono-sucursal** — el sistema es multi-empresa pero **cada empresa tiene una sola sucursal**. El "establecimiento" SAR es solo el código `EEE` (texto libre 3 dígitos en `adm_cai_facturacion.establecimiento_codigo`). Eliminado: tabla `adm_establecimiento`, columna `adm_cai_facturacion.establecimiento_id`, página `/establecimientos`, controller, service, DTO, entity, HTTP client, DI, item del menú, FK + índice + DbSet en EF. | ✅ |
| 8 (09-may) | **Snapshot V3 con saldo previo dinámico + multi-empresa** — `sp_obtener_cliente_saldo` y `sp_obtener_cliente_saldo_servicio_detalle` overload con `p_company_id`. `sp_adm_generar_snapshot_offline_cliente_lectura` sube a `OFFLINE_SNAPSHOT_V3_2`: incluye `saldo_anterior_total` y `saldos_por_servicio` (jsonb array dinámico). Smoke validado contra cliente real (102820, saldo L. 331.11 con desglose 4 servicios). | ✅ |
| 8 (09-may) | **App Android tabla SQLite dinámica** — `MedidorSaldoServicio(IdMedidor, ServicioCodigo, ServicioNombre, SaldoAnterior)` con PK compuesta. `VersionBD` 11→12 (auto-migración). `DescargarOfflineSnapshotV3ABD` puebla la tabla al guardar cada snapshot. `GetCobrosAtrasadosPorMedidor` lee dinámico (N items, no 12 hardcoded). Campos legacy `SaldoAnteriorAgua/Alcantarilla/etc.` quedan deprecated (limpieza post-25). APK debug compilado OK. | ✅ |
| 9 (09-may) | **Borrar `ReglasIntegracion.razor`** + endpoints (`GET/POST/DELETE /reglas-integracion` + `GET /documentos`) + service + DTOs. `con_regla_integracion` queda en BD huérfana (drop post-25). | ✅ |
| 9 (09-may) | **Actualizar docs**: `PLAN_ENTREGA` (este), `PLAN_SAR_COMPLIANCE`, `INVENTARIO_GAPS` con cierre Sprint 2. | ✅ |

**Avance Sprint 2 al 2026-05-09 (cierre)**: **100%** ✅
**Salida real del Sprint 2:** estados numéricos en producción + saldo con fuente única (multi-empresa) + snapshot V3_2 con saldos dinámicos + APK con tabla MedidorSaldoServicio dinámica + factura con datos SAR mínimos + sin página ReglasIntegracion + multi-empresa real en facturación legacy + **CAI completamente funcional** (estado lookup numérico, leyenda fiscal SAR, UI rica, mono-sucursal).

#### Archivos SQL aplicados en PROD (Sprint 2 días 2-4)

| Archivo | Aplicado |
|---------|----------|
| `Database/ddl_v3/20260507_sar_compliance_01_catalogos.sql` | ✅ 07-may |
| `Database/ddl_v3/20260507_sar_compliance_02_alter_cai_factura.sql` | ✅ 07-may |
| `Database/ddl_v3/20260507_sar_compliance_03_fn_validar_cai.sql` | ✅ 07-may |
| `Database/ddl_v3/20260507_sar_compliance_04_multitenancy_facturacion.sql` | ✅ 07-may |
| `Database/ddl_v3/20260507_sar_compliance_05_fix_sp_obtener_cliente_saldo.sql` | ✅ 07-may |
| `Database/ddl_v3/20260507_sar_compliance_06_sp_lectura_v3_company_id.sql` | ✅ 07-may |
| `Database/ddl_v3/20260507_sar_compliance_07_sp_calcular_factura_company_id.sql` | ✅ 07-may |
| ALTER manual: DEFAULT 1 en `factura.tipo_documento_fiscal_id` y `adm_cai_facturacion.tipo_documento_fiscal_id` | ✅ 07-may |
| INSERT manual: 2 establecimientos con RTN placeholder `00000000000000` | ✅ 07-may |
| `Database/ddl_v3/20260508_estados_numericos_01_lookups_y_backfill.sql` | ✅ 07-may |
| `Database/ddl_v3/20260508_estados_numericos_02_sp_lectura_v3_estado_id.sql` | ✅ 07-may |
| `Database/ddl_v3/20260508_estados_numericos_03_triggers_sync.sql` | ✅ 07-may |
| `Database/ddl_v3/20260508_cai_punto_emision.sql` | ✅ 09-may |
| `Database/ddl_v3/20260508_cai_estado_lookup.sql` | ✅ 09-may |
| `Database/ddl_v3/20260508_drop_adm_establecimiento.sql` | ✅ 09-may |
| `Database/ddl_v3/20260509_sp_obtener_cliente_saldo_company.sql` | ✅ 09-may |
| `Database/ddl_v3/20260509_snapshot_v3_saldo_previo.sql` | ✅ 09-may |

---

### Sprint 3 — Motor unificado facturación + NC/ND V3 + bugs UI + reglas (2026-05-14 → 2026-05-25) [REPLANTEADO 2026-05-14]

**Objetivo:** completar gaps SAR (NC/ND), unificar facturación lectura+miscelaneos, cerrar UI/reglas, tests.

#### Adelantos cerrados día 1-2 (14-may, jornada larga)

Validación E2E demo Azure trajo 5 bugs colaterales del motor V3 + 1 mejora de validación CAI. Todos cerrados y aplicados sobre `siad_v3` Azure PG + portal republicado. Detalle completo en [BUGS_MOTOR_FACTURACION_2026-05-13.md](BUGS_MOTOR_FACTURACION_2026-05-13.md).

| # | Bug | Fix |
|---|---|---|
| 1 | App calcula 12404.48 en lugar de 331.11 | UPDATE regla 29 `porcentaje 60.00 → 0.60` |
| 2 | Server omite `PORCENTAJE_SERVICIO` (calcula 209.16) | `20260514_fix_motor_facturacion_v3.sql` |
| 3 | `correlativo_actual` del CAI no avanza tras emitir | `20260514_bug3_avance_correlativo_cai.sql` + `_FIX_BACKFILL.sql` (UPDATE en `sp_adm_confirmar_correlativo_cai_sync` con `GREATEST`) |
| 4 | Estado de cuenta saldo del último mov, no total | `ClientesServices.cs:479` → SP `sp_obtener_cliente_saldo(company_id, clave)` |
| 4b | Grid de movimientos no muestra `numfactura` | DTO + EF subquery + 2 columnas en `ClienteEstadoCuentaTab.razor` |
| 5 | Captación en Caja "No se encontraron saldos" (quick fix) | `20260514_bug5_fix_fn_getclientesaldos_posteomanual.sql` + `MapTipoServicio` agrega `AGUA_POTABLE → AGUA` |
| 6 | App no incluye saldo previo en total | Ya estaba fixed por snapshot V3_2 (09-may). Pendiente validar E2E. |
| 7 | Descarga de ruta re-trae clientes ya facturados | `20260514_bug7_sp_medidores_por_ruta_ws_excluir_facturados.sql` (5to param con default `true`) |
| — | Validación CAI: tabla maestra `cfg_estado_cai` + filtros completos en SP de selección | `20260514_validacion_cai_seleccion.sql` |

**Resultado al cierre del día 2:** demo Azure E2E funcionando con 4 facturas emitidas (Jessel + 3 más). Captación + Misceláneos quedan marcadas como **reforma completa post-25** (decisión del usuario 14-may) — el fix #5 es band-aid, no solución final.

#### Tabla original del Sprint 3 (replanificada)

| Día | Tarea | Esfuerzo |
|---|---|---|
| 1-2 | ✅ Bugs colaterales motor V3 + validación CAI (ver bloque arriba) | 2 días |
| 3-5 | ✅ **Modelo NC/ND V3** — completo. Tablas `adm_nota_credito/_detalle`, `adm_nota_debito/_detalle`, `cfg_motivo_aumento`; SPs `sp_adm_emitir_nota_credito` / `sp_adm_emitir_nota_debito` (con movimiento en `transaccion_abonado`); entidades EF; service/controller/client refactorizados de legacy a V3; UI `/facturacion/notas` (emisión + listado carga remota + filtros) y `/facturacion/notas/motivos` (CRUD de motivos). Detalle en [BUGS_MOTOR_FACTURACION_2026-05-13.md](BUGS_MOTOR_FACTURACION_2026-05-13.md). | 3 días |
| 6-7 | ⏭️ **Refactor facturación miscelaneos** → **diferido**. Captación y Misceláneos pasan a reforma completa post-25 (decisión del usuario 14-may). El motor de cálculo ya es común a lecturas; misceláneos se reescribe en Sprint 4. | — |
| 8 | ✅ **Reglas Plan de Arbitrios** — **parcial cerrado**. (a) Descuento 25% tercera edad: ya estaba en el motor; se hizo explícito el `tope_mensual=300` (Art. 355). (b) Recargo por mora (Art. 130): nueva `cfg_recargo_mora` + cálculo de `v_recargos` en `sp_adm_calcular_factura_lectura` (estaba hardcodeado a 0). (c) Descuento 10% pago anticipado (Art. 153): **diferido** a la reforma de Captación post-25 — se aplica al momento del pago. Script: `20260514_dia8_reglas_arbitrios.sql`. | 1 día |
| 9 | ✅ **Bugs UI Sprint 2** — cerrado. (1) `usuarioapc`: combo Ciclo agregado (`codciclo` mapeado en entidad + DTO + service + form + list); Ruta ya era combo. (2) ClienteForm combo ruta con descripción: ya estaba. (3) ClientesList: columnas Ciclo y Ruta agregadas (DTO + query EF + grilla). (5) Portal Conflictos V3 con acciones reales: ya estaba (funcional y conectado). (4) **App Android "lecturas con problema": diferido post-25** — la app Android se va a migrar completa, no tiene sentido crear la pantalla sobre la base actual. | 1 día |
| 10 | ✅ **Mapeo `adm_servicio` → cuentas regulatorias 5.1.x** — cerrado. Seed `20260514_dia10_mapeo_cuentas_ersaps.sql` mapeó los 11 servicios de la empresa demo a su cuenta de ingreso (5.1.1 agua / 5.1.2 alcantarillado / 5.1.3 colaterales). `fn_adm_servicios_sin_cuenta_contable` para diagnóstico. **Enforcement**: `ServicioTarifarioV3Service.GuardarAsync` ahora exige cuenta contable obligatoria al crear/editar un servicio → combinado con el seed completo, garantiza que ningún servicio facturable quede sin cuenta. UI `MaestroServiciosV3` muestra badge "SIN CUENTA". Mapeo fino por categoría×condición = refinamiento post-25. | 4 h |
| 11 | **Tests automatizados**: idempotencia UUID, doble emisión, NC, anulación, recargo mora, descuento tercera edad. Vista prueba de cálculo. | 1 día |
| 12 | **Estabilización + buffer + handoff** | — |

**Salida del Sprint 3:** sistema legalmente emitible (CAI + RTN + leyendas + NC/ND), reglas regulatorias aplicadas (tercera edad + recargo mora), tests verdes. Captación + Misceláneos + descuento pago anticipado en reforma completa Sprint 4 post-25.

---

## 🔴 Gaps SAR/ERSAPS identificados 2026-05-05

Comparación del modelo V3 actual vs **SAR Acuerdo 481-2017 + reformas** y **manual contabilidad ERSAPS**.

### Bloqueantes legales (entran al 25-may sí o sí)

| # | Gap | Origen | Sprint |
|---|-----|--------|--------|
| 1 | **RTN emisor** ausente en `adm_cai_facturacion` y `factura` | SAR | Sprint 2 |
| 2 | **Leyendas CAI** (rango autorizado, fecha límite emisión) sin persistir | SAR | Sprint 2 |
| 3 | **Modelo NC/ND V3 inexistente** (sólo legacy) | SAR | Sprint 3 |
| 4 | NC sin **referencia a factura origen** (`factura_origen_id`) | SAR | Sprint 3 |
| 5 | NC sin **motivo estructurado** (`codigo_motivo_anulacion`) | SAR | Sprint 3 |

### Importantes (entran al 25 si hay tiempo, sino primer hot-fix post-entrega)

| # | Gap | Origen | Sprint |
|---|-----|--------|--------|
| 6 | ~~**Recargo mora** no automatizado (Plan Arbitrios L177)~~ ✅ **resuelto día 8** — `cfg_recargo_mora` + cálculo en `sp_adm_calcular_factura_lectura` | Municipio | Sprint 3 |
| 7 | ~~**Descuento 25% tercera edad** con tope L300 no implementado (L355)~~ ✅ **resuelto** — motor ya lo aplica, `tope_mensual=300` explícito día 8 | Municipio | Sprint 3 |
| 8 | ~~Mapeo `adm_servicio` → cuentas regulatorias 5.1.x **sin enforcement**~~ ✅ **resuelto día 10** — seed completo + cuenta obligatoria en alta/edición de servicio | ERSAPS L2387 | Sprint 3 |
| 10 | `tipo_documento_fiscal` ausente en factura | SAR | Sprint 2 |
| 11 | Misceláneos sin **modelo CAI propio** | SAR + arquitectura | Sprint 3 |

### Post-25 (no bloqueantes pero documentados)

| # | Gap | Origen |
|---|-----|--------|
| 9 | Reporte ERSAPS "Facturación y Cobranzas" por categoría | ERSAPS L1597 |
| 12 | `tipo_contribuyente` / `establecimiento_fiscal_SAR` en `cfg_company` | SAR |
| 13 | Tabla **auditoría fiscal** (cambios, cancelaciones) | SAR + buenas prácticas |
| 14 | **Constancia de solvencia** automatizada (Plan Arbitrios L716) | Municipio |
| 15 | Multas por no conexión alcantarillado (L1201) | Municipio |
| — | Botón "generar partidas contables de lecturas" (decisión diferida) | Arquitectura |
| — | Admin de reglas de partidas (decisión diferida) | Arquitectura |
| — | `adm_*` para `ciclos`/`medidor`/`ruta` (refactor estructural) | Convención |

---

---

## ✅ Criterios de aceptación al 2026-05-25

Para considerar el sistema entregado:

### 🔴 Cumplimiento legal SAR Acuerdo 481-2017 (BLOQUEANTE)
- [ ] **RTN emisor** persistido en `adm_cai_facturacion` + visible en cada factura emitida.
- [ ] **Leyendas CAI** completas: código CAI, rango autorizado, fecha límite emisión.
- [ ] **Modelo NC/ND V3** funcionando: `adm_nota_credito`, `adm_nota_debito` con `factura_origen_id` + `motivo_anulacion_id`.
- [ ] **`tipo_documento_fiscal`** (Factura/Recibo/NC/ND) explícito en cada documento emitido.
- [ ] CAI con correlativo único, no saltar correlativos, no usar CAI vencido (validación SP).
- [ ] **Anulación de factura** vía NC con justificación auditada.

### 🟡 Cumplimiento regulatorio ERSAPS + Plan Arbitrios PC 2026
- [ ] Cada servicio en `adm_servicio` mapeado a cuenta contable regulatoria (`5.1.1.x`/`5.1.2.x`/`5.1.3.x`) — enforcement antes de emitir.
- [ ] **Recargo mora** automático = tasa bancaria + 2% anual sobre saldos (Plan Arbitrios L177).
- [ ] **Descuento 25% tercera edad** con tope L300/mes y restricción a un inmueble por beneficiario (L355).
- [ ] **Descuento 10%** disponible para pago anticipado mensual (L317).

### Flujo operativo
- [ ] 5 lectores facturando en producción sin conflictos.
- [ ] Saldos previos se incluyen en factura impresa y servidor coincide con app.
- [ ] Anulación y reproceso de factura funcionan formalmente con trazabilidad.
- [ ] Portal permite supervisor resolver conflictos con acciones reales.
- [ ] **Saldo del cliente con fuente única** (`transaccion_abonado.saldo_detalle`) — sin doble cálculo.

### Datos y BD
- [ ] Cero tablas legacy en BD (`tarifas`, `tarifas_contador`, `cai`, `letras`, etc.) ✅ HECHO Sprint 1.
- [ ] `fn_generar_codigo_cliente` eliminada ✅ HECHO Sprint 1.
- [ ] **Estados numéricos** en `factura.estado_id`, `transaccion_abonado.estado_id`, `adm_cai_correlativo_emitido.estado_id`, etc. (Sprint 2).
- [ ] Catálogo `rutas` 100% en formato CC+LLL (5 dígitos) ✅ HECHO previo.
- [ ] Catálogos lookups (`cfg_estado_*`) poblados (Sprint 2).

### Backend
- [ ] WS sin endpoints legacy ✅ HECHO Sprint 1.
- [ ] `sp_medidores_por_ruta_ws` reescrito sin legacy ✅ HECHO Sprint 1.
- [ ] `sp_obtener_cliente_saldo` filtra por `estado='A'` (Sprint 2).
- [ ] **Motor único de facturación** `sp_adm_facturar` que sirve a lecturas y misceláneos (Sprint 3).
- [ ] Portal compila sin warnings de entidades dropeadas ✅ HECHO Sprint 1.

### Frontend
- [ ] Sidebar sin items legacy ✅ HECHO Sprint 1.
- [ ] Form de cliente con combos Ciclo + Ruta (no inputs libres) (Sprint 3).
- [ ] Vista mantenimiento `usuarioapc` con combos (Sprint 3).
- [ ] Lista de clientes con columnas Ciclo y Ruta (Sprint 3).
- [ ] Vista de conflictos con acciones reales (Sprint 3).
- [ ] **UI para emitir Nota de Crédito** (anular factura) con motivo SAR (Sprint 3).

### App Android
- [ ] APK sin tablas SQLite ni endpoints legacy ✅ HECHO Sprint 1.
- [ ] Vista "Lecturas con problema" con botones de reintento/reproceso (Sprint 3).
- [ ] Saldo previo del snapshot reflejado en factura impresa (Sprint 2).
- [ ] Factura impresa con leyendas SAR completas (Sprint 2).

### Calidad
- [ ] Tests automatizados: idempotencia UUID, doble emisión, NC, anulación, recargo mora, descuento tercera edad (Sprint 3).
- [ ] Cero conflictos pendientes en `adm_lectura_v3_conflicto_sync` durante 1 día de operación real.

### 🟢 Diferido a post-25 (no bloquea entrega, queda diseñado)
- Reporte ERSAPS "Facturación y Cobranzas" por categoría (gap #9).
- `tipo_contribuyente` + `establecimiento_fiscal_SAR` en `cfg_company` (gap #12).
- Auditoría fiscal completa (gap #13).
- Constancia de solvencia automatizada (gap #14).
- Multas alcantarillado automatizadas (gap #15).
- Botón "generar partidas contables de lecturas" (decisión diferida).
- Administración de reglas de partidas (decisión diferida).
- Refactor `adm_*` para ciclos/medidor/ruta y otras tablas comerciales.

---

## 🚦 Riesgos identificados

| Riesgo | Mitigación |
|---|---|
| **Sprint 3 (estados numéricos) sale más largo de lo previsto** porque toca BD + backend + app + portal | Si se complica, posponer a post-entrega. La operación funciona con strings. Lo importante es no bloquear la entrega del Sprint 1+2. |
| **Lectores reales en campo durante Sprint 1** (mientras se rompen endpoints legacy) | Coordinar ventana de mantenimiento de 1 día para Fase A+B+F (reescritura SP + WS + APK). Avisar a los 5 lectores. |
| **Migración de catálogos legacy con clientes en producción real** | Hacer DRY-RUN antes de cualquier UPDATE/DROP. Hoy solo hay 25 clientes de prueba — antes del 25-may el cliente real puede haber cargado más. |
| **Cambios visuales con MCP DevExpress** dependen de docs disponibles online | Tener fallbacks; si MCP no carga, usar code search en repo. |

---

## 📞 Cómo retomar al iniciar la próxima sesión

1. **Leer este documento** y confirmar el día/sprint en que estamos.
2. **Verificar conexión a PROD:**
   ```bash
   PGPASSWORD=root psql -h 172.16.0.9 -p 5432 -U postgres -d siad_v3 -c "SELECT 1"
   ```
3. **Confirmar estado de pruebas pasadas:** ¿hubo facturas creadas adicionales? ¿conflictos? ¿errores reportados por lectores?
4. **Continuar por la fase pendiente** según cronograma de arriba.
5. **Actualizar este documento** al cierre de cada sprint con lo realmente hecho vs lo planeado.

---

## 📚 Memoria persistente para Claude

El agente tiene memoria en:
```
C:\Users\DELL\.claude\projects\e--Koala-proyectos-HODSOFT-DEVEXPRESS\memory\
```

Archivos clave a leer al iniciar sesión:
- `MEMORY.md` (índice)
- `project_deadline_2026_05_25.md` (este plan condensado)
- `project_estado_pruebas_2026_04_26.md` (último smoke real)
- `project_despliegue_v3_prod_2026_04_24.md` (despliegue inicial)
- `feedback_estados_numericos.md` (decisión arquitectónica)

---

## ⚡ Estado al 2026-05-13 (martes) — Sprint 2 cerrado, demo Azure validada parcial

### 🟢 Demo Azure E2E ejecutada y documentada

Setup completo el 13-may:
- ✅ Portal Blazor en Azure App Service (Linux F1 Free): https://hodsoft-apc-demo-31680.azurewebsites.net
- ✅ PostgreSQL Flexible Server Burstable B1ms con dump de PROD APC restaurado (Empresa Demo + 25 clientes + CAI + saldos)
- ✅ WS WCF corriendo local en VS (IIS Express :2805) apuntando a Azure PG via SSL
- ✅ App Android (Android Studio) conectada por USB con `adb reverse tcp:2805 tcp:2805`
- ✅ Snapshot V3_2 con saldos dinámicos por servicio descargado correctamente al app
- ✅ Multi-empresa: Cam Computer borrada via CASCADE, solo queda Empresa Demo
- ✅ Bugs de datos corregidos en Azure PG (regla 5 RANGO_CONSUMO→MONTO_FIJO; 24 clientes tiene_medidor sync)

### 🔴 Demo Azure E2E parcial: 2 bugs graves del motor de facturación legacy

Detalle completo: [BUGS_MOTOR_FACTURACION_2026-05-13.md](BUGS_MOTOR_FACTURACION_2026-05-13.md)

| Bug | Quién | Síntoma | Esperado |
|---|---|---|---|
| #1 | App Android | TotalApp = L. 12,404.48 | L. 331.11 |
| #2 | SP backend | TotalServidor = L. 209.16 (falta ALCANTARILLADO) | L. 331.11 |

**Estos bugs existen desde antes** — vienen del legacy. Bloquean Sprint 3 día 4-5 (motor unificado).

### 🟢 Lo que SÍ se demostró funcional (Sprint 2 entregado)

- ✅ Login y navegación del portal con datos reales
- ✅ CAI offline UI completa (combo Estado, ColumnChooser, badges, Vencida read-only)
- ✅ Snapshot V3_2 con saldos dinámicos por N servicios
- ✅ Multi-empresa (queries todas con `company_id`)
- ✅ Mono-sucursal (eliminado adm_establecimiento)
- ✅ Estados lookup numérico (cfg_cai_estado + 6 catálogos previos)
- ✅ Leyenda fiscal SAR 3 líneas

### Sprint 2 cerrado al 100% el 09-may. Sprint 3 arranca el 14-may con ajustes.

| Día | Tarea | Esfuerzo | Notas |
|---|---|---|---|
| **14 may** ✅ | **Bugs #1 #2 motor de cálculo alineados** (fix dato regla 29 + fix SP PORCENTAJE_SERVICIO + UI conversión %) | 1 día | ✅ Hecho. 4 facturas E2E emitidas a L. 331.11. Ver [BUGS_MOTOR_FACTURACION_2026-05-13.md](BUGS_MOTOR_FACTURACION_2026-05-13.md) |
| **15 may** | **Bugs #3 #4 #5** descubiertos al validar E2E: correlativo CAI no avanza, estado de cuenta mal, captación en caja sin saldos | 1 día | **CRÍTICO**: bug #3 bloquea operación a partir de la 6ª factura |
| **16-18 may** | **Modelo NC/ND V3** (gaps SAR #3, #4, #5) | 3 días | bloqueante legal |
| 19 may | Bug #6 (app no incluye saldo previo) + validación CAI mejorada (correlativo < rango_hasta, estado lookup) | 1 día | |
| 20 may | Reglas regulatorias: recargo mora, tercera edad, descuento pago anticipado | 1 día | importante |
| 21 may | Bugs UI Sprint 2 originales + Mapeo `adm_servicio` → cuentas regulatorias 5.1.x | 1 día | |
| 22 may | Tests automatizados | 1 día | calidad |
| 23-25 may | Buffer / handoff / smoke E2E final | 3 días | colchón |

### Pendientes operativos para el equipo APC

1. Distribuir APK `app-debug.apk` (~11 MB) a los 5 lectores en campo
2. Smoke real: cliente `090041008` (Empresa Demo, saldo L. 331.11) — validar 4 servicios en cobros anteriores + impresión
3. Validar query `transaccion_abonado.company_id` en PROD: `SELECT COUNT(*) FROM transaccion_abonado WHERE company_id IS NULL;` debe ser 0

### Cierre del 2026-05-09 (lo que quedó hecho ayer)

✅ **CAI offline completamente funcional**: estado lookup numérico (`cfg_cai_estado` con 5 estados), reglas efectivas (ANULADA gana, fecha vencida fuerza VENCIDA), Vencida read-only en UI y service, leyenda fiscal SAR de 3 líneas autogenerada, prefijo `EEE-PPP-TD-NNNNNNNN` preview, ColumnChooser DevExpress, badges clickables por cada estado, combo "Todos los estados", search con Enter, anular/reactivar rápido en grid.

✅ **Decisión arquitectónica 2026-05-08: mono-sucursal**. Sistema multi-empresa pero cada empresa tiene una sola sucursal. Eliminado todo el slice de `/establecimientos`: tabla `adm_establecimiento`, columna `adm_cai_facturacion.establecimiento_id`, página, controller, service, DTO, entity, HTTP client, DI, item de menú, FK + índice + DbSet. El "establecimiento" SAR es solo el código `EEE` (texto libre 3 dígitos).

⏳ **Pendiente de aplicar a BD** (3 scripts):
```bash
psql -h <host> -U <user> -d siad_v3 \
  -f Prestadoras/Database/ddl_v3/20260508_cai_punto_emision.sql \
  -f Prestadoras/Database/ddl_v3/20260508_cai_estado_lookup.sql \
  -f Prestadoras/Database/ddl_v3/20260508_drop_adm_establecimiento.sql
```

⏳ **3 commits preparados** (no aplicados — Claude no commitea por convenio):
- `refactor(establecimientos)` — retirar slice mono-sucursal
- `feat(cai)` — estado_id lookup numérico + leyenda fiscal SAR completa
- `feat(cai-ui)` — combo Estado, ColumnChooser, badges clickables, Vencida read-only

### Plan para 2026-05-09 (próxima sesión)

**Snapshot V3 con saldo previo + servicios dinámicos** — confirmado por usuario el 2026-05-08:
- A. Servicios y saldos **dinámicos** (no hardcoded Agua/Alcant/etc.)
- B. **Corte** legacy: WS deja de mandar `SdoXxx`; toda la fuente es snapshot V3
- C. **Multi-empresa siempre en SPs** (`p_company_id` en todos los SPs de saldo)

#### Pasos concretos (2 commits planificados)

**Commit BD** (~1h):
1. `Database/ddl_v3/20260509_sp_obtener_cliente_saldo_company.sql` — `sp_obtener_cliente_saldo(p_company_id, p_cliente_clave)` y `sp_obtener_cliente_saldo_servicio_detalle(p_company_id, p_cliente_clave, p_servicio_codigo)` con `p_company_id` agregado. Actualizar callers en `sp_lectura_v3` y `sp_adm_calcular_factura_lectura`.
2. `Database/ddl_v3/20260509_snapshot_v3_saldo_previo.sql` — modificar `sp_adm_generar_snapshot_offline_cliente_lectura` para devolver en `snapshot_json` 2 nuevos campos: `saldo_anterior_total` (numeric) y `saldos_por_servicio` (jsonb array `[{servicio_codigo, servicio_nombre, saldo_anterior}]`).

**Commit Java/APK** (~3h):
3. `AppLectoresAPC` — nueva tabla SQLite `MedidorSaldoServicio(IdMedidor, ServicioCodigo, ServicioNombre, SaldoAnterior)` con `onUpgrade` (bump `VersionBD`).
4. Al guardar snapshot en `UtilidadesBD.java` (líneas ~728-790), poblar la tabla nueva desde `saldos_por_servicio` JSON.
5. `GetCobrosAtrasadosPorMedidor` lee dinámico de la tabla nueva (devuelve N items, no 4 hardcoded). Los campos legacy `SaldoAnteriorAgua/Alcantarilla/Ambiental/Convenio/etc.` quedan en 0 (deprecated, retirar post-25).
6. `gradlew assembleDebug` + reinstalar APK en los 5 dispositivos.
7. Smoke test con un cliente que tenga saldo > 0 para verificar que aparece en factura impresa.

### Después del snapshot V3 (Día 9)

| Tarea | Esfuerzo |
|---|---|
| Borrar `ReglasIntegracion.razor` del portal | ~30min |
| Actualizar docs: `PLAN_SAR_COMPLIANCE`, `INVENTARIO_GAPS` con todo lo cerrado de Sprint 2 | ~1h |

### Sprint 3 inicia 2026-05-16
Modelo NC/ND V3 + motor unificado facturación + bugs UI + reglas regulatorias (recargo mora, tercera edad, descuento pago anticipado).
