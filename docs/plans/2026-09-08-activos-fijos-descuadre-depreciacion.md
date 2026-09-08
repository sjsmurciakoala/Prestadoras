# Activos fijos — caracterización del descuadre en el histórico de depreciación

**Fecha:** 2026-09-08
**Base analizada:** `siad_v3_restore` (mirror local), `company_id = 2`
**Alcance:** solo lectura. Ninguna consulta de este documento escribe ni altera datos.
**Objetivo:** entender por qué el detalle mensual migrado explica solo dos tercios de la depreciación
acumulada del maestro, antes de construir el motor de depreciación.

---

## 0. Cifras de partida

```sql
SELECT company_id, count(*) AS activos,
       sum(valor_compra), sum(valor_a_depreciar), sum(valor_depreciado),
       sum(depreciacion_acumulada), sum(valor_libros)
FROM af_activo_fijo GROUP BY company_id;

SELECT company_id, count(*) AS filas, count(activo_fijo_id) AS con_fk,
       count(*) FILTER (WHERE activo_fijo_id IS NULL) AS sin_fk,
       min(anio), max(anio), sum(valor_depreciado)
FROM af_activo_fijo_depreciacion GROUP BY company_id;
```

| Concepto | Valor |
|---|---:|
| Activos en `af_activo_fijo` | 830 |
| `sum(valor_compra)` | L. 21,504,221.89 |
| `sum(valor_depreciado)` (el acumulado real) | **L. 16,048,909.78** |
| `sum(depreciacion_acumulada)` (columna casi vacía) | L. 1,837,217.79 |
| `sum(valor_libros)` | L. 5,459,052.18 |
| Filas en `af_activo_fijo_depreciacion` | 44,579 |
| Filas con `activo_fijo_id` | 43,787 |
| Filas sin `activo_fijo_id` | 792 |
| `sum(valor_depreciado)` del detalle | **L. 10,368,231.23** |
| Rango de años del detalle | 2016 – 2025 (más 3 filas con `anio = 0`) |

**Hueco bruto:** 16,048,909.78 − 10,368,231.23 = **L. 5,680,678.55**
**Hueco contra el detalle efectivamente enganchado al maestro:** 16,048,909.78 − 10,276,266.44 = **L. 5,772,643.34**
(la diferencia entre ambos, L. 91,964.79, son las filas huérfanas — ver §6)

### Dos trampas de nomenclatura, verificadas

1. **`valor_a_depreciar` NO es la base depreciable: es la cuota ANUAL.**
   ```sql
   SELECT count(*) FILTER (WHERE abs(valor_a_depreciar - depreciacion_mensual*12) < 0.5) AS es_cuota_anual,
          count(*) FILTER (WHERE abs(valor_a_depreciar - (valor_compra - valor_rescate)) < 0.5) AS es_base
   FROM af_activo_fijo WHERE company_id=2;
   ```
   → **827 de 830** cumplen `valor_a_depreciar = depreciacion_mensual × 12`; solo 26 coinciden con
   `valor_compra − valor_rescate`. Cualquier motor que trate esa columna como base depreciable
   producirá basura.

2. **`depreciacion_acumulada` está poblada en 2 activos, no en 0.**
   Son los únicos dos con `fecha_inicio_depreciacion` (ids 182 y 46, L. 1,836,523.96 + L. 693.83).
   Los 828 migrados de SIMAFI la tienen en 0 — confirma que el acumulado real vive en `valor_depreciado`.

---

## 1. ¿El descuadre se concentra o está repartido?

```sql
WITH det AS (
  SELECT activo_fijo_id, count(*) filas, sum(valor_depreciado) suma
  FROM af_activo_fijo_depreciacion
  WHERE company_id = 2 AND activo_fijo_id IS NOT NULL
  GROUP BY activo_fijo_id
), base AS (
  SELECT a.id, a.valor_depreciado AS maestro, COALESCE(d.suma,0) AS detalle,
         COALESCE(d.filas,0) AS filas, a.valor_depreciado - COALESCE(d.suma,0) AS dif
  FROM af_activo_fijo a LEFT JOIN det d ON d.activo_fijo_id = a.id
  WHERE a.company_id = 2
)
SELECT CASE WHEN filas = 0 THEN '0. sin detalle'
            WHEN abs(dif) < 0.005 THEN '1. cuadra exacto'
            WHEN abs(dif) <= 1 THEN '2. dif <= L.1'
            WHEN abs(dif) <= 100 THEN '3. dif <= L.100'
            WHEN abs(dif) <= 1000 THEN '4. dif <= L.1,000'
            WHEN abs(dif) <= 10000 THEN '5. dif <= L.10,000'
            WHEN abs(dif) <= 100000 THEN '6. dif <= L.100,000'
            ELSE '7. dif > L.100,000' END AS grupo,
       count(*), sum(maestro), sum(detalle), sum(dif), sum(abs(dif)),
       count(*) FILTER (WHERE dif > 0), count(*) FILTER (WHERE dif < 0)
FROM base GROUP BY 1 ORDER BY 1;
```

