-- ============================================================
-- Backfill controlado de posted_by en con_partida_hdr
-- Fecha: 2026-03-10
-- Objetivo:
--   Completar posted_by historico en polizas POSTED (status=1)
--   usando created_by/updated_by con resolucion por:
--   1) valor numerico directo
--   2) match por usuarioapc.usuario (case-insensitive)
-- ============================================================
--
-- Uso recomendado:
-- 1) Ajustar parametros en tmp_con_posted_by_backfill_params.
-- 2) Ejecutar seccion A (preview y conteos por fuente).
-- 3) Ejecutar seccion B (backup + update) en ventana controlada.
-- 4) Ejecutar seccion C (pendientes no resueltos).
-- 5) Solo si es necesario, ejecutar seccion D (rollback por backup_tag).
--

-- ------------------------------------------------------------
-- Parametros de alcance (NULL = todos)
-- ------------------------------------------------------------
DROP TABLE IF EXISTS tmp_con_posted_by_backfill_params;
CREATE TEMP TABLE tmp_con_posted_by_backfill_params (
    company_id bigint NULL,
    from_posted_at date NULL,
    to_posted_at date NULL,
    backup_tag text NOT NULL
);

INSERT INTO tmp_con_posted_by_backfill_params (
    company_id, from_posted_at, to_posted_at, backup_tag
) VALUES (
    NULL, NULL, NULL, 'backfill_posted_by_20260310'
);


-- ------------------------------------------------------------
-- A) Preview: resumen de resolucion + muestra de pendientes
-- ------------------------------------------------------------
WITH params AS (
    SELECT company_id, from_posted_at, to_posted_at
    FROM tmp_con_posted_by_backfill_params
    LIMIT 1
),
map_user AS (
    SELECT
        upper(btrim(u.usuario)) AS usuario_norm,
        MAX(u.ide)::bigint AS user_id
    FROM public.usuarioapc u
    WHERE u.usuario IS NOT NULL
      AND btrim(u.usuario) <> ''
    GROUP BY upper(btrim(u.usuario))
),
base AS (
    SELECT
        h.poliza_id,
        h.company_id,
        h.created_by,
        h.updated_by,
        h.posted_at
    FROM public.con_partida_hdr h
    JOIN params p ON true
    WHERE h.status = 1
      AND h.posted_by IS NULL
      AND (p.company_id IS NULL OR h.company_id = p.company_id)
      AND (p.from_posted_at IS NULL OR h.posted_at::date >= p.from_posted_at)
      AND (p.to_posted_at IS NULL OR h.posted_at::date <= p.to_posted_at)
),
resolved AS (
    SELECT
        b.*,
        CASE
            WHEN btrim(COALESCE(b.updated_by, '')) ~ '^[0-9]+$' THEN btrim(b.updated_by)::bigint
            WHEN btrim(COALESCE(b.created_by, '')) ~ '^[0-9]+$' THEN btrim(b.created_by)::bigint
            WHEN mu_upd.user_id IS NOT NULL THEN mu_upd.user_id
            WHEN mu_cre.user_id IS NOT NULL THEN mu_cre.user_id
            ELSE NULL
        END AS candidate_user_id,
        CASE
            WHEN btrim(COALESCE(b.updated_by, '')) ~ '^[0-9]+$' THEN 'updated_by_numeric'
            WHEN btrim(COALESCE(b.created_by, '')) ~ '^[0-9]+$' THEN 'created_by_numeric'
            WHEN mu_upd.user_id IS NOT NULL THEN 'updated_by_usuarioapc'
            WHEN mu_cre.user_id IS NOT NULL THEN 'created_by_usuarioapc'
            ELSE 'unresolved'
        END AS resolution_source
    FROM base b
    LEFT JOIN map_user mu_upd
      ON mu_upd.usuario_norm = upper(btrim(COALESCE(b.updated_by, '')))
    LEFT JOIN map_user mu_cre
      ON mu_cre.usuario_norm = upper(btrim(COALESCE(b.created_by, '')))
)
SELECT
    resolution_source,
    COUNT(*) AS cantidad
