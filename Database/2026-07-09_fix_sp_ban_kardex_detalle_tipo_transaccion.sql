-- =============================================================================
-- Fix: sp_ban_kardex_detalle devolvía el ID numérico del tipo de transacción
--      (k.id_tipo_transaccion::varchar, p.ej. "1") en lugar del CÓDIGO ("DEP").
--
-- Síntoma: al ver el detalle de una transacción bancaria, el combo
--          "Tipo de Transacción" quedaba en "-- Seleccione un tipo --",
--          porque el <select> tiene <option value="DEP">… y recibía "1".
--
-- Efecto secundario corregido: la CTE partidas_hdr compara
--   h.document_type = k.id_tipo_transaccion, y con_partida_hdr.document_type
--   guarda el CÓDIGO ('DEP'). Con el ID numérico ese join nunca casaba.
--
-- Cambio: única línea, la rama ELSE de v_tipo_col ahora resuelve el código
--   desde ban_tipos_transacciones (con fallback al ID si no se puede resolver).
--   Se conserva la rama de compatibilidad con tipo_movimiento.
-- Idempotente (CREATE OR REPLACE). Aplicar en mirror y SRV (regla espejo).
-- =============================================================================
CREATE OR REPLACE PROCEDURE public.sp_ban_kardex_detalle(IN p_company_id bigint, IN p_ban_kardex_id bigint, INOUT p_result refcursor)
 LANGUAGE plpgsql
AS $procedure$
DECLARE
    v_tipo_col   text;
    v_monto_expr text;
    v_poliza_expr text;
    v_use_source_doc boolean;
    v_sql        text;
BEGIN
    IF p_result IS NULL THEN
        p_result := 'sp_ban_kardex_detalle_cursor';
    END IF;

    -- Compatibilidad: tipo_movimiento (código directo) vs id_tipo_transaccion (FK numérico)
    IF EXISTS (
        SELECT 1
          FROM information_schema.columns
         WHERE table_schema = 'public'
           AND table_name = 'ban_kardex'
           AND column_name = 'tipo_movimiento'
    ) THEN
        v_tipo_col := 'k.tipo_movimiento::varchar';
    ELSIF EXISTS (
        SELECT 1
          FROM information_schema.tables
         WHERE table_schema = 'public'
           AND table_name = 'ban_tipos_transacciones'
    ) THEN
        -- id_tipo_transaccion es el FK numérico: resolver el código del tipo.
        v_tipo_col := 'COALESCE((SELECT tt.tipo_transaccion
                                   FROM public.ban_tipos_transacciones tt
                                  WHERE tt.company_id = k.company_id
                                    AND tt.ban_tipo_transaccion_id = k.id_tipo_transaccion
                                  LIMIT 1), k.id_tipo_transaccion::varchar)';
    ELSE
        v_tipo_col := 'k.id_tipo_transaccion::varchar';
    END IF;

    -- Compatibilidad: monto vs monto_debito/monto_credito
    IF EXISTS (
        SELECT 1
          FROM information_schema.columns
         WHERE table_schema = 'public'
           AND table_name = 'ban_kardex'
           AND column_name = 'monto'
    ) THEN
        v_monto_expr := 'k.monto';
    ELSE
        v_monto_expr := 'CASE WHEN k.monto_debito > 0 THEN k.monto_debito ELSE -k.monto_credito END';
    END IF;

    -- Compatibilidad: poliza_id (vinculo directo con partida contable)
    IF EXISTS (
        SELECT 1
          FROM information_schema.columns
         WHERE table_schema = 'public'
           AND table_name = 'ban_kardex'
           AND column_name = 'poliza_id'
    ) THEN
        v_poliza_expr := 'k.poliza_id';
    ELSE
        v_poliza_expr := 'NULL::bigint';
    END IF;

    v_use_source_doc := EXISTS (
        SELECT 1
          FROM information_schema.columns
         WHERE table_schema = 'public'
           AND table_name = 'con_partida_dtl'
           AND column_name = 'source_document'
    );

    v_sql := format(
        $fmt$
        WITH k AS (
            SELECT
                k.ban_kardex_id,
                k.company_id,
                k.banco_cuenta_id,
                %1$s AS id_tipo_transaccion,
                k.fecha_movimiento,
                k.descripcion,
                k.referencia,
                %3$s AS poliza_id,
                k.tasa_cambio,
                %2$s AS monto
            FROM public.ban_kardex k
            WHERE k.company_id = $1
              AND k.ban_kardex_id = $2
        ),
        partidas_hdr AS (
            SELECT h.poliza_id
            FROM public.con_partida_hdr h
            JOIN k ON h.company_id = k.company_id
            WHERE k.poliza_id IS NULL
              AND k.referencia IS NOT NULL
              AND h."module" = 'BANCOS'
              AND h.document_type = k.id_tipo_transaccion
              AND (
                    btrim(h.document_number) = btrim(k.referencia)
                 OR btrim(h.poliza_number) = btrim(k.referencia)
              )
        ),
        partidas_dtl AS (
            SELECT DISTINCT d.poliza_id
            FROM public.con_partida_dtl d
            JOIN k ON d.company_id = k.company_id
            WHERE k.poliza_id IS NULL
              AND k.referencia IS NOT NULL
              AND %4$s
        ),
        partidas AS (
            SELECT poliza_id FROM k WHERE poliza_id IS NOT NULL
            UNION
            SELECT poliza_id FROM partidas_hdr
            UNION
            SELECT poliza_id FROM partidas_dtl
        )
        SELECT
            k.ban_kardex_id,
            k.banco_cuenta_id,
            k.id_tipo_transaccion,
            k.fecha_movimiento,
            k.descripcion,
            k.referencia,
            k.tasa_cambio,
            k.monto,
            p.poliza_id,
            d.line_number,
            d.account_id,
            d.description AS line_description,
            d.debit_amount,
            d.credit_amount,
            d.source_document
        FROM k
        LEFT JOIN public.ban_cuenta c
          ON c.company_id = k.company_id
         AND c.banco_cuenta_id = k.banco_cuenta_id
        LEFT JOIN partidas p
          ON TRUE
        LEFT JOIN public.con_partida_dtl d
          ON d.company_id = k.company_id
         AND d.poliza_id = p.poliza_id
         AND (c.cont_account_id IS NULL OR d.account_id <> c.cont_account_id)
        ORDER BY d.line_number
        $fmt$,
        v_tipo_col,
        v_monto_expr,
        v_poliza_expr,
        CASE WHEN v_use_source_doc THEN 'd.source_document = k.referencia' ELSE '1=0' END
    );

    OPEN p_result FOR EXECUTE v_sql USING p_company_id, p_ban_kardex_id;
END;
$procedure$;
