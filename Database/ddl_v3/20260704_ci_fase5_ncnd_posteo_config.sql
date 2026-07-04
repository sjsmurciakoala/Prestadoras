-- =============================================================================
-- Integración Contable ↔ Comercial — Fase 5: NC/ND con posteo analítico
-- Fecha: 2026-07-04
-- Plan: docs/plans/2026-07-02-plan-integracion-contable-comercial.md §5 Fase 5
--       (D1: solo el motor único postea. D2: configuración única, sin plantillas
--        ni reglas. D10: las notas fiscales postean automático al emitir.)
--
-- SUPERSEDE a 20260702_nc_nd_posteo_contable.sql (versión sobre plantillas
-- con_plantilla_partida_*, revertida en producción el 2026-07-02): NO aplicar
-- ese script; este redefine los mismos SPs sobre la configuración de F1/F2.
-- Los seeds de plantillas/reglas que la versión vieja haya dejado son inertes
-- (D2: nada los consulta; retiro físico post-F7).
--
-- Contenido:
--   1. Columna poliza_id en adm_nota_credito / adm_nota_debito (idempotente;
--      ya existe donde corrió la versión superseded) + índice de documento en
--      con_partida_hdr (soporta el probe de idempotencia de
--      sp_con_generar_comprobante_config, que antes hacía seq scan).
--   2. fn_con_resolver_cuenta_opcional — variante de fn_con_resolver_cuenta
--      que devuelve NULL en vez de RAISE (para usos opcionales como
--      DEVOLUCION_NC).
--   2b. fn_con_lineas_nota — líneas de la partida espejo de la factura origen:
--      detalle de la nota (que por defecto copia las líneas de la factura)
--      prorrateado al total_nota por restos mayores, cuentas resueltas vía
--      fn_con_resolver_cuenta_modo (F1/F3) con el snapshot dimensional de la
--      factura (F3). Para NC, el uso DEVOLUCION_NC (opcional) se aplica POR
--      LÍNEA con fallback al espejo de ingresos donde no esté configurado
--      (comportamiento por defecto documentado en F2).
--   3. sp_adm_emitir_nota_credito / sp_adm_emitir_nota_debito — cuerpo canónico
--      de 20260514_nc_nd_v3_modelo.sql + paso de posteo por configuración:
--      solo si con_integracion_config.activo_notas está encendido, en la MISMA
--      transacción de la emisión (atómico), vía sp_con_generar_comprobante_config
--      (F4: asiento del módulo NOTAS, numeración fn_con_siguiente_poliza,
--      posteo sp_con_postear_poliza — motor único —, encolado en
--      con_partida_pendiente según encolar_sin_periodo).
--        NC: Debe Ingresos (o DEVOLUCION_NC) / Haber CxC analítica.
--        ND: Debe CxC analítica / Haber Ingresos.
--   4. sp_con_procesar_partida_pendiente v2 — al reprocesar una pendiente del
--      módulo NOTAS puebla poliza_id en la nota (única novedad vs F4).
--
-- Prerequisitos: 20260514_nc_nd_v3_modelo.sql, scripts F1–F4 de ddl_v3.
-- Idempotente. Producción: aplicar SOLO en ventana de deploy acordada.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1. Trazabilidad nota → partida contable
-- -----------------------------------------------------------------------------
ALTER TABLE public.adm_nota_credito ADD COLUMN IF NOT EXISTS poliza_id bigint;
ALTER TABLE public.adm_nota_debito  ADD COLUMN IF NOT EXISTS poliza_id bigint;

COMMENT ON COLUMN public.adm_nota_credito.poliza_id IS
'Partida contable (con_partida_hdr) generada automáticamente al emitir la NC (F5). NULL = emitida sin posteo (activo_notas apagado) o encolada en con_partida_pendiente.';
COMMENT ON COLUMN public.adm_nota_debito.poliza_id IS
'Partida contable (con_partida_hdr) generada automáticamente al emitir la ND (F5). NULL = emitida sin posteo (activo_notas apagado) o encolada en con_partida_pendiente.';

-- Soporta el probe de idempotencia por documento de
-- sp_con_generar_comprobante_config (F4) — sin él, cada comprobante (F4 y
-- ahora cada emisión de NC/ND) hacía un seq scan de con_partida_hdr dentro
-- de la transacción de emisión.
CREATE INDEX IF NOT EXISTS ix_con_partida_hdr_documento
    ON public.con_partida_hdr (company_id, module, document_type, document_id);

-- -----------------------------------------------------------------------------
-- 2. fn_con_resolver_cuenta_opcional — resolución que devuelve NULL sin RAISE
-- -----------------------------------------------------------------------------
-- Misma resolución "lo más específico gana" que fn_con_resolver_cuenta (F1)
-- pero devuelve NULL cuando no hay fila aplicable, para usos OPCIONALES cuyo
-- fallback decide el llamador (DEVOLUCION_NC en F5). Mantener el SELECT en
-- sintonía con fn_con_resolver_cuenta si la semántica de F1 cambia.
CREATE OR REPLACE FUNCTION public.fn_con_resolver_cuenta_opcional(
    p_company_id bigint,
    p_uso varchar,
    p_servicio_id bigint DEFAULT NULL,
    p_categoria_servicio_id integer DEFAULT NULL,
    p_con_medicion boolean DEFAULT NULL
) RETURNS bigint
LANGUAGE sql
STABLE
AS $function$
    SELECT ic.account_id
    FROM public.con_integracion_cuenta ic
    WHERE ic.company_id = p_company_id
      AND ic.uso = p_uso
      AND ic.is_active
      AND (ic.servicio_id IS NULL OR ic.servicio_id = p_servicio_id)
      AND (ic.categoria_servicio_id IS NULL OR ic.categoria_servicio_id = p_categoria_servicio_id)
      AND (ic.con_medicion IS NULL OR ic.con_medicion = p_con_medicion)
    ORDER BY (ic.servicio_id IS NOT NULL)::integer * 4
           + (ic.categoria_servicio_id IS NOT NULL)::integer * 2
           + (ic.con_medicion IS NOT NULL)::integer DESC,
             ic.integracion_cuenta_id
    LIMIT 1;
