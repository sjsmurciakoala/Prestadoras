# Mapeo de migración — Bancos SIMAFI (MySQL `bdsimafi`) → Postgres `siad_v3` (`ban_*`)

Fecha: 2026-07-09 · Autor: análisis asistido · Estado: **mapeo (pre-implementación)**

Sigue el mismo patrón que las migraciones de **Presupuesto** y **Almacén/Activo Fijo**:
landing crudo a `stg_simafi_*` → transform idempotente → carga en tablas destino, en
**mirror local y SRV** (regla espejo). El origen es el mismo MySQL 5.0.24a
(`172.16.0.67:3306`, user `migracion`), accesible **solo** por el driver .NET
`MySqlConnector` (el CLI moderno falla por protocolo).

> **ESTADO (2026-07-09): pipeline ESCRITO, EJECUTADO y VERIFICADO en mirror.** Ajustes
> respecto a este plan, descubiertos al implementar:
> - **company 2 NO estaba vacía**: ya tenía **6 bancos + 7 cuentas reales** (ligadas a
>   contabilidad, con movimientos en `ban_kardex`) que solapan el legacy. Decisión del
>   usuario: **integrar sin duplicar** — reutilizar bancos por nombre y cuentas por número;
>   crear solo lo que falta con código namespaced (`SIMB###`, `SIMC{n}`, `SIMS{n}`). Nunca
>   pisa lo existente. Los saldos de las 6 saldobancos reutilizadas **no se aplican** (quedan
>   en `vw_stg_bancos_pendientes`).
> - `currency_code = **LPS**` (no HNL) por consistencia con las cuentas y `ban_moneda` existentes.
> - **detalleck → una cabecera por (voucher, cuenta banco)** (no una por voucher): 12 vouchers
>   tocan 2 cuentas (traslados) y sumar todo en una cabecera inflaba/mal-atribuía el monto.
> - Extractor: `AllowZeroDateTime=true` devuelve `MySqlDateTime` (no `DateTime`) → formatear ISO.
> - Guard de staging vacío + `vw_stg_bancos_pendientes` con 6 motivos. Revisión adversarial
>   (5 lentes) pasada: 3 ALTA + varias MEDIA/BAJA corregidas; encoding verificado sin mojibake.

---

## 1. Decisiones tomadas

| Decisión | Valor | Nota |
|---|---|---|
| Destino de los movimientos | **Landing fiel** `ban_movimiento` / `_detalle` | NO se transforma al módulo vivo `ban_kardex`; se preservan tal cual (tablas creadas por la migración `AddLegacyBancosCore`, hoy vacías). No se ven en la UI actual de Bancos. |
| Alcance del origen | `ctacheques`, `saldobancos`, `detalleck`, `maestroche` | Se **omite** `bitacorabanco` (1,030,974 filas = log de reasignación recibo↔banco, no es libro mayor). |
| `company_id` | **2** (Empresa Demo) *(a confirmar)* | Consistente con Almacén/Presupuesto (mismo origen `bdsimafi`). La semilla e2e de bancos usó `company_id=1`; la data legacy SIMAFI vive en `2`. |
| Fechas MySQL `0000-00-00` | → `NULL` | (En origen las fechas de bancos vienen completas: 0 nulas en `detalleck`/`maestroche`.) |

---

## 2. Inventario de origen (MySQL `bdsimafi`)

| Tabla | Filas | Rol | Destino |
|---|---|---|---|
| `ctacheques` | 22 | Cuentas de cheques (**11 reales** banco `111-02-01-xx` + **11 transitorias** `115/211`) | `ban_cuenta` (solo las 11 reales) + `ban_banco` |
| `saldobancos` | 27 | Cuentas con **saldos reales** (hasta L12.9M), set distinto a `ctacheques` | `ban_cuenta` |
| `maestroche` | 43,615 | **Registro de movimientos** (cheques CK, notas ND/NC, débitos D1–D9) por cuenta | `ban_movimiento` |
| `detalleck` | 14,312 | **Detalle contable** (partida doble) de vouchers de cheque (todos `docu`=CK) | `ban_movimiento_detalle` (+ header sintético) |
| `bitacorabanco` | 1,030,974 | Log auditoría recibo↔banco | **OMITIDO** |