| Grupo | Activos | Maestro | Detalle | Dif. neta | % del hueco | Dif+ | Dif− |
|---|---:|---:|---:|---:|---:|---:|---:|
| 0. sin detalle | 174 | 1,578,083.05 | 0.00 | **1,578,083.05** | 27.3 % | 156 | 0 |
| 1. cuadra exacto | 107 | 707,370.94 | 707,370.94 | 0.00 | 0.0 % | 0 | 0 |
| 2. dif ≤ L.1 | 1 | 1,885.46 | 1,886.26 | −0.80 | 0.0 % | 0 | 1 |
| 3. dif ≤ L.100 | 178 | 456,617.33 | 452,170.74 | 4,446.59 | 0.1 % | 160 | 18 |
| 4. dif ≤ L.1,000 | 178 | 1,755,467.10 | 1,693,728.89 | 61,738.21 | 1.1 % | 162 | 16 |
| 5. dif ≤ L.10,000 | 153 | 2,514,417.35 | 2,051,060.18 | 463,357.17 | 8.0 % | 141 | 12 |
| 6. dif ≤ L.100,000 | 33 | 5,464,092.55 | 4,633,993.04 | 830,099.51 | 14.4 % | 33 | 0 |
| 7. dif > L.100,000 | **6** | 3,570,976.00 | 736,056.39 | **2,834,919.61** | **49.1 %** | 6 | 0 |
| **Total** | **830** | **16,048,909.78** | **10,276,266.44** | **5,772,643.34** | 100 % | | |

**Está muy concentrado.** Seis activos aportan la mitad del hueco; 39 activos (grupos 6+7) aportan
L. 3,665,019.12 = **63.5 %**. En el otro extremo, 108 activos cuadran exacto o dentro de L.1, y otros
356 tienen diferencias por debajo de L.1,000 (aportan juntos solo el 1.2 %). El descuadre **no** es
un error sistemático de redondeo repartido por toda la población.

### Los 6 grandes

```sql
WITH det AS (
  SELECT activo_fijo_id, count(*) filas, sum(valor_depreciado) suma,
         min(anio*100+mes) desde, max(anio*100+mes) hasta
  FROM af_activo_fijo_depreciacion WHERE company_id=2 AND activo_fijo_id IS NOT NULL
  GROUP BY activo_fijo_id)
SELECT a.id, a.codigo_activo, a.descripcion, a.valor_compra, a.valor_depreciado,
       a.depreciacion_mensual, a.fecha_compra, a.fecha_ultima_depreciacion,
       COALESCE(d.filas,0), COALESCE(d.suma,0), d.desde, d.hasta,
       a.valor_depreciado - COALESCE(d.suma,0) AS dif
FROM af_activo_fijo a LEFT JOIN det d ON d.activo_fijo_id=a.id
WHERE a.company_id=2 ORDER BY dif DESC LIMIT 20;
```

| id | Código | Descripción | Compra | Maestro | Detalle | Filas | Ventana | Dif. |
|---:|---|---|---:|---:|---:|---:|---|---:|
| 168 | IN-02-02-18 | Retroexcavadora John Deere 310J | 2008-09-03 | 1,440,633.50 | 310,357.75 | 28 | 2016-06 → 2018-09 | 1,130,275.75 |
| 341 | INV-01-01-33 | Vehículo Nissan Frontier 4x4 | 2002-10-10 | 457,015.00 | 152.52 | 14 | 2016-06 → 2017-07 | 456,862.48 |
| 112 | INV-02-03-33 | Tanque Avast-ATI Ariete 500 L | 2018-03-20 | 422,977.33 | 10,236.04 | 2 | 2018-03 → 2018-04 | 412,741.29 |
| 192 | INV-02-03-13 | Pick up Isuzu | 2013-07-16 | 532,636.53 | 137,060.51 | 28 | 2016-06 → 2018-09 | 395,576.02 |
| 187 | IN-02-02-31 | Vehículo Toyota Vigo | 2005-09-30 | 373,751.92 | 0.00 | **0** | — | 373,751.92 |
| 189 | IN-02-02-32 | Pick up Isuzu | 2013-07-16 | 532,636.53 | 220,533.31 | 28 | 2016-06 → 2018-09 | 312,103.22 |

Todos son activos **comprados mucho antes de 2016** o con el detalle truncado. Ninguno es un activo
nacido dentro de la ventana del detalle. Esto ya apunta al origen.

---

## 2. Los activos sin ninguna fila de detalle

```sql
WITH det AS (SELECT DISTINCT activo_fijo_id FROM af_activo_fijo_depreciacion
             WHERE company_id=2 AND activo_fijo_id IS NOT NULL)
SELECT count(*), sum(a.valor_depreciado), sum(a.valor_compra),
       count(*) FILTER (WHERE a.valor_depreciado = 0),
       count(*) FILTER (WHERE a.descargado), count(*) FILTER (WHERE a.depreciar)
FROM af_activo_fijo a
WHERE a.company_id=2 AND a.id NOT IN (SELECT activo_fijo_id FROM det);
```

| Concepto | Valor |
|---|---:|
| Activos sin detalle | **174** |
| `sum(valor_depreciado)` | **L. 1,578,083.05** |
| `sum(valor_compra)` | L. 3,153,761.91 |
| Con `valor_depreciado = 0` (no aportan hueco) | 18 |
| Descargados | 3 |
| Marcados `depreciar = true` | 12 |

**Explican el 27.3 % del hueco, no la mayor parte.** Es el segundo bloque en tamaño, pero el
principal es otro (§7).