$function$;

COMMENT ON FUNCTION public.fn_con_resolver_cuenta_opcional(bigint, varchar, bigint, integer, boolean) IS
'Variante de fn_con_resolver_cuenta (F1) que devuelve NULL en vez de RAISE cuando no hay fila aplicable — para usos opcionales (DEVOLUCION_NC) donde el llamador decide el fallback (plan F5).';

-- -----------------------------------------------------------------------------
-- 2b. fn_con_lineas_nota — líneas de la partida espejo de una NC/ND
-- -----------------------------------------------------------------------------
-- Devuelve el jsonb de líneas que consume sp_con_generar_comprobante_config:
-- [{"account_id":…,"debe":…,"haber":…,"descripcion":…}, …].
--
-- Fuente: el detalle de la nota (adm_nota_*_detalle), que en el flujo por
-- defecto es copia de las líneas de la factura origen. Los montos se
-- prorratean al total_nota (el total de la nota puede diferir de la suma del
-- detalle: NC parcial, o total basado en saldototal) por RESTOS MAYORES:
-- base truncada a 2 decimales por línea y los centavos residuales repartidos
-- de a 0.01 a las líneas de mayor resto fraccional — ninguna línea cambia de
-- signo y la suma es exactamente round(total_nota, 2), de modo que la partida
-- SIEMPRE cuadra con lo asentado en transaccion_abonado.
--
-- Resolución de cuentas (config F1, modos de con_integracion_config, snapshot
-- dimensional de la factura de F3): INGRESO con modo_ventas y CXC con modo_cxc
-- por servicio de cada línea. En NC, el uso DEVOLUCION_NC se aplica POR LÍNEA:
-- donde su matriz resuelva se usa esa cuenta y donde no, la línea cae al
-- espejo de INGRESO (F2: opcional; sin configurar la NC espeja las cuentas de
-- ingreso de la factura — una config parcial de DEVOLUCION_NC nunca bloquea
-- la emisión). La columna cuenta_contable_codigo del detalle NO se consulta:
-- la config es la única fuente de cuentas (D2).
--
-- Dirección: NC = Debe Ingresos / Haber CxC; ND = Debe CxC / Haber Ingresos.
-- Una línea de detalle negativa (p.ej. descuento copiado de la factura) va al
-- lado contrario, igual que en el lote de F3.
CREATE OR REPLACE FUNCTION public.fn_con_lineas_nota(
    p_company_id bigint,
    p_tipo varchar,          -- 'NC' | 'ND'
    p_nota_id bigint
) RETURNS jsonb
LANGUAGE plpgsql
STABLE
AS $function$
DECLARE
    v_tipo varchar := upper(btrim(p_tipo));
    v_modo_ventas varchar;
    v_modo_cxc varchar;
    v_hay_devolucion boolean := false;
    v_total numeric(18,2);
    v_numero text;
    v_factura_id integer;
    v_categoria integer;
    v_medicion boolean;
    v_detalle jsonb;
    v_sum_detalle numeric;
    v_lineas jsonb;
