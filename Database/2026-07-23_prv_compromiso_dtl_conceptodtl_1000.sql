-- =====================================================================
-- 2026-07-23 — Compromisos de proveedor: ampliar conceptodtl del detalle
--
-- Motivo:
--   prv_compromiso_dtl.conceptodtl es un campo derivado de la descripcion de
--   la linea (no se captura en la vista). Estaba limitado a 100 caracteres,
--   por lo que la descripcion (que admite 250) se recortaba al copiarse hacia
--   conceptodtl. Se amplia a varchar(1000) para dar holgura y que conceptodtl
--   pueda conservar la descripcion completa sin recorte.
--
-- Naturaleza: NO destructivo. Ampliar la longitud de un varchar en Postgres
--   no reescribe la tabla ni altera los datos existentes.
--
-- Idempotente: solo aplica el ALTER si la longitud actual es menor a 1000.
-- =====================================================================

BEGIN;

DO $$
DECLARE
    v_longitud integer;
BEGIN
    SELECT character_maximum_length
      INTO v_longitud
      FROM information_schema.columns
     WHERE table_schema = 'public'
       AND table_name   = 'prv_compromiso_dtl'
       AND column_name  = 'conceptodtl';

    IF v_longitud IS NULL THEN
        RAISE EXCEPTION 'No existe public.prv_compromiso_dtl.conceptodtl (o no es varchar con longitud definida).';
    END IF;

    IF v_longitud < 1000 THEN
        ALTER TABLE public.prv_compromiso_dtl
            ALTER COLUMN conceptodtl TYPE varchar(1000);
        RAISE NOTICE 'prv_compromiso_dtl.conceptodtl ampliada de varchar(%) a varchar(1000).', v_longitud;
    ELSE
        RAISE NOTICE 'prv_compromiso_dtl.conceptodtl ya es varchar(%); no se aplica ningun cambio.', v_longitud;
    END IF;
END
$$;

COMMIT;

-- Verificacion posterior:
-- SELECT column_name, data_type, character_maximum_length
--   FROM information_schema.columns
--  WHERE table_schema = 'public'
--    AND table_name   = 'prv_compromiso_dtl'
--    AND column_name  = 'conceptodtl';