Desglose contra la fecha de corte del detalle (2016-06):

```sql
SELECT CASE WHEN a.fecha_ultima_depreciacion IS NULL THEN '(sin fecha)'
            WHEN a.fecha_ultima_depreciacion < DATE '2016-06-01' THEN 'termino ANTES de 2016-06'
            ELSE '2016-06 o posterior' END,
       count(*), sum(a.valor_depreciado), count(*) FILTER (WHERE a.valor_libros <= 0.01)
FROM af_activo_fijo a WHERE a.company_id=2 AND a.id NOT IN (...)
GROUP BY 1;
```

| Clase | Activos | Monto | Totalmente depreciados |
|---|---:|---:|---:|
| Terminó de depreciar antes de 2016-06 | 57 | 320,494.12 | 0 |
| Última depreciación 2016-06 o posterior | **115** | **1,255,371.33** | 5 |
| Sin fecha de última depreciación | 2 | 2,217.60 | 0 |

Los 57 primeros son **coherentes** con el corte: dejaron de depreciar antes de que empezara el
histórico, por eso no tienen filas. Los **115 restantes son anómalos**: el maestro dice que
depreciaron dentro de la ventana 2016-2026, pero no hay ni una fila que lo respalde. De ellos,
91 tienen última depreciación en 2016 o 2017 (L. 572,042.97) — la migración perdió su detalle.

---

## 3. Huecos temporales dentro del detalle

```sql
WITH d AS (SELECT activo_fijo_id, (anio::int*12 + mes::int) AS m
           FROM af_activo_fijo_depreciacion
           WHERE company_id=2 AND activo_fijo_id IS NOT NULL AND anio>0 AND mes BETWEEN 1 AND 12),
     r AS (SELECT activo_fijo_id, min(m) m0, max(m) m1, count(DISTINCT m) md FROM d GROUP BY 1)
SELECT CASE WHEN (m1-m0+1)-md = 0 THEN 'sin huecos'
            WHEN (m1-m0+1)-md <= 3 THEN '1-3 meses faltantes'
            WHEN (m1-m0+1)-md <= 12 THEN '4-12 meses'
            WHEN (m1-m0+1)-md <= 36 THEN '13-36 meses'
            ELSE '>36 meses' END,
       count(*), sum((m1-m0+1)-md)
FROM r GROUP BY 1;
```

| Grupo | Activos | Meses faltantes |
|---|---:|---:|
| Sin huecos | 146 | 0 |
| 1–3 meses faltantes | 484 | 931 |
| 4–12 meses | 16 | 127 |
| 13–36 meses | 8 | 150 |
| > 36 meses | 2 | 116 |
| **Total** | **656** | **1,324** |

**Sí hay huecos, pero son menudos.** El caso típico son 1–3 meses sueltos (484 activos, el 74 %).
Los huecos grandes son excepcionales. Ejemplos:

| id | Código | Descripción | Compra | Primera | Última | Filas | Rango (meses) | Faltan |
|---:|---|---|---|---|---|---:|---:|---:|
| 402 | INV-02-03-62 | Aire acondicionado | 2008-08-27 | 2017-00 | 2025-09 | 30 | 106 | **76** |
| 593 | INV-02-03-04 | Mini split 12 mil BTU | 2019-06-11 | 2016-06 | 2025-09 | 72 | 112 | 40 |
| 610 | INV-02-02-36 | Isuzu D-Max pick up | 2020-04-16 | 2016-10 | 2025-09 | 72 | 108 | 36 |
| 557 | INV-01-07-11 | Impresora Epson multifuncional | 2018-06-23 | 2016-06 | 2025-09 | 90 | 112 | 22 |
| 336 | INV-01-07-16 | Extintor de 10 lbs | 2015-12-31 | 2016-06 | 2025-09 | 94 | 112 | 18 |

Nótese en los ids 593 y 610: **el detalle empieza ANTES de la fecha de compra**. Y el id 402
tiene filas con `mes = 0` ("2017-00"). Son inconsistencias de la migración, no del negocio.

**El hueco temporal masivo no está dentro del rango, sino antes de él.** El detalle no existe
antes de 2016:

```sql
SELECT anio, count(*) filas, count(DISTINCT activo_fijo_id) activos, sum(valor_depreciado)
FROM af_activo_fijo_depreciacion WHERE company_id=2 GROUP BY anio ORDER BY anio;
```

| Año | Filas | Activos | Monto |
|---:|---:|---:|---:|
| 0 | 3 | 0 | 0.00 |
| 2016 | 1,222 | 167 | 611,429.76 |
| 2017 | 2,778 | 272 | 1,021,964.93 |
| 2018 | 3,806 | 378 | 1,157,833.09 |
| 2019 | 4,276 | 375 | 1,004,969.09 |
| 2020 | 4,729 | 433 | 1,095,545.55 |
| 2021 | 5,356 | 449 | 1,141,312.67 |
| 2022 | 5,186 | 472 | 1,066,020.21 |
| 2023 | 5,542 | 523 | 1,062,620.34 |
| 2024 | 6,554 | 571 | 1,281,517.37 |
| 2025 | 5,127 | 591 | 925,018.22 |