BEGIN
    IF v_tipo NOT IN ('NC', 'ND') THEN
        RAISE EXCEPTION 'Tipo de nota no soportado: % (use NC o ND).', p_tipo;
    END IF;

    SELECT c.modo_ventas, c.modo_cxc
    INTO v_modo_ventas, v_modo_cxc
    FROM public.con_integracion_config c
    WHERE c.company_id = p_company_id;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'La empresa % no tiene configuración de integración contable (pantalla Integración Contable / perfil ERSAPS).', p_company_id;
    END IF;

    IF v_tipo = 'NC' THEN
        SELECT nc.total_nota, nc.numero_documento, nc.factura_origen_id
        INTO v_total, v_numero, v_factura_id
        FROM public.adm_nota_credito nc
        WHERE nc.company_id = p_company_id AND nc.nota_credito_id = p_nota_id;
        IF NOT FOUND THEN
            RAISE EXCEPTION 'La nota de crédito % no existe para la empresa %.', p_nota_id, p_company_id;
        END IF;

        -- Gate barato: solo intentamos resolver DEVOLUCION_NC por línea si la
        -- empresa tiene al menos una fila del uso (el fallback real es por
        -- línea, en la CTE cuentas).
        v_hay_devolucion := EXISTS (
            SELECT 1 FROM public.con_integracion_cuenta ic
            WHERE ic.company_id = p_company_id
              AND ic.uso = 'DEVOLUCION_NC'
              AND ic.is_active);

        SELECT jsonb_agg(jsonb_build_object(
                   'servicio_id', d.servicio_id,
                   'monto', d.monto_total,
                   'descripcion', COALESCE(NULLIF(btrim(d.descripcion), ''), d.servicio_codigo))
               ORDER BY d.nota_credito_detalle_id)
        INTO v_detalle
        FROM public.adm_nota_credito_detalle d
        WHERE d.nota_credito_id = p_nota_id
          AND COALESCE(d.monto_total, 0) <> 0;
    ELSE
        SELECT nd.total_nota, nd.numero_documento, nd.factura_origen_id
        INTO v_total, v_numero, v_factura_id
        FROM public.adm_nota_debito nd
        WHERE nd.company_id = p_company_id AND nd.nota_debito_id = p_nota_id;
        IF NOT FOUND THEN
            RAISE EXCEPTION 'La nota de débito % no existe para la empresa %.', p_nota_id, p_company_id;
        END IF;

        SELECT jsonb_agg(jsonb_build_object(
                   'servicio_id', d.servicio_id,
                   'monto', d.monto_total,
                   'descripcion', COALESCE(NULLIF(btrim(d.descripcion), ''), d.servicio_codigo))
               ORDER BY d.nota_debito_detalle_id)
        INTO v_detalle
        FROM public.adm_nota_debito_detalle d
        WHERE d.nota_debito_id = p_nota_id
          AND COALESCE(d.monto_total, 0) <> 0;
    END IF;

    v_total := round(COALESCE(v_total, 0), 2);
    IF v_total <= 0 THEN
        RAISE EXCEPTION 'La nota %/% tiene total_nota inválido (%).', v_tipo, p_nota_id, v_total;
    END IF;

    -- Snapshot dimensional de la factura origen (F3): categoría y medición
    -- AL MOMENTO de facturar; la única dimensión que varía por línea es el
    -- servicio.
    SELECT f.categoria_servicio_id, f.con_medicion
    INTO v_categoria, v_medicion
    FROM public.factura f
    WHERE f.company_id = p_company_id AND f.id = v_factura_id;

    -- Detalle degenerado (sin líneas con monto, o neto no positivo — no se
    -- puede prorratear): una sola línea general por el total de la nota.
    SELECT SUM((e->>'monto')::numeric) INTO v_sum_detalle
    FROM jsonb_array_elements(COALESCE(v_detalle, '[]'::jsonb)) AS e;

    IF v_detalle IS NULL OR COALESCE(v_sum_detalle, 0) <= 0 THEN
        v_detalle := jsonb_build_array(jsonb_build_object(
            'servicio_id', NULL, 'monto', v_total, 'descripcion', NULL));
        v_sum_detalle := v_total;
    END IF;

    WITH det AS (
        SELECT x.servicio_id, x.monto, x.descripcion, x.ord
        FROM ROWS FROM (
                 jsonb_to_recordset(v_detalle)
                 AS (servicio_id bigint, monto numeric, descripcion text)
             ) WITH ORDINALITY AS x(servicio_id, monto, descripcion, ord)
    ),
    -- Prorrateo al total de la nota por RESTOS MAYORES: base truncada a 2
    -- decimales por línea; los centavos residuales se reparten de a 0.01 a
    -- las líneas de mayor resto fraccional (desempate determinista por orden
    -- del detalle). Garantiza suma exacta = v_total y que ninguna línea
    -- cambie de signo (regla F4: nunca redondear la suma cruda de un lado).
    exacta AS (
        SELECT d.servicio_id, d.descripcion, d.ord,
               d.monto * v_total / v_sum_detalle AS exacto,
               trunc(d.monto * v_total / v_sum_detalle, 2) AS base
        FROM det d
    ),
    rankeada AS (
        SELECT e.servicio_id, e.descripcion, e.base,
               row_number() OVER (ORDER BY (e.exacto - e.base) DESC, e.ord) AS rk,
               COUNT(*) OVER () AS n,
               round((v_total - SUM(e.base) OVER ()) * 100) AS centavos
        FROM exacta e
    ),
    ajustada AS (
        SELECT r.servicio_id, r.descripcion,
               r.base + CASE
                   WHEN r.centavos > 0 AND r.rk <= r.centavos THEN 0.01
                   WHEN r.centavos < 0 AND r.rk > r.n + r.centavos THEN -0.01
                   ELSE 0
               END AS monto
        FROM rankeada r
    ),
    -- Resolución UNA VEZ por servicio distinto (mismo patrón que el lote F3).
    combos AS (
        SELECT DISTINCT a.servicio_id FROM ajustada a WHERE a.monto <> 0
    ),
    cuentas AS (
        SELECT co.servicio_id,
               -- DEVOLUCION_NC por línea (opcional): donde no resuelva, la
               -- línea cae al espejo de INGRESO — una config parcial nunca
               -- bloquea la emisión.
               COALESCE(
                   CASE WHEN v_hay_devolucion THEN
                       public.fn_con_resolver_cuenta_opcional(p_company_id, 'DEVOLUCION_NC',
                           CASE WHEN v_modo_ventas <> 'GENERAL' THEN co.servicio_id END,
                           CASE WHEN v_modo_ventas = 'POR_SERVICIO_CATEGORIA' THEN v_categoria END,
                           CASE WHEN v_modo_ventas = 'POR_SERVICIO_CATEGORIA' THEN v_medicion END)
                   END,
                   public.fn_con_resolver_cuenta_modo(p_company_id, 'INGRESO', v_modo_ventas,
                       co.servicio_id, v_categoria, v_medicion)) AS ingreso_account,
               public.fn_con_resolver_cuenta_modo(p_company_id, 'CXC', v_modo_cxc,
                   co.servicio_id, v_categoria, v_medicion) AS cxc_account
        FROM combos co
    ),
    resueltas AS (
        SELECT a.monto, a.descripcion, cu.ingreso_account, cu.cxc_account
        FROM ajustada a
        JOIN cuentas cu ON cu.servicio_id IS NOT DISTINCT FROM a.servicio_id
        WHERE a.monto <> 0
    )
    SELECT jsonb_agg(sub.linea) INTO v_lineas
    FROM (
        -- Lado Ingresos/Devolución: NC lo debita, ND lo acredita; una línea
        -- negativa invierte el lado.
        SELECT jsonb_build_object(
                   'account_id', r.ingreso_account,
                   'debe',  CASE WHEN (v_tipo = 'NC') = (r.monto > 0) THEN abs(r.monto) ELSE 0 END,
                   'haber', CASE WHEN (v_tipo = 'NC') = (r.monto > 0) THEN 0 ELSE abs(r.monto) END,
                   'descripcion', left(concat(CASE v_tipo WHEN 'NC' THEN 'N/C ' ELSE 'N/D ' END,
                                              v_numero,
                                              CASE WHEN r.descripcion IS NULL THEN '' ELSE ' — ' || r.descripcion END), 200)
               ) AS linea
        FROM resueltas r
        UNION ALL
        -- Lado CxC analítica: espejo exacto del anterior.
        SELECT jsonb_build_object(
                   'account_id', r.cxc_account,
                   'debe',  CASE WHEN (v_tipo = 'ND') = (r.monto > 0) THEN abs(r.monto) ELSE 0 END,
                   'haber', CASE WHEN (v_tipo = 'ND') = (r.monto > 0) THEN 0 ELSE abs(r.monto) END,
                   'descripcion', left(concat(CASE v_tipo WHEN 'NC' THEN 'N/C ' ELSE 'N/D ' END,
                                              v_numero, ' — CxC abonados'), 200)
               ) AS linea
        FROM resueltas r
    ) sub;

    RETURN v_lineas;
