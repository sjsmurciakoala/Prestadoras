# M0 — Mapeo campo-a-campo SIMAFI → SIAD + decisiones

**Fecha:** 2026-07-08
**Entregable de M0** (análisis, bloqueante) del [PLAN_MIGRACION_SIMAFI_2CICLOS.md](PLAN_MIGRACION_SIMAFI_2CICLOS.md).
**Fuente:** SIMAFI MySQL `172.16.0.3` / `bdsimafi` (solo lectura, `--default-character-set=utf8`).
**Destino:** SIAD `siad_v3`, `company_id = 2` (probar en `siad_v3_test` local primero).

> Regla dura respetada: solo `SELECT` sobre SIMAFI. Ninguna escritura en 172.16.0.3.

---

## 1. Resumen ejecutivo — hallazgos que cambian el plan

1. **`contratos` NO es maestro de clientes.** Es contratos de **obra pública** (contratista,
   monto, plazoejec, retención, garantía, órdenes de cambio). Descarta la hipótesis del plan §0.
2. **`maestro` (3,421 filas) tampoco.** Es un catálogo de contribuyentes (codigo/contribuyente/numero).
3. **La identidad del abonado es un código de 9 dígitos** `090XXXXXX`. Es la clave de cruce entre
   `facturacion.clave`, `transaccion_abonado.cliente`, `historicomedicion.clave` y
   `clientesaldos.cliente` (99% de `clientesaldos` es de 9 díg; ~700 filas usan un variante de 10 díg
   con prefijo `6` — normalizar quitando el `6`).
4. **No existe un "roster" único con categoría.** El padrón vigente por ciclo se **deriva** del último
   período facturado. La categoría del cliente vive en `historicomedicion.categoriaCliente` (medidos);
   los **sin medidor** no la traen ahí (ver §6, sub-punto abierto de M2).
5. **`clientesaldos` NO es confiable como "saldo".** No reconcilia con el mayor de cartera —diverge en
   ambos sentidos— y no tiene fecha. **El saldo autoritativo es `transaccion_abonado`** (ver §4).
6. **SIAD ya tiene `transaccion_abonado`** con la misma estructura (+ `company_id`, `saldo`). El saldo
   de apertura aterriza como fila(s) en esa tabla; no hace falta inventar un almacén nuevo.

---

## 2. Las 4 preguntas de M0

| # | Pregunta | Respuesta |
|---|---|---|
| 1 | ¿Qué 2 ciclos? | **Decisión de negocio — te consulto** (§3). Recomiendo **19 + 20**. |
| 2 | Fuente del "saldo al 31-may" | **Decisión de negocio — te consulto** (§4). Recomiendo **mayor `transaccion_abonado`, neto por cliente, `estado<>'A'`, corte `fecha_docu <= 2026-05-31`**, como **una** fila de saldo inicial por cliente. |
| 3 | Maestro autoritativo | **RESUELTO.** Identidad/nombre/dirección/ciclo/ruta/secuencia ← `facturacion` (último período por `clave`). Medidor/lectura/categoría ← `historicomedicion`. Saldo ← `transaccion_abonado`. `contratos`/`maestro` se descartan. |
| 4 | Con/sin medidor | **RESUELTO.** Medido = tiene fila en `historicomedicion` del período (trae `contador`, `lect_ant`, `lect_act`, `consumo`); sin medidor = aparece en `facturacion`/`historicosinmedidor` pero **no** en `historicomedicion`. `transaccion_abonado.tiene_med` ('S'/'N') corrobora. |

---

## 3. Elección de 2 ciclos (conteos reales, período completo 2026/06)

Padrón activo = `COUNT(DISTINCT clave)` en `facturacion` del último mes completo (2026/06).
Medidos = filas en `historicomedicion` 2026/06. Sin medidor = diferencia.

| Ciclo | Clientes activos | Medidos | Sin medidor | Cartera apertura al 31-may¹ |
|------:|-----------------:|--------:|------------:|----------------------------:|
| **21** | 442 | 353 | ~89 | 767,028.60 |
| **20** | 570 | 464 | ~106 | 812,733.70 |
| **19** | 575 | 475 | ~100 | 742,389.74 |
| 14 | 763 | 728 | ~35 | — |
| … | … | … | … | (resto 795–1363) |

¹ Neto `debitos−creditos`, `estado<>'A'`, `fecha_docu<=2026-05-31`, agrupado por cliente y sumado.
El conteo "con movimiento" del mayor es algo mayor que el padrón activo porque incluye históricos que
cambiaron de ciclo (`cambiociclo`); la migración se acota al **padrón activo** (§5).

**Recomendación: ciclos 19 y 20.** Son dos ciclos **normales, adyacentes y de tamaño similar**
(~570 clientes c/u, ~80% medidos), buena mezcla medido/no-medido y una cartera manejable para el demo.
El **21** es el más chico pero es `"CICLO 21"` (alta posterior en el catálogo) y conviene descartarlo
salvo que negocio confirme que es un ciclo residencial normal. *(Confírmame — §7.)*