De los 167 activos que arrancan en 2016, **119 lo hacen exactamente en junio**. No hay una sola
fila anterior a 2016-06 para ningún activo, ni siquiera para los comprados en 1989 o 2002.

---

## 4. Coherencia interna del detalle mensual

### 4a. La última fila contra el `valor_libros` del maestro

```sql
WITH ult AS (
  SELECT DISTINCT ON (activo_fijo_id) activo_fijo_id, anio, mes, valor_neto_libros
  FROM af_activo_fijo_depreciacion WHERE company_id=2 AND activo_fijo_id IS NOT NULL AND anio>0
  ORDER BY activo_fijo_id, anio DESC, mes DESC, id DESC)
SELECT CASE WHEN abs(u.valor_neto_libros - a.valor_libros) < 0.01 THEN '1. cuadra' ... END,
       count(*), sum(u.valor_neto_libros - a.valor_libros)
FROM ult u JOIN af_activo_fijo a ON a.id=u.activo_fijo_id GROUP BY 1;
```

| Grupo | Activos | Dif. neta |
|---|---:|---:|
| Cuadra exacto | **610** | 0.00 |
| Dif ≤ L.1 | 6 | 0.01 |
| Dif ≤ L.100 | 15 | −274.75 |
| Dif ≤ L.10,000 | 21 | 8,925.85 |
| Dif > L.10,000 | 4 | 481,018.48 |

**610 de 656 (93 %) cierran perfecto.** El saldo final del detalle coincide al centavo con el
maestro. Esto es decisivo: **el detalle no está mal, está incompleto por el frente**.

Los 40 con desvío ≥ L.1 (L. 489,669.58 en total) son casi todos activos cuyo detalle se corta en
2018 o antes (39 de 40) mientras el maestro siguió depreciando:

| id | Código | Última fila | VNL última fila | `valor_libros` maestro | Dif. |
|---:|---|---|---:|---:|---:|
| 112 | INV-02-03-33 | 2018-04 | 412,741.21 | 0.00 | 412,741.21 |
| 210 | INV-01-06-18 | 2018-01 | 32,268.31 | 0.00 | 32,268.31 |
| 338 | INV-02-02-30 | 2018-01 | 25,681.83 | 299.00 | 25,382.83 |
| 14 | INV-01-01-14 | 2017-07 | 10,804.91 | 178.78 | 10,626.13 |
| 115 | INV-01-06-15 | 2017-07 | 7.73 | 7,453.96 | −7,446.23 |

### 4b. El encadenamiento fila a fila

¿Se cumple `VNL(t) = VNL(t−1) − valor_depreciado(t)`?

```sql
WITH s AS (
  SELECT activo_fijo_id, anio, mes, valor_depreciado, valor_neto_libros,
         lag(valor_neto_libros) OVER (PARTITION BY activo_fijo_id ORDER BY anio, mes, id) AS vnl_prev
  FROM af_activo_fijo_depreciacion WHERE company_id=2 AND activo_fijo_id IS NOT NULL AND anio>0)
SELECT count(*) FILTER (WHERE vnl_prev IS NOT NULL) AS comparables,
       count(*) FILTER (WHERE abs(vnl_prev - valor_depreciado - valor_neto_libros) < 0.01) AS encadenan,
       count(*) FILTER (WHERE abs(vnl_prev - valor_depreciado - valor_neto_libros) >= 0.01) AS rompen,
       count(*) FILTER (WHERE valor_neto_libros > vnl_prev + 0.01) AS vnl_sube
FROM s;
```

| Concepto | Valor |
|---|---:|
| Filas comparables | 43,131 |
| **Encadenan bien** | **41,802 (96.9 %)** |
| Rompen la cadena | 1,329 (3.1 %) |
| Filas donde el VNL **sube** | 198 (en 179 activos, +L. 1,296,766.54) |

Desglose de las 1,329 rupturas:

| Clase | Filas | Monto |
|---|---:|---:|
| Cayó de más (ajuste de bajada no registrado) | 810 | +1,305,784.78 |
| Cayó de menos o subió (ajuste de alza) | 519 | −1,373,759.81 |
| **Neto** | **1,329** | **−67,975.03** |

### 4c. Ejemplo canónico: activo 168 (Retroexcavadora John Deere)

```sql
SELECT anio, mes, fecha_depreciacion, valor_depreciado, valor_neto_libros,
       valor_neto_libros - lag(valor_neto_libros) OVER (ORDER BY anio,mes,id) AS delta_vnl
FROM af_activo_fijo_depreciacion WHERE company_id=2 AND activo_fijo_id=168 ORDER BY anio, mes, id;
```

| Año-Mes | `valor_depreciado` | `valor_neto_libros` | Δ VNL | Comentario |
|---|---:|---:|---:|---|
| 2016-06 | 11,952.16 | 887,857.69 | — | primera fila; la compra fue de L. 1,448,746.48 |
| 2016-07 … 2017-04 | 11,952.16 | ↓ regular | −11,952.16 | encadena bien |
| **2017-05** | 11,952.16 | 732,461.17 | **−36,273.33** | cayó 24,321.17 de más |
| 2017-06 … 2017-11 | 11,952.16 | ↓ regular | −11,952.16 | encadena bien |
| **2017-12** | 11,155.35 | **122,853.71** | **−537,894.50** | cayó **526,739.15** de más |
| 2018-01 | 13,147.38 | 108,909.52 | −13,944.19 | 796.81 de más |
| **2018-02** | 11,553.75 | 67,873.78 | **−41,035.74** | cayó 29,481.99 de más |
| 2018-03 … 2018-07 | 11,952.16 | ↓ hasta 8,112.98 | −11,952.16 | encadena bien |
| 2018-08, 2018-09 | 0.00 | 8,112.98 | 0.00 | ya agotado (valor de rescate) |

