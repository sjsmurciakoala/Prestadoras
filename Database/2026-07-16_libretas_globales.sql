-- =============================================================================
-- Libretas globales: adm_libreta reemplaza al catálogo rutas-por-ciclo
-- Fecha: 2026-07-16
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost)
--
-- POR QUÉ
-- En SIMAFI la libreta (00L1..00L5, el "libro" de cada lector) no tiene ciclo:
-- ni siquiera existe como catálogo — vive como texto dentro del indicativo del
-- cliente (CC-BBB-LLLL-SSSSS), y el lector (usuarioapc) solo tiene su libreta.
-- Verificado contra 172.16.0.3: los 21 ciclos usan las mismas 5 libretas
-- (~4,000 abonados por libreta en 20,276 abonados). El catálogo rutas-por-ciclo
-- que introdujimos obligaba a repetir cada libreta en cada ciclo (21x5=105
-- filas, +21 por cada libreta futura) y a duplicar credenciales de lector por
-- ciclo. Este script deja UNA fila por libreta, por empresa.
--
-- QUÉ HACE
--   1. Crea adm_libreta (tenant-scoped, UNIQUE company+codigo, solo mayúsculas).
--   2. Siembra desde el catálogo rutas existente (DISTINCT codruta activas)
--      para cada empresa con clientes.
--   3. Normaliza a MAYÚSCULAS el segmento libreta de los indicativos migrados
--      (34 clientes con 00l4/00l3/... que sp_medidores_por_ruta_ws no matchea
--      porque compara case-sensitive) y el de historicomedicion.ruta.
--   4. Endurece fn_adm_ruta_de_indicativo: con 4+ segmentos toma el 3.º
--      POSICIONAL (la versión anterior descartaba segmentos vacíos, y con
--      barrio vacío '20--00L2-00070' devolvía la SECUENCIA como ruta).
--   5. Reescribe fn_adm_periodo_ciclo_info y fn_adm_periodo_ciclo_rutas_pendientes
--      para derivar las rutas del ciclo desde los CLIENTES reales (indicativo)
--      en vez de la tabla rutas. preview/abrir/checklist las consumen sin cambio.
--   6. Lectores: desactiva las credenciales duplicadas por ciclo (*19) y anula
--      codciclo — el ciclo del lector nunca se usó (el ciclo pendiente lo
--      resuelve GetCiclo por períodos abiertos, igual que el WS viejo).
--
-- La tabla rutas queda en su lugar (solo deja de consumirse) para rollback.
-- =============================================================================

BEGIN;

-- -----------------------------------------------------------------------------
-- 1. Catálogo global de libretas
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.adm_libreta (
    libreta_id   bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id   bigint NOT NULL REFERENCES public.cfg_company (company_id),
    codigo       varchar(10) NOT NULL,
    descripcion  varchar(100),
    activo       boolean NOT NULL DEFAULT true,
    created_by   varchar(100) NOT NULL DEFAULT 'system',
    created_at   timestamptz NOT NULL DEFAULT now(),
    updated_by   varchar(100),
    updated_at   timestamptz,
    CONSTRAINT uq_adm_libreta_company_codigo UNIQUE (company_id, codigo),
    -- El código viaja dentro del indicativo separado por '-': mayúsculas,
    -- sin espacios y sin guiones, o el split_part del lado del lector se rompe.
    CONSTRAINT ck_adm_libreta_codigo CHECK (
        codigo = upper(btrim(codigo))
        AND codigo <> ''
        AND position('-' IN codigo) = 0
    )
);

COMMENT ON TABLE public.adm_libreta IS
    'Catálogo global de libretas (libro del lector). Sin ciclo: en SIMAFI la libreta atraviesa los 21 ciclos; la combinación con el ciclo vive en el indicativo del cliente.';

-- -----------------------------------------------------------------------------
-- 2. Seed desde el catálogo rutas existente (por empresa con clientes)
-- -----------------------------------------------------------------------------
INSERT INTO public.adm_libreta (company_id, codigo, descripcion, created_by)
SELECT co.company_id,
       upper(btrim(r.codruta)) AS codigo,
       min(r.descripcion)      AS descripcion,
       'seed-libretas-globales'
FROM public.rutas r
CROSS JOIN (SELECT DISTINCT cm.company_id FROM public.cliente_maestro cm) co
WHERE r.estado = true
  AND btrim(COALESCE(r.codruta, '')) <> ''
GROUP BY co.company_id, upper(btrim(r.codruta))
ON CONFLICT (company_id, codigo) DO NOTHING;

