# Informe — Migración de 2 ciclos de SIMAFI a SIAD

**Proyecto:** adaptación de datos comerciales reales de SIMAFI al sistema SIAD para el demo del viernes 10-jul-2026.
**Última actualización:** 2026-07-08
**Documentos relacionados:** [Plan de ejecución](PLAN_MIGRACION_SIMAFI_2CICLOS.md) · [Mapeo técnico campo-a-campo](MAPEO_SIMAFI_SIAD_M0.md) · [Estado general del proyecto](ESTADO_GENERAL_2026-07-08.md)

> Este informe es el resumen ejecutivo para seguimiento. Se actualiza al cerrar cada fase.

---

## 1. Objetivo y alcance

Adaptar **2 ciclos reales** de la producción de SIMAFI (base MySQL `bdsimafi`) a las tablas del
nuevo sistema SIAD, y demostrar el flujo completo:

1. Migrar los clientes de los 2 ciclos con su **saldo al 31 de mayo**.
2. **Facturar** los 2 ciclos en SIAD → verificar afectación del estado de cuenta + partida contable.
3. Simular **pago de banco** → verificar estado de cuenta + partida.
4. Verificar **abono / recibo** → estado de cuenta + partida.

**Alcance:** 2 ciclos, para el demo. La migración de toda la cartera es una etapa posterior.
**Regla de trabajo:** *adaptar, no volcar* (traducir cada dato al modelo SIAD) · solo lectura sobre
SIMAFI · idempotente · probar en base de pruebas antes de producción.

---

## 2. Estado por fase

| Fase | Descripción | Estado |
|---|---|---|
| **M0** | Análisis del origen + decisiones de negocio | ✅ Completado |
| **M1** | Extracción a área de staging (solo lectura) | ✅ Completado |
| **M2** | Transformación / conciliación de catálogos | ✅ Completado |
| **M3** | Carga a SIAD (pruebas → producción) | ⏳ Siguiente |
| **M4** | Ensayo del demo (los 4 puntos) | ⬜ Pendiente |

**Resultado de M2:** los **1,145 clientes** quedaron resueltos a los campos de SIAD y sus **3,316 líneas
de servicio** (Agua, Fondo Ambiental, Tasa ERSAPS) fueron **100% emparejadas** a su cuadro tarifario
oficial, sin ningún caso sin resolver.

---

## 3. Decisiones de negocio tomadas

| # | Decisión | Elección | Justificación |
|---|---|---|---|
| 1 | Qué 2 ciclos migrar | **Ciclos 19 y 20** | Normales, adyacentes, tamaño similar (~570 clientes c/u), buena mezcla con/sin medidor. |
| 2 | Fuente del "saldo al 31-may" | **"Saldo Anterior" oficial** de la factura de junio (lo que SIMAFI imprime al cliente) | Actualizado tras M2: es el número oficial que ve el abonado e incluye mora/convenios. El mayor recomputado se conserva solo como auditoría (coincide 630/817 exacto). |
| 3 | Período que factura el demo | **Junio 2026** | Reproduce lo que SIMAFI ya facturó → permite **validar que SIAD da los mismos montos**. |
| 4 | Clientes institucionales sin identidad | **Identidad genérica** (`0000-0000-00000`) | Escuelas/entes sin documento registrado; la clave real del abonado queda como identificador trazable. |
| 5 | Categoría de clientes sin medidor | **Maestro `maestrosep`** (fuente única) | Se encontró el maestro real de abonados: tiene categoría para **todos** (medidos y no). Validado 99.8% contra las lecturas. La cascada de 3 niveles quedó descartada por innecesaria. |

---

## 4. Hallazgos clave del origen (SIMAFI)

1. **El maestro de clientes no estaba donde el plan asumía.** Las tablas `contratos` (obra pública) y
   `maestro` (contribuyentes) **no** son el padrón de abonados. El padrón vigente se **deriva** del
   último período facturado, combinando: identidad/nombre/dirección/ciclo (de `facturacion`),
   medidor/lectura/categoría (de `historicomedicion`) y saldo (de `transaccion_abonado`).

2. **La identidad del abonado es un código de 9 dígitos** (`090XXXXXX`), común a todas las tablas.

3. **El "saldo vivo" de SIMAFI (`clientesaldos`) no es confiable.** No coincide con el mayor de cartera
   y no tiene fecha. En el ciclo 20, solo **29 de 637** clientes cuadraban; a nivel agregado difería ~45%.
   Se verificó a mano que el **mayor de cartera sí reconstruye el saldo real** (ejemplos: un cliente
   doméstico dio exactamente su saldo de mayo; otro que dejó de pagar en abril-2024 mostró su deuda real
   acumulada, que el "saldo vivo" subestimaba). **Por eso el saldo de apertura se toma del mayor.**

4. **Con/sin medidor se detecta con precisión:** el cliente medido tiene registro de lectura en
   `historicomedicion` (con lectura anterior, actual y consumo); el no-medido no.

---

## 5. Regla de categoría (clientes sin medidor)

