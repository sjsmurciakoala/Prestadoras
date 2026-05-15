-- =============================================================================
-- Validacion CAI: tabla maestra cfg_estado_cai + filtros en SP de seleccion
-- Fecha: 2026-05-14
--
-- Problema:
--   sp_adm_obtener_o_reservar_bloque_cai_ruta tenia validaciones laxas:
--     - No filtraba por tipo_documento_fiscal_id (podia agarrar CAI de NC para factura)
--     - No verificaba c.fecha_limite_emision >= current_date
--     - Usaba c.status_id (boolean legacy) en vez de un estado_id semantico
--     - No verificaba c.correlativo_actual < c.rango_hasta (agotado)
--
--   Ademas: no existia tabla maestra para adm_cai_facturacion.estado_id (la
--   check constraint dice estado_id BETWEEN 1 AND 5 pero ningun catalogo lo
--   documenta). Resultado: en Azure CAI 1 tiene estado_id=4 con fecha_limite
--   vencida y CAI 2 tiene estado_id=2 vigente — los valores eran arbitrarios.
--
-- Fix:
--   1. Crear cfg_estado_cai con 5 valores semanticos:
--        1=VIGENTE, 2=VENCIDO, 3=AGOTADO, 4=ANULADO, 5=SUSPENDIDO
--   2. Backfill adm_cai_facturacion.estado_id segun condicion real.
--   3. Modificar sp_adm_obtener_o_reservar_bloque_cai_ruta:
--        - Aceptar p_tipo_documento_fiscal_id (default 1 = Factura)
--        - Filtrar por tipo_documento_fiscal_id, estado_id=1 VIGENTE,
--          fecha_limite_emision >= current_date y correlativo_actual < rango_hasta
--   4. Funcion helper sp_adm_actualizar_estado_cai para refrescar estados
--      (llamada en cron o antes de seleccion para mantener consistencia).
-- =============================================================================

-- Paso 1: tabla maestra
CREATE TABLE IF NOT EXISTS public.cfg_estado_cai (
    estado_id smallint PRIMARY KEY,
    codigo varchar(20) NOT NULL UNIQUE,
    descripcion varchar(200) NOT NULL,
    permite_emision boolean NOT NULL DEFAULT false,
    activo boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now()
);

INSERT INTO public.cfg_estado_cai (estado_id, codigo, descripcion, permite_emision)
VALUES
    (1, 'VIGENTE',    'CAI dentro de fecha y con correlativo disponible', true),
    (2, 'VENCIDO',    'Paso la fecha_limite_emision',                     false),
    (3, 'AGOTADO',    'correlativo_actual alcanzo rango_hasta',           false),
    (4, 'ANULADO',    'CAI anulado por SAR',                              false),
    (5, 'SUSPENDIDO', 'CAI suspendido manualmente',                       false)
ON CONFLICT (estado_id) DO UPDATE
SET codigo = EXCLUDED.codigo,
    descripcion = EXCLUDED.descripcion,
    permite_emision = EXCLUDED.permite_emision;

-- Paso 2: FK desde adm_cai_facturacion → cfg_estado_cai (si no existe).
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'fk_adm_cai_facturacion_estado_id'
          AND conrelid = 'public.adm_cai_facturacion'::regclass
    ) THEN
        ALTER TABLE public.adm_cai_facturacion
            ADD CONSTRAINT fk_adm_cai_facturacion_estado_id
            FOREIGN KEY (estado_id) REFERENCES public.cfg_estado_cai (estado_id);
    END IF;
END $$;

-- Paso 3: backfill estado_id segun condicion real.
-- Orden de evaluacion: ANULADO > SUSPENDIDO se respetan; resto se recalcula.
UPDATE public.adm_cai_facturacion c
SET estado_id = CASE
    WHEN c.estado_id IN (4, 5) THEN c.estado_id  -- respetar manuales
    WHEN c.correlativo_actual >= c.rango_hasta THEN 3  -- AGOTADO
    WHEN c.fecha_limite_emision < current_date THEN 2  -- VENCIDO
    WHEN c.vigencia_hasta IS NOT NULL AND c.vigencia_hasta < current_date THEN 2  -- VENCIDO
    WHEN c.vigencia_desde > current_date THEN 5  -- no entro en vigencia → SUSPENDIDO
    ELSE 1  -- VIGENTE