-- -----------------------------------------------------------------------------
-- 3. Normalización: libreta en MAYÚSCULAS en indicativos y planillas
-- -----------------------------------------------------------------------------
UPDATE public.cliente_maestro cm
SET maestro_cliente_indicativo_ruta = (
        SELECT string_agg(CASE WHEN t.ord = 3 THEN upper(t.seg) ELSE t.seg END,
                          '-' ORDER BY t.ord)
        FROM unnest(string_to_array(cm.maestro_cliente_indicativo_ruta, '-'))
             WITH ORDINALITY AS t(seg, ord)
    )
WHERE cm.maestro_cliente_indicativo_ruta IS NOT NULL
  AND split_part(cm.maestro_cliente_indicativo_ruta, '-', 3)
      <> upper(split_part(cm.maestro_cliente_indicativo_ruta, '-', 3));

UPDATE public.historicomedicion h
SET ruta = (
        SELECT string_agg(CASE WHEN t.ord = 3 THEN upper(t.seg) ELSE t.seg END,
                          '-' ORDER BY t.ord)
        FROM unnest(string_to_array(h.ruta, '-')) WITH ORDINALITY AS t(seg, ord)
    )
WHERE h.ruta IS NOT NULL
  AND split_part(h.ruta, '-', 3) <> upper(split_part(h.ruta, '-', 3));

-- Corrección puntual: el cliente 090806560 quedó con libreta '19000' (fallback
-- "ciclo sin libreta" del formulario viejo: ciclo 19 + '000'). SIMAFI tiene la
-- ruta real 19-069-00L3-00850 (verificado en facturacion de 172.16.0.3).
-- Con la derivación desde clientes esa libreta fantasma bloquearía el cierre
-- del ciclo 19 como "ruta pendiente" que nadie puede leer.
UPDATE public.cliente_maestro
SET maestro_cliente_indicativo_ruta = '19-069-00L3-00850'
WHERE company_id = 2
  AND maestro_cliente_clave = '090806560'
  AND maestro_cliente_indicativo_ruta = '19-010-19000-00850';

-- -----------------------------------------------------------------------------
-- 4. fn_adm_ruta_de_indicativo: 3.er segmento POSICIONAL con 4+ segmentos
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.fn_adm_ruta_de_indicativo(p_indicativo text)
 RETURNS text
 LANGUAGE sql
 IMMUTABLE
AS $function$
    -- Indicativo canónico: ciclo-barrio-libreta-secuencia (4 segmentos).
    -- Con 4+ segmentos crudos se toma el 3.º POSICIONAL aunque el barrio
    -- venga vacío ('20--00L2-00070' -> '00L2'). El filtrado de vacíos de la
    -- versión anterior corría los índices y devolvía la secuencia.
    -- Con menos de 4 segmentos se conserva el comportamiento histórico
    -- (partes no vacías; fallback al texto completo).
    SELECT upper(CASE
        WHEN array_length(string_to_array(COALESCE(p_indicativo, ''), '-'), 1) >= 4
            THEN NULLIF(btrim(split_part(p_indicativo, '-', 3)), '')
        WHEN cardinality(s.partes) >= 3 THEN NULLIF(s.partes[3], '')
        ELSE NULLIF(btrim(COALESCE(p_indicativo, '')), '')
    END)
    FROM (
        SELECT ARRAY(
            SELECT btrim(u.x)
            FROM unnest(string_to_array(COALESCE(p_indicativo, ''), '-')) AS u(x)
            WHERE btrim(u.x) <> ''
        ) AS partes
    ) s;
$function$;

-- -----------------------------------------------------------------------------
-- 5a. fn_adm_periodo_ciclo_info: rutas derivadas de los clientes del ciclo
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.fn_adm_periodo_ciclo_info(p_company_id bigint, p_anio integer, p_mes integer, p_ciclo text)
 RETURNS TABLE(ciclo_norm character varying, periodo_comercial_id bigint, periodo_status smallint, periodo_ciclo_id bigint, ciclo_status smallint, anterior_abierto boolean, ciclos_ids integer[], fecha_limite_calendario date, planilla_existente bigint, rollover_disponible bigint, clientes_activos bigint, rutas jsonb, rutas_sin_lector bigint, otros_ciclos_abiertos jsonb)
 LANGUAGE plpgsql
 STABLE
AS $function$
DECLARE
    v_ciclo text := public.fn_adm_ciclo_norm(p_ciclo);
    v_anio_prev integer;
    v_mes_prev integer;
