# Runbook de despliegue a SRV — El informe de saldos por categoría deja de morir por tiempo de espera

**Base destino:** `siad_v4` @ `172.16.0.9`
**Fecha:** 2026-09-04
**Alcance:** 1 script. `CREATE OR REPLACE` de una sola función de reportería.
**Origen:** error HTTP 500 reportado por el usuario en `/informes/cobranzas/saldo-clientes-categoria`, diagnosticado el 2026-09-04 contra `siad_v4` en sesión de solo lectura.

> ✅ **Esta tanda NO depende de las tandas pendientes.** Se verificó en `siad_v4` que la
> función `rep_saldo_clientes_categoria_cobranza(bigint,date,date,integer)` y la vista
> `vw_rep_movimiento_vigente` **ya existen arriba**. El script solo reemplaza el cuerpo de
> esa función, así que puede aplicarse antes, después o entre las tandas pendientes.
>
> ⚠️ **Tandas anteriores aún pendientes** y que esta **no reemplaza ni cierra**: `2026-08-22`
> (CxP unificada), `2026-08-27` (control presupuestario, 5 scripts), `2026-08-31` (aprobación
> por niveles, 3 scripts) y `2026-09-01` (disponible sin truncar).
>
> ✅ **APLICADO en `siad_v4` el 2026-09-05.** Ver §6.
>
> ⚠️ **El SQL por sí solo no cierra el incidente.** Hace falta además desplegar el cambio de
> `SIAD.Reports` que fija el tiempo de espera de los informes. Ver §7.
>
> ⚠️ La base **ACTIVA es `siad_v4`**, no `siad_v3`.

---

## 1. Qué cubre este runbook

El informe respondía con la página de error genérica del portal. No faltaba ningún objeto en
la base: la consulta tardaba **97 segundos** y Npgsql la cancelaba a los **30**, que es su
valor por omisión cuando nadie configura otro. El log de `siad_v4` registró el 2026-09-04
cuatro cancelaciones de la sentencia del reporte, como
`cancelando la sentencia debido a una petición del usuario`.

| Causa medida con `EXPLAIN ANALYZE` | Corrección |
|---|---|
| La función recorría `vw_rep_movimiento_vigente` **dos veces**, en los CTE `ultimo_saldo_anterior` y `movimientos_periodo`. Cada recorrido son 19.5 M de filas; en el plan costaron 63 s y 28 s | Un solo recorrido, con agregados `FILTER` |
| Ambos CTE filtraban con `CROSS JOIN parametros p … WHERE ta.company_id = p.company_id`. Al depender de otra relación, el planificador **no podía empujar** `company_id` ni la fecha dentro de las ramas del `UNION ALL` de la vista, y la materializaba entera | Los predicados usan los parámetros de la función directamente |

| Medición en `siad_v4` | Tiempo |
|---|---:|
| Antes, versión original | 82 a 97 s |
| Después, primera corrida con buffers fríos | 41 s |
| Después, corridas siguientes | 19 a 20 s |

Medido el 2026-09-05 tras aplicar. El rango de un año tarda lo mismo que el del mes corriente.

**No se toca `vw_rep_movimiento_vigente`.** Se probó que no hace falta: la mejora sale entera
del lado de la función, y esa vista la consumen otros módulos.

## 2. Antes de empezar (obligatorio)

**Backup de `siad_v4`:**

```bash
pg_dump -h 172.16.0.9 -U postgres -d siad_v4 -Fc -f siad_v4_antes_perf_saldo_categoria.backup
```

**Definir la conexión** (la clave no va en el repo):

```bash
export SRV="postgresql://USUARIO:CLAVE@172.16.0.9:5432/siad_v4"
```

**Confirmar la base antes de escribir nada:**

```bash
psql "$SRV" -c "SELECT current_database(), pg_size_pretty(pg_database_size(current_database()));"
```

Debe decir `siad_v4`. Si dice `siad_v3`, **parar**.

**Comprobar el prerrequisito** — los dos objetos deben existir ya:

```sql
SELECT to_regproc('public.rep_saldo_clientes_categoria_cobranza(bigint,date,date,integer)') AS funcion,
       to_regclass('public.vw_rep_movimiento_vigente')                                      AS vista;
```

