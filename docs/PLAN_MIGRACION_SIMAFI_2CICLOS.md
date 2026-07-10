# Plan — Migración de 2 ciclos de SIMAFI → SIAD (demo viernes 10-jul)

**Fecha:** 2026-07-08
**Objetivo:** adaptar 2 ciclos reales de SIMAFI (clientes + saldos al 31-may) a las
tablas de SIAD en `siad_v3` @ 172.16.0.9, y demostrar el flujo completo
(facturación → pago banco → abono) con afectación de estado de cuenta + partida contable.
**Alcance:** 2 ciclos, para el demo. La migración completa de toda la cartera es DESPUÉS.

> Regla: **adaptar, no volcar.** Mapear SIMAFI → esquema SIAD, no copiar tablas crudas.

---

## 0. Origen (SIMAFI) — acceso y hallazgos de la exploración (2026-07-08)

**Conexión (read-only, ya validada):** MySQL `172.16.0.3`, base **`bdsimafi`**
(las credenciales NO van al repo — están en la memoria del proyecto
`simafi-migracion-comercial-2ciclos` / `simafi-migracion-contabilidad-company2`,
son las mismas de la migración contable). Cliente local: MySQL 5.5 en
`C:\Program Files (x86)\MySQL\MySQL Server 5.5\bin\mysql.exe`. **SIEMPRE**
`--default-character-set=utf8` (si no, ñ/acentos salen corruptos). SIMAFI corre
MySQL 5.0.96. La VPN a 172.16.0.3 estaba activa (ping ~79ms).

**Tablas comerciales relevantes descubiertas:**
| Tabla | Rol probable | Notas |
|---|---|---|
| `ciclos` | catálogo de ciclos | 20 ciclos (`codciclo` 1–20, `codciclo2` = "01".."20") |
| `facturacion` | **maestro central de clientes** | tiene `ciclo` + `nombre` + `direccion`; ~1.16M filas (¿incluye historia? confirmar el subconjunto vigente) |
| `contratos` | datos de contrato (nombre+dirección) | contrastar con `facturacion` para el maestro autoritativo |
| `clientesaldos` | saldo por cliente | `cliente char(20)`, `saldo`, `antiguedad`. **⚠️ SIN columna de fecha** → es saldo VIVO, no "al 31-may" |
| `transaccion_abonado` | movimientos del abonado | ~12M filas; fuente para recomputar saldo a una fecha |
| `facturas` | facturas emitidas | ~3.2M |
| `historicomedicion` / `historicosinmedidor` | lecturas | con/sin medidor |
| `rutas`, `tablarutas2` | rutas | |
| `historialmes` | (existe también en SIMAFI) | |

**Preguntas abiertas que M0 debe cerrar (críticas):**
1. **¿Qué 2 ciclos?** — decisión de negocio (Cristian/Alexis). Elegir 2 chicos para el demo.
2. **"Saldo al 31 de mayo":** `clientesaldos` no tiene fecha. ¿Se recomputa desde
   `transaccion_abonado` con corte 31-may, o hay una tabla de saldo mensual/histórico?
   Definir la fuente autoritativa del saldo de apertura.
3. **Maestro de cliente autoritativo:** `facturacion` vs `contratos` — cuál tiene la
   identidad + ciclo + categoría + medidor correctos y vigentes.
4. **Medición:** qué clientes tienen medidor (con/sin medición) — determina la tarifa
   en SIAD (RANGO_CONSUMO vs MONTO_FIJO).

---

## 1. Destino (SIAD) — dónde aterriza cada cosa

| Dato SIMAFI | Tabla SIAD destino | Notas |
|---|---|---|
| Cliente (identidad, nombre, RTN, dirección, ciclo) | `cliente_maestro` | `company_id=2`; `categoria_servicio_id`, `maestro_cliente_tiene_medidor` |
| Servicios del cliente + tarifa/categoría | `adm_cliente_servicio` (+ cuadro tarifario) | mapear a los cuadros ERSAPS ya cargados (AGUA/ALCANTARILLADO/etc.) |
| Saldo al 31-may | saldo de apertura / `transaccion_abonado` de SIAD | como saldo anterior para que la facturación lo arrastre |
| Ciclo | catálogo `ciclos` de SIAD + `adm_periodo_comercial_ciclo` (F7) | |
| Medidores / lecturas anteriores | `historicomedicion` (SIAD) | para que el motor V3 derive consumo |

> El esquema de staging contable de SIMAFI existente (`stg_simafi_*`) es SOLO contable
> (vouchers GL). Para lo comercial **no hay tooling** — se construye nuevo, patrón
> staging → transform → SIAD (como el contable).

---

## 2. Fases de la migración

### M0 — Análisis y decisiones (bloqueante, primero)
- Cerrar las 4 preguntas abiertas del §0 (2 ciclos, fuente del saldo 31-may, maestro
  autoritativo, medición). Explorar `facturacion`/`contratos`/`transaccion_abonado`
  estructuras y contar clientes por ciclo (para elegir 2 chicos).
- Entregable: doc de mapeo campo-a-campo SIMAFI → SIAD + los 2 ciclos elegidos.

### M1 — Extracción a staging (read-only sobre SIMAFI)
- Tablas `stg_simafi_comercial_*` en `siad_v3` (o export TSV + `\copy`, como el contable).
- Extraer SOLO los 2 ciclos: clientes, servicios, medidores, saldos al 31-may.

### M2 — Transformación / mapeo
- Mapear categorías/servicios SIMAFI → cuadros tarifarios ERSAPS de SIAD.
- Resolver con/sin medición → cuadro correcto.
- Normalizar identidad de cliente, RTN, dirección.
- Reporte de excepciones (clientes sin categoría mapeable, sin cuadro, etc.).

### M3 — Carga a SIAD
- Insertar en `cliente_maestro`, `adm_cliente_servicio`, saldos de apertura,
  `adm_periodo_comercial_ciclo`. **Idempotente** (re-correr no duplica).
- Correr en `siad_v3_test` primero, verificar, luego en `siad_v3` @ 172.16.0.9.

### M4 — Ensayo del demo (los 4 puntos de Cristian)
1. Facturar los 2 ciclos → estado de cuenta afectado + partida contable (lote F3).
2. Simular pago completo de banco → estado de cuenta + partida (F4/WS bancario).
3. Simular abono/recibo → estado de cuenta + partida (F4).
4. Verificar reportes contables pedidos.

---

## 3. Riesgos y honestidad sobre el viernes
- **La migración comercial es lo incierto** (esquema SIMAFI + reglas de negocio).
  El deploy ya está; esto es lo nuevo.
- Con pocos días, si los 2 ciclos completos no son viables, **acotar** (2 ciclos pero
  subconjunto de clientes representativo) — decidir con negocio.
- El "saldo al 31-may" es el punto más delicado: si hay que recomputarlo desde 12M de
  `transaccion_abonado`, validar el número contra `clientesaldos` (saldo vivo) para 2-3
  clientes conocidos antes de confiar en el corte.

## 4. Cómo ejecutar sin perder el hilo
Este plan + `ESTADO_GENERAL_2026-07-08.md` + la memoria `simafi-migracion-comercial-2ciclos`
son autosuficientes. Una sesión nueva arranca leyéndolos. La conexión a SIMAFI y los
hallazgos están arriba (§0). Empezar por M0.