FROM resolved
GROUP BY resolution_source
ORDER BY resolution_source;

WITH params AS (
    SELECT company_id, from_posted_at, to_posted_at
    FROM tmp_con_posted_by_backfill_params
    LIMIT 1
),
map_user AS (
    SELECT
        upper(btrim(u.usuario)) AS usuario_norm,
        MAX(u.ide)::bigint AS user_id
    FROM public.usuarioapc u
    WHERE u.usuario IS NOT NULL
      AND btrim(u.usuario) <> ''
    GROUP BY upper(btrim(u.usuario))
),
base AS (
    SELECT
        h.poliza_id,
        h.company_id,
        h.created_by,
        h.updated_by,
        h.posted_at
    FROM public.con_partida_hdr h
    JOIN params p ON true
    WHERE h.status = 1
      AND h.posted_by IS NULL
      AND (p.company_id IS NULL OR h.company_id = p.company_id)
      AND (p.from_posted_at IS NULL OR h.posted_at::date >= p.from_posted_at)
      AND (p.to_posted_at IS NULL OR h.posted_at::date <= p.to_posted_at)
),
resolved AS (
    SELECT
        b.*,
        CASE
            WHEN btrim(COALESCE(b.updated_by, '')) ~ '^[0-9]+$' THEN btrim(b.updated_by)::bigint
            WHEN btrim(COALESCE(b.created_by, '')) ~ '^[0-9]+$' THEN btrim(b.created_by)::bigint
            WHEN mu_upd.user_id IS NOT NULL THEN mu_upd.user_id
            WHEN mu_cre.user_id IS NOT NULL THEN mu_cre.user_id
            ELSE NULL
        END AS candidate_user_id,
        CASE
            WHEN btrim(COALESCE(b.updated_by, '')) ~ '^[0-9]+$' THEN 'updated_by_numeric'
            WHEN btrim(COALESCE(b.created_by, '')) ~ '^[0-9]+$' THEN 'created_by_numeric'
            WHEN mu_upd.user_id IS NOT NULL THEN 'updated_by_usuarioapc'
            WHEN mu_cre.user_id IS NOT NULL THEN 'created_by_usuarioapc'
            ELSE 'unresolved'
        END AS resolution_source
    FROM base b
    LEFT JOIN map_user mu_upd
      ON mu_upd.usuario_norm = upper(btrim(COALESCE(b.updated_by, '')))
    LEFT JOIN map_user mu_cre
      ON mu_cre.usuario_norm = upper(btrim(COALESCE(b.created_by, '')))
)
SELECT
    poliza_id,
    company_id,
    created_by,
    updated_by,
    posted_at,
    resolution_source
FROM resolved
WHERE candidate_user_id IS NULL
ORDER BY poliza_id DESC
LIMIT 200;


-- ------------------------------------------------------------
-- B) Backup + backfill
-- ------------------------------------------------------------
BEGIN;

LOCK TABLE public.con_partida_hdr IN SHARE ROW EXCLUSIVE MODE;

CREATE TABLE IF NOT EXISTS public.con_partida_hdr_posted_by_backfill_hist (
    backup_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    backup_at timestamptz NOT NULL DEFAULT now(),
    backup_tag text NOT NULL,
    poliza_id bigint NOT NULL,
    company_id bigint NOT NULL,
    old_posted_by bigint NULL,
    old_updated_at timestamptz NULL,
    old_updated_by varchar(100) NULL
);

