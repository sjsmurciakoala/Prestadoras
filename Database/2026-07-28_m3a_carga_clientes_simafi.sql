-- =============================================================================
-- M3a — Carga de clientes SIMAFI → cliente_maestro (company_id = 2)
-- =============================================================================
-- Migración total SIMAFI. Ver docs/PLAN_MIGRACION_SIMAFI_TOTAL_2026-07.md
-- y docs/M2_VALIDACION_SALDOS_SIMAFI_2026-07.md
--
-- Requiere el esquema simafi_stg (M1) cargado en la misma base.
--
-- Idempotente: solo inserta las claves que aún no existen. Puede repetirse.
--
-- Mapeo derivado empíricamente de los 1,143 clientes del piloto de 2 ciclos
-- que ya cruzan por `clave`:
--   maestro_cliente_clave           <- clave            (con trim)
--   maestro_cliente_identidad       <- identidad
--   maestro_cliente_nombre          <- nombreinquilino
--   maestro_cliente_indicativo_ruta <- ruta
--   maestro_cliente_secuencia       <- secuencia
--   contador                        <- medidor
--   maestro_cliente_tiene_medidor   <- tiene_med   ('S')
--   maestro_cliente_tercera_edad    <- terceraedad ('S')
--   descuento_tercera_edad          <- descuento
--   estado                          <- estado      ('A' = activo)
--   ciclos_id                       <- ciclo       (solo si existe en public.ciclos)
--   categoria_servicio_id           <- categoria   (solo si existe en el catálogo)
--   maestro_cliente_tiene_contrato  <- tienecontrato (1)
--   maestro_cliente_tiene_convenio  <- planpago      (1)
--   maestro_cliente_fecha_baja      <- fechabaja
--
-- NO se migran (verificado: no hay fuente válida en SIMAFI):
--   maestro_cliente_rtn  — `rtm` no es un RTN (trae 'CONSTANCIA', 'SIN INFO.', '0')
--   barrio_codigo        — `sector` NO es el barrio: el catálogo del portal usa
--                          códigos de 3 dígitos con nombre propio y SIMAFI trae
--                          2 dígitos, vacío en 10,173 clientes. El piloto tampoco
--                          lo migró (3 de 1,147 poblados, y no coinciden).
--   tipo_uso_codigo      — vacío en los 25,766 de SIMAFI y en los 1,147 del piloto.
--
-- Quedan en NULL por no existir en catálogo:
--   ciclos_id             — 567 clientes con ciclo '0'
--   categoria_servicio_id — 1 cliente con categoría '0'
-- =============================================================================

BEGIN;

\echo '--- antes ---'
SELECT count(*) AS clientes_antes FROM public.cliente_maestro WHERE company_id = 2;

INSERT INTO public.cliente_maestro (
    maestro_cliente_clave,
    maestro_cliente_identidad,
    maestro_cliente_nombre,
    maestro_cliente_tercera_edad,
    descuento_tercera_edad,
    categoria_servicio_id,
    maestro_cliente_fecha_baja,
    maestro_cliente_indicativo_ruta,
    maestro_cliente_secuencia,
    estado,
    usuariocreacion,
    fechacreacion,
    ciclos_id,
    maestro_cliente_tiene_contrato,
    maestro_cliente_tiene_convenio,
    maestro_cliente_tiene_medidor,
    contador,
    company_id
)
SELECT
    trim(s.clave),
    coalesce(trim(s.identidad), ''),
    coalesce(trim(s.nombreinquilino), ''),
    (trim(coalesce(s.terceraedad, '')) = 'S'),
    s.descuento,
    (SELECT c.categoria_servicio_id
       FROM public.categoria_servicio c
      WHERE c.categoria_servicio_id::text = trim(s.categoria)),
    s.fechabaja,
    nullif(trim(coalesce(s.ruta, '')), ''),
    nullif(trim(coalesce(s.secuencia, '')), ''),
    (trim(coalesce(s.estado, '')) = 'A'),
    'migracion_simafi',
    now(),
    (SELECT c.ciclos_id
       FROM public.ciclos c
      WHERE c.ciclos_id::text = trim(s.ciclo)),
    (coalesce(s.tienecontrato, 0) = 1),
    (coalesce(s.planpago, 0) = 1),
    (trim(coalesce(s.tiene_med, '')) = 'S'),
    nullif(trim(coalesce(s.medidor, '')), ''),
    2
FROM simafi_stg.maestrosep s
WHERE NOT EXISTS (
    SELECT 1 FROM public.cliente_maestro p
     WHERE p.company_id = 2
       AND trim(p.maestro_cliente_clave) = trim(s.clave)
);

\echo '--- despues ---'
SELECT count(*) AS clientes_despues FROM public.cliente_maestro WHERE company_id = 2;

\echo '--- control: todos los de SIMAFI presentes? ---'
SELECT
    (SELECT count(DISTINCT trim(clave)) FROM simafi_stg.maestrosep)                        AS en_simafi,
    (SELECT count(*) FROM public.cliente_maestro WHERE company_id = 2)                     AS en_portal,
    (SELECT count(*) FROM simafi_stg.maestrosep s
      WHERE NOT EXISTS (SELECT 1 FROM public.cliente_maestro p
                         WHERE p.company_id = 2
                           AND trim(p.maestro_cliente_clave) = trim(s.clave)))              AS faltantes;

\echo '--- control: reparto de estado y catalogos ---'
SELECT
    count(*) FILTER (WHERE estado)                       AS activos,
    count(*) FILTER (WHERE NOT estado)                   AS inactivos,
    count(*) FILTER (WHERE ciclos_id IS NULL)            AS sin_ciclo,
    count(*) FILTER (WHERE categoria_servicio_id IS NULL) AS sin_categoria,
    count(*) FILTER (WHERE maestro_cliente_tiene_medidor) AS con_medidor
FROM public.cliente_maestro WHERE company_id = 2;

COMMIT;