END;
$function$;

COMMENT ON FUNCTION public.fn_con_lineas_nota(bigint, varchar, bigint) IS
'Líneas de la partida espejo de una NC/ND (plan F5): detalle de la nota prorrateado al total_nota por restos mayores (suma exacta, sin cambios de signo), cuentas resueltas vía fn_con_resolver_cuenta_modo con el snapshot dimensional de la factura origen (F3). NC: Debe Ingresos — o DEVOLUCION_NC por línea donde esté configurada, con fallback al espejo — / Haber CxC. ND: inverso. Consumido por los SPs de emisión vía sp_con_generar_comprobante_config.';

-- -----------------------------------------------------------------------------
-- 3a. sp_adm_emitir_nota_credito — posteo por configuración (paso 9c)
--     Cuerpo canónico de 20260514_nc_nd_v3_modelo.sql salvo el paso 9c.
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.sp_adm_emitir_nota_credito(
    p_company_id bigint,
    p_factura_origen_id integer,
    p_motivo_anulacion_id smallint,
    p_motivo_detalle varchar,
    p_monto_disminuir numeric,          -- NULL = total de la factura origen
    p_lineas jsonb,                     -- NULL = copia las líneas de la factura origen
    p_usuario_emisor varchar,
    p_cai_id bigint                     -- CAI específico tipo NC
)
RETURNS TABLE (
    success boolean,
    codigo text,
    mensaje text,
    nota_credito_id bigint,
    numero_documento text,
    correlativo bigint
)
LANGUAGE plpgsql
AS $function$
DECLARE
    v_factura record;
    v_cai record;
    v_company record;
    v_correlativo bigint;
    v_numero text;
    v_nota_id bigint;
    v_total numeric(18,4);
    v_monto numeric(18,4);
    v_anula boolean;
    v_linea record;
    v_saldo_anterior numeric(18,4) := 0;
    v_activo_notas boolean;
    v_poliza_id bigint;