WITH params AS (
    SELECT company_id, from_posted_at, to_posted_at, backup_tag
    FROM tmp_con_posted_by_backfill_params
    LIMIT 1
),
map_user AS (
    SELECT
        upper(btrim(u.usuario)) AS usuario_norm,
        MAX(u.ide)::bigint AS user_id
    FROM public.usuarioapc u
    WHERE u.usuario IS NOT NULL
      AND btrim(u.usuario) <> ''
    GROUP BY upper(btrim(u.usuario))
),
base AS (
    SELECT
        h.poliza_id,
        h.company_id,
        h.created_by,
        h.updated_by,
        h.posted_at
    FROM public.con_partida_hdr h
    JOIN params p ON true
    WHERE h.status = 1
      AND h.posted_by IS NULL
      AND (p.company_id IS NULL OR h.company_id = p.company_id)
      AND (p.from_posted_at IS NULL OR h.posted_at::date >= p.from_posted_at)
      AND (p.to_posted_at IS NULL OR h.posted_at::date <= p.to_posted_at)
),
resolved AS (
    SELECT
        b.poliza_id,
        CASE
            WHEN btrim(COALESCE(b.updated_by, '')) ~ '^[0-9]+$' THEN btrim(b.updated_by)::bigint
            WHEN btrim(COALESCE(b.created_by, '')) ~ '^[0-9]+$' THEN btrim(b.created_by)::bigint
            WHEN mu_upd.user_id IS NOT NULL THEN mu_upd.user_id
            WHEN mu_cre.user_id IS NOT NULL THEN mu_cre.user_id
            ELSE NULL
        END AS candidate_user_id
    FROM base b
    LEFT JOIN map_user mu_upd
      ON mu_upd.usuario_norm = upper(btrim(COALESCE(b.updated_by, '')))
    LEFT JOIN map_user mu_cre
      ON mu_cre.usuario_norm = upper(btrim(COALESCE(b.created_by, '')))
)
INSERT INTO public.con_partida_hdr_posted_by_backfill_hist (
    backup_at, backup_tag, poliza_id, company_id, old_posted_by, old_updated_at, old_updated_by
)
SELECT
    now(),
    p.backup_tag,
    h.poliza_id,
    h.company_id,
    h.posted_by,
    h.updated_at,
    h.updated_by
FROM public.con_partida_hdr h
JOIN resolved r ON r.poliza_id = h.poliza_id
JOIN params p ON true
WHERE h.posted_by IS NULL
  AND r.candidate_user_id IS NOT NULL;

WITH params AS (
    SELECT company_id, from_posted_at, to_posted_at
    FROM tmp_con_posted_by_backfill_params
    LIMIT 1
),
map_user AS (
    SELECT
        upper(btrim(u.usuario)) AS usuario_norm,
        MAX(u.ide)::bigint AS user_id
    FROM public.usuarioapc u
    WHERE u.usuario IS NOT NULL
      AND btrim(u.usuario) <> ''
    GROUP BY upper(btrim(u.usuario))
),
base AS (
    SELECT
        h.poliza_id,
        h.company_id,
        h.created_by,
        h.updated_by,
        h.posted_at
    FROM public.con_partida_hdr h
    JOIN params p ON true
    WHERE h.status = 1
      AND h.posted_by IS NULL
      AND (p.company_id IS NULL OR h.company_id = p.company_id)
      AND (p.from_posted_at IS NULL OR h.posted_at::date >= p.from_posted_at)
      AND (p.to_posted_at IS NULL OR h.posted_at::date <= p.to_posted_at)
),
resolved AS (
    SELECT
        b.poliza_id,
        CASE
            WHEN btrim(COALESCE(b.updated_by, '')) ~ '^[0-9]+$' THEN btrim(b.updated_by)::bigint
            WHEN btrim(COALESCE(b.created_by, '')) ~ '^[0-9]+$' THEN btrim(b.created_by)::bigint
            WHEN mu_upd.user_id IS NOT NULL THEN mu_upd.user_id
            WHEN mu_cre.user_id IS NOT NULL THEN mu_cre.user_id
            ELSE NULL
        END AS candidate_user_id
    FROM base b
    LEFT JOIN map_user mu_upd
      ON mu_upd.usuario_norm = upper(btrim(COALESCE(b.updated_by, '')))
    LEFT JOIN map_user mu_cre
      ON mu_cre.usuario_norm = upper(btrim(COALESCE(b.created_by, '')))
),
upd AS (
    UPDATE public.con_partida_hdr h
       SET posted_by = r.candidate_user_id,
           updated_at = now(),
           updated_by = COALESCE(h.updated_by, 'backfill_posted_by')
      FROM resolved r
     WHERE h.poliza_id = r.poliza_id
       AND h.posted_by IS NULL
       AND r.candidate_user_id IS NOT NULL
    RETURNING h.poliza_id
)
SELECT COUNT(*) AS updated_rows
FROM upd;