Ambos deben devolver nombre. Al 2026-09-04 **los dos existían** en `siad_v4`.

## 3. Advertencias clave (leer antes de aplicar)

- El script es **re-ejecutable**: un solo `CREATE OR REPLACE FUNCTION` más su `COMMENT`.
  No hay `DROP`, `DELETE`, `TRUNCATE` ni `UPDATE`. **No modifica ni una fila.**
- **No cambia la firma ni el tipo de retorno.** Las 18 columnas y los 4 parámetros quedan
  idénticos, así que la plantilla DevExpress no necesita tocarse ni regenerar su esquema.
- **No cambia los resultados.** Se comparó la versión nueva contra la instalada dentro de una
  misma transacción `REPEATABLE READ READ ONLY` en `siad_v4`: las 8 categorías salieron
  idénticas en las 18 columnas. Verificación reproducible en §5.
- ⚠️ Ojo al comparar a mano en horario laboral: `siad_v4` es una base viva. Durante el
  diagnóstico, dos corridas separadas difirieron en 10,000.00 porque entró un pago real entre
  medio. **Comparar siempre dentro de una misma transacción**, no en dos corridas sueltas.
- El script **no crea índices**. Los cuatro del script de rendimiento de reportes
  (`2026-07-30_uc_f7_h5c_perf_reportes.sql`) ya estaban aplicados en `siad_v4`.

## 4. Orden de aplicación (resumen)

| Paso | Script | Naturaleza | ¿Re-ejecutable? | Depende de |
|---:|---|---|:--:|---|
| 1 | `2026-09-04_perf_rep_saldo_clientes_categoria_cobranza.sql` | Objetos (1 función) | Sí | Nada pendiente. Solo que la función y la vista ya existan (§2) |

## 5. Detalle por paso

### Paso 1 — Un solo recorrido de los movimientos (`2026-09-04_perf_rep_saldo_clientes_categoria_cobranza.sql`)

Reemplaza el cuerpo de `rep_saldo_clientes_categoria_cobranza`: funde los dos CTE que
recorrían la vista en uno solo con `FILTER`, y quita el `CROSS JOIN parametros` de los
predicados para habilitar el pushdown.

```bash
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-09-04_perf_rep_saldo_clientes_categoria_cobranza.sql
```

**¿Ya aplicado?**

> ⚠️ Contar `vw_rep_movimiento_vigente` a secas **da un falso negativo**: el cuerpo nuevo
> nombra la vista también en sus comentarios. Hay que contar el `FROM` real.

```sql
SELECT (SELECT count(*) FROM regexp_matches(d, 'FROM public\.vw_rep_movimiento_vigente', 'g')) AS from_reales,
       (SELECT count(*) FROM regexp_matches(d, 'FILTER \(WHERE', 'g'))                          AS agregados_filter,
       (d LIKE '%PERF 2026-09-04%')                                                             AS lleva_marca_nueva
  FROM (SELECT pg_get_functiondef(
          'public.rep_saldo_clientes_categoria_cobranza(bigint,date,date,integer)'::regprocedure) AS d) x;
```

Esperado tras aplicar: `from_reales` = **1**, `agregados_filter` = **3**, `lleva_marca_nueva` = **t**.
Si `from_reales` devuelve **2**, todavía está la versión vieja.

**Verificación:**

```sql
-- a) Tiempo: debe bajar de ~97 s a ~28 s
\timing on
SELECT categoria, cant_total, facturacion_total, saldo_total
  FROM public.rep_saldo_clientes_categoria_cobranza(
         2, date_trunc('month', current_date)::date, current_date, 0)
 ORDER BY categoria_orden, categoria;

-- b) El plan ya NO materializa la vista entera:
--    el nodo Append debe quedar muy por debajo de los 19.5 M de filas
EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM public.rep_saldo_clientes_categoria_cobranza(
         2, date_trunc('month', current_date)::date, current_date, 0)
 ORDER BY categoria_orden, categoria;
```

