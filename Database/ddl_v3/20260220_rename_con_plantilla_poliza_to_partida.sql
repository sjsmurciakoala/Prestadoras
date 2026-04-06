-- Renombra tablas de plantillas contables a la convención de partidas.
-- con_plantilla_poliza -> con_plantilla_partida_hdr
-- con_plantilla_poliza_linea -> con_plantilla_partida_dtl

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.tables
        WHERE table_schema = 'public' AND table_name = 'con_plantilla_poliza'
    ) THEN
        ALTER TABLE public.con_plantilla_poliza RENAME TO con_plantilla_partida_hdr;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.tables
        WHERE table_schema = 'public' AND table_name = 'con_plantilla_poliza_linea'
    ) THEN
        ALTER TABLE public.con_plantilla_poliza_linea RENAME TO con_plantilla_partida_dtl;
    END IF;
END
$$;
