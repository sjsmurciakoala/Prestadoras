# Deploy server APC on-prem + fixes motor V3 — 2026-05-20

Sesión de despliegue del sistema completo (DB + portal + WS + app lectores) en el
servidor APC on-prem (`172.16.0.9`) y corrección de los bugs que afloraron al
ejercer el flujo real del lector.

---

## 1. Deploy server APC on-prem

### Base de datos
- Backup de Azure (`pg-hodsoft-demo-31680` / `siad_v3`) restaurado en el server
  APC. Archivo: `Database/Backups/azure_siad_v3_20260518_091701.backup`.
- En el server APC (`172.16.0.9`, PostgreSQL 17.7) la BD activa es `siad_v3`,
  restaurada **con** los datos demo (25 clientes, ruta 01002 con 5 clientes,
  ciclos, CAI) y el Sprint 3 completo.
- Respaldos en el server: `siad_v3_demo_20260518` (BD vieja desactualizada),
  `siad_v3_vacia_20260518` (restore + wipe transaccional, descartado).
- Scripts de apoyo: `Database/Backups/wipe_transaccional.sql`,
  `validar_db_pruebas.sql`, `README_RESTORE_APC.md`.

### Componentes
| Componente | Detalle |
|---|---|
| Portal Blazor | Publicado desde Visual Studio. `appsettings.Local.json` del server apunta a `localhost:5432/siad_v3`. |
| WS WCF | `publish-onprem.ps1 -Solo ws`. IIS puerto **2805**. `Web.Release.config` (XDT) connection a `localhost`. Requiere feature **WCF HTTP Activation** habilitado. |
| App Android | `URL_WebService = http://172.16.0.9:2805/APCService.svc`. |

### Cambios de build asociados (commit `cd765dfc`)
- `apc.csproj`: excluye `appsettings.Local.json` del publish; `RuntimeIdentifiers win-x64`.
- `apc.Client.csproj`: `UseAppHost=false` (WASM).
- `publish-onprem.ps1`: descubre MSBuild via `vswhere`; restore sin RID + publish `--no-restore`.

---

## 2. Bug — saldo fantasma del cliente 090041008

**Síntoma:** `SYNC_CONFLICT_TOTAL`, el servidor calculaba 662.22 = 2 × 331.11.

**Causa:** la factura id=10 del cliente fue anulada **a mano** (sin NC) durante las
pruebas del 24-abr. La anulación manual no revirtió los movimientos en
`transaccion_abonado`: los 4 movimientos (331.11) quedaron `estado='A'`. El
servidor los contaba como saldo previo.

**Fix (datos):** se anularon los 4 movimientos (`estado='N'`, `estado_id=3`).
`sp_obtener_cliente_saldo('090041008')` pasó a devolver 0.

**No es bug sistémico:** `sp_adm_emitir_nota_credito` sí revierte
`transaccion_abonado`. El flujo normal de anulación por NC funciona. El 090041008
fue dato sucio puntual.

---

## 3. Bug 42702 — `cai_bloque_id` ambiguo en confirmación de CAI

**Script:** `Database/ddl_v3/20260520_fix_cai_bloque_id_ambiguo_confirmar_sync.sql`
(commit `abea245b`).

**Causa:** `sp_adm_confirmar_correlativo_cai_sync` declara `RETURNS TABLE(...
cai_bloque_id ...)`. Los `UPDATE public.adm_cai_bloque_reservado ... WHERE
cai_bloque_id = ...` referenciaban la columna sin alias → PostgreSQL no podía
distinguir entre la columna de la tabla y la variable OUT → error 42702 al
confirmar el correlativo CAI tras sincronizar una factura.

**Fix:** alias de tabla `b` en los dos UPDATE a `adm_cai_bloque_reservado`.

---

## 4. Bug — descuento tercera edad no se aplicaba (3 capas)