---

## 4. "Saldo al 31 de mayo" — metodología y evidencia

**Problema:** `clientesaldos` es un saldo vivo, sin fecha, y **no reconcilia** con el mayor. En el
ciclo 20 solo **29 de 637** clientes coinciden (`clientesaldos` vs neto del mayor); la cartera agregada
difiere 45% (mayor 819k vs clientesaldos 450k). No sirve como saldo de apertura auditable.

**El mayor `transaccion_abonado` SÍ es reconstruible transacción a transacción.** Verificado a mano:

- **090808290** (doméstico): el mayor neto **da 483.14 hoy** —cuadra exacto con la secuencia
  factura/pago— y **188.61 al 31-may** (justo la factura de 2026/05, pagada la de abril el 21-may).
  `clientesaldos` decía 908.59 → **incorrecto**.
- **090807830**: dejó de pagar en **abril-2024** y sigue facturándose; el mayor neto
  (excluyendo una fila `estado='A'` anulada de L500) da **4,554.78** de deuda real acumulada.
  `clientesaldos` decía 513.30 → **obsoleto**.

**Regla propuesta para el saldo de apertura por cliente (al 31-may):**
```
saldo_apertura = SUM(debitos) - SUM(creditos)
FROM transaccion_abonado
WHERE cliente = :clave
  AND estado <> 'A'                 -- excluir anulados
  AND fecha_docu <= '2026-05-31'    -- corte calendario
```
- Se materializa como **UNA** fila de "saldo inicial" por cliente en el `transaccion_abonado` de SIAD
  (idempotente por `company_id + cliente_clave + tipotransaccion='SALDO_INICIAL' + periodo='2026/05'`).
- Signo: positivo = deuda; negativo = **saldo a favor** (hay 133/183/134 casos a favor en 19/20/21 —
  clientes que prepagaron; SIAD debe soportarlo).
- **Corte por `fecha_docu`** (no por `periodo`) porque "al 31 de mayo" es una fecha calendario y hay
  pagos de un período aplicados en fecha posterior.

**Alternativa a decidir contigo:** ¿el demo factura **junio (2026/06)** sobre esta apertura
—reproduciendo lo que SIMAFI ya hizo, lo que permite validar paridad SIAD vs SIMAFI— o un período
nuevo? Ver §7.

---

## 5. Mapeo campo-a-campo

### 5.1 `cliente_maestro` (destino) — padrón activo, `company_id=2`

| SIAD `cliente_maestro` | Origen SIMAFI | Regla |
|---|---|---|
| `maestro_cliente_clave` | `facturacion.clave` (=`transaccion_abonado.cliente`) | 9 díg, normalizar (quitar prefijo `6` si viene de clientesaldos) |
| `maestro_cliente_identidad` | `facturacion.identidad` | DNI hondureño `0000-0000-00000`; vacío en institucionales → placeholder/regla M2 |
| `maestro_cliente_rtn` | `facturacion.identidad` si es RTN (14 díg) | si aplica |
| `maestro_cliente_nombre` | `facturacion.nombre` (o `historicomedicion.propietario`) | último período |
| `categoria_servicio_id` | `historicomedicion.categoriaCliente` → `categoria` (SIMAFI 1/2/3/4/6/7/8) → `categoria_servicio` SIAD | mapear catálogo (§5.4); **sin medidor: pendiente M2** |
| `ciclos_id` | `facturacion.ciclo` → `ciclo` SIAD | catálogo ciclos (1–21) |
| `maestro_cliente_indicativo_ruta` | `facturacion.ruta` / `historicomedicion.ruta` | |
| `maestro_cliente_secuencia` | `facturacion.secuencia` / `historicomedicion.secuencia` | |
| `maestro_cliente_tiene_medidor` | `true` si en `historicomedicion`; si no `false` | |
| `contador` | `historicomedicion.contador` | solo medidos |
| `tipo_uso_codigo` | derivar de categoría | M2 |
| `maestro_cliente_tercera_edad` / `descuento_tercera_edad` | (no visto en SIMAFI aún) | M2, si aplica |
| `estado` | `true` (activos del padrón) | |
| `company_id` | `2` | fijo |

### 5.2 `cliente_detalle` (destino)

| SIAD `cliente_detalle` | Origen SIMAFI | Regla |
|---|---|---|
| `detalle_cliente_direccion` | `facturacion.direccion` (o `historicomedicion.ubicacion`) | último período |
| `maestro_medidor_id` | vía `maestro_medidor` desde `historicomedicion.contador` | medidos |
| `clave` | `maestro_cliente_clave` | |
| `company_id` | `2` | |

### 5.3 `adm_cliente_servicio` (destino) — servicio + cuadro tarifario por cliente