BEGIN
    -- 1. Validar factura origen
    SELECT f.id, f.numfactura, f.fechaemision, f.clientecodigo, f.saldototal,
           f.estado, f.company_id
    INTO v_factura
    FROM public.factura f
    WHERE f.id = p_factura_origen_id
      AND f.company_id = p_company_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'FACTURA_NO_EXISTE: factura origen % no existe para company %.',
            p_factura_origen_id, p_company_id;
    END IF;

    IF COALESCE(v_factura.estado, '') = 'N' THEN
        RAISE EXCEPTION 'FACTURA_YA_ANULADA: la factura origen % ya está anulada.', v_factura.numfactura;
    END IF;

    -- 2. Validar CAI emitible y que sea tipo NC (6)
    IF NOT public.fn_adm_validar_cai_emitible(p_company_id, p_cai_id) THEN
        RAISE EXCEPTION 'CAI_NO_EMITIBLE: el CAI % no está vigente o pasó su fecha límite de emisión.', p_cai_id;
    END IF;

    SELECT c.cai_id, c.prefijo_documento, c.correlativo_actual, c.rango_hasta,
           c.fecha_limite_emision, c.leyenda_rango, c.tipo_documento_fiscal_id,
           c.establecimiento_codigo
    INTO v_cai
    FROM public.adm_cai_facturacion c
    WHERE c.company_id = p_company_id AND c.cai_id = p_cai_id;

    IF v_cai.tipo_documento_fiscal_id <> 6 THEN
        RAISE EXCEPTION 'CAI_TIPO_INCORRECTO: el CAI % es tipo %, se requiere tipo 6 (Nota de Crédito).',
            p_cai_id, v_cai.tipo_documento_fiscal_id;
    END IF;

    -- 3. Tomar correlativo siguiente
    v_correlativo := v_cai.correlativo_actual + 1;
    IF v_correlativo > v_cai.rango_hasta THEN
        RAISE EXCEPTION 'CAI_AGOTADO: el CAI % alcanzó su rango máximo (%).', p_cai_id, v_cai.rango_hasta;
    END IF;

    v_numero := concat(COALESCE(v_cai.prefijo_documento, ''), lpad(v_correlativo::text, 8, '0'));

    -- 4. Snapshot emisor desde cfg_company
    SELECT co.tax_id, co.legal_name, co.commercial_name, co.address
    INTO v_company
    FROM public.cfg_company co
    WHERE co.company_id = p_company_id;

    -- 5. Monto a disminuir (default = saldo total de la factura)
    v_total := COALESCE(v_factura.saldototal, 0)::numeric(18,4);
    v_monto := COALESCE(p_monto_disminuir, v_total)::numeric(18,4);
    IF v_monto <= 0 THEN
        RAISE EXCEPTION 'MONTO_INVALIDO: el monto a disminuir debe ser mayor a 0.';
    END IF;
    IF v_monto > v_total THEN
        RAISE EXCEPTION 'MONTO_EXCEDE_FACTURA: monto a disminuir % supera el saldo de la factura %.',
            v_monto, v_total;
    END IF;
    v_anula := (v_monto >= v_total);

    -- 6. INSERT cabecera
    INSERT INTO public.adm_nota_credito (
        company_id, establecimiento_codigo,
        tipo_documento_fiscal_id, numero_documento, cai_id, correlativo,
        fecha_limite_cai, leyenda_cai_rango,
        rtn_emisor, razon_social_emisor, direccion_emisor,
        cliente_id, rtn_receptor, razon_social_receptor, direccion_receptor,
        factura_origen_id, factura_origen_numero, factura_origen_fecha, factura_origen_cai,
        motivo_anulacion_id, motivo_detalle,
        monto_disminuir, isv_disminuir, total_nota, anula_factura_origen,
        estado_id, usuario_emisor, created_by
    )
    SELECT
        p_company_id, COALESCE(v_cai.establecimiento_codigo, '000'),
        6, v_numero, p_cai_id, v_correlativo,
        v_cai.fecha_limite_emision, v_cai.leyenda_rango,
        COALESCE(v_company.tax_id, ''), COALESCE(v_company.legal_name, v_company.commercial_name, ''), v_company.address,
        cm.maestro_cliente_id, cm.maestro_cliente_rtn,
        cm.maestro_cliente_nombre, NULL,
        v_factura.id, v_factura.numfactura, v_factura.fechaemision, v_factura.numfactura,
        p_motivo_anulacion_id, p_motivo_detalle,
        v_monto, 0, v_monto, v_anula,
        1, p_usuario_emisor, p_usuario_emisor
    FROM public.cliente_maestro cm
    WHERE cm.maestro_cliente_clave = v_factura.clientecodigo
      AND cm.company_id = p_company_id
    LIMIT 1
    RETURNING adm_nota_credito.nota_credito_id INTO v_nota_id;

    IF v_nota_id IS NULL THEN
        RAISE EXCEPTION 'CLIENTE_NO_EXISTE: no se encontró cliente % para la factura origen.',
            v_factura.clientecodigo;
    END IF;

    -- 7. INSERT detalle: desde p_lineas o copiando de factura_detalle de la factura origen
    IF p_lineas IS NOT NULL THEN
        FOR v_linea IN
            SELECT *
            FROM jsonb_to_recordset(p_lineas) AS l(
                servicio_id bigint,
                servicio_codigo text,
                descripcion text,
                cantidad numeric,
                monto_unitario numeric,
                monto_total numeric,
                isv_monto numeric,
                cuenta_contable_codigo text
            )
        LOOP
            INSERT INTO public.adm_nota_credito_detalle (
                nota_credito_id, servicio_id, servicio_codigo, descripcion,
                cantidad, monto_unitario, monto_total, isv_monto, cuenta_contable_codigo
            )
            VALUES (
                v_nota_id, v_linea.servicio_id, v_linea.servicio_codigo,
                COALESCE(v_linea.descripcion, v_linea.servicio_codigo, ''),
                COALESCE(v_linea.cantidad, 1), COALESCE(v_linea.monto_unitario, 0),
                COALESCE(v_linea.monto_total, 0), COALESCE(v_linea.isv_monto, 0),
                v_linea.cuenta_contable_codigo
            );
        END LOOP;
    ELSE
        INSERT INTO public.adm_nota_credito_detalle (
            nota_credito_id, servicio_id, servicio_codigo, descripcion,
            cantidad, monto_unitario, monto_total, isv_monto
        )
        SELECT
            v_nota_id,
            s.servicio_id,
            fd.tiposervicio,
            COALESCE(fd.descripcion, fd.tiposervicio, ''),
            1,
            COALESCE(fd.montovalor, 0),
            COALESCE(fd.montovalor, 0),
            0
        FROM public.factura_detalle fd
        LEFT JOIN public.adm_servicio s
          ON s.company_id = p_company_id
         AND s.codigo = fd.tiposervicio
        WHERE fd.factura_id = v_factura.id
          AND COALESCE(fd.montovalor, 0) <> 0;
    END IF;

    -- 8. Avanzar correlativo del CAI
    UPDATE public.adm_cai_facturacion
    SET correlativo_actual = v_correlativo,
        updated_at = now(),
        updated_by = p_usuario_emisor
    WHERE company_id = p_company_id AND cai_id = p_cai_id;

    -- 9. Marcar la factura origen como anulada SOLO si la NC cubre el total
    IF v_anula THEN
        UPDATE public.factura
        SET estado = 'N',
            estado_id = 3,  -- ANULADA
            motivo_anulacion_id = p_motivo_anulacion_id,
            updated_at = now()
        WHERE id = v_factura.id;
    END IF;

    -- 9b. Reflejar la NC en el estado de cuenta del cliente (transaccion_abonado).
    -- Una NC DISMINUYE el saldo del cliente → creditos = monto, saldo_detalle negativo.
    SELECT COALESCE(ta.saldo, 0)
    INTO v_saldo_anterior
    FROM public.transaccion_abonado ta
    WHERE ta.company_id = p_company_id
      AND ta.cliente_clave = v_factura.clientecodigo
      AND ta.estado = 'A'
    ORDER BY ta.ide DESC
    LIMIT 1;

    INSERT INTO public.transaccion_abonado (
        company_id, cliente_clave, tipotransaccion, docufuente,
        fecha_docu, tipo_partida, descripcion,
        debitos, creditos, saldo,
        estado, estado_id, fecha_registro, usuario, saldo_detalle
    )
    VALUES (
        p_company_id, v_factura.clientecodigo, '205', v_nota_id,
        current_date, '01',
        concat('N/C ', v_numero, ' s/factura ', v_factura.numfactura),
        0, v_monto, COALESCE(v_saldo_anterior, 0) - v_monto,
        'A', 1, current_date, p_usuario_emisor, -v_monto
    );

    -- 9c. Posteo contable por configuración (plan F5, D1/D2/D10): partida
    -- espejo de la factura origen — Debe Ingresos (o DEVOLUCION_NC si está
    -- configurado) / Haber CxC analítica — SOLO si activo_notas está
    -- encendido. Misma transacción: si el posteo falla (sin asiento NOTAS,
    -- cuenta sin resolver, sin período y sin encolar), la emisión completa se
    -- revierte. Devuelve NULL si quedó encolada en con_partida_pendiente.
    SELECT c.activo_notas INTO v_activo_notas
    FROM public.con_integracion_config c
    WHERE c.company_id = p_company_id;

    IF COALESCE(v_activo_notas, false) THEN
        v_poliza_id := public.sp_con_generar_comprobante_config(
            p_company_id,
            'NOTAS',
            'NC',
            v_nota_id,
            v_numero,
            current_date,
            concat('N/C ', v_numero, ' s/factura ', v_factura.numfactura),
            p_usuario_emisor,
            public.fn_con_lineas_nota(p_company_id, 'NC', v_nota_id));

        UPDATE public.adm_nota_credito
        SET poliza_id = v_poliza_id
        WHERE adm_nota_credito.nota_credito_id = v_nota_id;
    END IF;

    -- 10. Resultado
    RETURN QUERY SELECT
        true,
        'OK'::text,
        CASE WHEN v_anula
             THEN 'Nota de crédito emitida y factura origen anulada.'
             ELSE 'Nota de crédito parcial emitida (factura origen sigue activa).'
        END::text,
        v_nota_id,
        v_numero,
        v_correlativo;