Este activo lo dice todo:
- Arranca con VNL 887,857.69 sobre una compra de 1,448,746.48 → **L. 548,936.63 de depreciación
  ya acumulada que nunca se detalló** (los años 2008–2016).
- Los cuatro saltos suman L. 581,339.12 de caída de saldo que **el SIMAFI legacy escribió
  directamente en `valor_neto_libros` sin registrarlo en `valor_depreciado`**.
- 548,936.63 + 581,339.12 = **1,130,275.75**, exactamente el hueco de este activo.

### 4d. Ejemplo de contraste: activo 182 (Retroexcavadora Caterpillar)

Es el único activo comprado dentro de la ventana (2016-04) y con `fecha_inicio_depreciacion`
poblada. Su detalle va de 2016-06 a 2025-09, 110 filas, todas de L. 16,209.39 exactas, con el VNL
bajando sin un solo salto, desde 1,916,146.82 hasta 128,251.05 — que es **exactamente** el
`valor_libros` del maestro. Suma L. 1,793,298.83 contra un acumulado de L. 1,836,523.96 (dif. de
L. 43,225.13, correspondiente a los dos meses previos a la primera fila). **Cuando la vida entera
del activo cae dentro de la ventana del detalle, el detalle cuadra.**

---

## 5. Valores raros en el detalle

```sql
SELECT 'mes fuera de 1..12', count(*), sum(valor_depreciado) FROM af_activo_fijo_depreciacion WHERE company_id=2 AND (mes<1 OR mes>12)
UNION ALL SELECT 'anio = 0', ... UNION ALL SELECT 'valor_depreciado < 0', ... etc.
```

| Anomalía | Filas | Monto |
|---|---:|---:|
| `valor_depreciado < 0` | **0** | — |
| `valor_neto_libros < 0` | **0** | — |
| Fecha incoherente con `anio`/`mes` | **0** | — |
| `anio = 0` | 3 | 0.00 |
| `mes` fuera de 1..12 (los mismos 3) | 3 | 0.00 |
| `codigo_activo` nulo o vacío (los mismos 3) | 3 | 0.00 |
| `fecha_depreciacion` NULL (los mismos 3) | 3 | 0.00 |
| `valor_depreciado = 0` | 8,557 | 0.00 |

**Duplicados:**

```sql
WITH dup AS (SELECT activo_fijo_id, anio, mes, count(*) n
             FROM af_activo_fijo_depreciacion WHERE company_id=2 AND activo_fijo_id IS NOT NULL
             GROUP BY 1,2,3 HAVING count(*)>1)
SELECT count(*), sum(n-1) FROM dup;
```

→ **Cero duplicados** por (`activo_fijo_id`, `anio`, `mes`). Por `codigo_activo` solo aparece 1
combinación duplicada: las 3 filas basura de `anio=0`/`mes=0` con código vacío.

**Filas con FK apuntando a un código distinto:** 24 filas. Son sólo diferencia de
mayúsculas/minúsculas (`inv-02-02-06` vs `INV-02-02-06`, `inv-02-03-11` vs `INV-02-03-11`).
Falso positivo, no requiere corrección de datos.

**Conclusión de este apartado: el detalle está limpio.** Ni negativos, ni duplicados, ni fechas
incoherentes. Sólo 3 filas basura de valor cero. Las 8,557 filas en cero son legítimas: activos ya
agotados que siguieron generando fila mensual con importe 0.

---

## 6. Filas sin `activo_fijo_id` y códigos huérfanos

```sql
SELECT count(*), count(DISTINCT codigo_activo), sum(valor_depreciado)
FROM af_activo_fijo_depreciacion WHERE company_id=2 AND activo_fijo_id IS NULL;

SELECT CASE WHEN a.id IS NULL THEN 'codigo NO existe en el maestro' ELSE 'FK perdida' END,
       count(*), count(DISTINCT d.codigo_activo), sum(d.valor_depreciado)
FROM af_activo_fijo_depreciacion d
LEFT JOIN af_activo_fijo a ON a.company_id=d.company_id AND a.codigo_activo=d.codigo_activo
WHERE d.company_id=2 AND d.activo_fijo_id IS NULL GROUP BY 1;
```

| Concepto | Valor |
|---|---:|
| Filas sin FK | **792** |
| Códigos distintos | **80** |
| Monto | **L. 91,964.79** |
| ¿Existe el código en el maestro? | **Ninguno.** Las 792 filas apuntan a códigos que no están en `af_activo_fijo` |

Por año:

| Año | Filas | Códigos | Monto | % |
|---:|---:|---:|---:|---:|
| 0 | 3 | 0 | 0.00 | — |
| 2016 | 318 | 69 | 54,216.27 | 59.0 % |
| 2017 | 396 | 65 | 25,290.63 | 27.5 % |
| 2018 | 24 | 2 | 2,174.66 | 2.4 % |
| 2019 | 45 | 5 | 3,984.91 | 4.3 % |
| 2021 | 6 | 2 | 6,298.32 | 6.8 % |