Todo cliente de tercera edad daba `SYNC_CONFLICT` sistemático. El descuento es
**25% sobre AGUA_POTABLE**, tope L300/mes (`adm_ajuste_tarifario`,
`TERCERA_EDAD_DOMESTICO`). Para el cliente 090041005: 25% de 199.27 = 49.82 →
factura 331.11 sin descuento vs 281.29 con descuento.

El día 8 del Sprint 3 agregó el descuento a `sp_adm_calcular_factura_lectura`
(el recálculo al sincronizar) pero **no** a las otras dos piezas del flujo
offline. Resultado: 3 capas a corregir.

### Capa 1 — el snapshot offline no traía el descuento
**Script:** `Database/ddl_v3/20260520_snapshot_descuento_tercera_edad.sql`
(commit `abea245b`).

`sp_adm_generar_snapshot_offline_cliente_lectura` detectaba al cliente como
tercera edad pero **nunca incluía la regla del descuento** en el JSON que el app
descarga. Fix: cuando el cliente es tercera edad con regla activa, el snapshot
agrega un servicio `DESC_TERCERA_EDAD` con una regla `DESCUENTO_PORCENTAJE`
(% del ajuste sobre AGUA_POTABLE, con tope).

### Capa 2 — el app no sabía aplicar un descuento
`ConstruirFacturaV3DesdeOfflineSnapshot` solo procesaba reglas `MONTO_FIJO`,
`RANGO_CONSUMO`, `PORCENTAJE_SERVICIO`, y descartaba montos ≤ 0. Fixes:
- Nuevo handler para la regla `DESCUENTO_PORCENTAJE` (resta el % del servicio de
  referencia, respeta el tope).
- El guard `if (montoServicio > 0)` permite ahora montos negativos en líneas de
  tipo `DESCUENTO` → el descuento entra como **línea visible** en la factura.

### Capa 3 — el payload de sync descartaba la línea de descuento
`ConstruirPayloadLecturaV3` armaba el `DetalleServicios` con
`if (monto <= 0) continue;`, excluyendo la línea de descuento. El servidor
recibía un `TotalApp` sin descuento (331.11) → conflicto. Fix: las líneas de
tipo `DESCUENTO` se incluyen en el payload con su monto negativo. El WS suma
todo el detalle (`DetalleServicios.Sum(d => d.Monto)`) → 281.29.

### Capa 3b — falso conflicto cosmético en `FacturaV3Coincide`
Aun con totales iguales, el app marcaba conflicto porque `FacturaV3Coincide`
comparaba el detalle **línea por línea**: el servidor embebe el descuento en la
línea base (Agua Potable con monto neto, 4 líneas) y el app lo lleva como línea
separada (5 líneas). Fix: `FacturaV3Coincide` compara **solo el total**
(redondeado, tolerancia 0.05, igual que el preflight del WS).

### Resultado verificado
Factura del 090041005 en el servidor: `id=21`, `saldototal=281.29`, detalle
Agua Potable **149.45** (199.27 − 49.82), Alcantarillado 119.56, Tasa Ambiental
5.90, Tasa SVA ERSAPS 6.38. Descuento tercera edad aplicado end-to-end.

**Commits app (`AppLectoresAPC`):** `d1b91de` (handler + guard) y el commit de
esta documentación (payload + `FacturaV3Coincide`).

---

## 5. Pendientes anotados

- **`GetCiclo` (APCService.svc.cs:1409)** lanza `NpgsqlOperationInProgressException`
  (reusa la misma conexión Npgsql para queries solapadas). No bloqueó, pero
  conviene arreglarlo antes del 25 si habrá varios lectores concurrentes.
- **`ActualizarLecturaV3` warning** `CondicionLectura truncada 'MIN' -> 'M'`: la
  columna destino acepta 1 char. Warning, no bloquea — revisar.
- El servidor calcula el total con más de 2 decimales en el preflight
  (281.2911) aunque guarda la factura redondeada (281.29). Conviene redondear
  el total del preflight a 2 decimales por prolijidad.
