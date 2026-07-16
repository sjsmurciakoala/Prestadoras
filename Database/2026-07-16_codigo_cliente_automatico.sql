-- =============================================================================
-- Código de cliente automático y secuencia sugerida
-- Fecha: 2026-07-16
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost)
--
-- POR QUÉ
-- En SIMAFI la clave del cliente es un correlativo fijo con prefijo (09 +
-- 7 dígitos, verificado contra 172.16.0.3: NO codifica barrio/zona/ruta y
-- sobrevive los cambios de ruta — cambiociclo mueve el indicativo, nunca la
-- clave). Hoy el portal exige digitarla a mano al crear clientes (directo o
-- desde solicitud), lo que invita a colisiones y huecos. Este script agrega:
--
--   1. adm_codigo_cliente_config: configuración POR EMPRESA del generador
--      (prefijo, longitud total, próximo correlativo, activo). Editable en
--      /mantenimientos/codigo-cliente.
--   2. fn_adm_siguiente_codigo_cliente(company): consume atómicamente el
--      siguiente código (UPDATE ... RETURNING; dos altas concurrentes jamás
--      reciben el mismo). Si el código calculado ya existe (datos migrados
--      por fuera del generador), salta al máximo existente + 1 y sigue —
--      auto-correctivo tras migraciones masivas.
--   3. fn_adm_siguiente_secuencia(company, ciclo, libreta): sugiere la
--      siguiente secuencia de caminata = (max de la libreta en ese ciclo
--      redondeado a decena) + 10 — el esquema SIMAFI de numerar de 10 en 10
--      dejando huecos para intercalar. Con 5 dígitos caben ~9,999 clientes
--      por libreta por ciclo (hoy el máximo real anda por 02020).
--
-- La clave sigue siendo editable: el generador solo actúa cuando el campo
-- viene vacío. maestro_cliente_clave es varchar(20); el default 09+7d usa 9.
-- =============================================================================

BEGIN;

-- -----------------------------------------------------------------------------
-- 1. Configuración por empresa
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.adm_codigo_cliente_config (
    company_id  bigint PRIMARY KEY REFERENCES public.cfg_company (company_id),
    activo      boolean NOT NULL DEFAULT true,
    prefijo     varchar(5) NOT NULL DEFAULT '',
    longitud    smallint NOT NULL DEFAULT 9,
    siguiente   bigint NOT NULL DEFAULT 1,
    updated_by  varchar(100),
    updated_at  timestamptz,
    CONSTRAINT ck_codigo_cliente_prefijo CHECK (prefijo = upper(btrim(prefijo)) AND prefijo ~ '^[0-9A-Z]*$'),
    CONSTRAINT ck_codigo_cliente_longitud CHECK (longitud BETWEEN 4 AND 20 AND longitud > length(prefijo)),
    CONSTRAINT ck_codigo_cliente_siguiente CHECK (siguiente >= 1)
);

COMMENT ON TABLE public.adm_codigo_cliente_config IS
    'Generador del código de cliente por empresa: codigo = prefijo || lpad(siguiente, longitud - len(prefijo)). Solo actúa cuando la clave llega vacía.';

-- Seed: empresa 2 continúa el correlativo SIMAFI (09 + 7 dígitos).
INSERT INTO public.adm_codigo_cliente_config (company_id, prefijo, longitud, siguiente, updated_by)
SELECT 2, '09', 9,
       COALESCE(max(substring(maestro_cliente_clave FROM 3)::bigint), 0) + 1,
       'seed-codigo-automatico'
FROM public.cliente_maestro
WHERE company_id = 2
  AND maestro_cliente_clave ~ '^09[0-9]{7}$'
ON CONFLICT (company_id) DO NOTHING;

-- -----------------------------------------------------------------------------
-- 2. Siguiente código (consume; atómico; auto-correctivo ante colisiones)
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.fn_adm_siguiente_codigo_cliente(p_company_id bigint)
 RETURNS text
 LANGUAGE plpgsql
AS $function$
DECLARE
    v record;
    v_codigo text;
    v_max bigint;
    v_intentos int := 0;
BEGIN
    LOOP
        -- El UPDATE bloquea la fila de config: dos altas concurrentes se
        -- serializan aquí y cada una recibe un correlativo distinto.
        UPDATE public.adm_codigo_cliente_config
        SET siguiente = siguiente + 1,
            updated_at = now()
        WHERE company_id = p_company_id AND activo
        RETURNING prefijo, longitud, siguiente - 1 AS correlativo INTO v;

        IF NOT FOUND THEN
            RETURN NULL; -- sin configuración activa: el llamador decide (clave manual obligatoria)
        END IF;

        v_codigo := v.prefijo || lpad(v.correlativo::text, v.longitud - length(v.prefijo), '0');

        EXIT WHEN NOT EXISTS (
            SELECT 1 FROM public.cliente_maestro
            WHERE company_id = p_company_id AND maestro_cliente_clave = v_codigo
        );

        -- Colisión: hay claves migradas por fuera del generador. Saltar el
        -- correlativo al máximo existente del mismo formato + 1 (una sola vez;
        -- si vuelve a chocar, avanzar de a uno como red de seguridad).
        v_intentos := v_intentos + 1;
        IF v_intentos = 1 THEN
            SELECT COALESCE(max(substring(maestro_cliente_clave FROM length(v.prefijo) + 1)::bigint), 0)
            INTO v_max
            FROM public.cliente_maestro
            WHERE company_id = p_company_id
              AND maestro_cliente_clave ~ ('^' || v.prefijo || '[0-9]{' || (v.longitud - length(v.prefijo))::text || '}$');

            UPDATE public.adm_codigo_cliente_config
            SET siguiente = greatest(siguiente, v_max + 1)
            WHERE company_id = p_company_id;
        ELSIF v_intentos > 100 THEN
            RAISE EXCEPTION 'No se pudo generar un código de cliente libre para company_id=% tras % intentos.',
                p_company_id, v_intentos;
        END IF;
    END LOOP;

    RETURN v_codigo;
END;
$function$;

-- -----------------------------------------------------------------------------
-- 3. Siguiente secuencia de caminata (solo sugiere; no consume nada)
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.fn_adm_siguiente_secuencia(p_company_id bigint, p_ciclo_id integer, p_libreta text)
 RETURNS text
 LANGUAGE sql
 STABLE
AS $function$
    -- max de la libreta dentro del ciclo, redondeado a decena, + 10.
    -- Esquema SIMAFI: 00010, 00020, ... con huecos (00005, 00025) para
    -- intercalar a mano sin renumerar la ruta.
    SELECT lpad((((COALESCE(max(split_part(cm.maestro_cliente_indicativo_ruta, '-', 4)::int), 0)) / 10) * 10 + 10)::text, 5, '0')
    FROM public.cliente_maestro cm
    WHERE cm.company_id = p_company_id
      AND cm.estado = true
      AND cm.ciclos_id = p_ciclo_id
      AND upper(btrim(split_part(cm.maestro_cliente_indicativo_ruta, '-', 3))) = upper(btrim(COALESCE(p_libreta, '')))
      AND split_part(cm.maestro_cliente_indicativo_ruta, '-', 4) ~ '^[0-9]+$';
$function$;

COMMIT;
