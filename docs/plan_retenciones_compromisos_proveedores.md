# Plan — Retenciones en compromisos de proveedores (Prestadoras)

**Fecha:** 2026-07-21 · **Estado:** propuesta, sin implementar (nada de este plan está aplicado)
**Base de análisis:** [`docs/centura-flujos/README_retenciones_proveedores.md`](centura-flujos/README_retenciones_proveedores.md) (flujo Merendon/Centura completo) y el estado actual de la rama `Cambios_almacen1.0`.

---

## 1. Punto de partida (lo que YA existe y no hay que construir)

- **Backend:** `OrdenesPagoDirectoService.MarkAsProcessedAsync` ya soporta el modelo GENERAL de líneas con Débito/Crédito: agrega la línea del proveedor al DEBE por el bruto, valida XOR débito/crédito por línea, cuadre con tolerancia 0.01, neto de banco > 0, y asienta el banco por el **neto** vía `sp_ban_kardex_registrar_movimiento`. Cubierto por `SIAD.Tests/Presupuesto/ProcesamientoRetencionesTests.cs` (5 tests).
- **UI:** `CompromisoProveedorProcesar.razor` tiene el modal "Retenciones y deducciones" (draft + aplicar, tipo Retención/Cargo, vista previa de partida, validaciones) — pero el usuario **digita el monto y escoge la cuenta a mano**.
- **Plantilla de catálogo fiscal con vigencias:** `cfg_impuesto` / `cfg_impuesto_tasa` (`Database/2026-07-14_cfg_impuestos.sql`): EXCLUDE gist anti-solapes, tipos GRAVADO/EXENTO/EXONERADO, seed ISV 15/18.
- **Infraestructura CAI:** `adm_cai_facturacion` (rango, vigencia, `fecha_limite_emision`, `correlativo_actual` en BD, bloques reservados) + `cfg_tipo_documento_fiscal`; consumida por NC/ND y tarifario.
- **Impresión:** patrón `XtraReport` por código (`Rpt_Dev_Compromiso_Proveedor`, `Rpt_Dev_Comprobante_Abono`) + endpoint PDF inline.
- **Abonos:** `prv_compromiso_abono` con estado `V/A` y **saldo derivado** (no almacenado).

**Brecha a cerrar:** catálogo de retenciones con % y cuenta, autocálculo en la pantalla, registro estructurado de la retención (consultable/reimprimible), constancia fiscal (si aplica) y reportería.

---

## 2. Decisiones previas (bloquean fases 4–5; preguntar antes de implementar)

| # | Decisión | Impacto |
|---|---|---|
| D1 | ¿Las prestadoras son **agentes de retención** que emiten constancia formal (numeración autorizada por SAR + CAI), o basta el registro interno con folio propio? | Define si la Fase 4 (CAI/correlativo) se hace o se omite |
| D2 | **Catálogo inicial**: qué retenciones aplican (p. ej. ISR 12.5% honorarios profesionales, retención anticipo ISR 1%, ISV retenido) con su % y cuenta contable por empresa | Seed de Fase 1; confirmar con el contador (junto con la pregunta abierta de ISV al costo vs crédito fiscal) |
| D3 | **Base imponible**: ¿sobre el subtotal sin ISV (como Merendon con `FLAG_CALCULA_IMPUESTO`) o sobre el total del compromiso? ¿La base es editable? | Cálculo de Fase 2 (propuesta: sugerir `monto / (1 + tasa ISV vigente)` y dejarla editable) |
| D4 | ¿Se permite retener también en **abonos parciales** o solo al procesar el compromiso completo? | Si sí, extender `RegistrarAbonoAsync` al modelo GENERAL (hoy es contra-magnitud) |

---

## 3. Fases

### Fase 1 — Catálogo de retenciones

**BD** (script nuevo `Database/2026-XX-XX_cfg_retenciones.sql`, imitando `cfg_impuestos`):

