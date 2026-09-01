# Retenciones a proveedores — estado del sistema y plan de mejora

**Fecha:** 2026-08-06 · **Alcance:** proveedores (el lado cliente se explica en el doc fiscal hermano, fuera del alcance de implementación) · **Referencia comparativa:** ERP de mercado.
**Documentos hermanos:** `2026-08-06-regimen-retenciones-honduras.md` (régimen fiscal/contable), `plan_retenciones_compromisos_proveedores.md` (plan 2026-07-21 que este documento evoluciona), `centura-flujos/README_retenciones_proveedores.md` (flujo legacy MERENDON).

Este documento nace del pedido "En los Abonos del compromiso de proveedor se pueden aplicar retenciones" + "díganme cómo lo tenemos, qué está bien, qué mejorar y qué no encaja". Se basó en **lectura directa del código** (DTOs, SQL, el SP de posteo, `EstadosNumericos.cs`) y tres auditorías dirigidas (convenciones, ciclo de vida de la partida al mayor, régimen fiscal SAR).

---

## 1. Cómo lo tenemos hoy

El pago a un proveedor pasa por **compromisos de proveedor / órdenes de pago directo** (`SIAD.Services/Presupuesto/OrdenesPagoDirectoService.cs`, 5121 líneas):

- Al **procesar** (`MarkAsProcessedAsync:677`) el sistema arma una partida contable y, si el método es bancario, el movimiento de banco. Soporta un **modelo de partida GENERAL** (`BuildGeneralProcessingPartidaLineasAsync:1483`) donde el usuario puede agregar líneas de **retención/deducción**: la línea del proveedor va al DEBE por el bruto, las deducciones al HABER, y el banco sale por el **neto**.
- Los **abonos** (pagos parciales, `RegistrarAbonoAsync:970`) reutilizan el mismo servicio, pero por un camino distinto (contra-magnitud) que **hoy no admite retención**.
- La partida se asienta con el SP `sp_registrar_partida_contable`, que valida el cuadre.

**Ya existe y funciona** la mecánica de aplicar una retención **a mano** al procesar (elegir cuenta + digitar monto). **No existe** ninguna capa de retención propiamente dicha: ni catálogo de tipos/porcentajes, ni cálculo automático, ni registro fiscal, ni constancia, ni reporte para la declaración, ni retención en abonos.

---

## 2. La lista completa: bien / mejorar / no encaja

### ✅ Lo que está BIEN (conservar)

1. **Multi-tenancy impecable.** Las 3 entidades son `ICompanyScopedEntity`; el SQL crudo filtra `company_id` en todos los writes/reads; los DTOs de entrada NO llevan `CompanyId` (lo resuelve `ICurrentCompanyService`, documentado en `AbonosCompromisoDtos.cs:8-9`); FK compuesta `(company_id, numero_orden)` como defensa en profundidad.
2. **El modelo de partida GENERAL es base sólida para retención.** `BuildGeneralProcessingPartidaLineasAsync:1483`: XOR débito/crédito por línea, valida cada cuenta contra `con_plan_cuentas` (existe / `allows_posting` / activa / ≠ cuenta del proveedor), cuadre con tolerancia 0.01, banco al **neto**. Solo falta el catálogo/autocálculo encima.
3. **Concurrencia y anulación robustas.** FOR UPDATE + revalidación del saldo bajo lock (`:823-836`); anulación con **partida de reverso** + reverso bancario que **falla si el movimiento está conciliado** (mejor que MERENDON, que no tiene reversión). Convención de partida (proveedor al DEBE bruto, banco al HABER neto) blindada por test.
4. **Saldo derivado** (no almacenado) — evita drift; compat legacy limpia. Escritura de la partida por **SP con cuadre validado** (`sp_registrar_partida_contable.sql:30-32`).
5. **Tests de integración** (BEGIN/ROLLBACK) + calculador puro TDD; **controller delgado**.
6. **Infraestructura reutilizable ya lista:** catálogo con vigencia `cfg_impuesto/cfg_impuesto_tasa` (EXCLUDE gist), CAI `adm_cai_facturacion`, tipo fiscal **CRT sembrado** (id 9, `20260507_sar_compliance_01_catalogos.sql:33`), reporte por código `Rpt_Dev_*`.

