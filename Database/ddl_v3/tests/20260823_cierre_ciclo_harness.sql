-- =============================================================================
-- Harness de sp_adm_periodo_ciclo_cerrar — validacion de folios sin confirmar
-- =============================================================================
-- No necesita la base de produccion: levanta las tablas minimas en un cluster
-- desechable.
--
--   initdb -D <tmp>\pgdata -U postgres -A trust
--   pg_ctl -D <tmp>\pgdata -o "-p 55433" start
--   createdb -h 127.0.0.1 -p 55433 -U postgres ciclotest
--
--   1) psql ... -d ciclotest -f Database/ddl_v3/tests/20260823_cierre_ciclo_harness.sql
--   2) psql ... -f Database/ddl_v3/20260823_cierre_ciclo_valida_folios_sin_confirmar.sql
--   3) psql ... -f Database/ddl_v3/tests/20260823_cierre_ciclo_verificacion.sql
-- =============================================================================

DO $$
BEGIN
    IF current_database() <> 'ciclotest' THEN
        RAISE EXCEPTION 'Correlo en la base ciclotest, no en %.', current_database();
    END IF;
END $$;

DROP SCHEMA IF EXISTS public CASCADE;
CREATE SCHEMA public;

CREATE TABLE public.adm_periodo_comercial (
    periodo_comercial_id bigint,
    company_id bigint,
    anio integer,
    mes integer,
    status_id smallint DEFAULT 1
);

CREATE TABLE public.adm_periodo_comercial_ciclo (
    periodo_ciclo_id bigint,
    company_id bigint,
    periodo_comercial_id bigint,
    ciclo_codigo varchar(10),
    status_id smallint DEFAULT 1,
    fecha_cierre timestamptz,
    cerrado_por varchar(100),
    updated_at timestamptz,
    updated_by varchar(100)
);

CREATE TABLE public.ciclos (ciclos_id integer, ciclos_codigo varchar(10));

CREATE TABLE public.cliente_maestro (
    company_id bigint,
    maestro_cliente_id bigint,
    maestro_cliente_clave varchar(20),
    ciclos_id integer
);

CREATE TABLE public.adm_cai_correlativo_emitido (
    cai_correlativo_emitido_id bigint,
    company_id bigint,
    cliente_id bigint,
    correlativo bigint,
    numero_factura varchar(80),
    factura_id bigint,
    estado_codigo varchar(30),
    status_id smallint DEFAULT 1,
    created_at timestamptz DEFAULT now(),
    created_by varchar(100) DEFAULT current_user
);

-- Stub: en produccion mira facturas por ruta. Acá siempre "sin pendientes",
-- para aislar la validacion nueva.
CREATE FUNCTION public.fn_adm_periodo_ciclo_rutas_pendientes(
    p_company_id bigint, p_periodo_ciclo_id bigint)
RETURNS TABLE (ruta_codigo varchar, pendiente boolean)
LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY SELECT NULL::varchar, false WHERE false;
END;
$$;

-- Ciclo 19 de agosto (el que se va a cerrar) y ciclo 21 del mismo periodo.
INSERT INTO public.adm_periodo_comercial VALUES (500, 2, 2026, 8, 1);
INSERT INTO public.adm_periodo_comercial_ciclo (periodo_ciclo_id, company_id, periodo_comercial_id, ciclo_codigo)
VALUES (901, 2, 500, '19'), (902, 2, 500, '21');

INSERT INTO public.ciclos VALUES (19, '19'), (21, '21');

INSERT INTO public.cliente_maestro VALUES
    (2, 103072, '090806832', 19),   -- ciclo 19
    (2, 103514, '090807756', 19),   -- ciclo 19
    (2, 104000, '090800001', 21);   -- ciclo 21