**No-regresión (comparar contra el backup, si se quiere ser exhaustivo):** restaurar el
respaldo en una base aparte y correr la misma llamada en ambas; los totales por categoría
deben coincidir peso a peso. Esa equivalencia ya se verificó en `siad_v4` el 2026-09-04, en
una sola transacción, con resultado idéntico en las 8 categorías.

**Prueba funcional en el portal: ✅ HECHA Y APROBADA el 2026-09-05.** El usuario generó el
informe en el portal del servidor y **el PDF salió**. El log de `siad_v4` de ese día no
registra ninguna cancelación (el único `ERROR` que aparece es de una consulta de diagnóstico
que falló por codificación, no del portal). El informe pasó a leer ~151 M de filas en una
corrida que completó sin ser abortada.

⚠️ **Con el SQL solo, el informe queda al filo.** En caliente tarda 19 a 20 s y entra dentro
de los 30 s por omisión de Npgsql, pero la primera corrida del día, con los buffers fríos,
tardó **41 s** y ésa sí se cancelaría. Por eso el cambio de código de §7 sigue haciendo falta:
sin él, el informe funcionará casi siempre y fallará justo cuando nadie lo haya abierto en un
rato, que es el caso más probable a primera hora.

## 6. Estado presunto

| Base | Estado |
|---|---|
| `siad_v3_restore` (mirror, localhost) | ✅ **APLICADO** el 2026-09-08, exit 0 (`CREATE FUNCTION` + `COMMENT`), autorizado por el usuario. La huella `md5(prosrc)` de la función coincide exactamente con la de `siad_v4`. Se aplicó junto con los otros tres scripts de informes que le faltaban al mirror; ver `2026-09-08_runbook_despliegue_srv.md` §5 paso 3. Antes estaba sin aplicar: el diagnóstico y las mediciones originales se hicieron contra `siad_v4` en solo lectura, porque el problema de rendimiento solo se reproduce con el volumen de producción |
| `siad_v4` @ 172.16.0.9 (SRV) | ✅ **APLICADO** el 2026-09-05 por la mañana, exit 0 (`CREATE FUNCTION` + `COMMENT`), autorizado por el usuario. Verificado: `from_reales` = 1, `agregados_filter` = 3, marca presente; firma y 18 columnas de retorno intactas; las 8 categorías devuelven los mismos totales que antes |

Nunca se verifica conectándose a la BD desde aquí: el paso trae su consulta «¿ya aplicado?».

## 7. Cambio de código que acompaña a esta tanda

**Va con el despliegue del portal, no con el SQL, pero el incidente no se cierra sin él.**

Con la función optimizada la consulta baja a 28 s, y el valor por omisión de Npgsql son 30 s.
El margen es demasiado fino, y el resto de informes de cobranza siguen expuestos al mismo
corte. Por eso se fija explícitamente el tiempo de espera de todos los informes DevExpress:

- `SIAD.Reports/ReportingRuntimeBootstrap.cs` — `ConfigureSqlDataSources` fija
  `ConnectionOptions.DbCommandTimeout` en cada `SqlDataSource`. Se resuelve desde la
  configuración y cae en 180 segundos si no hay clave.
- `apc/appsettings.json` — nueva sección `Reportes:DbCommandTimeoutSegundos`, en 180.

Al ser el punto por donde pasan todos los informes, el ajuste cubre también los otros que el
log mostró cancelados el mismo día, entre ellos `rep_saldo_clientes_categoria`, el informe
equivalente del módulo de medición.

**Fuera de alcance a propósito:** `SIAD.Reports/Informes/ReportesDatasetService.cs` mantiene
su `CommandTimeout = 30`. Ese es el camino de la **vista previa** de datasets del catálogo,
no el de los informes, y para una vista previa 30 segundos es un tope razonable.

## 8. Versionado (git)

Archivos nuevos, **untracked** al 2026-09-04:

- `Database/2026-09-04_perf_rep_saldo_clientes_categoria_cobranza.sql`
- `Database/2026-09-04_runbook_despliegue_srv.md`

Cambios de código de la misma sesión (van con el despliegue del portal, ver §7):

- `SIAD.Reports/ReportingRuntimeBootstrap.cs`
- `apc/appsettings.json`
