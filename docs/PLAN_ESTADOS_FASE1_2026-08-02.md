# Plan Estados — Fase 1: `factura.estado_id` como fuente de LECTURA (2026-08-02)

Primer paso del retiro de las letras legacy `A`/`B`/`C`/`N` de `factura.estado`.
**Fase 1 = solo lecturas.** Los escritores (C#, SPs de emisión, WS bancario) y el
trigger de sincronización NO se tocan — quedan para las fases 2/3 (post-deploy).

## Contexto y regla de oro de la fase

- Hoy la **letra es la fuente de escritura**: el trigger `trg_factura_sync_estado_id`
  deriva `estado_id` de la letra en cada INSERT/UPDATE (`fn_estado_documento_comercial_id_from_codigo`:
  A→1, C→2, N→3, B→4, default 1).
- Por eso, mientras esta fase esté vigente: **NADIE escribe `estado_id` directo.**
  Si C# escribiera `estado_id` sin cambiar la letra, el trigger no se dispara y la
  letra queda desfasada. Se sigue escribiendo la letra; el trigger deriva.
- Constantes: `SIAD.Core/Constants/EstadosNumericos.cs` → `EstadoDocumentoComercial`
  (`Activa=1`, `Cobrada=2`, `Anulada=3`, `ParcialmenteAbonada=4`).
- Columna `factura.estado_id`: smallint NOT NULL en la práctica (0 NULLs en 3.9M);
  dominio real de letras en copia09: solo `A`/`B`/`C` (no hay `N` migradas).

## Auditoría de datos (ejecutada 2026-08-02, antes de tocar código)

| BD | Resultado |
|---|---|
| `siad_v3_copia09` | A→1: 179,369 ✔ · B→4: 4,159 ✔ · C→2: 3,713,370 ✔ · **C→1: 11 filas DESCUADRADAS** |
| `siad_v3_test` | 0 descuadres (A→1: 16, C→2: 11, N→3: 5) |

Las 11 descuadradas tienen `fechapago` 29/30-jul-2026 — hipótesis: escrituras de
la ventana F7 o cargas M2–M4 con triggers deshabilitados. **Confirmar causa en
ejecución** (si fue carga masiva, anotarlo para el runbook de migración: toda
carga con triggers off debe recalcular `estado_id` al final).

### Paso 0 — Saneo (bloqueante, antes de cualquier cambio de código)

Script `Database/2026-08-02_saneo_factura_estado_id.sql`, idempotente, genérico
para las 4 letras:

```sql
UPDATE factura f SET estado_id = fn_estado_documento_comercial_id_from_codigo(f.estado)
WHERE f.estado_id IS DISTINCT FROM fn_estado_documento_comercial_id_from_codigo(f.estado);
-- verificación: el SELECT agregado letra×id no debe tener pares fuera de A1/B4/C2/N3
```

Aplicar a copia09 y siad_v3_test; re-correr la auditoría → **0 descuadres o no se
sigue**. El script entra a la cola de la ventana 0.9 y se registra en el runbook
SRV (skill `runbook-despliegue-srv`).

## Inventario exacto de cambios (11 sitios — lecturas de `factura.estado`)

Verificado por grep el 2026-08-02 sobre main `f712b02`. Regla de traducción:
`"A"→Activa(1)`, `"B"→ParcialmenteAbonada(4)`, `"C"→Cobrada(2)`, `"N"→Anulada(3)`.

### LINQ/EF (comparaciones sobre la entidad)

| # | Sitio | Hoy | Queda |
|---|---|---|---|
| 1 | `AbonoService.cs:58` | `f.estado == "A" \|\| "B" \|\| "C"` | `f.estado_id == Activa \|\| ParcialmenteAbonada \|\| Cobrada` |
| 2 | `AbonoService.cs:114` | `saldoPendiente > 0 \|\| x.estado == "A" \|\| "B"` | ídem con `estado_id` (1, 4) |
| 3 | `AbonoService.cs:150` | `f.estado == "A" \|\| "B"` | `estado_id` 1/4 |
| 4 | `AbonoService.cs:736` | `factura.estado == "C"` | `estado_id == Cobrada` |
| 5 | `CobranzaService.cs:57` | `f.estado == "A" \|\| "B"` | `estado_id` 1/4 |
| 6 | `CobranzaService.cs:256` | `f.estado == "A" \|\| "B"` | `estado_id` 1/4 |
| 7 | `CobranzaService.cs:717` | `f.estado == "A"` | `estado_id == Activa` |
| 8 | `CobroService.cs:227` | `vista.estado == "N"` | proyectar `estado_id` en la vista anónima y comparar `== Anulada` |
| 9 | `FacturacionMiscelaneosService.cs:430` | `tipofactura == "R" && estado == "A"` | `estado_id == Activa` (tipofactura queda igual — no es estado) |

### SQL embebido (Dapper)

| # | Sitio | Hoy | Queda |
|---|---|---|---|
| 10 | `ClientesServices.cs:665` | `AND f.estado IN ('A','B')` | `AND f.estado_id IN (1, 4)` + comentario `-- Activa/ParcialmenteAbonada` |
| 11 | `ReclasificacionCxcClienteSql.cs:70` | `AND f.estado IN ('A', 'B')` | ídem |

### Opcional no bloqueante

- `apc.Client/Pages/Facturacion/Notas/NotasCreditoDebito.razor:660` mapea la letra
  del DTO a texto (`"A" => "PENDIENTE"`). Solo se migra si el DTO **ya** trae
  `EstadoId`; si exige tocar DTO+controller+cliente, se difiere a fase 2. La regla
  "el usuario nunca ve la letra" ya se cumple (muestra el texto).

## Falsos positivos — PROHIBIDO tocarlos (mismas letras, otro dominio)

| Sitio | Dominio real |
|---|---|
| `AbonoService.cs:406` (`"A" ? "ANULADO" : "POSTED"`) y `:1140` (`"C"/"P"/"A"`) | estados de pago/recibo legacy (`EstadoPago`: C=aplicado, P=pendiente, A=anulado) — otro catálogo |
| `CobranzaService.cs:76` (`!= "N"/"R"/"P"`), `:1287` y `CorteMasivoService.cs:270` (`ta.estado='C'`) | `transaccion_abonado` — tabla CONGELADA, histórico; sus letras se quedan para siempre |
| `ChequesService.cs:596` (`@estado='A'`) | `ban_cheque` ('A'=activo, CHECK propio de almacén 2.0) |
| `OrdenesPagoDirectoService.cs:3145` (`SET estado='A'`) | presupuesto (además es escritura) |
| `Ordenes*.razor` (`"A" => "Asignada"`) y `CorteMasivoService.cs:166` | órdenes de trabajo — contrato legacy 8086, intocable |

## Escrituras de la letra — SE QUEDAN tal cual (fase 3)

`CobranzaService.cs:378,510` · `CobroService.cs:464,471,783,789` ·
`FacturacionMiscelaneosService.cs:297` · SPs (`sp_lectura_v3`, `sp_ban_ws_pagar/
reversar`, anulación). El trigger sigue derivando el id de estas escrituras.

## Riesgos y mitigaciones

1. **Datos descuadrados** → paso 0 bloqueante: saneo + auditoría en 0 ANTES del código.
2. **Letra fuera de catálogo** (el trigger colapsa a 1 con WARNING) → la auditoría
   lista `DISTINCT estado`; hoy solo A/B/C. Si apareciera otra letra, parar y analizar.
3. **Confundir dominios** → tabla de falsos positivos arriba; cada edición se hace
   leyendo el contexto del archivo, nunca con reemplazo masivo.
4. **`estado_id` NULL** → verificado 0 en 3.9M; los sitios EF comparan con `==`
   (NULL nunca iguala, mismo comportamiento que la letra NULL hoy).
5. **Regresión funcional** → suite completa (411) + focalizadas: `AbonoService`,
   `Cobranza*`, `CobroMotor`, `ReciboBancoPendiente`, `PlanCuotas`,
   `NotaDebitoCobrable`, `ClienteRecategorizacion`, `SaldoDocumentos`, `Anulacion`.
   Los tests del motor ejercitan justo los sitios 1–11 (emisión→cobro→anulación).
6. **El usuario corre Release en VS** → tras el merge debe recompilar; ningún
   cambio visible en pantalla (misma semántica).

## Secuencia de ejecución

1. Rama `feat/estados-fase1-factura-estado-id` (creada desde `f712b02`). ✔
2. Paso 0: script de saneo → aplicar a copia09 y test → auditoría = 0 descuadres.
   Investigar y anotar la causa de las 11 filas.
3. Migrar sitios 1–9 (EF), uno por uno con lectura de contexto.
4. Migrar sitios 10–11 (SQL embebido).
5. Evaluar el opcional de Notas (solo si el DTO ya trae `EstadoId`).
6. `dotnet build` — 0 errores.
7. Suite completa + focalizadas — 407+ verdes.
8. Re-auditoría de descuadre en copia09 (los tests corren en test con ROLLBACK).
9. Commit + PR (checklist = este plan) + merge. Registrar el script en el runbook SRV.

## Fuera de alcance (fases siguientes)

- **Fase 2 — lectores SQL**: `vw_rep_movimiento_vigente`, `sp_obtener_cliente_saldo`,
  funciones `rep_*`, snapshots de la app. Descubrimiento:
  `SELECT proname FROM pg_proc WHERE prosrc ILIKE '%estado%IN%''A''%'` + `pg_views`.
- **Fase 3 — escritores + retiro (ventana post-deploy)**: SPs de emisión y WS
  bancario (contrato SIMAFI congelado, golden files), motor y C#; invertir la
  dirección del trigger o escribir solo `estado_id`; al final `DROP COLUMN estado`
  con la vista de compatibilidad que haga falta.

## Criterio de éxito y rollback

- Éxito: 0 descuadres, build limpio, suite verde, cero cambio visible.
- Rollback: revert del PR (solo código). El saneo de datos NO se revierte — es
  corrección válida con o sin el código nuevo.
