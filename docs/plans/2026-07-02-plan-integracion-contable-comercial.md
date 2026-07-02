# Plan: Integración Contable ↔ Comercial (multitenant, plan-agnóstica)

**Fecha:** 2026-07-02
**Estado:** Aprobado para ejecución por fases
**Rama base de trabajo:** cada fase se desarrolla en su propia rama (ver §7)

---

## 1. Objetivo

Que todos los movimientos comerciales del sistema (facturación desde app de lectores,
caja, abonos, misceláneos, notas de crédito/débito, bancos, WS bancario) generen su
contabilidad de forma correcta, configurable **una sola vez por empresa**, multitenant,
y conforme al comportamiento exigido por el Manual de Contabilidad Regulatoria ERSAPS
(`docs/regulatorio/manual_contabilidad_regulatoria.pdf.txt`) — sin asumir que todas las
empresas usan ese plan (debe funcionar igual con NIIF o un plan propio).

---

## 2. Decisiones de arquitectura (acordadas — NO reabrir)

| # | Decisión | Detalle |
|---|---|---|
| D1 | **Motor único de mayorización intocable** | Solo `sp_con_postear_poliza` / `sp_con_revertir_poliza` modifican `con_saldo_cuenta` (ESTANDAR_ESTADOS_Y_FLUJO_CONTABLE.md §4) |
| D2 | **NO usar `con_regla_integracion` ni `con_plantilla_partida_*`** como configuración | Reglas: deprecadas 2026-03-13, pantalla eliminada 2026-05-25. Plantillas: plomería interna sin UI (acordado "no crear UI de plantillas"). Ambas se retiran gradualmente |
| D3 | **Configuración de Integración Contable por empresa** | Modal con pestañas (estilo HODSOFT comercial / ConfiguracionSistema existente). Se configura una vez al dar de alta la empresa. Multitenant (`ICompanyScopedEntity`) |
| D4 | **Plan-agnóstica** | La matriz apunta a `account_id` del plan que la empresa tenga (ERSAPS, NIIF, propio). Modos de granularidad: `Cuenta General` / `Por Servicio` / `Por Servicio + Categoría`. Perfiles de auto-llenado opcionales (ERSAPS primero; NIIF después) |
| D5 | **Partidas de facturación (app lectores) = proceso MANUAL** | La sincronización de lecturas NUNCA postea ni depende del período contable (el lector jamás se bloquea). El contador genera el lote de partidas después, desde una pantalla |
| D6 | **Dos períodos: mes comercial Y mes contable, separados** | Cada uno con su ciclo de vida y su cierre manual. Mes comercial: nuevo modelo multitenant (`adm_periodo_comercial` + ciclos) que **reemplaza a `historialmes`** (sin company_id, estados en letras, columnas rotas). Mes contable: `con_periodo_contable` como está. Relación por reglas, no por fusión: el lote de partidas de un mes comercial exige período contable abierto; el sistema avisa si se desfasan. `historicomedicion` queda solo como histórico de lecturas — ninguna lógica de período depende de ella |
| D7 | **Cierre de mes = proceso manual con avisos** | Botón "Cerrar mes" con checklist de validaciones. El sistema avisa (banner/alertas) cuando hay cierre pendiente, facturas sin partida o falta período |
| D8 | **WS de bancos: contrato CONGELADO — es una MIGRACIÓN, no una certificación** | La certificación bancaria ya existe (el banco consume el WS SIMAFI en producción). Mismas rutas `/simafi/api/...`, mismos XML de petición/respuesta (Manual desarrollo Web Service Aguas.docx). Se reimplementa dentro del portal `apc` con respuestas byte-compatibles y corte controlado (cutover) |
| D9 | **App Android y WS de lectores: cero cambios** | Toda la contabilidad se genera del lado servidor (SPs / servicios) |
| D10 | Automático vs manual | Automático donde hay dinero real que debe cuadrar al instante (caja, bancos, notas fiscales). Manual donde hay volumen masivo con revisión contable (lote de facturación, previsión, cierres) |

---

## 3. Estado de partida (auditoría producción 172.16.0.9, 2026-07-02)

