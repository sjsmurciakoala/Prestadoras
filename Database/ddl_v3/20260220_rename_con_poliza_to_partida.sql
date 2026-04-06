-- Rename accounting tables to the partida naming convention.
-- con_poliza -> con_partida_hdr
-- con_poliza_linea -> con_partida_dtl

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.tables
        WHERE table_schema = 'public' AND table_name = 'con_poliza'
    ) THEN
        ALTER TABLE public.con_poliza RENAME TO con_partida_hdr;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.tables
        WHERE table_schema = 'public' AND table_name = 'con_poliza_linea'
    ) THEN
        ALTER TABLE public.con_poliza_linea RENAME TO con_partida_dtl;
    END IF;
END
$$;
