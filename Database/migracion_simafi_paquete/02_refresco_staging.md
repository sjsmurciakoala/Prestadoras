# 02 — Refresco del staging (M1): SIMAFI MySQL → simafi_stg

Solo para el **escenario B** (corrida completa con datos frescos) o si el
origen cambió desde el último volcado. Si el staging local ya está cuadrado
contra el origen (paso 01, bloque 1), saltar este paso.

## Regla de oro: TODO el volcado va en LATIN1

La base MySQL de SIMAFI es latin1. Si el dump se hace en UTF-8 "por defecto",
los nombres salen con signos y NO hay arreglo posterior confiable — se repite
el volcado. (Verificado en la sesión M1 de julio: ¡LATIN1 obligatorio!)

## Receta (por tabla; las 7 tablas del espejo)

1. En el servidor origen (credenciales de la sesión M1, fuera del repo):

```
mysqldump --default-character-set=latin1 --no-create-info --skip-triggers \
  --compact --complete-insert bdsimafi <tabla> > <tabla>.sql
```

   (Variante CSV si el dump SQL da problemas: `mysql --default-character-set=latin1
   -e "SELECT ... INTO OUTFILE ..."` — mismo charset.)

2. Convertir a UTF-8 ANTES de cargar a Postgres:

```
iconv -f LATIN1 -t UTF-8 <tabla>.sql > <tabla>.utf8.sql
```

3. Cargar a `simafi_stg.<tabla>` (truncar y recargar — nunca merge):

```
set PGCLIENTENCODING=UTF8
psql -d <base> -c "TRUNCATE simafi_stg.<tabla>;"
psql -d <base> -f <tabla>.utf8.sql       (adaptando INSERTs al esquema simafi_stg)
```

4. **Cuadre obligatorio contra el origen** antes de seguir: `SELECT count(*)`
   por tabla en MySQL vs `simafi_stg` — deben ser idénticos. El censo de
   referencia de julio: 18.1M filas totales, `facturacion` 1.22M.

## Trampa de las tablas archivadas

SIMAFI corta y renombra tablas con la fecha (`facturacion_YYYY…`). El ledger
completo = tabla viva + archivadas. Si el origen archivó de nuevo desde julio,
incluir la archivada nueva en el volcado y en la unificación del prep de M3b.
