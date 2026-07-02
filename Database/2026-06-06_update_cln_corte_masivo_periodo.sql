-- Reestructura cortes masivos: agregar período, días de corte; hacer criterio nullable
ALTER TABLE cln_corte_masivo_hdr
    ALTER COLUMN criterio DROP NOT NULL;

ALTER TABLE cln_corte_masivo_hdr
    ADD COLUMN IF NOT EXISTS periodo_anio INT,
    ADD COLUMN IF NOT EXISTS periodo_mes  INT,
    ADD COLUMN IF NOT EXISTS dias_corte   INT NOT NULL DEFAULT 0;