- 1 empresa activa (company_id=2, prestadora con plan ERSAPS de 1,674 cuentas y contabilidad SIMAFI 2021-2026 migrada).
- Comercial en piloto: 25 facturas, 27 clientes (25 con categoría), 12 servicios (todos con `cont_account_id`), 1 NC, 4 ND, 102 movimientos.
- **Ninguna partida proviene del comercial** (config de integración: 0 reglas, 0 plantillas, 0 document types).
- Períodos: contable 202606 abierto (julio no existe); comercial `historialmes` mayo/ciclo-01 abierto → desincronizados.
- `sp_lectura_v3` NO postea (decisión PLAN_02 2026-04-17 §9 — "paso posterior" que este plan retoma).
- NC/ND V3 emiten sin partida (los SPs con posteo del 2026-07-02 se revirtieron en producción; se rehacen en Fase 5).
- Dimensiones disponibles: `adm_servicio.cont_account_id`, `cliente_maestro.categoria_servicio_id`, `cliente_maestro.maestro_cliente_tiene_medidor`.

---

## 4. Escenarios contables (comportamiento objetivo)

Dos calendarios independientes: **mes comercial** (`adm_periodo_comercial` + ciclos de
lectura, por empresa) y **mes contable** (`con_periodo_contable`, por empresa). Facturar
exige ciclo del mes comercial abierto; postear exige período contable abierto. El lote
de partidas de facturación conecta ambos: factura del mes comercial X se postea en el
período contable que cubra la fecha del lote.

| Escenario | Generación | Partida (Debe / Haber) |
|---|---|---|
| Lectura app + sync (factura) | **Manual** (lote del contador) | CxC abonados (servicio×categoría×medición) / Ingresos por servicio (mismo detalle) |
| Cobro total en caja (201) | Automática | Caja / CxC abonados |
| Abono parcial (202) | Automática | Caja o Banco / CxC abonados |
| Pago por banco (portal o WS bancario) | Automática | Banco (cuenta de `ban_cuenta`) / CxC abonados |
| Factura misceláneos | Automática | Caja o CxC / Ingreso del concepto |
| Nota de crédito | Automática al emitir | Ingresos (líneas de la factura origen) / CxC — espejo exacto |
| Nota de débito | Automática al emitir | CxC / Ingresos |
| Movimiento bancario directo | Automática | DEP: Banco / contrapartida; RET inverso; TRF banco/banco |
| Compromiso proveedor | Automática (checkbox) | Gasto-presupuesto / CxP proveedor |
| Partida manual | Manual | Libre (pantalla Pólizas) |
| Previsión incobrables | Manual (desde cartera vencida) | Gasto pérdida incobrables / Previsión (contra-activo) |
| Cierre anual 31-dic | Manual | Ingresos 5.x → Resultados del ejercicio; Resultados → Costos 6.x y Gastos 7.x |

Comportamiento regulatorio clave (ERSAPS): base de **devengo**; detalle analítico de CxC
e Ingresos por servicio × categoría × medición (según el modo configurado); balance de
comprobación por subcuenta (saldo anterior + débitos + créditos + saldo actual = rol de
`con_saldo_cuenta`); snapshot de categoría en la factura (los reportes por categoría usan
la categoría AL MOMENTO de facturar).

---

## 5. Fases

> Convención de ramas: `feat/ci-fase<N>-<slug>` partiendo de `main` actualizado.
> Cada fase: PR propio, build verde, tests verdes, script SQL timestamped idempotente,
> y **aplicación a producción solo en la ventana de deploy acordada** (regla espejo).

### Paso previo (antes de Fase 1)

- [ ] Merge de `feat/contabilidad-2026-07` → `main` (consolidación junio + limpieza + fix tests; ya compilado y probado).
- [ ] Deploy pendiente que ya estaba en cola: script `prv_proveedor_cuenta_bancaria` + publish (cierra el 405 de proveedores).

---

### Fase 1 — Modelo de configuración de integración contable
**Rama:** `feat/ci-fase1-modelo-config` · **Estimación:** 2–3 días · **Depende de:** nada

