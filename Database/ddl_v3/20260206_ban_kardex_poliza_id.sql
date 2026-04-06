-- Vinculo directo con partida contable en ban_kardex
ALTER TABLE public.ban_kardex
    ADD COLUMN IF NOT EXISTS poliza_id bigint;

CREATE INDEX IF NOT EXISTS ix_ban_kardex_poliza
  ON public.ban_kardex (poliza_id);

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.tables
        WHERE table_schema = 'public'
          AND table_name = 'con_partida_hdr'
    ) THEN
        IF NOT EXISTS (
            SELECT 1
            FROM pg_constraint
            WHERE conname = 'fk_ban_kardex_poliza'
              AND conrelid = 'public.ban_kardex'::regclass
        ) THEN
            ALTER TABLE public.ban_kardex
                ADD CONSTRAINT fk_ban_kardex_poliza
                FOREIGN KEY (poliza_id)
                REFERENCES public.con_partida_hdr(poliza_id)
                ON DELETE SET NULL;
        END IF;
    END IF;
END $$;