**El 86.5 % del monto huérfano es de 2016–2017.** Los códigos (INV-01-04-23, INV-01-01-172,
INV-01-04-21, INV-01-01-166…) tienen descripciones normales — aires acondicionados, computadoras,
UPS. Son **activos que existieron en SIMAFI, se dieron de baja antes del corte de migración y por
eso no viajaron al maestro, pero su histórico de depreciación sí viajó**.

Ojo: el FK no está perdido, el maestro nunca tuvo esos activos. No hay nada que "re-enganchar".

Impacto: **L. 91,964.79 sobran en el detalle** (depreciación de activos que ya no existen). No
suman al hueco, lo reducen: es la diferencia entre el hueco bruto (5,680,678.55) y el hueco contra
el detalle enganchado (5,772,643.34).

---

## 7. Descomposición exacta del hueco

Para cada activo con detalle, el hueco se descompone algebraicamente en cuatro términos:

```
hueco = maestro.valor_depreciado − Σ(detalle.valor_depreciado)

  T1 = valor_compra − (VNL_primera_fila + depreciado_primera_fila)      depreciación anterior al detalle
  T2 = (VNL_inicial_bruto − VNL_final) − Σ(detalle.valor_depreciado)    ajustes de VNL no registrados
  T3 = VNL_final − maestro.valor_libros                                 desvío de cierre
  T4 = valor_compra − valor_libros − valor_depreciado                   desvío interno del maestro

  hueco = T1 + T2 + T3 − T4
```

```sql
WITH d AS (SELECT activo_fijo_id, sum(valor_depreciado) S FROM af_activo_fijo_depreciacion
           WHERE company_id=2 AND activo_fijo_id IS NOT NULL AND anio>0 GROUP BY 1),
     p AS (SELECT DISTINCT ON (activo_fijo_id) activo_fijo_id, valor_neto_libros vnl0, valor_depreciado dep0
           FROM af_activo_fijo_depreciacion WHERE company_id=2 AND activo_fijo_id IS NOT NULL AND anio>0
           ORDER BY activo_fijo_id, anio, mes, id),
     u AS (SELECT DISTINCT ON (activo_fijo_id) activo_fijo_id, valor_neto_libros vnlf
           FROM af_activo_fijo_depreciacion WHERE company_id=2 AND activo_fijo_id IS NOT NULL AND anio>0
           ORDER BY activo_fijo_id, anio DESC, mes DESC, id DESC)
SELECT count(*),
       sum(a.valor_depreciado - d.S)                             AS hueco,
       sum(a.valor_compra - (p.vnl0 + p.dep0))                   AS t1,
       sum((p.vnl0 + p.dep0 - u.vnlf) - d.S)                     AS t2,
       sum(u.vnlf - a.valor_libros)                              AS t3,
       sum(a.valor_compra - a.valor_libros - a.valor_depreciado) AS t4
FROM af_activo_fijo a JOIN d ON d.activo_fijo_id=a.id JOIN p ON p.activo_fijo_id=a.id
     JOIN u ON u.activo_fijo_id=a.id WHERE a.company_id=2;
```

Resultado sobre los 656 activos con detalle:

| Término | Monto | % del hueco de los 656 |
|---|---:|---:|
| **T1 — depreciación anterior a la primera fila del detalle** | **3,772,863.18** | **89.9 %** |
| T2 — ajustes de VNL no registrados (neto) | −67,975.03 | −1.6 % |
| T3 — desvío de cierre (detalle truncado antes que el maestro) | 489,669.59 | 11.7 % |
| T4 — desvío interno del maestro | −2.55 | 0.0 % |
| **Suma de términos** | **4,194,560.29** | ✔ cuadra al centavo con el hueco |

**Reconciliación completa del hueco de L. 5,772,643.34:**

| Bloque | Monto | % |
|---|---:|---:|
| T1 · Depreciación previa al arranque del detalle (656 activos) | 3,772,863.18 | 65.4 % |
| Activos sin ninguna fila de detalle (174 activos) | 1,578,083.05 | 27.3 % |
| T3 · Detalle truncado antes que el maestro (40 activos) | 489,669.59 | 8.5 % |
| T2 · Ajustes de VNL no registrados (neto) | −67,975.03 | −1.2 % |
| T4 · Desvío interno del maestro | −2.55 | 0.0 % |
| **Total** | **5,772,643.34** | **100 %** |

Cierra al centavo. Contraste de sanidad con un cálculo independiente: la depreciación teórica no
detallada (meses entre `fecha_compra` y la primera fila × `depreciacion_mensual`) da
**L. 4,893,876.04** sobre 406 activos cuyo detalle arranca después de la compra — del mismo orden
que T1 + los sin detalle, lo que corrobora la lectura.

T1 por año de compra:

| Año de compra | Activos | T1 |
|---:|---:|---:|
| ≤ 2007 | 22 | 594,219.99 |
| 2008 | 16 | 751,235.64 |
| 2009–2012 | 73 | 381,436.63 |
| 2013 | 46 | 859,904.66 |
| 2014–2015 | 45 | 178,175.51 |
| 2016 | 93 | 272,821.59 |
| 2017–2019 | 147 | 234,691.83 |
| 2020 | 66 | 503,146.09 |
| 2021 | 26 | −2,768.76 |
| 2022–2025 | 122 | 0.00 |