Entregables:
1. Tabla `con_integracion_config` (cabecera por empresa): modo ventas (`GENERAL` / `POR_SERVICIO` / `POR_SERVICIO_CATEGORIA`), modo CxC, comportamiento sin período (encolar), flags de activación por módulo.
2. Tabla `con_integracion_cuenta` (matriz): `company_id, uso, servicio_id?, categoria_servicio_id?, con_medicion?, account_id` + índices únicos tenant-safe. Usos mínimos: `CXC`, `INGRESO`, `CAJA`, `BANCO_DEFAULT`, `ISV`, `DESCUENTO`, `RECARGO_MORA`, `PREVISION_INCOBRABLE`, `GASTO_INCOBRABLE`, `RESULTADO_EJERCICIO`, `RESULTADO_ACUMULADO`, `DEVOLUCION_NC`, `TRANSITORIA`.
3. Función `fn_con_resolver_cuenta(company, uso, servicio?, categoria?, medido?)` con fallback "lo más específico gana" → servicio → general. `RAISE EXCEPTION` claro si no resuelve.
4. Tabla `con_partida_pendiente` (cola de regularización) — solo estructura; se consume en Fase 3.
5. Perfil de auto-llenado **ERSAPS** (por convención de códigos del plan cargado) como SP `sp_con_aplicar_perfil_integracion(company, 'ERSAPS')`. Diseño abierto para perfil NIIF futuro.
6. Scaffold EF de las tablas nuevas (partial context, `ICompanyScopedEntity`).
7. Tests xUnit: resolución con fallback, unicidad por tenant, perfil ERSAPS sobre BD con plan cargado.

Criterio de aceptación: en una BD con plan ERSAPS, `sp_con_aplicar_perfil_integracion`
llena la matriz completa y `fn_con_resolver_cuenta` resuelve los 3 modos con fallback.

### Fase 2 — Pantalla "Configuración de Integración Contable"
**Rama:** `feat/ci-fase2-pantalla-config` · **Estimación:** 3–4 días · **Depende de:** F1

1. DTOs + `IIntegracionContableService` + controller (`[ModuleAuthorize(contabilidad, ...)]` + permisos en `PermissionNames`/`PermissionEndpointCatalog`).
2. Modal por empresa con pestañas (patrón ConfiguracionSistema.razor / DevExpress 25.1.7):
   - **Cuentas generales** (usos generales, selectores de cuenta con búsqueda).
   - **Ventas / CxC**: selector de modo + grid de la matriz (servicio × categoría × medición) con edición inline; botón "Aplicar perfil ERSAPS".
   - **Notas C/D**: espejo de ventas por defecto; cuenta de devolución opcional.
   - **Asientos**: diario y tipo de partida por módulo.
3. Cliente HTTP registrado en `CommonServices` + entrada de menú.
4. Validaciones: cuentas posteables (`allows_posting`), sin huecos según el modo elegido.

Criterio: el contador configura una empresa nueva de punta a punta sin tocar SQL.

### Fase 3 — Lote manual de partidas de facturación
**Rama:** `feat/ci-fase3-lote-facturacion` · **Estimación:** 3–4 días · **Depende de:** F1 (F2 deseable)

1. Snapshot dimensional en factura: columnas `categoria_servicio_id` y `con_medicion` (nullable) + backfill de las existentes + `sp_lectura_v3` las llena al emitir (ÚNICO cambio a ese SP: snapshot; **sigue sin postear**).
2. SP `sp_con_generar_partidas_facturacion(company, fecha_desde, fecha_hasta, p_modo_agrupacion)`:
   - toma facturas sin partida (excluye anuladas), agrupa (resumen por día — patrón SIMAFI — o por período),
   - arma líneas analíticas vía `fn_con_resolver_cuenta` según el modo,
   - postea vía motor único; idempotente (facturas quedan marcadas vía `con_partida_hdr.document_id`/tabla puente `con_partida_factura`),
   - si no hay período abierto → encola en `con_partida_pendiente`.
3. Pantalla "Generar partidas de facturación": preview agrupado (facturas, totales por cuenta), botón generar, historial de lotes, reproceso de pendientes.
4. Decisión operativa incluida: partida retroactiva de regularización para las 25 facturas piloto (o corte desde julio — decidir con el contador al desplegar).
5. Tests: lote balanceado, idempotencia, NC posterior no rompe (factura anulada antes del lote no entra; anulada después se reversa por NC), multi-servicio multi-categoría.

Criterio: correr el lote dos veces no duplica; ESF/balance de comprobación reflejan la facturación del rango.

### Fase 4 — Captación / abonos / misceláneos sobre la config única
**Rama:** `feat/ci-fase4-captacion-config` · **Estimación:** 2–3 días · **Depende de:** F1