- `cfg_retencion` (GLOBAL, sin `company_id` — el % lo fija la ley): `id, codigo, nombre, descripcion, base ('TOTAL' | 'SIN_ISV'), activo, auditoría`.
- `cfg_retencion_tasa` (GLOBAL, con vigencia): `id, retencion_id FK, codigo, nombre, porcentaje NUMERIC(5,2) > 0, vigencia_desde, vigencia_hasta, activo, auditoría` + `EXCLUDE gist` anti-solapes + CHECK de rango.
- `prv_retencion_cuenta` (**tenant-scoped**, `ICompanyScopedEntity`): `id, company_id, retencion_id FK, account_id FK con_plan_cuentas, activo, auditoría` + UNIQUE `(company_id, retencion_id)`. La cuenta contable del pasivo es por empresa — a diferencia de Merendon, donde iba en el catálogo global.
- Seed según D2.

**Código:** entidades en `SIAD.Core/Entities`, partial de contexto (`SiadDbContext.Mantenimientos.cs` o nuevo), DTOs en `SIAD.Core/DTOs/Retenciones/`, servicio + interfaz en `SIAD.Services/Retenciones/` (registrar en `ServiceRegistration.cs`), controller `apc/Controllers/Configuracion/RetencionesController.cs`, cliente HTTP en `apc.Client/Services` (registrar en `CommonServices.cs`), páginas de mantenimiento en `apc.Client/Pages/Configuracion/` siguiendo el grid estándar (`ClientesList` como referencia), entrada de menú en `SidebarNavigationDefinition.cs`.

**Permisos:** hoy los compromisos usan la policy gruesa `CanContabilidad`; el catálogo encaja en `module.configuracion.*`. Si se quiere granularidad, agregar recurso en `PermissionResources` + `PermissionEndpointCatalog`.

### Fase 2 — Autocálculo en CompromisoProveedorProcesar

- En el editor del modal: combo "Tipo de retención" (catálogo vigente a la fecha del compromiso) que al elegirse:
  - fija la **cuenta contable** desde `prv_retencion_cuenta` (editable solo si no hay cuenta configurada → aviso),
  - propone la **base** según D3 (`monto` o `monto / (1 + tasaISV)` con la tasa vigente de `cfg_impuesto_tasa`), editable,
  - calcula `monto = base × % / 100` (editable, como en Merendon donde la base era ajustable),
  - llena la descripción (`nombre + %`).
- La línea manual libre se conserva para "Cargo" y casos no catalogados.
- El DTO **no cambia** (`PartidaLineaOrdenPagoDto` ya viaja con Débito/Crédito); solo se agrega, en Fase 3, la referencia al tipo de retención.
- Cliente: nuevo lookup `GET api/.../retenciones/vigentes` (+ cuenta por empresa).

### Fase 3 — Registro estructurado de la retención

**BD** (script nuevo):

- `prv_retencion_hdr` (tenant-scoped): `retencion_hdr_id IDENTITY PK, company_id, numero_orden FK → prv_compromiso_hdr, numero_abono NULL` (si D4=sí), `folio` (correlativo interno o fiscal según D1), `fecha_emision, cod_proveedor, base NUMERIC(18,2), total_retenido NUMERIC(18,2), partida_id, estado CHAR(1) 'V'/'A', cai_id NULL, cai_proveedor VARCHAR NULL, motivo_anulacion, auditoría, rowid UUID` + UNIQUE `(company_id, folio)`.
- `prv_retencion_dtl`: `retencion_dtl_id PK, retencion_hdr_id FK, retencion_id, codigo, nombre, porcentaje, base_linea, monto_retenido, account_id`.
- Regla tipo abonos: **anulación** marca `estado='A'` y genera **partida de reverso** (stage `RRT{n}` análogo a `RAB{n}`) — mejora deliberada sobre Merendon, que no tiene reversión. Solo anulable si el compromiso no está pagado/cerrado (definir regla exacta al implementar).

**Código:** al procesar con líneas de retención catalogadas, `MarkAsProcessedAsync` persiste hdr/dtl **en la misma transacción** de la partida `PRC` (guardando `partida_id`). Consulta: página "Consulta de retenciones" (filtro proveedor/fechas — mejora sobre Merendon) + endpoint. Tests de integración nuevos (persistencia, anulación con reverso, tenancy).

### Fase 4 — Constancia fiscal con CAI (solo si D1 = sí)