> El legacy **no tiene catálogo normalizado de bancos**: el banco es texto libre en
> `ctacheques.banco` / `saldobancos.banco` (15 valores distintos con typos:
> "Cuenta Trnasitoria", "Cuneta transitoria").

---

## 3. Hallazgos de perfilado (verificados contra datos)

1. **`ctacheques` mezcla 11 cuentas de banco reales** (`contable` `111-02-01-0x-01`, con
   correlativos de cheque reales `ncheque` 20203, 18342, …) **con 11 cuentas transitorias/contra**
   (`115-xx`/`211-xx`: "Poliza de Seguros", "Décimo tercero", "Aportación patronal") que **no son bancos**.
2. **`ctacheques` (22) y `saldobancos` (27) son sets de cuentas disjuntos** — 0/27 números coinciden.
   `ctacheques` = cuentas viejas (Cuscatlán, HSBC) con historial de movimientos; `saldobancos` = 27
   cuentas nuevas (mayoría Banco de Occidente) que cargan los **saldos reales** (los saldos de
   `ctacheques` están en 0). No se pueden deduplicar de forma fiable.
3. **`detalleck` es detalle de partida doble** de vouchers de cheque: 100% `docu`=CK, 5,320 cheques
   distintos, ~2.6 líneas c/u, `debe` L201.5M ≈ `haber` L201.6M (cuadrado). 5,502/14,312 líneas tocan
   una cuenta de banco (`111-02`); el resto son las contrapartidas (gasto/por pagar).
4. **`maestroche` es el registro de movimientos** limpio: 43,604/43,615 (99.97%) enlazan a
   `ctacheques.numero`; rango 2013-10 → 2025-11; incluye anulados (`borr='X'` ~3,648) y notas.
5. **Cross-walk contable LIMPIO**: `con_plan_cuentas.code` (company 2) = legacy `contable` **sin guiones**.
   `111-02-01-04-01` → `11102010401` = "Banco Atlantida". Cobertura de bancos: **9/10** (falta solo la
   cuenta de ahorro `111-02-01-04-02`). Las transitorias `115/211` casi no mapean (esperado).

---

## 4. Mapeo tabla → tabla

```
ctacheques (11 reales) ─┐
                        ├─► ban_banco   (catálogo, normalizado de nombres libres)
saldobancos ────────────┘

ctacheques (11 reales) ──► ban_cuenta   (origen_legacy='ctacheques')  ◄─ enlazan movimientos
saldobancos (27) ─────────► ban_cuenta   (origen_legacy='saldobancos') ◄─ cargan saldos

maestroche (43,615) ──────► ban_movimiento            (grano documento, origen_legacy='maestroche')
detalleck (5,320 vou) ────► ban_movimiento            (header sintético, origen_legacy='detalleck')
detalleck (14,312 líneas) ► ban_movimiento_detalle    (partida doble)

ban_cta / ban_config / ban_movimiento_transito ──► NO se cargan (sin equivalente fiel en origen)
```

---

## 5. Mapeo columna por columna

### 5.1 `ban_banco`  ← DISTINCT `banco` normalizado
Se construye un mapa de normalización `stg_simafi_banco_map` (nombre_libre → banco canónico),
sembrado con los 15 valores distintos → ~11 bancos canónicos (Atlántida, Occidente, País, Ficohsa,
Lafise, Promérica, Trabajadores, HSBC, Cuscatlán, La Constancia, Central).

| `ban_banco` | Origen | Regla |
|---|---|---|
| `company_id` | — | `2` |
| `code` | generado | correlativo `BCO001…` o slug del nombre; UNIQUE(company_id, code) |
| `nombre` | `banco_map.canonico` | ≤60 chars |
| `activo` | — | `true` |
| `created_by` | — | `'migracion'` |
| resto (sucursal, pais_id=0, direccion…, memo) | — | defaults / NULL |

