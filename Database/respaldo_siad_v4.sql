-- =============================================================================
-- respaldo_siad_v4.sql -- Respaldo completo de la base de datos siad_v4
-- =============================================================================
--
-- QUE HACE
--   1. Verifica que la sesion esta conectada a la base correcta (guardia).
--   2. Muestra un informe previo: version del servidor, tamano, tablas mas
--      grandes y sesiones abiertas.
--   3. Arma el comando pg_dump con nombre y fecha, lo escribe en un .cmd
--      auxiliar y lo ejecuta.
--   4. Respalda ademas los objetos globales (roles y tablespaces), que NO
--      viajan dentro del dump de una base.
--
-- COMO SE EJECUTA  (SOLO con psql; pgAdmin y DBeaver ignoran los meta-comandos)
--
--   $env:PGPASSWORD = '<clave>'
--   & "C:\Program Files\PostgreSQL\17\bin\psql.exe" -h 172.16.0.9 -p 5432 -U postgres -d siad_v4 -f Database\respaldo_siad_v4.sql
--
-- PARAMETROS OPCIONALES  (se pasan con -v nombre=valor)
--   destino          Carpeta de salida.
--                    Default: <repo>\Database\Backups  (ignorada por git)
--   etiqueta         Sufijo para el nombre del archivo, p.ej. antes_de_roles.
--   globales         si | no  Respaldar roles y tablespaces.  Default: si
--   bd_esperada      Guardia de seguridad.  Default: siad_v4
--   pg_dump_exe      Ruta del ejecutable pg_dump.
--   pg_dumpall_exe   Ruta del ejecutable pg_dumpall.
--
--   Ejemplo:
--   psql ... -v etiqueta=antes_de_permisos -v destino=D:/Respaldos -f Database\respaldo_siad_v4.sql
--
-- NOTAS IMPORTANTES
--   * El respaldo se escribe en la MAQUINA DONDE CORRE psql, no en el servidor.
--   * La clave NO va en este archivo. Usar PGPASSWORD o el archivo
--     %APPDATA%\postgresql\pgpass.conf.
--   * pg_dump debe ser de version igual o mayor a la del servidor.
--   * El formato es custom (.backup): comprimido y restaurable con pg_restore,
--     incluso objeto por objeto.
--   * El script solo lee: no bloquea escrituras ni modifica nada.
--   * Deja en la carpeta actual el archivo auxiliar _respaldo_siad_v4.cmd con
--     el comando exacto que se ejecuto (util si algo falla).
--
-- COMO SE RESTAURA (referencia)
--   pg_restore -h <host> -U postgres -d <bd_nueva> --no-owner --verbose "<archivo>.backup"
--   psql -h <host> -U postgres -d postgres -f "<archivo>_globales.sql"
-- =============================================================================

\set ON_ERROR_STOP on
\timing off

-- --- Parametros: valores por defecto si no se pasaron con -v -----------------

\if :{?destino}
\else
\set destino 'E:/Koala/Users/Dell/Documents/GitHub/Prestadoras/Database/Backups'
\endif

\if :{?etiqueta}
\else
\set etiqueta ''
\endif

\if :{?globales}
\else
\set globales 'si'
\endif

\if :{?bd_esperada}
\else
\set bd_esperada 'siad_v4'
\endif

\if :{?pg_dump_exe}
\else
\set pg_dump_exe 'C:/Program Files/PostgreSQL/17/bin/pg_dump.exe'
\endif

\if :{?pg_dumpall_exe}
\else
\set pg_dumpall_exe 'C:/Program Files/PostgreSQL/17/bin/pg_dumpall.exe'
\endif

-- --- Guardia: no respaldar una base distinta a la esperada -------------------

SELECT (current_database() = :'bd_esperada') AS es_la_base_esperada \gset

\if :es_la_base_esperada
\else
\echo ''
\echo '*******************************************************************'
\echo '  ABORTADO: la sesion NO esta conectada a la base esperada.'
\echo '  Esperada:' :bd_esperada
\echo '  Revise los parametros de conexion, o pase -v bd_esperada=<nombre>'
\echo '  si de verdad quiere respaldar otra base.'
\echo '*******************************************************************'
\echo ''
\q
\endif

-- --- Datos de la conexion actual (los hereda pg_dump) ------------------------

SELECT coalesce(host(inet_server_addr()), 'localhost') AS pg_host,
       coalesce(inet_server_port(), 5432)::text        AS pg_port,
       current_user                                    AS pg_user,
       current_database()                              AS pg_db
\gset

-- --- Nombre y rutas de salida ------------------------------------------------

SELECT current_database()
       || '_' || to_char(now(), 'YYYYMMDD_HH24MISS')
       || CASE WHEN :'etiqueta' = '' THEN '' ELSE '_' || :'etiqueta' END AS stamp
\gset