BEGIN
    IF p_mes = 1 THEN
        v_anio_prev := p_anio - 1; v_mes_prev := 12;
    ELSE
        v_anio_prev := p_anio; v_mes_prev := p_mes - 1;
    END IF;

    ciclo_norm := v_ciclo;

    SELECT p.periodo_comercial_id, p.status_id
    INTO periodo_comercial_id, periodo_status
    FROM public.adm_periodo_comercial p
    WHERE p.company_id = p_company_id AND p.anio = p_anio AND p.mes = p_mes;

    SELECT pc.periodo_ciclo_id, pc.status_id
    INTO periodo_ciclo_id, ciclo_status
    FROM public.adm_periodo_comercial_ciclo pc
    WHERE pc.company_id = p_company_id
      AND pc.periodo_comercial_id = fn_adm_periodo_ciclo_info.periodo_comercial_id
      AND pc.ciclo_codigo = v_ciclo;

    anterior_abierto := EXISTS (
        SELECT 1 FROM public.adm_periodo_comercial p
        WHERE p.company_id = p_company_id
          AND p.anio = v_anio_prev AND p.mes = v_mes_prev
          AND p.status_id = 1
    );

    -- Catálogo de ciclos: TODOS los ids que matcheen (exacto o numérico),
    -- como el C# original (cicloIds.Contains). Si el catálogo tuviera '1' y
    -- '01' como filas distintas, ambos entran. Fallback al número crudo.
    SELECT COALESCE(array_agg(c.ciclos_id), '{}')
    INTO ciclos_ids
    FROM public.ciclos c
    WHERE btrim(c.ciclos_codigo) = v_ciclo
       OR (c.ciclos_codigo ~ '^[0-9]+$' AND v_ciclo ~ '^[0-9]+$'
           AND c.ciclos_codigo::int = v_ciclo::int);

    IF cardinality(ciclos_ids) = 0 AND v_ciclo ~ '^[0-9]+$' THEN
        ciclos_ids := ARRAY[v_ciclo::int];
    END IF;

    SELECT cp.fechalec
    INTO fecha_limite_calendario
    FROM public.calendariopro cp
    WHERE cp.company_id = p_company_id
      AND cp.ano = p_anio AND cp.mes = p_mes
      AND public.fn_adm_ciclo_norm(cp.ciclo) = v_ciclo
    ORDER BY cp.ide DESC
    LIMIT 1;

    SELECT count(*) INTO planilla_existente
    FROM public.historicomedicion h
    WHERE h.company_id = p_company_id
      AND h.ano = p_anio AND h.mes = p_mes
      AND public.fn_adm_ciclo_norm(h.ciclo) = v_ciclo;

    SELECT count(*) INTO rollover_disponible
    FROM public.historicomedicion h
    WHERE h.company_id = p_company_id
      AND h.ano = v_anio_prev AND h.mes = v_mes_prev
      AND public.fn_adm_ciclo_norm(h.ciclo) = v_ciclo;

    SELECT count(*) INTO clientes_activos
    FROM public.cliente_maestro cm
    WHERE cm.company_id = p_company_id
      AND cm.estado = true
      AND cm.ciclos_id = ANY (fn_adm_periodo_ciclo_info.ciclos_ids);

    -- Rutas del ciclo con su lector asignado. Libretas globales (2026-07-16):
    -- se derivan de los CLIENTES reales del ciclo (segmento 3 del indicativo),
    -- no del catálogo rutas-por-ciclo. Igual que antes, una libreta sin
    -- clientes en el ciclo no aparece (el catálogo viejo hacía INNER JOIN).
    SELECT COALESCE(jsonb_agg(jsonb_build_object(
               'ruta', r.codruta,
               'lector', lc.lector
           ) ORDER BY r.codruta), '[]'::jsonb),
           count(*) FILTER (WHERE lc.lector IS NULL)
    INTO rutas, rutas_sin_lector
    FROM (
        SELECT DISTINCT public.fn_adm_ruta_de_indicativo(cm.maestro_cliente_indicativo_ruta) AS codruta
        FROM public.cliente_maestro cm
        WHERE cm.company_id = p_company_id
          AND cm.estado = true
          AND cm.ciclos_id = ANY (fn_adm_periodo_ciclo_info.ciclos_ids)
          AND public.fn_adm_ruta_de_indicativo(cm.maestro_cliente_indicativo_ruta) IS NOT NULL
    ) r
    LEFT JOIN LATERAL (
        SELECT concat_ws(' - ', btrim(l.codigo), NULLIF(btrim(l.lector_nombre), '')) AS lector
        FROM public.adm_lector_credencial l
        WHERE l.company_id = p_company_id
          AND l.activo
          AND upper(btrim(COALESCE(l.ruta, ''))) = r.codruta
        LIMIT 1
    ) lc ON true;

    rutas := COALESCE(rutas, '[]'::jsonb);
    rutas_sin_lector := COALESCE(rutas_sin_lector, 0);

    SELECT COALESCE(jsonb_agg(jsonb_build_object(
               'anio', p.anio, 'mes', p.mes, 'ciclo', pc.ciclo_codigo
           ) ORDER BY p.anio, p.mes, pc.ciclo_codigo), '[]'::jsonb)
    INTO otros_ciclos_abiertos
    FROM public.adm_periodo_comercial_ciclo pc
    JOIN public.adm_periodo_comercial p
      ON p.company_id = pc.company_id
     AND p.periodo_comercial_id = pc.periodo_comercial_id
    WHERE pc.company_id = p_company_id
      AND pc.status_id = 1
      AND NOT (p.anio = p_anio AND p.mes = p_mes AND pc.ciclo_codigo = v_ciclo);

    RETURN NEXT;