### 5.2 `ban_cuenta`  ← `ctacheques` (11 reales) ∪ `saldobancos` (27)

| `ban_cuenta` | Origen `ctacheques` | Origen `saldobancos` | Notas |
|---|---|---|---|
| `company_id` | `2` | `2` | |
| `code` | `numero` (char4) | `'SB'‖numero` | UNIQUE(company_id, code); prefijo evita choque entre sets |
| `numero_cuenta` | `cuenta` | `cuenta` | UNIQUE(company_id, numero_cuenta) |
| `nombre` | `'CTA '‖numero‖' '‖banco` | `'CTA '‖numero‖' '‖banco` | NOT NULL; legible |
| `banco_nombre` | `banco` | `banco` | texto libre original |
| `ban_banco_id` | resuelto vía `banco_map` | idem | FK opcional a `ban_banco` |
| `tipo` | `'CHEQUES'` | `'CHEQUES'` | default |
| `currency_code` | `'HNL'` | `'HNL'` | NOT NULL; sin origen |
| `cont_account_id` | strip-dashes(`contable`)→`con_plan_cuentas` | (sin `contable`) → NULL | 9/10 cobertura en `ctacheques` |
| `saldo_inicial` | `saldobancoant` (=0) | `saldobancoant` | |
| `saldo_actual` | 0 | `saldoactual` | **el saldo real vive en `saldobancos`** |
| `estado` / `activo` | `'ACTIVE'/'INACTIVE'` según `activa` | idem | |
| `proximo_cheque` | `ncheque` | 0 | correlativo siguiente cheque |
| `proximo_nddb` | `ndebito` | 0 | (aprox.; `ndepo`/`ncredito` sin hogar exacto → se conservan en staging) |
| `created_by` | `'migracion'` | `'migracion'` | |

> Las 11 filas transitorias de `ctacheques` (`contable` `115/211`) **se excluyen** de `ban_cuenta`
> (son cuentas contra/transitorias, no bancos; no tienen movimientos en `maestroche`). Quedan en staging.

### 5.3 `ban_movimiento`  ← `maestroche` (grano documento)

| `ban_movimiento` | Origen `maestroche` | Notas |
|---|---|---|
| `company_id` | `2` | |
| `banco_cuenta_id` | `cuenta` (char4) → `ban_cuenta.code` (=`ctacheques.numero`) | 99.97% enlaza |
| `tipo` | derivado de `LEFT(docu,2)`: CK→`'CHEQUE'`, ND→`'ND'`, NC→`'NC'`, D#→`'DEBITO'` | |
| `fecha_movimiento` | `fecha` | NOT NULL (0 nulas) |
| `currency_code` | `'HNL'` | |
| `monto` | `debe + haber` (uno es 0) | monto bruto de la línea |
| `mto_db` / `mto_cr` | `debe` / `haber` | preserva débito/crédito |
| `descripcion` | `concepto` | varchar700 → 300 (truncar) |
| `documento` | `docu` | nº cheque / nota |
| `cod_bene` / `bene_origen` | `beneficiar` | |
| `fecha_lib` | `fechaent` | fecha de entrega |
| `estado` | `'POSTED'`; si `borr='X'` → `'VOID'` | anulados |
| `obcp` | `entregado` | flag entregado (N/X/D) |
| `origen_legacy` | `'maestroche'` | trazabilidad |
| `created_by` | `'migracion'` | |

### 5.4 `ban_movimiento` (header sintético) + `ban_movimiento_detalle`  ← `detalleck`
Cada **voucher** de `detalleck` (agrupado por `docu` + `fecha`; la cuenta banco = la línea `111-02`)
genera **1** `ban_movimiento` (`origen_legacy='detalleck'`) y **N** `ban_movimiento_detalle`.