### 🔧 Lo que hay que MEJORAR (deuda técnica / brechas funcionales)

1. **Retención bloqueada en abonos (el disparador del pedido).** `RegistrarAbonoAsync:970` normaliza con `NormalizeContraProcessingLinesAsync:1960`, que **exige `Credito=0`**; y `CompromisoProveedorAbonar.razor` ni siquiera muestra el popup de deducciones. Hay que habilitar el modelo GENERAL en abonos + UI.
2. **Retención 100% manual al procesar:** sin catálogo, sin %, cuenta a mano. Falta `cfg_retencion` + autocálculo (imitando `cfg_impuesto`).
3. **Sin trazabilidad fiscal:** la retención es una línea débito/crédito genérica; no guarda concepto, %, base ni RTN, ni existe hdr/dtl consultable/reimprimible, ni reporte mensual para la declaración.
4. **`third_party_id` siempre NULL** (`AddPartidaLineaParameters:3453`), aunque el tipo `tipo_linea_partida` SÍ tiene el campo (`sp_registrar_partida_contable.sql:7`). Sin poblar el tercero no hay subledger "retenciones por proveedor/RTN".
5. **Dualidad de modelos de partida** (GENERAL vs contra-magnitud): 2 builders casi verbatim, 3 sobrecargas, 2 vinculadores bancarios, rama legacy semi-muerta. Unificar sobre GENERAL simplifica y desbloquea abonos de una.
6. **Modelo de estado fragmentado:** `status_transacc bool` + `anulado bool` (hdr) + `estado string` (abono) describen el mismo ciclo en 3 representaciones.
7. **Nomenclatura dual:** "Orden de pago directo" (backend/ruta) vs "Compromiso de proveedor" (UI); el diálogo de borrado dice "orden" bajo una pantalla "Compromisos".

### ⚠️ Lo que NO ENCAJA con el resto del proyecto (decisiones a tomar)

1. **★ Las partidas de compromisos NO llegan al mayor oficial.** *(hallazgo central, verificado por código)* Nacen `status=0` (borrador) y **nada las postea**: no hay trigger en `con_partida_hdr/dtl`, no hay job, y el **cierre de período las bloquea** ("N partidas en borrador: postearlas o eliminarlas antes de cerrar", `20260704_ci_fase7_periodo_cierre.sql`). El mayor/balanza/`con_saldo_cuenta` solo leen `status=1` (`fn_con_saldo_libro` filtra `h.status=1`). En contraste, facturación/cobranza/caja/bancos **sí** postean vía `sp_con_generar_comprobante` / `IPolizaService.RegistrarAsync`. ⇒ **Si las retenciones se contabilizan por el mismo camino de compromisos, "Retenciones por pagar" no aparecerá en la contabilidad oficial** y el contador no verá qué enterar al SAR. Existe posteo manual documentado (`docs/GUIA_POSTEO_MANUAL_TESTING.md`), así que podría ser intencional — pero entonces falta una pantalla de mayorización de compromisos. **Solución (post-merge):** el proyecto ya tiene el motor config-driven que postea al mayor (`AlmacenContabilidad.cs` → `sp_con_generar_comprobante_config`, POSTED) y el flag `activo_proveedores` disponible; las retenciones deberían postear por ahí (ver §5 F0). **Verificar en la BD viva:**
   ```sql
   SELECT status, count(*) FROM con_partida_hdr WHERE module='PROV' GROUP BY status;
   ```
2. **Columna de estado string nueva.** `prv_compromiso_abono.estado CHAR(1) 'V'/'A'` (creada jun-2026) es justo lo que CLAUDE.md prohíbe. Todo Almacén reciente (recepción, movimiento, requisición, descargo, O/C) usa **estados numéricos** (`EstadosNumericos.cs`, p.ej. `1=Registrado … 9=Anulado`); no existe `EstadoAbono*`. El plan viejo de retenciones proponía repetir el string.
3. **Autorización gruesa.** Todo bajo `[Authorize(Policy=CanContabilidad)]`, **cero `ModuleAuthorize`**, pese a que CLAUDE.md lo prescribe y existe el módulo `proveedores`. El mismo feature se autoriza como *Contabilidad*, se ubica en UI como *Proveedores* y en backend como *Presupuesto*.
4. **Grid de la lista fuera del estándar.** `CompromisosProveedorList.razor` usa `grid-modern`, PageSize 12, datos en memoria, sin `@ref`/toolbar de columnas/paginador — diverge en ~8 puntos del grid-standard que Almacén y `ClientesList` ya adoptaron (Proveedores no está exento).
5. **Dominio partido entre Presupuesto / Proveedores / Contabilidad** con dos nombres — incoherente con el "slice through the stack".