END;
$function$;

-- -----------------------------------------------------------------------------
-- 5b. fn_adm_periodo_ciclo_rutas_pendientes: derivadas de los clientes
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.fn_adm_periodo_ciclo_rutas_pendientes(p_company_id bigint, p_periodo_ciclo_id bigint)
 RETURNS TABLE(codruta character varying, clientes_activos bigint, facturas_mes bigint, pendiente boolean)
 LANGUAGE plpgsql
 STABLE
AS $function$
DECLARE
    v_ciclo record;
    v_ciclo_id integer;
BEGIN
    SELECT pc.ciclo_codigo, p.anio, p.mes
    INTO v_ciclo
    FROM public.adm_periodo_comercial_ciclo pc
    JOIN public.adm_periodo_comercial p
      ON p.company_id = pc.company_id
     AND p.periodo_comercial_id = pc.periodo_comercial_id
    WHERE pc.company_id = p_company_id
      AND pc.periodo_ciclo_id = p_periodo_ciclo_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'No existe el ciclo de período comercial % para company_id=%.',
            p_periodo_ciclo_id, p_company_id;
    END IF;

    SELECT c.ciclos_id
    INTO v_ciclo_id
    FROM public.ciclos c
    WHERE btrim(c.ciclos_codigo) = v_ciclo.ciclo_codigo
       OR (c.ciclos_codigo ~ '^[0-9]+$' AND v_ciclo.ciclo_codigo ~ '^[0-9]+$'
           AND c.ciclos_codigo::int = v_ciclo.ciclo_codigo::int)
    ORDER BY CASE WHEN btrim(c.ciclos_codigo) = v_ciclo.ciclo_codigo THEN 0 ELSE 1 END
    LIMIT 1;

    IF v_ciclo_id IS NULL THEN
        -- ciclo sin fila de catálogo: no hay rutas que validar
        RETURN;
    END IF;

    -- Libretas globales (2026-07-16): las rutas del ciclo se derivan de los
    -- clientes reales (segmento libreta del indicativo), no de la tabla rutas.
    -- Semántica idéntica a la anterior: el INNER JOIN catálogo-clientes ya
    -- excluía libretas sin clientes; ahora simplemente se agrupa por libreta.
    RETURN QUERY
    SELECT public.fn_adm_ruta_de_indicativo(cm.maestro_cliente_indicativo_ruta)::varchar AS codruta,
           count(DISTINCT cm.maestro_cliente_id) AS clientes_activos,
           count(DISTINCT f.id) AS facturas_mes,
           (count(DISTINCT f.id) = 0) AS pendiente
    FROM public.cliente_maestro cm
    LEFT JOIN public.factura f
      ON f.company_id = p_company_id
     AND btrim(f.clientecodigo) = btrim(cm.maestro_cliente_clave)
     AND f.tipofacturacion = 'S'
     AND f.tipofactura = 'F'
     AND COALESCE(f.estado_id, 1) <> 3
     AND COALESCE(f.estado, 'A') <> 'N'
     AND btrim(f.ano) ~ '^[0-9]+$' AND btrim(f.ano)::int = v_ciclo.anio
     AND btrim(f.mes) ~ '^[0-9]+$' AND btrim(f.mes)::int = v_ciclo.mes
    WHERE cm.company_id = p_company_id
      AND cm.estado = true
      AND cm.ciclos_id = v_ciclo_id
      AND public.fn_adm_ruta_de_indicativo(cm.maestro_cliente_indicativo_ruta) IS NOT NULL
    GROUP BY 1;
END;
$function$;

-- -----------------------------------------------------------------------------
-- 6. Lectores: fuera las credenciales por ciclo; codciclo deja de usarse
-- -----------------------------------------------------------------------------
UPDATE public.adm_lector_credencial
SET activo = false,
    updated_by = 'libretas-globales',
    updated_at = now()
WHERE activo
  AND codigo ~ '[0-9]+$'
  AND EXISTS (
      -- solo si existe la credencial "base" activa del mismo lector
      SELECT 1 FROM public.adm_lector_credencial b
      WHERE b.company_id = adm_lector_credencial.company_id
        AND b.activo
        AND b.codigo = regexp_replace(adm_lector_credencial.codigo, '[0-9]+$', '')
  );

UPDATE public.adm_lector_credencial
SET codciclo = NULL
WHERE codciclo IS NOT NULL;

COMMIT;