La categoría (Doméstica, Comercial, Industrial, Pública, etc.) determina la tarifa. Se resuelve así:

Durante M2 se encontró el **maestro real de abonados de SIMAFI (`maestrosep`)**, que tiene la categoría
de **todos** los clientes — medidos y sin medidor. Esto resolvió el problema por completo:

- Los **1,145 clientes tienen categoría** (0 sin clasificar).
- Se validó `maestrosep` contra una fuente independiente (las lecturas): coinciden en **99.8%** (937/939).
- Como bonus, el maestro también aporta **tercera edad** (96 clientes con descuento) y la **tarifa fija**
  que permite ubicar el segmento tarifario de los sin medidor.

Queda una **lista corta de 29 clientes sin medidor** cuya tarifa fija no calza exacto con SIAD (se les
asigna el segmento base y se marcan), más algunos casos de factura de agua muy alta que conviene que APC
confirme. La cascada de 3 niveles que se había propuesto quedó **descartada por innecesaria**.

---

## 6. Números de los 2 ciclos (extraídos, staging)

| Ciclo | Clientes (padrón) | Con medidor | Sin medidor | Sin identidad (instit.) |
|--:|--:|--:|--:|--:|
| 19 | 575 | ~475 | ~100 | 17 |
| 20 | 570 | ~464 | ~106 | 36 |
| **Total** | **1,145** | **930** | **215** | **53** |

Distribución de categoría (roster completo, vía `maestrosep`): 1,056 Doméstica · 59 Prev. Doméstica ·
13 Pública · 9 Comercial · 7 Industrial · 1 Prev. Comercial. Con descuento de tercera edad: **96**.

**Cartera de apertura al 31-may (fuente oficial "Saldo Anterior"): L 1,753,818.94**
— 811 clientes con deuda, 328 en cero, 6 con saldo a favor.

**Servicios facturados en estos ciclos:** Agua Potable (1,145), Fondo/Tasa Ambiental (1,085) y
Tasa ERSAPS (1,086). **No hay alcantarillado** (zona rural). Total 3,316 líneas de servicio,
todas emparejadas a su cuadro tarifario.

---

## 7. Conciliación de catálogos SIMAFI → SIAD

Verificado que los catálogos destino ya están sembrados y calzan casi 1:1:

- **Categorías:** las 7 de SIMAFI se mapean a las 4 categorías regulatorias de SIAD **más un segmento**
  (p.ej. Prev. Doméstica → Doméstica + segmento preventivo; Industrial ENP → Industrial + segmento ENP).
- **Ciclos:** por código (`19`/`20`).
- **Servicios/conceptos:** Agua Potable, Fondo/Tasa Ambiental y Tasa ERSAPS → servicios equivalentes de
  SIAD, cada uno emparejado a su cuadro tarifario por (categoría × condición de medición × segmento).

El detalle completo campo-a-campo y la tabla de conciliación están en el
[mapeo técnico](MAPEO_SIMAFI_SIAD_M0.md).

---

## 8. Riesgos y pendientes

- **Cuadros tarifarios ERSAPS:** ✅ verificado — los 3,316 servicios encontraron cuadro (0 sin resolver).
- **29 sin medidor con tarifa fija distinta a SIAD** + algunos casos de factura de agua alta → lista corta
  para que APC confirme categoría/segmento. No bloquean la carga (se asigna segmento base y se marcan).
- **Paridad de montos:** SIAD factura con **su tarifa oficial (ERSAPS)**, que en algunos servicios difiere
  de SIMAFI. El demo valida que SIAD factura correcto y afecta estado de cuenta + contabilidad, no la
  reproducción exacta de los montos de SIMAFI.
- **Producción vs pruebas:** todo lo hecho está en la base de **pruebas** local. La carga a producción
  (`siad_v3` @ 172.16.0.9) se hace en M3, con respaldo previo y de forma idempotente.
- **SIMAFI sigue facturando en paralelo:** este ejercicio es para el demo; no reemplaza aún la operación.

---

## 9. Próximos pasos

1. **M3 — Carga a SIAD:** insertar los clientes, servicios, saldos de apertura y lecturas en la base de
   pruebas; verificar; luego replicar en producción, de forma idempotente (re-correr no duplica).
2. **M4 — Ensayo del demo:** facturar junio, simular pago de banco y abono, y verificar los estados de
   cuenta y las partidas contables (los 4 puntos solicitados).

---

### Bitácora de actualizaciones
- **2026-07-08 (1)** — M0 y M1 cerrados. Decisiones 1–5 confirmadas. Staging cargado y validado en pruebas.
- **2026-07-08 (2)** — M2 cerrado. Se halló el maestro `maestrosep` (resuelve categoría de todos, 0 sin
  clasificar; validado 99.8%). Saldo de apertura cambiado a "Saldo Anterior" oficial de la factura
  (cartera L 1,753,818.94). 1,145 clientes y 3,316 servicios resueltos con 100% de cuadro tarifario.
  Sin alcantarillado en estos ciclos. Próximo: M3 (carga).