- El tipo de documento fiscal **ya existe**: `cfg_tipo_documento_fiscal` tiene sembrado el código `CRT` "Comprobante de retención" (id 9, `Database/ddl_v3/20260507_sar_compliance_01_catalogos.sql:34`; esa tabla no tiene columna `requires_cai` — esa columna es de `cfg_document_type`). No hace falta seed nuevo del tipo.
- Reutilizar `adm_cai_facturacion` con `tipo_documento_fiscal_id = 9 (CRT)` para el talonario de constancias (rango, vigencia, `fecha_limite_emision`); emisión avanza `correlativo_actual` con la misma mecánica de los SP de NC/ND (SP nuevo `sp_prv_emitir_constancia_retencion` o lógica en servicio con advisory lock, como `GenerateMonthlyPartidaNumberAsync`).
- `prv_retencion_hdr.folio` pasa a ser `prefijo + correlativo` y guarda `cai_id`; se captura además el **CAI del documento del proveedor** (`cai_proveedor`) — en Merendon viene del documento origen; aquí habría que capturarlo en el compromiso o en el modal (decidir).
- Alerta "pocos correlativos restantes" (equivalente config '104'): umbral configurable + aviso en la pantalla de proceso.

### Fase 5 — Impresión y reportería

- `SIAD.Reports/Templates/Rpt_Dev_Constancia_Retencion.cs` (XtraReport por código, patrón de los existentes; aplicar las lecciones de la memoria de reportes programáticos): empresa, proveedor (RTN), documento origen, base, líneas (código, nombre, %, monto), total en letras, CAI propio + rango + fecha límite (leyenda Acuerdo 481-2017, como `CaiTarifarioService`), CAI del proveedor, marca de agua "ANULADO" si `estado='A'`.
- Endpoint `GET .../retenciones/{folio}/pdf` inline + botón de reimpresión en la consulta.
- **Reporte mensual de retenciones** (para la declaración): por rango de fechas, agrupado por tipo de retención y proveedor (folio, RTN, base, %, retenido) — el entregable que Merendon nunca tuvo y que el contador necesita.

### Fase 6 (opcional, depende de D4) — Retenciones en abonos parciales

- Extender `RegistrarAbonoAsync` al modelo GENERAL (hoy `NormalizeContraProcessingLinesAsync` exige crédito = 0 en todas las líneas) o prorratear; ligar `prv_retencion_hdr.numero_abono`.

---

## 4. Qué NO copiar de Merendon (decisiones ya tomadas por diseño de Prestadoras)

1. Saldos precalculados (`SALDO_ACTUAL`, kardex con saldo arrastrado) → saldo **derivado**.
2. Una partida por línea de retención → **una sola partida** con todas las líneas (modelo GENERAL actual).
3. Cuentas/centros hardcoded (`'99-0'`, `'99997'`, `'9998'`, `COD_EMPRESA=1`).
4. Config '13' ambigua → tasa ISV desde `cfg_impuesto_tasa` con vigencia.
5. Avance de correlativo con `REPLICATE` sin lock → advisory lock / SP transaccional.
6. Los defectos listados en §12 del README (UPDATE que no aborta, resta de lempiras a dólares, validación de cuadre sin retención, retención sin prorrateo entre documentos, etc.).
7. Sin reversión → aquí la anulación con partida de reverso es parte del diseño (Fase 3).

---

## 5. Checklist de despliegue por fase (regla del proyecto)

Cada fase que toque BD debe, antes de darse por cerrada:

- [ ] Script timestamped en `Database/` (idempotente: `IF NOT EXISTS` / `ON CONFLICT`).
- [ ] Registro en `docs/db-cambios/README_db_cambios_produccion.md` (**crear el archivo con el primer script** — hoy no existe): objeto, script, orden, prerrequisitos, rollback, validación post-ejecución.
- [ ] Aplicado primero en el mirror local (`siad_v3_restore`) — **el usuario aplica en prod/SRV**.
- [ ] `dotnet build HODSOFT_DEVEXPRESS.sln` sin errores.
- [ ] Tests de integración (`SIAD_TEST_DB`) verdes, incluidos los nuevos.
- [ ] Smoke en la pantalla afectada.

**Orden propuesto:** F1 → F2 (ya usable sin constancia) → F3 → F5 (consulta+PDF con folio interno) → F4 (si D1) → F6 (si D4). F1–F2 no dependen de ninguna decisión salvo D2/D3; F3 en adelante conviene congelar D1 primero.