1. `CaptacionPagosService`, `AbonoService`, `FacturacionMiscelaneosService`: resolver cuentas vía `fn_con_resolver_cuenta` (reemplaza plantillas/reglas). La lógica de negocio y los flujos NO cambian.
2. `BanTransaccionesService`: fallback de contrapartida desde la config (la cuenta por `ban_cuenta` se mantiene).
3. Retirar los seeds de plantillas de los flujos (los objetos `con_plantilla_*`/`con_regla_integracion` NO se dropean aún — solo dejan de consultarse).
4. La pantalla `CuentasAbonos` se absorbe en la pantalla de F2 (redirección o retiro del menú).
5. Tests de regresión de captación/abonos existentes en verde + casos nuevos de resolución.

### Fase 5 — NC/ND con posteo analítico (rehecho)
**Rama:** `feat/ci-fase5-ncnd-analitico` · **Estimación:** 1–2 días · **Depende de:** F1, F3 (snapshot)

1. Nuevo script que **supersede** `20260702_nc_nd_posteo_contable.sql` (versión plantilla, ya revertida en producción): los SPs de emisión arman la partida espejo de la factura origen — Debe Ingresos por las cuentas de las líneas / Haber CxC analítica — vía `fn_con_resolver_cuenta`, misma transacción (atómico).
2. `poliza_id` en `adm_nota_credito`/`adm_nota_debito` se mantiene (ya existe).
3. Adaptar `NotaCreditoDebitoContabilidadTests` (base ya escrita) al esquema de config.

### Fase 6 — `con_saldo_cuenta` oficial + balance de comprobación
**Rama:** `feat/ci-fase6-saldos-oficiales` · **Estimación:** 2–3 días · **Depende de:** F1–F5 (posteos activos)

1. SP de reconstrucción total desde `con_partida_dtl` (base: `20260310_reconciliacion_con_saldo_cuenta.sql`) — corrige la inconsistencia acumulada por remigraciones+cierres SIMAFI.
2. Reconciliación automática (job/SP verificador): caché vs cálculo vivo; alerta si divergen.
3. `rep_balance_comprobacion` de períodos **cerrados** lee del caché; períodos abiertos siguen en vivo.
4. Regla operativa: única escritura vía motor único (ya garantizado por D1); la remigración SIMAFI futura debe terminar con reconstrucción.

### Fase 7 — Períodos comercial y contable + cierres de mes con avisos
**Rama:** `feat/ci-fase7-periodo-cierre` · **Estimación:** 4–6 días · **Depende de:** F3 (validación "facturas con partida")

1. Tabla `adm_periodo_comercial` (empresa × año-mes, estado, auditoría) + `adm_periodo_comercial_ciclo` (período × ciclo, estado de lectura/facturación) — **reemplazan a `historialmes`** como fuente de verdad del mes comercial. Multitenant, estados numéricos.
2. Función `fn_adm_periodo_comercial_actual(company)` + repunte de consumidores: `sp_adm_calcular_factura_lectura`, SP de GetCiclo del WS (solo SQL — la app NO cambia), y los 4 servicios C# que hoy leen `historialmes`.
3. Trigger espejo → `historialmes` durante la transición (solo lectura legacy); plan de retiro.
4. Migración de datos de `historialmes` existente.
5. **Dos cierres manuales, cada uno con su checklist:**
   - **Cierre del mes comercial**: todos los ciclos del mes cerrados, sin rutas pendientes de sincronizar (validado contra facturas emitidas por ciclo, NO contra `historicomedicion`) → habilita el mes comercial siguiente.
   - **Cierre del mes contable** (pantalla Períodos): facturas del mes con partida generada, sin partidas en borrador, caja del mes posteada, cola de pendientes vacía → precierre → cierre → crea el período contable siguiente.
6. Reglas de relación (sin fusión): el lote de partidas exige período contable abierto; aviso si el mes comercial abierto se desfasa más de N meses del contable (configurable).
7. Avisos en el portal (componente en layout): mes comercial/contable vencido sin cerrar, N facturas sin partida, período del mes actual inexistente, desfase comercial-contable.
8. Tests de regresión del motor tarifario (`LecturaV3Tests`) — riesgo mayor de la fase.

### Fase 8 — Migración del WS bancario (contrato congelado, cutover controlado)
**Rama:** `feat/ci-fase8-ws-bancos` · **Estimación:** 5–7 días dev + ventana de corte · **Depende de:** F1, F4 (posteo de pagos)

