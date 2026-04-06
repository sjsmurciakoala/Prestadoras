-- Ajustes para pólizas manuales: tipo, totales, posteo y tercero en línea
-- Ejecutar solo si las columnas no existen; status se mantiene como smallint (0 draft, 1 posted, 2 void)

-- con_partida_hdr: columnas faltantes y FK a con_tipo_transaccion
ALTER TABLE public.con_partida_hdr
  ADD COLUMN IF NOT EXISTS type_id bigint NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS posted_at timestamptz NULL,
  ADD COLUMN IF NOT EXISTS posted_by uuid NULL,
  ADD COLUMN IF NOT EXISTS total_debit numeric(18,2) NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS total_credit numeric(18,2) NOT NULL DEFAULT 0;

-- Alinear status si estuviera como texto
DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.columns
             WHERE table_schema='public' AND table_name='con_partida_hdr'
               AND column_name='status' AND data_type IN ('character varying','text')) THEN
    EXECUTE 'ALTER TABLE public.con_partida_hdr ALTER COLUMN status TYPE smallint USING status::smallint';
  END IF;
END$$;

-- UNIQUE requerida para poder referenciar por empresa + tipo
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_tipo_trans_company_type') THEN
    ALTER TABLE public.con_tipo_transaccion
      ADD CONSTRAINT uq_tipo_trans_company_type UNIQUE (company_id, type_id);
  END IF;
END$$;

-- FK con_partida_hdr -> con_tipo_transaccion (por empresa + tipo)
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_poliza_tipo') THEN
    ALTER TABLE public.con_partida_hdr
      ADD CONSTRAINT fk_poliza_tipo
        FOREIGN KEY (company_id, type_id)
        REFERENCES public.con_tipo_transaccion(company_id, type_id);
  END IF;
END$$;

-- con_partida_dtl: tercero y FK
ALTER TABLE public.con_partida_dtl
  ADD COLUMN IF NOT EXISTS third_party_id bigint NULL;

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_poliza_linea_tercero') THEN
    ALTER TABLE public.con_partida_dtl
      ADD CONSTRAINT fk_poliza_linea_tercero
        FOREIGN KEY (company_id, third_party_id)
        REFERENCES public.con_tercero(company_id, third_party_id);
  END IF;
END$$;

-- Índices de apoyo (solo si faltan)
CREATE INDEX IF NOT EXISTS ix_con_partida_hdr_type ON public.con_partida_hdr (company_id, type_id);
CREATE INDEX IF NOT EXISTS ix_con_partida_dtl_third ON public.con_partida_dtl (company_id, third_party_id);