END,
updated_at = now(),
updated_by = current_user;

-- Paso 4: funcion helper para refrescar estados (idempotente, sin tocar manuales).
DROP FUNCTION IF EXISTS public.sp_adm_actualizar_estado_cai(bigint);

CREATE OR REPLACE FUNCTION public.sp_adm_actualizar_estado_cai(
    p_company_id bigint DEFAULT NULL
)
RETURNS integer
LANGUAGE plpgsql
AS $function$
DECLARE
    v_actualizados integer := 0;
BEGIN
    UPDATE public.adm_cai_facturacion c
    SET estado_id = CASE
        WHEN c.estado_id IN (4, 5) THEN c.estado_id
        WHEN c.correlativo_actual >= c.rango_hasta THEN 3
        WHEN c.fecha_limite_emision < current_date THEN 2
        WHEN c.vigencia_hasta IS NOT NULL AND c.vigencia_hasta < current_date THEN 2
        WHEN c.vigencia_desde > current_date THEN 5
        ELSE 1
    END,
    updated_at = now(),
    updated_by = current_user
    WHERE (p_company_id IS NULL OR c.company_id = p_company_id)
      AND c.estado_id <> CASE
        WHEN c.estado_id IN (4, 5) THEN c.estado_id
        WHEN c.correlativo_actual >= c.rango_hasta THEN 3
        WHEN c.fecha_limite_emision < current_date THEN 2
        WHEN c.vigencia_hasta IS NOT NULL AND c.vigencia_hasta < current_date THEN 2
        WHEN c.vigencia_desde > current_date THEN 5
        ELSE 1
    END;

    GET DIAGNOSTICS v_actualizados = ROW_COUNT;
    RETURN v_actualizados;
END
$function$;

-- Paso 5: sp_adm_obtener_o_reservar_bloque_cai_ruta con validaciones completas.
DROP FUNCTION IF EXISTS public.sp_adm_obtener_o_reservar_bloque_cai_ruta(bigint, varchar, integer, varchar);
DROP FUNCTION IF EXISTS public.sp_adm_obtener_o_reservar_bloque_cai_ruta(bigint, varchar, integer, varchar, smallint);

CREATE OR REPLACE FUNCTION public.sp_adm_obtener_o_reservar_bloque_cai_ruta(
    p_company_id bigint,
    p_ruta_codigo varchar,
    p_cantidad integer DEFAULT 250,
    p_usuario varchar DEFAULT current_user,
    p_tipo_documento_fiscal_id smallint DEFAULT 1  -- 1 = Factura
)
RETURNS TABLE (
    cai_bloque_id bigint,
    cai_id bigint,
    codigo_cai varchar,
    prefijo_documento varchar,
    correlativo_desde bigint,
    correlativo_hasta bigint,
    correlativo_actual bigint,
    correlativo_siguiente bigint,
    fecha_expiracion date,
    estado_codigo varchar
)
LANGUAGE plpgsql
AS $function$
DECLARE
    v_ruta_codigo varchar := NULLIF(BTRIM(COALESCE(p_ruta_codigo, '')), '');
    v_cai_id bigint;