END;
$function$;

-- -----------------------------------------------------------------------------
-- 3b. sp_adm_emitir_nota_debito — posteo por configuración (paso 8c)
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.sp_adm_emitir_nota_debito(
    p_company_id bigint,
    p_factura_origen_id integer,
    p_motivo_aumento_id smallint,
    p_motivo_detalle varchar,
    p_monto_aumentar numeric,            -- requerido (un ND siempre indica cuánto aumenta)
    p_lineas jsonb,                      -- NULL = una sola línea con el motivo
    p_usuario_emisor varchar,
    p_cai_id bigint                      -- CAI específico tipo ND
)
RETURNS TABLE (
    success boolean,
    codigo text,
    mensaje text,
    nota_debito_id bigint,
    numero_documento text,
    correlativo bigint
)
LANGUAGE plpgsql
AS $function$
DECLARE
    v_factura record;
    v_cai record;
    v_company record;
    v_correlativo bigint;
    v_numero text;
    v_nota_id bigint;
    v_monto numeric(18,4);
    v_linea record;
    v_motivo_desc text;
    v_saldo_anterior numeric(18,4) := 0;
    v_activo_notas boolean;
    v_poliza_id bigint;
BEGIN
    -- 1. Validar factura origen
    SELECT f.id, f.numfactura, f.fechaemision, f.clientecodigo, f.saldototal,
           f.estado, f.company_id
    INTO v_factura
    FROM public.factura f
    WHERE f.id = p_factura_origen_id
      AND f.company_id = p_company_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'FACTURA_NO_EXISTE: factura origen % no existe para company %.',
            p_factura_origen_id, p_company_id;
    END IF;

    IF COALESCE(v_factura.estado, '') = 'N' THEN
        RAISE EXCEPTION 'FACTURA_ANULADA: no se puede emitir ND sobre la factura anulada %.', v_factura.numfactura;
    END IF;

    -- 2. Validar CAI emitible y tipo ND (7)
    IF NOT public.fn_adm_validar_cai_emitible(p_company_id, p_cai_id) THEN
        RAISE EXCEPTION 'CAI_NO_EMITIBLE: el CAI % no está vigente o pasó su fecha límite de emisión.', p_cai_id;
    END IF;

    SELECT c.cai_id, c.prefijo_documento, c.correlativo_actual, c.rango_hasta,
           c.fecha_limite_emision, c.leyenda_rango, c.tipo_documento_fiscal_id,
           c.establecimiento_codigo
    INTO v_cai
    FROM public.adm_cai_facturacion c
    WHERE c.company_id = p_company_id AND c.cai_id = p_cai_id;

    IF v_cai.tipo_documento_fiscal_id <> 7 THEN
        RAISE EXCEPTION 'CAI_TIPO_INCORRECTO: el CAI % es tipo %, se requiere tipo 7 (Nota de Débito).',
            p_cai_id, v_cai.tipo_documento_fiscal_id;
    END IF;

    -- 3. Correlativo
    v_correlativo := v_cai.correlativo_actual + 1;
    IF v_correlativo > v_cai.rango_hasta THEN
        RAISE EXCEPTION 'CAI_AGOTADO: el CAI % alcanzó su rango máximo (%).', p_cai_id, v_cai.rango_hasta;
    END IF;
    v_numero := concat(COALESCE(v_cai.prefijo_documento, ''), lpad(v_correlativo::text, 8, '0'));

    -- 4. Snapshot emisor
    SELECT co.tax_id, co.legal_name, co.commercial_name, co.address
    INTO v_company
    FROM public.cfg_company co
    WHERE co.company_id = p_company_id;

    -- 5. Monto a aumentar (requerido)
    v_monto := COALESCE(p_monto_aumentar, 0)::numeric(18,4);
    IF v_monto <= 0 THEN
        RAISE EXCEPTION 'MONTO_INVALIDO: el monto a aumentar debe ser mayor a 0.';
    END IF;

    SELECT ma.descripcion INTO v_motivo_desc
    FROM public.cfg_motivo_aumento ma
    WHERE ma.motivo_aumento_id = p_motivo_aumento_id;

    -- 6. INSERT cabecera
    INSERT INTO public.adm_nota_debito (
        company_id, establecimiento_codigo,
        tipo_documento_fiscal_id, numero_documento, cai_id, correlativo,
        fecha_limite_cai, leyenda_cai_rango,
        rtn_emisor, razon_social_emisor, direccion_emisor,
        cliente_id, rtn_receptor, razon_social_receptor, direccion_receptor,
        factura_origen_id, factura_origen_numero, factura_origen_fecha, factura_origen_cai,
        motivo_aumento_id, motivo_detalle,
        monto_aumentar, isv_aumentar, total_nota,
        estado_id, usuario_emisor, created_by
    )
    SELECT
        p_company_id, COALESCE(v_cai.establecimiento_codigo, '000'),
        7, v_numero, p_cai_id, v_correlativo,
        v_cai.fecha_limite_emision, v_cai.leyenda_rango,
        COALESCE(v_company.tax_id, ''), COALESCE(v_company.legal_name, v_company.commercial_name, ''), v_company.address,
        cm.maestro_cliente_id, cm.maestro_cliente_rtn,
        cm.maestro_cliente_nombre, NULL,
        v_factura.id, v_factura.numfactura, v_factura.fechaemision, v_factura.numfactura,
        p_motivo_aumento_id, p_motivo_detalle,
        v_monto, 0, v_monto,
        1, p_usuario_emisor, p_usuario_emisor
    FROM public.cliente_maestro cm
    WHERE cm.maestro_cliente_clave = v_factura.clientecodigo
      AND cm.company_id = p_company_id
    LIMIT 1
    RETURNING adm_nota_debito.nota_debito_id INTO v_nota_id;

    IF v_nota_id IS NULL THEN
        RAISE EXCEPTION 'CLIENTE_NO_EXISTE: no se encontró cliente % para la factura origen.',
            v_factura.clientecodigo;
    END IF;

    -- 7. Detalle
    IF p_lineas IS NOT NULL THEN
        FOR v_linea IN
            SELECT *
            FROM jsonb_to_recordset(p_lineas) AS l(
                servicio_id bigint,
                servicio_codigo text,
                descripcion text,
                cantidad numeric,
                monto_unitario numeric,
                monto_total numeric,
                isv_monto numeric,
                cuenta_contable_codigo text
            )
        LOOP
            INSERT INTO public.adm_nota_debito_detalle (
                nota_debito_id, servicio_id, servicio_codigo, descripcion,
                cantidad, monto_unitario, monto_total, isv_monto, cuenta_contable_codigo
            )
            VALUES (
                v_nota_id, v_linea.servicio_id, v_linea.servicio_codigo,
                COALESCE(v_linea.descripcion, v_linea.servicio_codigo, ''),
                COALESCE(v_linea.cantidad, 1), COALESCE(v_linea.monto_unitario, 0),
                COALESCE(v_linea.monto_total, 0), COALESCE(v_linea.isv_monto, 0),
                v_linea.cuenta_contable_codigo
            );
        END LOOP;
    ELSE
        -- Una sola línea con el motivo del aumento
        INSERT INTO public.adm_nota_debito_detalle (
            nota_debito_id, servicio_id, servicio_codigo, descripcion,
            cantidad, monto_unitario, monto_total, isv_monto
        )
        VALUES (
            v_nota_id, NULL, NULL,
            COALESCE(v_motivo_desc, 'Ajuste por nota de débito'),
            1, v_monto, v_monto, 0
        );
    END IF;

    -- 8. Avanzar correlativo
    UPDATE public.adm_cai_facturacion
    SET correlativo_actual = v_correlativo,
        updated_at = now(),
        updated_by = p_usuario_emisor
    WHERE company_id = p_company_id AND cai_id = p_cai_id;

    -- 8b. Reflejar la ND en el estado de cuenta del cliente (transaccion_abonado).
    -- Una ND AUMENTA el saldo del cliente → debitos = monto, saldo_detalle positivo.
    SELECT COALESCE(ta.saldo, 0)
    INTO v_saldo_anterior
    FROM public.transaccion_abonado ta
    WHERE ta.company_id = p_company_id
      AND ta.cliente_clave = v_factura.clientecodigo
      AND ta.estado = 'A'
    ORDER BY ta.ide DESC
    LIMIT 1;

    INSERT INTO public.transaccion_abonado (
        company_id, cliente_clave, tipotransaccion, docufuente,
        fecha_docu, tipo_partida, descripcion,
        debitos, creditos, saldo,
        estado, estado_id, fecha_registro, usuario, saldo_detalle
    )
    VALUES (
        p_company_id, v_factura.clientecodigo, '206', v_nota_id,
        current_date, '01',
        concat('N/D ', v_numero, ' s/factura ', v_factura.numfactura),
        v_monto, 0, COALESCE(v_saldo_anterior, 0) + v_monto,
        'A', 1, current_date, p_usuario_emisor, v_monto
    );

    -- 8c. Posteo contable por configuración (plan F5, D1/D2/D10): partida
    -- inversa a la NC — Debe CxC analítica / Haber Ingresos — SOLO si
    -- activo_notas está encendido. Misma transacción (atómico); NULL =
    -- encolada en con_partida_pendiente.
    SELECT c.activo_notas INTO v_activo_notas
    FROM public.con_integracion_config c
    WHERE c.company_id = p_company_id;

    IF COALESCE(v_activo_notas, false) THEN
        v_poliza_id := public.sp_con_generar_comprobante_config(
            p_company_id,
            'NOTAS',
            'ND',
            v_nota_id,
            v_numero,
            current_date,
            concat('N/D ', v_numero, ' s/factura ', v_factura.numfactura),
            p_usuario_emisor,
            public.fn_con_lineas_nota(p_company_id, 'ND', v_nota_id));

        UPDATE public.adm_nota_debito
        SET poliza_id = v_poliza_id
        WHERE adm_nota_debito.nota_debito_id = v_nota_id;
    END IF;

    -- 9. Resultado (un ND NO anula la factura origen, solo aumenta el saldo del cliente)
    RETURN QUERY SELECT
        true,
        'OK'::text,
        'Nota de débito emitida correctamente.'::text,
        v_nota_id,
        v_numero,
        v_correlativo;