COMMIT;


-- ------------------------------------------------------------
-- C) Pendientes no resueltos despues del backfill
-- ------------------------------------------------------------
WITH params AS (
    SELECT company_id, from_posted_at, to_posted_at
    FROM tmp_con_posted_by_backfill_params
    LIMIT 1
)
SELECT
    h.poliza_id,
    h.company_id,
    h.created_by,
    h.updated_by,
    h.posted_at
FROM public.con_partida_hdr h
JOIN params p ON true
WHERE h.status = 1
  AND h.posted_by IS NULL
  AND (p.company_id IS NULL OR h.company_id = p.company_id)
  AND (p.from_posted_at IS NULL OR h.posted_at::date >= p.from_posted_at)
  AND (p.to_posted_at IS NULL OR h.posted_at::date <= p.to_posted_at)
ORDER BY h.poliza_id DESC
LIMIT 200;


-- ------------------------------------------------------------
-- D) Rollback (ejecutar solo si se requiere)
-- ------------------------------------------------------------
-- BEGIN;
--
-- WITH params AS (
--     SELECT backup_tag
--     FROM tmp_con_posted_by_backfill_params
--     LIMIT 1
-- ),
-- latest_backup AS (
--     SELECT
--         b.poliza_id,
--         b.old_posted_by,
--         ROW_NUMBER() OVER (PARTITION BY b.poliza_id ORDER BY b.backup_at DESC, b.backup_id DESC) AS rn
--     FROM public.con_partida_hdr_posted_by_backfill_hist b
--     JOIN params p ON p.backup_tag = b.backup_tag
-- )
-- UPDATE public.con_partida_hdr h
--    SET posted_by = lb.old_posted_by,
--        updated_at = now(),
--        updated_by = 'rollback_posted_by_backfill'
--   FROM latest_backup lb
--  WHERE h.poliza_id = lb.poliza_id
--    AND lb.rn = 1;
--
-- COMMIT;


BEGIN;

WITH target AS (
  SELECT poliza_id, company_id, posted_by, updated_at, updated_by
  FROM public.con_partida_hdr
  WHERE status = 1
    AND posted_by IS NULL
    AND (
      upper(btrim(COALESCE(created_by, ''))) = 'ADMIN@SIAD-DEMO.COM'
      OR upper(btrim(COALESCE(updated_by, ''))) = 'ADMIN@SIAD-DEMO.COM'
    )
),
bk AS (
  INSERT INTO public.con_partida_hdr_posted_by_backfill_hist
    (backup_at, backup_tag, poliza_id, company_id, old_posted_by, old_updated_at, old_updated_by)
  SELECT now(), 'manual_map_admin_demo_20260310',
         t.poliza_id, t.company_id, t.posted_by, t.updated_at, t.updated_by
  FROM target t
  RETURNING poliza_id
)
UPDATE public.con_partida_hdr h
   SET posted_by = 1, -- ide destino (jmurcia)
       updated_at = now(),
       updated_by = 'backfill_posted_by_manual'
 WHERE h.poliza_id IN (SELECT poliza_id FROM bk)
RETURNING h.poliza_id, h.posted_by;

COMMIT;


SELECT
  COUNT(*) FILTER (WHERE status = 1) AS total_posted,
  COUNT(*) FILTER (WHERE status = 1 AND posted_by IS NULL) AS posted_sin_usuario
FROM public.con_partida_hdr;


select  sp_con_generar_comprobante