BEGIN
    IF v_ruta_codigo IS NULL THEN
        RAISE EXCEPTION 'RUTA_REQUERIDA: se requiere ruta para resolver bloque CAI offline.';
    END IF;

    IF p_tipo_documento_fiscal_id IS NULL OR p_tipo_documento_fiscal_id <= 0 THEN
        RAISE EXCEPTION 'TIPO_DOCUMENTO_REQUERIDO: tipo_documento_fiscal_id no valido.';
    END IF;

    -- Refresca estado del CAI antes de seleccionar (idempotente, barato).
    PERFORM public.sp_adm_actualizar_estado_cai(p_company_id);

    -- Branch 1: bloque ya reservado para esta ruta + CAI vigente del tipo correcto.
    RETURN QUERY
    SELECT
        b.cai_bloque_id,
        b.cai_id,
        c.codigo_cai,
        c.prefijo_documento,
        b.correlativo_desde,
        b.correlativo_hasta,
        b.correlativo_actual,
        LEAST(b.correlativo_actual + 1, b.correlativo_hasta) AS correlativo_siguiente,
        b.fecha_expiracion,
        b.estado_codigo
    FROM public.adm_cai_bloque_reservado b
    JOIN public.adm_cai_facturacion c
      ON c.company_id = b.company_id
     AND c.cai_id = b.cai_id
    WHERE b.company_id = p_company_id
      AND b.ruta_codigo = v_ruta_codigo
      AND b.status_id = 1
      AND c.status_id = 1
      AND c.tipo_documento_fiscal_id = p_tipo_documento_fiscal_id
      AND c.estado_id = 1  -- VIGENTE
      AND current_date >= c.vigencia_desde
      AND (c.vigencia_hasta IS NULL OR current_date <= c.vigencia_hasta)
      AND c.fecha_limite_emision >= current_date
      AND c.correlativo_actual < c.rango_hasta
      AND (b.fecha_expiracion IS NULL OR current_date <= b.fecha_expiracion)
      AND b.correlativo_actual < b.correlativo_hasta
    ORDER BY b.fecha_reserva DESC, b.cai_bloque_id DESC
    LIMIT 1;

    IF FOUND THEN
        RETURN;
    END IF;

    -- Branch 2: no hay bloque vigente — busca CAI vigente del tipo correcto.
    SELECT c.cai_id
    INTO v_cai_id
    FROM public.adm_cai_facturacion c
    WHERE c.company_id = p_company_id
      AND c.status_id = 1
      AND c.tipo_documento_fiscal_id = p_tipo_documento_fiscal_id
      AND c.estado_id = 1  -- VIGENTE
      AND current_date >= c.vigencia_desde
      AND (c.vigencia_hasta IS NULL OR current_date <= c.vigencia_hasta)
      AND c.fecha_limite_emision >= current_date
      AND c.correlativo_actual < c.rango_hasta
    ORDER BY c.vigencia_desde DESC, c.cai_id DESC
    LIMIT 1;

    IF v_cai_id IS NULL THEN
        RAISE EXCEPTION 'CAI_VIGENTE_NO_DISPONIBLE: no existe CAI vigente del tipo % para la empresa %. Revise vigencia, fecha limite y agotamiento de rango.',
            p_tipo_documento_fiscal_id, p_company_id;
    END IF;

    PERFORM 1
    FROM public.sp_adm_reservar_bloque_cai(
        p_company_id,
        v_cai_id,
        NULL,
        NULL,
        v_ruta_codigo,
        COALESCE(NULLIF(p_cantidad, 0), 250),
        NULL,
        p_usuario
    );

    RETURN QUERY
    SELECT
        b.cai_bloque_id,
        b.cai_id,
        c.codigo_cai,
        c.prefijo_documento,
        b.correlativo_desde,
        b.correlativo_hasta,
        b.correlativo_actual,
        LEAST(b.correlativo_actual + 1, b.correlativo_hasta) AS correlativo_siguiente,
        b.fecha_expiracion,
        b.estado_codigo
    FROM public.adm_cai_bloque_reservado b
    JOIN public.adm_cai_facturacion c
      ON c.company_id = b.company_id
     AND c.cai_id = b.cai_id
    WHERE b.company_id = p_company_id
      AND b.ruta_codigo = v_ruta_codigo
      AND b.status_id = 1
      AND c.cai_id = v_cai_id
    ORDER BY b.fecha_reserva DESC, b.cai_bloque_id DESC
    LIMIT 1;
END;
$function$;

COMMENT ON FUNCTION public.sp_adm_obtener_o_reservar_bloque_cai_ruta(bigint, varchar, integer, varchar, smallint) IS
'Resuelve o reserva bloque CAI para una ruta. Filtros V3 (2026-05-14):
  - tipo_documento_fiscal_id (default 1=FAC)
  - estado_id = 1 VIGENTE (cfg_estado_cai)
  - fecha_limite_emision >= current_date
  - correlativo_actual < rango_hasta (no agotado)
Llama sp_adm_actualizar_estado_cai antes de seleccionar para refrescar VENCIDO/AGOTADO automaticamente.';