SELECT replace(rtrim(:'destino', '/\'), '/', '\')                                        AS dir_win,
       replace(rtrim(:'destino', '/\') || '/' || :'stamp' || '.backup', '/', '\')        AS ruta_backup,
       replace(rtrim(:'destino', '/\') || '/' || :'stamp' || '_globales.sql', '/', '\')  AS ruta_globales
\gset

-- --- Informe previo ----------------------------------------------------------

\echo ''
\echo '=============================================================='
\echo ' INFORME PREVIO'
\echo '=============================================================='

SELECT current_database()                                       AS base,
       current_setting('server_version')                        AS version_servidor,
       :'pg_host' || ':' || :'pg_port'                          AS servidor,
       current_user                                             AS usuario,
       pg_size_pretty(pg_database_size(current_database()))     AS tamano_en_disco;

SELECT count(*) FILTER (WHERE c.relkind = 'r')  AS tablas,
       count(*) FILTER (WHERE c.relkind = 'v')  AS vistas,
       count(*) FILTER (WHERE c.relkind = 'm')  AS vistas_materializadas,
       (SELECT count(*)
          FROM pg_proc p
          JOIN pg_namespace pn ON pn.oid = p.pronamespace
         WHERE pn.nspname NOT IN ('pg_catalog', 'information_schema')) AS funciones_y_sp
  FROM pg_class c
  JOIN pg_namespace n ON n.oid = c.relnamespace
 WHERE n.nspname NOT IN ('pg_catalog', 'information_schema')
   AND c.relkind IN ('r', 'v', 'm');

\echo ''
\echo '-- Diez tablas mas grandes --'
SELECT n.nspname || '.' || c.relname                     AS tabla,
       pg_size_pretty(pg_total_relation_size(c.oid))     AS tamano
  FROM pg_class c
  JOIN pg_namespace n ON n.oid = c.relnamespace
 WHERE c.relkind = 'r'
   AND n.nspname NOT IN ('pg_catalog', 'information_schema')
 ORDER BY pg_total_relation_size(c.oid) DESC
 LIMIT 10;

\echo ''
\echo '-- Sesiones abiertas contra esta base (el respaldo no las bloquea) --'
SELECT count(*)                                                AS sesiones,
       count(*) FILTER (WHERE state = 'idle in transaction')   AS en_transaccion_ociosa,
       coalesce(max(now() - xact_start), interval '0')         AS transaccion_mas_vieja
  FROM pg_stat_activity
 WHERE datname = current_database()
   AND pid <> pg_backend_pid();

-- --- Generacion del comando --------------------------------------------------
-- pg_dump es un programa externo: un archivo .sql no puede llamarlo por si solo.
-- Por eso se escribe aqui un .cmd con el comando exacto y despues se ejecuta.
-- Ojo: \! no interpola variables de psql, por eso el nombre del .cmd es fijo.

\pset format unaligned
\pset tuples_only on
\o _respaldo_siad_v4.cmd

SELECT linea
  FROM (VALUES
    ( 10, '@echo off'::text),
    ( 20, 'echo.'),
    ( 30, 'echo ============================================================'),
    ( 40, 'echo  RESPALDO DE ' || :'pg_db' || ' EN ' || :'pg_host' || ':' || :'pg_port' || ' (usuario ' || :'pg_user' || ')'),
    ( 50, 'echo  Archivo: ' || :'ruta_backup'),
    ( 60, 'echo ============================================================'),
    ( 70, 'echo.'),
    ( 80, 'if not exist "' || :'dir_win' || '" mkdir "' || :'dir_win' || '"'),
    ( 90, 'if not exist "' || :'dir_win' || '" echo *** ERROR: no se pudo crear la carpeta destino. ***'),
    (100, 'if not exist "' || :'dir_win' || '" exit /b 1'),
    (110, '"' || :'pg_dump_exe' || '" --version'),
    (120, 'echo.'),
    (130, '"' || :'pg_dump_exe' || '"'
           || ' --host=' || :'pg_host'
           || ' --port=' || :'pg_port'
           || ' --username=' || :'pg_user'
           || ' --dbname=' || :'pg_db'
           || ' --format=custom --verbose'
           || ' --file="' || :'ruta_backup' || '"'),
    (140, 'set "RC=%ERRORLEVEL%"'),
    (150, 'if not "%RC%"=="0" echo *** ERROR: pg_dump termino con codigo %RC%. Se descarta el archivo parcial. ***'),
    (160, 'if not "%RC%"=="0" if exist "' || :'ruta_backup' || '" del /q "' || :'ruta_backup' || '"'),
    (170, 'if not "%RC%"=="0" exit /b %RC%'),
    (180, 'echo.'),
    (190, 'echo Respaldo de la base terminado:'),
    (200, 'dir "' || :'ruta_backup' || '"'),
    (210, 'echo.'),
    (220, 'echo Respaldando objetos globales (roles y tablespaces)...'),
    (230, '"' || :'pg_dumpall_exe' || '"'
           || ' --host=' || :'pg_host'
           || ' --port=' || :'pg_port'
           || ' --username=' || :'pg_user'
           || ' --globals-only'
           || ' --file="' || :'ruta_globales' || '"'),
    (240, 'if not "%ERRORLEVEL%"=="0" echo AVISO: los objetos globales no se respaldaron (requiere superusuario). El respaldo de la base SI se genero.'),
    (250, 'if exist "' || :'ruta_globales' || '" dir "' || :'ruta_globales' || '"'),
    (300, 'echo.'),
    (310, 'echo Listo.')
  ) AS t(n, linea)
 WHERE n NOT BETWEEN 210 AND 250
    OR lower(:'globales') IN ('si', 's', 'yes', 'y', 'true', '1')
 ORDER BY n;

\o
\pset tuples_only off
\pset format aligned

-- --- Ejecucion ---------------------------------------------------------------

\echo ''
\echo '=============================================================='
\echo ' EJECUTANDO EL RESPALDO'
\echo '=============================================================='
\echo 'Destino:' :ruta_backup
\echo 'Puede tardar varios minutos. No cierre la ventana.'
\echo ''

\! _respaldo_siad_v4.cmd

\echo ''
\echo 'Fin del script. El comando ejecutado quedo en _respaldo_siad_v4.cmd'
\echo 'Si pg_dump pidio contrasena y no la recibio, defina PGPASSWORD y repita.'
\echo ''