END;
$function$;

COMMENT ON FUNCTION public.sp_adm_emitir_nota_credito(bigint, integer, smallint, varchar, numeric, jsonb, varchar, bigint) IS
'Emite NC V3 (SAR) y, si con_integracion_config.activo_notas está encendido, postea la partida espejo de la factura origen (Debe Ingresos o DEVOLUCION_NC / Haber CxC analítica) vía sp_con_generar_comprobante_config en la misma transacción (plan F5, D1/D2). Sin período abierto aplica encolar_sin_periodo.';
COMMENT ON FUNCTION public.sp_adm_emitir_nota_debito(bigint, integer, smallint, varchar, numeric, jsonb, varchar, bigint) IS
'Emite ND V3 (SAR) y, si con_integracion_config.activo_notas está encendido, postea la partida inversa (Debe CxC analítica / Haber Ingresos) vía sp_con_generar_comprobante_config en la misma transacción (plan F5, D1/D2). Sin período abierto aplica encolar_sin_periodo.';

-- -----------------------------------------------------------------------------
-- 4. sp_con_procesar_partida_pendiente v2 — puebla poliza_id de la nota
--    Única novedad vs la versión de F4: al postear una pendiente del módulo
--    NOTAS, actualiza adm_nota_credito/adm_nota_debito.poliza_id.
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.sp_con_procesar_partida_pendiente(
    p_company_id bigint,
    p_partida_pendiente_id bigint,
    p_user text
) RETURNS bigint
LANGUAGE plpgsql
AS $function$
DECLARE
    v_pendiente record;
    v_poliza_id bigint;
    v_doc_type varchar;