> **Nota honesta sobre LINQ (a propósito NO va en "no encaja").** La auditoría detectó ~230 usos de LINQ en el servicio + los 3 `.razor`, que la skill `hodsoft-sin-linq` prohíbe. Pero **todo el proyecto usa LINQ**: **2.253 usos en 83 archivos** de `SIAD.Services`, incluidos los módulos "migrados" de Almacén (`ArticulosService` 100, `CargaInicialInventarioService` 40). El LINQ de compromisos **sí encaja** con la práctica real; la contradicción es *regla declarada vs. código de facto del proyecto entero* — una decisión a nivel proyecto, no un defecto de retenciones.

---

## 3. Reconciliación con el merge de estados de Jessel — HECHO

- **Estado del merge:** ✅ **hecho.** Branch actual `feat/almacen-integracion-contable` (HEAD `ec48d97`); las fases de estados de Jessel (`feat/estados-fase1/2`) **ya están en HEAD**.
- **Re-verificación (verificada por git):** `git diff 48a3cd8..HEAD` sobre la superficie de compromisos/abonos/retenciones tocó **un solo archivo: `EstadosNumericos.cs`** (adiciones de Jessel). El servicio, las pantallas, los DTOs, la entidad de abonos y `sp_registrar_partida_contable` quedaron **intactos** ⇒ **las 3 listas siguen 100% válidas**. Sigue **sin existir** `EstadoAbono`/`EstadoRetencion` numérico (el "no encaja" #2 se agudiza).
- **Qué tocó Jessel:** el lado comercial/cobros (`factura.estado→estado_id`, DTOs sin letras, lectores SQL, `Descripcion()/FromCodigo()`). **No** tocó `OrdenesPagoDirectoService`, `prv_compromiso_*` ni `con_partida.status`.
- **Novedad clave (mismo branch, commit `58d14b9`):** se construyó la integración contable de almacén (`AlmacenContabilidad.cs` + `AjusteContabilidadTests.cs`) sobre el **motor config-driven** de Jessel (`IntegracionContableConfigSql` → `sp_con_generar_comprobante_config`, que **postea POSTED**). El flag `activo_proveedores` ya existe en `con_integracion_config`. ⇒ el hallazgo ★ tiene solución conocida y probada (ver §5 F0). **Autoría (git):** el motor = Jessel (Fases 4-5 unificación contable-comercial, `78b71a2`/`5f5baff`, 2026-07-03/04); el adaptador de almacén = Emilio (`58d14b9`, 2026-08-06).
- **Acción:** leer `docs/ESTANDAR_ESTADOS_Y_FLUJO_CONTABLE.md` y `docs/ESTADOS_DOCUMENTOS_COMERCIALES.md`; seguir el patrón fase1/fase2 (estado_id + `Descripcion()`), sin columnas string.

---

## 4. Benchmark: cómo lo resuelve un ERP de mercado

| Capacidad de un ERP de mercado | Lo nuestro hoy | Brecha |
|---|---|---|
| **Motor de retención configurable**: matriz concepto × impuesto × tasa × base × cuenta × vigencia | Solo el asiento GENERAL manual; sin matriz | Falta el catálogo (F1) |
| **Cálculo automático** al pagar (AP) y al facturar/cobrar (AR); el usuario no digita montos | Digitación manual del monto y la cuenta | Falta autocálculo (F2) |
| **Config por tercero**: proveedor exento / sujeto a pagos a cuenta, residente vs. no residente, es agente | No hay | Falta (F1/F4) |
| **Constancia/certificado** con numeración legal, reimpresión y anulación | No hay (CRT solo sembrado) | Falta (F5) |
| **Subledger por tercero**: "retenciones por pagar/a favor" con auxiliar por proveedor y RTN | `third_party_id` va NULL | Falta poblar el tercero (F4) |
| **Reportes fiscales** listos para la declaración (por concepto/tercero/período) | No hay | Falta (F5) |
| **Contabilización automática y POSTED** (la retención llega al mayor como pasivo) | Partidas en borrador, no llegan al mayor | **F0** — ya hay motor + plantilla (`AlmacenContabilidad.cs`, `activo_proveedores`) |
| Anulación/reverso, multi-empresa, auditoría | **Sí** (reverso, tenancy, bitácora) | **Ya lo tenemos** |

Lectura: tenemos la **plomería** (asiento GENERAL, CAI, tenancy, reverso) pero falta la **capa de retención** encima (catálogo → cálculo → registro fiscal → constancia → reporte) y **resolver el destino contable** (que hoy no llega al mayor).

---

## 5. Plan de mejora por fases

Evoluciona el plan 2026-07-21, reordenado para **priorizar la retención en abonos** (lo pedido) y corrigiendo lo que "no encaja".

- **P0 · Base sana** — merge de Jessel **ya hecho**; solo dejar build + tests verdes en el branch mergeado y resolver el conflicto pre-existente `apc/appsettings.Development.json` (config, no del feature).
- **F0 · Posteo al mayor (resuelve ★; D5)** — postear el asiento del compromiso/retención vía `IntegracionContableConfigSql.GenerarComprobanteAsync(module='PROV', …)` (**POSTED**) en vez de `sp_registrar_partida_contable` (borrador). Plantilla directa: `AlmacenContabilidad.cs`. Activar `con_integracion_config.activo_proveedores` + registrar el asiento del módulo (SQL análogo a `2026-08-05_con_integracion_asiento_module_almacen.sql`). Cuidar el **movimiento bancario** del compromiso (no doble-postear) y las partidas `PROV` en borrador históricas. Reverso por `RevertirComprobanteAsync`.
- **F1 · Catálogo** — `cfg_retencion` + `cfg_retencion_tasa` (global, con vigencia, EXCLUDE gist) + `prv_retencion_cuenta` (por empresa, cuenta del pasivo). **Estado numérico**, no string. Slice completo (entidades, DTOs, servicio, controller, cliente, páginas con grid estándar, menú, permisos `ModuleAuthorize`).
- **F2 · Autocálculo** en `CompromisoProveedorProcesar` — combo tipo de retención (vigente a la fecha) → fija la cuenta → propone la **base sin ISV** (`monto / (1 + tasaISV)`, editable) → `monto = base × % / 100`.
- **F3 · Retención en ABONOS ★** (el disparador) — habilitar el modelo GENERAL en `RegistrarAbonoAsync` (levantar la restricción de `NormalizeContraProcessingLinesAsync`) + popup de deducciones en `CompromisoProveedorAbonar.razor` (reutiliza el patrón del de Procesar) + tests.
- **F4 · Registro estructurado** — `prv_retencion_hdr/dtl` con **`estado_id` numérico** (+ helper `Descripcion()`), **`third_party_id` poblado** (subledger por proveedor), anulación por reverso (stage `RRT{n}`), consulta con filtro proveedor/fechas.
- **F5 · Constancia CRT + reportería** — emisión con CAI (si D1), `Rpt_Dev_Constancia_Retencion` (XtraReport por código), endpoint PDF, y **reporte mensual** para la declaración (por tipo y proveedor).
- **Transversal (higiene, fuera del camino crítico):** `ModuleAuthorize` fino, grid estándar en la lista, unificación del modelo de partida.

---

## 6. Decisiones para el contador

| # | Decisión | Recomendación / default |
|---|---|---|
| D1 | ¿Emitimos constancia de retención formal con CAI (documento CRT) o basta folio interno? | Confirmar; si aplica CAI, F5 usa `adm_cai_facturacion` con tipo 9 (CRT) |
| D2 | Catálogo inicial de retenciones (% y cuenta por empresa) | ISR 12.5% honorarios, ISR 1% proveedores, ISV retenido — ver doc fiscal |
| D3 | Base imponible: ¿sobre subtotal sin ISV o total? ¿editable? | **Sin ISV**, editable (`monto / (1 + tasaISV)`) |
| D4 | ¿Retención en abonos parciales? | **Sí** (ya pedido) → F3 |
| **D5** | **Destino contable de las partidas de compromiso/retención** | **Postear al mayor vía el motor config-driven** (como almacén: `AlmacenContabilidad.cs`; `activo_proveedores` ya existe) para que "Retenciones por pagar" sea visible y enterrable. Alternativa: dejar borrador + pantalla de mayorización |
| **D6** | ¿Poblar `third_party_id` (subledger por proveedor)? | **Sí**, el esquema ya lo soporta |

---

## 7. Estimación de esfuerzo por fase

**Supuestos:** desarrollo asistido — **Claude Code escribe el grueso del código**; una persona **decide, revisa, aplica SQL al mirror y hace smoke logueado**. 1 día ≈ 6 h. Rangos = optimista → realista.

| Fase | Reparto Claude Code / humano | Estimado |
|---|---|---|
| **P0 · Merge de Jessel (prerrequisito)** | CC asiste conflictos · humano merge/tests | **1–2 días** |
| **F0 · Decisión contable (D5)** | CC wiring · humano decide+prueba | decisión ~0.5 día; **+1–2 días** si se implementa posteo |
| **F1 · Catálogo retenciones** | CC casi todo · humano SQL+smoke | **2–3.5 días** |
| **F2 · Autocálculo en Procesar** | CC todo · humano smoke | **1–2 días** |
| **F3 · Retención en ABONOS ★** | CC todo · humano pruebas de saldo/dinero | **2–3 días** |
| **F4 · Registro hdr/dtl + tercero** | CC todo · humano SQL+pruebas de anulación | **2.5–4 días** |
| **F5 · Constancia CRT + reporte mensual** | CC todo (reporte = lo más lento) · humano calibra formato | **3–5 días** (o 1.5–2 con folio interno) |
| **Higiene (opcional)** | CC todo · humano revisión | permisos 0.5–1 · grid 0.5–1 · unificar 1.5–3 |

**Totales orientativos:**
- **Camino crítico** (P0 + F0 decisión + F1–F4, estado numérico incluido) ≈ **9–15.5 días** (~2–3 semanas).
- **+ F5 con constancia CAI formal** ≈ **12–20 días** (~3–4 semanas).
- **+ toda la higiene** ≈ +2.5–5 días.

Las "correcciones" que no encajan ya van **dentro** de las fases (estado numérico en F1/F4, posteo al mayor en F0), no como trabajo extra. F5 y F0 cargan la mayor incertidumbre. El SQL va primero al mirror local; el SRV/0.9 en la ventana de deploy, nunca por iniciativa propia.

---

## 8. Qué NO copiar de MERENDON (legacy)

1. Saldos precalculados → **saldo derivado**.
2. Una partida por línea de retención → **una sola partida** (modelo GENERAL).
3. Cuentas/centros hardcodeados (`'99-0'`, `'99997'`, `'9998'`, `COD_EMPRESA=1`).
4. Config '13' ambigua → tasa ISV desde `cfg_impuesto_tasa` con vigencia.
5. Correlativo con `REPLICATE` sin lock → advisory lock / SP transaccional.
6. Sin reversión → aquí la anulación con partida de reverso es parte del diseño.
7. Riesgo transversal: **doble contabilización** — atar el asiento a `module + documentType + documentId` e idempotencia.

---

## Referencias

- Régimen fiscal: `docs/retenciones/2026-08-06-regimen-retenciones-honduras.md`.
- Plan previo (proveedores): `docs/plan_retenciones_compromisos_proveedores.md`.
- Flujo legacy: `docs/centura-flujos/README_retenciones_proveedores.md`.
- Estados y flujo contable (post-merge Jessel): `docs/ESTANDAR_ESTADOS_Y_FLUJO_CONTABLE.md`, `docs/ESTADOS_DOCUMENTOS_COMERCIALES.md`, `docs/NOTAS_MERGE_ALMACEN2_PARA_EMILIO_2026-08-02.md`.
- Posteo manual (contexto del hallazgo ★): `docs/GUIA_POSTEO_MANUAL_TESTING.md`, `docs/DEBUG_POSTEO_MANUAL_ERROR.md`.
- Código clave: `SIAD.Services/Presupuesto/OrdenesPagoDirectoService.cs`, `Database/ddl_v3/PROCEDURE-public.sp_registrar_partida_contable.sql`, `SIAD.Core/Constants/EstadosNumericos.cs`.