Los activos comprados desde 2022 tienen **T1 = 0**: nacieron dentro de la ventana y su detalle es
íntegro. El T1 de 2020 (L. 503,146.09) está dominado por un solo caso anómalo, el id 610
(Isuzu D-Max, compra 2020-04-16, primera fila del detalle 2016-10 — imposible), que aporta
L. 503,056.16 él solo. Hay 14 activos con T1 negativo (−L. 31,606.72): su VNL inicial es **mayor**
que el valor de compra.

---

## 8. Estado actual del maestro (relevante para reanudar la depreciación)

```sql
SELECT count(*) FILTER (WHERE valor_libros > 0.01 AND NOT descargado AND NOT vendido),
       sum(valor_libros) FILTER (WHERE valor_libros > 0.01 AND NOT descargado AND NOT vendido),
       count(*) FILTER (WHERE depreciar),
       count(*) FILTER (WHERE fecha_inicio_depreciacion IS NULL),
       count(*) FILTER (WHERE metodo_depreciacion_id IS NULL)
FROM af_activo_fijo WHERE company_id=2;
```

| Concepto | Valor |
|---|---:|
| Activos vivos con saldo por depreciar | 800 |
| Saldo pendiente de depreciar | L. 5,420,045.74 |
| Marcados `depreciar = true` | 593 |
| **Sin `fecha_inicio_depreciacion`** | **827 de 830** |
| **Sin `metodo_depreciacion_id`** | **827 de 830** |
| Sobredepreciados (`valor_depreciado > valor_compra − valor_rescate`) | 200, exceso L. 59,439.11 |
| Con `valor_rescate ≠ 0` | 824 |
| Con `valor_libros < 0` | 0 |
| `valor_libros = valor_compra − valor_depreciado` | 642 cuadran / 188 no (desvío neto −L. 3,740.07) |

**Último período del detalle: 2025-09** (570 filas). A la fecha de este análisis (2026-09-08) el
histórico está **un año entero desactualizado**. Y todo el maestro fue creado el 2026-09-08 — la
migración corrió hoy.

---

## 9. Hipótesis sobre el origen del descuadre

### La conclusión central

**No hay un error de datos que corregir. El detalle no está mal: está incompleto por el frente.**

El histórico `af_activo_fijo_depreciacion` es un **extracto parcial** que arranca en **junio de
2016**, mientras que el maestro trae el acumulado de toda la vida de los activos, algunos comprados
en 1989. La evidencia converge desde cinco ángulos independientes:

1. **No existe una sola fila anterior a 2016-06** para ningún activo, ni siquiera para los
   comprados en 1989 o 2002. De los 167 activos que arrancan en 2016, 119 lo hacen exactamente en
   junio.
2. **El 93 % de los activos con detalle cierra al centavo** contra el `valor_libros` del maestro
   (610 de 656). El saldo final es correcto; lo que falta es el pasado.
3. **El 97 % de las filas encadena perfectamente** (`VNL(t) = VNL(t−1) − depreciado(t)`).
4. **Los activos nacidos dentro de la ventana cuadran** — cero T1 para todos los comprados desde
   2022; el id 182 (comprado 2016-04) tiene 110 filas idénticas sin un solo salto.
5. **La descomposición algebraica cierra al centavo**, con T1 aportando el 89.9 % del hueco de los
   activos con detalle.

Esto es exactamente lo que se espera de una migración desde SIMAFI que trajo el **saldo** del
maestro completo, pero del **libro auxiliar** sólo lo que el sistema legacy conservaba en línea
(una ventana de ~9 años).

### Los tres mecanismos secundarios

- **Detalle perdido en la migración (L. 1,578,083.05).** 115 de los 174 activos sin detalle
  declaran haber depreciado dentro de la ventana 2016–2026 pero no tienen ni una fila. 91 de ellos
  con última depreciación en 2016–2017. Aquí sí hay pérdida real de información, no sólo corte
  temporal.
- **Ajustes de saldo del legacy no registrados (±L. 1.3 M brutos, −L. 67,975.03 neto).** En 1,329
  filas el SIMAFI reescribió `valor_neto_libros` sin registrar el movimiento correspondiente en
  `valor_depreciado` — el caso del activo 168 en 2017-12 (−L. 537,894.50 de un golpe con
  `valor_depreciado = 11,155.35`) es el ejemplo canónico. Probablemente revaluaciones, ajustes de
  cierre fiscal o correcciones manuales. Neto casi se anula, pero **la columna `valor_depreciado`
  del detalle no es una serie de movimientos íntegra**: hay que tratarla como informativa, no como
  el libro auxiliar auditable.
- **Detalle truncado antes que el maestro (L. 489,669.59, 40 activos).** El detalle se corta en
  2017–2018 mientras el maestro siguió depreciando hasta agotar. 65 activos tienen su última fila
  antes de 2025 (2 en 2016, 25 en 2017, 37 en 2018, 1 en 2020). El caso mayor es el id 112 (tanque
  Ariete): 2 filas de detalle contra L. 422,977.33 de acumulado.