BEGIN
    SELECT * INTO v_pendiente
    FROM public.con_partida_pendiente pp
    WHERE pp.company_id = p_company_id
      AND pp.partida_pendiente_id = p_partida_pendiente_id
    FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'La pendiente % no existe para la empresa %.', p_partida_pendiente_id, p_company_id;
    END IF;

    IF v_pendiente.status_id <> 1 THEN
        RAISE EXCEPTION 'La pendiente % no está en estado PENDIENTE (status_id=%).', p_partida_pendiente_id, v_pendiente.status_id;
    END IF;

    IF v_pendiente.origen_tipo = 'LOTE_FACTURACION' THEN
        RAISE EXCEPTION 'Las pendientes del lote de facturación se reprocesan desde la pantalla del lote (sp_con_generar_partidas_facturacion).';
    END IF;

    v_poliza_id := public.sp_con_generar_comprobante_config(
        p_company_id,
        v_pendiente.payload->>'module',
        v_pendiente.payload->>'document_type',
        (v_pendiente.payload->>'document_id')::bigint,
        v_pendiente.payload->>'document_number',
        (v_pendiente.payload->>'poliza_date')::date,
        v_pendiente.payload->>'description',
        p_user,
        v_pendiente.payload->'lineas');

    IF v_poliza_id IS NOT NULL THEN
        UPDATE public.con_partida_pendiente pp
        SET status_id = 2, poliza_id = v_poliza_id,
            procesada_at = now(), procesada_by = p_user,
            updated_at = now(), updated_by = p_user
        WHERE pp.partida_pendiente_id = p_partida_pendiente_id;

        -- Trazabilidad NC/ND (F5): la nota encolada recupera su poliza_id.
        IF upper(COALESCE(v_pendiente.payload->>'module', '')) = 'NOTAS' THEN
            v_doc_type := upper(COALESCE(v_pendiente.payload->>'document_type', ''));
            IF v_doc_type = 'NC' THEN
                UPDATE public.adm_nota_credito nc
                SET poliza_id = v_poliza_id
                WHERE nc.company_id = p_company_id
                  AND nc.nota_credito_id = (v_pendiente.payload->>'document_id')::bigint;
            ELSIF v_doc_type = 'ND' THEN
                UPDATE public.adm_nota_debito nd
                SET poliza_id = v_poliza_id
                WHERE nd.company_id = p_company_id
                  AND nd.nota_debito_id = (v_pendiente.payload->>'document_id')::bigint;
            END IF;
        END IF;
    ELSE
        UPDATE public.con_partida_pendiente pp
        SET ultimo_error = 'Sigue sin período contable abierto para la fecha del documento',
            updated_at = now(), updated_by = p_user
        WHERE pp.partida_pendiente_id = p_partida_pendiente_id;
    END IF;

    RETURN v_poliza_id;
END;
$function$;

COMMENT ON FUNCTION public.sp_con_procesar_partida_pendiente(bigint, bigint, text) IS
'Reprocesa una pendiente de comprobante de con_partida_pendiente (plan F4; v2 en F5): reinvoca sp_con_generar_comprobante_config con el payload guardado; si postea marca PROCESADA con su poliza_id y, para pendientes del módulo NOTAS, puebla poliza_id en adm_nota_credito/adm_nota_debito. Las de LOTE_FACTURACION se reprocesan con el lote (F3).';
