-- ============================================================
-- 2026-03-30_wipe_dev_db_except_identity.sql
-- Objetivo:
--   Limpiar por completo la base funcional en entorno DEV,
--   preservando unicamente el esquema identity y el historial
--   tecnico de migraciones de EF en public.
--
-- Alcance:
--   - TRUNCATE de todas las tablas base del esquema public
--     excepto public.__EFMigrationsHistory
--   - RESTART IDENTITY para reiniciar IDs
--   - RESET adicional de todas las secuencias en public
--   - NO toca tablas del esquema identity
--
-- Uso:
--   Ejecutar unicamente en entorno de desarrollo.
-- ============================================================

BEGIN;

DO $$
DECLARE
    v_tables text;
    v_identity_users bigint;
    v_identity_roles bigint;
    v_public_tables bigint;
    v_seq record;
BEGIN
    SELECT COUNT(*)
      INTO v_identity_users
      FROM identity."AspNetUsers";

    SELECT COUNT(*)
      INTO v_identity_roles
      FROM identity."AspNetRoles";

    SELECT COUNT(*)
      INTO v_public_tables
      FROM information_schema.tables
     WHERE table_schema = 'public'
       AND table_type = 'BASE TABLE'
       AND table_name <> '__EFMigrationsHistory';

    RAISE NOTICE 'Wipe DEV iniciado. public tables=% identity users=% identity roles=%',
        v_public_tables, v_identity_users, v_identity_roles;

    SELECT string_agg(format('%I.%I', schemaname, tablename), ', ' ORDER BY tablename)
      INTO v_tables
      FROM pg_tables
     WHERE schemaname = 'public'
       AND tablename <> '__EFMigrationsHistory';

    IF v_tables IS NOT NULL THEN
        EXECUTE 'TRUNCATE TABLE ' || v_tables || ' RESTART IDENTITY CASCADE';
    END IF;

    FOR v_seq IN
        SELECT sequence_schema, sequence_name
          FROM information_schema.sequences
         WHERE sequence_schema = 'public'
    LOOP
        EXECUTE format(
            'ALTER SEQUENCE %I.%I RESTART WITH 1',
            v_seq.sequence_schema,
            v_seq.sequence_name);
    END LOOP;

    RAISE NOTICE 'Wipe DEV completado.';
END
$$;

COMMIT;

-- ============================================================
-- Verificacion posterior
-- ============================================================
DROP TABLE IF EXISTS tmp_public_counts;
CREATE TEMP TABLE tmp_public_counts (
    table_name text,
    row_count bigint
);

DO $$
DECLARE
    v_table record;
BEGIN
    FOR v_table IN
        SELECT table_name
          FROM information_schema.tables
         WHERE table_schema = 'public'
           AND table_type = 'BASE TABLE'
           AND table_name <> '__EFMigrationsHistory'
         ORDER BY table_name
    LOOP
        EXECUTE format(
            'INSERT INTO tmp_public_counts(table_name, row_count) SELECT %L, count(*) FROM public.%I',
            v_table.table_name,
            v_table.table_name);
    END LOOP;
END
$$;

SELECT
    'public'::text AS table_schema,
    'tablas_public_con_datos'::text AS table_name,
    COUNT(*)::bigint AS row_count
FROM tmp_public_counts
WHERE row_count > 0

UNION ALL

SELECT
    'identity',
    'AspNetUsers',
    COUNT(*)::bigint
FROM identity."AspNetUsers"

UNION ALL

SELECT
    'identity',
    'AspNetRoles',
    COUNT(*)::bigint
FROM identity."AspNetRoles"

UNION ALL

SELECT
    'public',
    '__EFMigrationsHistory',
    COUNT(*)::bigint
FROM public."__EFMigrationsHistory";

DROP TABLE IF EXISTS tmp_public_counts;



select * from public.cfg_company


select * from public.con_periodo_contable