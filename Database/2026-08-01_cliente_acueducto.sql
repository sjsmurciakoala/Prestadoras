-- =============================================================================
-- Pruebas operativas jul-2026: "El campo Acueducto debe visualizarse a nivel
-- de cliente y no en la sección del medidor".
--
-- Nueva columna cliente_maestro.acueducto con backfill desde el acueducto del
-- medidor asignado (cliente_detalle más reciente → maestro_medidor). El campo
-- del medidor se conserva (describe al aparato); el del cliente es el que la
-- ficha muestra y edita.
-- =============================================================================

\set ON_ERROR_STOP on

BEGIN;

ALTER TABLE public.cliente_maestro
    ADD COLUMN IF NOT EXISTS acueducto varchar(50);

COMMENT ON COLUMN public.cliente_maestro.acueducto IS
'Acueducto que sirve al cliente (pruebas operativas jul-2026; backfill inicial desde el medidor asignado).';

WITH medidor_cliente AS (
    SELECT DISTINCT ON (cd.maestro_cliente_id)
           cd.maestro_cliente_id,
           m.maestro_medidor_acueducto
    FROM public.cliente_detalle cd
    JOIN public.maestro_medidor m ON m.maestro_medidor_id = cd.maestro_medidor_id
    WHERE NULLIF(TRIM(m.maestro_medidor_acueducto), '') IS NOT NULL
    ORDER BY cd.maestro_cliente_id, COALESCE(cd.fechamodificacion, cd.fechacreacion) DESC NULLS LAST
)
UPDATE public.cliente_maestro cm
SET acueducto = mc.maestro_medidor_acueducto
FROM medidor_cliente mc
WHERE mc.maestro_cliente_id = cm.maestro_cliente_id
  AND cm.acueducto IS NULL;

COMMIT;