`ban_movimiento` (header): `banco_cuenta_id` ← cuenta banco de la línea `111-02` del voucher (vía
`ctacheques.contable` → `ban_cuenta`); `fecha_movimiento` ← `fecha`; `documento` ← `docu`;
`c_refer` ← `vou`; `monto`/`mto_db`/`mto_cr` ← suma de la línea banco; `origen_legacy='detalleck'`.

| `ban_movimiento_detalle` | Origen `detalleck` | Notas |
|---|---|---|
| `company_id` | `2` | |
| `movimiento_id` | header sintético del voucher | FK NOT NULL |
| `linea_num` | `row_number()` por voucher | UNIQUE(movimiento_id, linea_num) |
| `cod_cta` | `cuenta` (cuenta contable) | GL de la línea |
| `mto_db` / `mto_cr` | `debe` / `haber` | |
| `monto` | `debe + haber` | |
| `dh` | 1 si `debe>0` else 2 | |
| `descripcion` | `concepto` | varchar400 → 60 (truncar) |
| `fecha` | `fecha` | NOT NULL |
| `cod_usua` | `usuario` | |
| `origen` | `'detalleck'` | |

---

## 6. Cross-walk contable (cuenta mayor)

`cont_account_id = con_plan_cuentas.account_id  WHERE company_id=2 AND code = replace(contable,'-','')`

Cobertura medida (cuentas de banco `ctacheques`): **9/10**. Solo falta `111-02-01-04-02` (cuenta de
ahorro Atlántida). Decisión (igual que Presupuesto): **enlazar lo que mapea; dejar `NULL` lo demás** y
emitir vista de pendientes. No auto-crear cuentas.

---

## 7. Decisiones abiertas / riesgos

| # | Tema | Recomendación |
|---|---|---|
| R1 | `company_id` real del tenant SIMAFI | Confirmar `2`. |
| R2 | `detalleck` ↔ `maestroche` describen actividad de cheques **solapada** (podría duplicar montos si se suman juntos) | Se cargan **separados** por `origen_legacy`; `ban_movimiento` es tabla dormida (sin suma en vivo). No se intenta join frágil entre ambas fuentes. |
| R3 | Sets disjuntos `ctacheques`/`saldobancos` (misma cuenta física en 2 eras) | Cargar ambos; prefijo `SB` en `code` evita choque. Documentar que el saldo vive en el registro `saldobancos`. |
| R4 | Nombres de banco libres con typos | Mapa `stg_simafi_banco_map` curado a mano (~15 filas). |
| R5 | Correlativos `ndepo`/`ncredito` sin destino exacto en `ban_cuenta` | Conservar en staging; no forzar. |
| R6 | Cuentas transitorias (11) de `ctacheques` | Excluir de `ban_cuenta` (no son bancos). |

---

## 8. Pipeline propuesto (artefactos, patrón Almacén)

| Archivo | Qué hace |
|---|---|
| `tools/MigradorBancos/extract.cs` | Lee MySQL (`MySqlConnector`) y **genera** `..._01_landing.sql`. Único que conecta al MySQL 5.0. |
| `Database/2026-07-09_bancos_simafi_00_staging_ddl.sql` | Crea `stg_simafi_ctacheques`, `_saldobancos`, `_detalleck`, `_maestroche`, `_banco_map` (idempotente). |
| `Database/2026-07-09_bancos_simafi_01_landing.sql` | **Generado.** TRUNCATE + INSERT de datos crudos. |
| `Database/2026-07-09_bancos_simafi_02_transform.sql` | Normaliza bancos, arma `ban_banco`/`ban_cuenta`/`ban_movimiento`/`_detalle`; upsert idempotente por `origen_legacy`; vista de contables pendientes. |
| `Database/2026-07-09_bancos_simafi_03_reconciliacion.sql` | Cuadres (conteos, sumas debe/haber por cuenta) solo lectura. |

Aplicar `00→01→02` y correr `03` **primero en mirror** (`localhost/siad_v3_restore`), luego en
**SRV** (`172.16.0.9/siad_v3`). Idempotente. Nunca `TRUNCATE CASCADE`.
