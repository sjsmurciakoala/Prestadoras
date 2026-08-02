-- =====================================================================
-- 2026-07-23 — Compromisos de proveedor: ampliar la descripcion del detalle
--
-- Motivo:
--   El concepto del encabezado (prv_compromiso_hdr.concepto) se copia hacia
--   la descripcion de cada linea del detalle. La descripcion estaba limitada
--   a 150 caracteres, el mismo tope que el concepto, por lo que no habia
--   margen para ampliarla o complementarla en la linea.
--
--   Se amplia la columna a varchar(1000) para dejar holgura en la tabla.
--   La vista (CompromisoProveedorEdit.razor) y la validacion de servicio
--   siguen limitando la captura a 250 caracteres.
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
       AND column_name  = 'descripcion';

    IF v_longitud IS NULL THEN
        RAISE EXCEPTION 'No existe public.prv_compromiso_dtl.descripcion (o no es varchar con longitud definida).';
    END IF;

    IF v_longitud < 1000 THEN
        ALTER TABLE public.prv_compromiso_dtl
            ALTER COLUMN descripcion TYPE varchar(1000);
        RAISE NOTICE 'prv_compromiso_dtl.descripcion ampliada de varchar(%) a varchar(1000).', v_longitud;
    ELSE
        RAISE NOTICE 'prv_compromiso_dtl.descripcion ya es varchar(%); no se aplica ningun cambio.', v_longitud;
    END IF;
END
$$;

COMMIT;

-- Verificacion posterior:
-- SELECT column_name, data_type, character_maximum_length
--   FROM information_schema.columns
--  WHERE table_schema = 'public'
--    AND table_name   = 'prv_compromiso_dtl'
--    AND column_name  = 'descripcion';