### Qué haría falta antes de reanudar la depreciación

**Bloqueantes duros (sin esto el motor no puede correr):**

1. **827 de 830 activos no tienen `metodo_depreciacion_id` ni `fecha_inicio_depreciacion`.** El
   motor no tiene de dónde derivar la regla de cálculo. Hay que decidir si se poblan por defecto
   (línea recta desde `fecha_compra`) o si el contador debe revisarlos. Es el bloqueante más
   grande, independiente del descuadre.
2. **Fijar cuál columna es la fuente de verdad del acumulado.** Hoy `valor_depreciado` lo es y
   `depreciacion_acumulada` está en 0 salvo en 2 activos. O se sincronizan (poblando
   `depreciacion_acumulada = valor_depreciado` en los 828 migrados), o se documenta que
   `depreciacion_acumulada` es una columna muerta y todo el código lee `valor_depreciado`.
   Convivir con ambas es la receta para el próximo descuadre.
3. **Corregir la lectura de `valor_a_depreciar`.** Es la cuota anual, no la base depreciable
   (827/830). Cualquier fórmula del motor que la use como base producirá cifras erróneas de
   inmediato.

**Decisiones del contador (no técnicas):**

4. **¿Se acepta el corte de 2016-06 como saldo de apertura?** La recomendación técnica es sí:
   registrar los L. 5,772,643.34 de hueco como **depreciación de apertura pre-2016** en una fila
   sintética por activo (o en una tabla de saldos iniciales), y declarar el detalle desde 2016-06
   como el único libro auxiliar auditable. Reconstruir 27 años de detalle mes a mes para activos de
   1989 no es viable ni útil: el saldo final ya es correcto en el 93 % de los casos.
5. **Los 200 activos sobredepreciados** (exceso L. 59,439.11 sobre `valor_compra − valor_rescate`).
   Hay que decidir si se topan al valor de rescate antes de arrancar, o el motor los volverá a
   pasar de largo.
6. **Los 40 activos con detalle truncado** (L. 489,669.59): decidir si se cierra el hueco con una
   fila de ajuste al último período o se deja como diferencia de apertura.

**Higiene de datos (barato, hacerlo antes de arrancar):**

7. Borrar las **3 filas basura** (`anio=0`, `mes=0`, código vacío, valor 0).
8. Decidir el destino de las **792 filas huérfanas / 80 códigos** (L. 91,964.79, 86 % de
   2016–2017): son activos dados de baja antes de la migración. Lo lógico es marcarlas o moverlas
   a un archivo histórico, no dejarlas en la tabla activa sin FK — hoy inflan el detalle contra un
   maestro que no las tiene.
9. **Normalizar los códigos a mayúsculas** en el detalle (24 filas en minúsculas). Cosmético, pero
   evita falsos positivos en cualquier reconciliación futura por código.
10. **Revisar los 8 activos con detalle anterior a su `fecha_compra`** (ids 610, 593, 527, 59…) y
    los **14 con VNL inicial mayor que el valor de compra** (−L. 31,606.72). Son inconsistencias
    puntuales de la migración; el id 610 solo aporta L. 503,056.16 de T1 espurio.

**Antes de la primera corrida:**

11. El detalle llega a **2025-09** y hoy es **2026-09-08**: falta un año completo. Hay que definir
    si el motor arranca en 2025-10 y recupera los 12 meses faltantes, o si se toma 2026-09 como
    período de arranque y el año intermedio se registra como ajuste. **Esta decisión es
    independiente del descuadre histórico y es la que realmente bloquea la puesta en marcha.**

### Lo que NO hace falta

- No hay que buscar corrupción de datos: cero negativos, cero duplicados, cero fechas incoherentes.
- No hay que re-enganchar los 80 códigos huérfanos: nunca existieron en el maestro.
- No hay que reconstruir el detalle previo a 2016 para cuadrar el saldo: el saldo ya cuadra en el
  93 % de los activos. Reconstruirlo sería inventar movimientos que nadie puede auditar.

---

## Anexo · Consultas usadas

Todas las consultas de este documento son de solo lectura y se ejecutaron contra
`siad_v3_restore` (mirror local). Se listan íntegras en línea en cada sección; las principales son:

| § | Propósito |
|---|---|
| 0 | Totales globales de ambas tablas; verificación de `valor_a_depreciar` y `depreciacion_acumulada` |
| 1 | Distribución del descuadre por tramos de diferencia; top 20 activos por hueco |
| 2 | Activos sin detalle; desglose contra la fecha de corte 2016-06 |
| 3 | Huecos temporales (meses esperados vs. presentes); distribución anual del detalle |
| 4 | Última fila vs. `valor_libros`; encadenamiento `VNL(t) = VNL(t−1) − depreciado(t)`; historial completo de los activos 168 y 182 |
| 5 | Anomalías: negativos, fechas, duplicados por (`activo_fijo_id`, `anio`, `mes`) y por código |
| 6 | Filas sin FK: monto, años, existencia del código en el maestro, top 20 códigos |
| 7 | Descomposición algebraica T1/T2/T3/T4 del hueco; T1 por año de compra |
| 8 | Estado del maestro: `metodo_depreciacion_id`, `fecha_inicio_depreciacion`, sobredepreciados, último período |