| SIAD `adm_cliente_servicio` | Origen SIMAFI | Regla |
|---|---|---|
| `cliente_id` | FK a `cliente_maestro` | |
| `servicio_id` | conceptos de `facturacion.codigo` (01 Agua, 02 Alcantarillado, 03…, 04/05 tasas) → `adm_servicio` | mapear conceptos (§5.4) |
| `categoria_regulatoria_id` | categoría del cliente | |
| `condicion_medicion_id` | medido / no-medido | determina `RANGO_CONSUMO` vs `MONTO_FIJO` |
| `cuadro_tarifario_id` | cuadro ERSAPS por (categoría × servicio × medición) | ya sembrados en SIAD (verificar en M2) |
| `medidor_id` | de `historicomedicion.contador` | medidos |
| `fecha_alta` | fecha de alta o `2026-06-01` | |

### 5.4 Catálogos a conciliar (SIMAFI → SIAD)

- **Categoría** (`categoria` SIMAFI): 1 Doméstica, 2 Comercial, 3 Industrial, 4 Pública, 6 Industrial (ENP),
  7 Preventiva Comercial, 8 Preventiva Doméstica → `categoria_servicio` de SIAD (verificar seeds M2).
- **Conceptos/servicios** (`facturacion.codigo`, 2026/06): `01`=Agua Potable (líneas medidas),
  `02`=Alcantarillado, `03`=(servicio adicional), `04`/`05`=tasas (Fondo Ambiental / Tasa ERSAPS,
  montos fijos chicos), `16`/`17`=ajustes/descuentos (negativos), `12`/`15`=otros. Detalle fino en M2.
- **Ciclos**: `ciclos` SIMAFI (codciclo 1–21) → `ciclo` SIAD.

### 5.5 `historicomedicion` (SIAD, medidos) — lecturas para que el motor V3 facture

| SIAD | SIMAFI `historicomedicion` |
|---|---|
| lect_ant / lect_act / consumo | `lect_ant` / `lect_act` / `consumo` |
| fecha_lect_ant / fecha_lect_act | `fecha_lect_ant` / `fecha_lect_act` |
| contador, ruta, secuencia, condicion | idem |
| categoria | `categoriaCliente` |

Para facturar el período demo se necesita, por cliente medido, la **lectura anterior** (= lectura de
mayo) como base; el motor V3 deriva el consumo.

### 5.6 Saldo de apertura → `transaccion_abonado` (SIAD)

| SIAD `transaccion_abonado` | Valor |
|---|---|
| `company_id` | `2` |
| `cliente_clave` | `clave` (9 díg) |
| `tipotransaccion` | `'SALDO_INICIAL'` (o el código SIAD equivalente — confirmar en M2) |
| `debitos`/`creditos` | según signo del neto al 31-may (§4) |
| `saldo` | neto |
| `periodo` | `'2026/05'` |
| `fecha_docu` | `2026-05-31` |
| `ciclo` | ciclo del cliente |
| `estado` | activo |

---

## 6. Riesgos / sub-puntos abiertos para M2

- **Categoría de los sin-medidor:** `historicomedicion` no los cubre y `historicosinmedidor` no trae
  categoría. Resolver en M2 (¿default Doméstica? ¿derivar del monto de `facturacion`? ¿otra tabla?).
  Reporte de excepciones para los que no mapeen.
- **Identidad vacía** (institucionales, ej. "JARDIN DE NIÑOS…"): regla de placeholder/estado.
- **Filas `estado='A'`** (anuladas) en el mayor: excluir siempre (chicas pero presentes: 342 en ciclo 20).
- **Convenios / cuotas futuras:** el mayor tiene filas con `periodo` futuro (2027/2028) — el corte por
  `fecha_docu<=2026-05-31` las excluye naturalmente; validar que no haya cuotas de convenio ya devengadas
  que deban entrar a la apertura.
- **Seeds de cuadros tarifarios ERSAPS** en `siad_v3`/`_test`: verificar que existan los cuadros por
  (categoría×servicio×medición) antes de M3 (desbloquea el motor).

---

## 7. Decisiones de negocio — CONFIRMADAS (2026-07-08)

1. **Ciclos: 19 y 20.** ✅
2. **Saldo de apertura: mayor `transaccion_abonado` neto al 31-may** (§4), una fila `SALDO_INICIAL` por cliente. ✅
3. **El demo factura junio (2026/06)** sobre esa apertura → habilita validar **paridad SIAD vs SIMAFI**. ✅

**M0 CERRADO.** Siguiente: **M1 (extracción a staging `stg_simafi_comercial_*` en `siad_v3`, read-only sobre SIMAFI)**,
acotado a ciclos 19 y 20: padrón activo (facturacion último período), lecturas (historicomedicion 2026/05 y 2026/06),
y el mayor `transaccion_abonado` hasta 2026-05-31 para computar la apertura. Probar en `siad_v3_test` antes de `siad_v3` @172.16.0.9.
</content>
</invoke>