> La certificación con el banco YA EXISTE (consume el WS SIMAFI en producción).
> Esto es una migración: respuestas byte-compatibles y cambio de backend sin que
> el banco modifique nada.

1. Controllers XML en `apc` replicando el contrato EXACTO (Manual 2014): rutas `/simafi/api/auth/genkey`, `/consulta/{servicios|otros}`, `/pago/{servicios|otros}`, `/reversion/{servicios|otros}`; mismos elementos XML (`<factura><cabecera><detalle>`, `<pago>`, `<reversion>`, `<mensaje>` de error con HTTP 400).
2. Tabla `ban_ws_credencial` (banco, llave, vigencia, activo) multitenant + validación `banco`+`key` — migrar las llaves vigentes del WS actual para que las peticiones del banco sigan autenticando sin cambios.
3. Consulta → saldos pendientes SIAD (facturas + mora) mapeados al XML.
4. Pago → aplicación FIFO a facturas (parcial = abono), movimiento bancario (`ban_kardex` por cuenta del banco) + partida automática. **Idempotencia por `referencia`**.
5. Reversión → por referencia, reusa reversas existentes.
6. **Pruebas de equivalencia**: golden files XML del WS viejo vs nuevo (mismas consultas → misma estructura y semántica de respuesta); shadow testing con tráfico de consulta real si es posible.
7. **Plan de cutover** (el pago debe caer en UN solo sistema de registro): fecha/hora de corte acordada con el banco u operaciones → repuntar IP/DNS o proxy al portal → monitoreo intensivo de las primeras transacciones → rollback plan (repuntar al WS viejo). Congelar reversiones cruzadas durante la ventana (una reversión de pago pre-corte se maneja manualmente).

---

## 6. Resumen de estimación

| Fase | Días hábiles |
|---|---|
| Previo (merge + deploy proveedores) | 0.5–1 |
| F1 Modelo config | 2–3 |
| F2 Pantalla config | 3–4 |
| F3 Lote facturación | 3–4 |
| F4 Captación → config | 2–3 |
| F5 NC/ND analítico | 1–2 |
| F6 Saldos oficiales | 2–3 |
| F7 Períodos + cierres | 4–6 |
| F8 Migración WS bancos | 5–7 (+ ventana de corte) |
| **Total** | **~23–33 días (5–7 semanas)** |

Paralelizable con dos personas: F2 en paralelo con F3; F8 (contrato/consultas) en paralelo desde F4.

## 7. Estrategia de ramas y despliegue

1. `main` siempre desplegable. Cada fase: `feat/ci-fase<N>-<slug>` → PR → build + tests (`SIAD_TEST_DB` contra BD de pruebas restaurada de backup) → merge.
2. Scripts SQL: timestamped en `Database/ddl_v3/`, idempotentes, **nunca** se corren en producción fuera de la ventana de deploy (lección del incidente NC/ND del 2026-07-02).
3. BD de pruebas local `siad_v3_test` (restore del backup más reciente de producción) para tests de integración de cada fase.
4. Orden de deploy a producción: por fase o agrupado F1+F2 / F3+F4+F5 / F6+F7 / F8 — decidir según ventanas del cliente.

## 8. Riesgos y decisiones pendientes

| Riesgo / pendiente | Mitigación / dueño |
|---|---|
| Repunte de `sp_adm_calcular_factura_lectura` (corazón tarifario, F7) | Tests de regresión `LecturaV3Tests` + espejo `historialmes` transitorio |
| Cutover del WS bancario (F8): los pagos deben caer en UN solo sistema | Golden files viejo-vs-nuevo, ventana de corte acordada, rollback repuntando al WS viejo, reversiones cruzadas manuales durante la ventana |
| 25 facturas piloto sin contabilidad | Decidir con el contador: partida retroactiva vs corte desde julio (F3) |
| 2 clientes sin categoría en producción | Corregir datos + validación obligatoria en pantalla Clientes (F3) |
| Perfil NIIF | Fase posterior; el modelo (D4) ya lo soporta |
| Previsión de incobrables y cierre anual en app | Post-plan (usa cartera vencida + patrón cierre SIMAFI); el modelo de F1 ya reserva los usos de cuenta |
| Retiro físico de `con_regla_integracion` / plantillas / `historialmes` | Solo después de ≥1 mes estable post-F7 |
