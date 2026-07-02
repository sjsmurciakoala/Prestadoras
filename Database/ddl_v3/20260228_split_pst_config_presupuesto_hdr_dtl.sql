-- Divide public.pst_config_presupuesto en encabezado/detalle.
-- Mantiene idempotencia y migra los datos existentes.

CREATE TABLE IF NOT EXISTS public.pst_config_presupuesto_hdr (
    id_presupuesto VARCHAR(10) NOT NULL,
    valor_global NUMERIC(18, 4) NOT NULL DEFAULT 0,
    valor_disponible NUMERIC(18, 4) NOT NULL DEFAULT 0,
    rango_periodo INTEGER NOT NULL,
    fecha_inicia DATE NOT NULL,
    fecha_finaliza DATE NOT NULL,
    estado VARCHAR(5) NOT NULL,
    estado_aprobado BOOLEAN NOT NULL DEFAULT FALSE,
    CONSTRAINT pk_pst_config_presupuesto_hdr PRIMARY KEY (id_presupuesto),
    CONSTRAINT ck_pst_config_presupuesto_hdr_rango_periodo CHECK (rango_periodo > 0),
    CONSTRAINT ck_pst_config_presupuesto_hdr_fechas CHECK (fecha_finaliza >= fecha_inicia)
);

CREATE TABLE IF NOT EXISTS public.pst_config_presupuesto_dtl (
    id_presupuesto VARCHAR(10) NOT NULL,
    con_cuenta_code VARCHAR(20) NOT NULL,
    valor_proyeccion NUMERIC(18, 4) NOT NULL DEFAULT 0,
    valor_real NUMERIC(18, 4) NOT NULL DEFAULT 0,
    valor_disponible NUMERIC(18, 4) NOT NULL DEFAULT 0,
    CONSTRAINT pk_pst_config_presupuesto_dtl PRIMARY KEY (id_presupuesto, con_cuenta_code),
    CONSTRAINT fk_pst_config_presupuesto_dtl_hdr FOREIGN KEY (id_presupuesto)
        REFERENCES public.pst_config_presupuesto_hdr (id_presupuesto)
        ON UPDATE CASCADE
        ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_pst_config_presupuesto_dtl_con_cuenta_code
    ON public.pst_config_presupuesto_dtl (con_cuenta_code);

CREATE INDEX IF NOT EXISTS ix_pst_config_presupuesto_dtl_id_presupuesto
    ON public.pst_config_presupuesto_dtl (id_presupuesto);

DO $$
BEGIN
    IF to_regclass('public.pst_config_presupuesto') IS NULL THEN
        RETURN;
    END IF;

    INSERT INTO public.pst_config_presupuesto_hdr (
        id_presupuesto,
        valor_global,
        valor_disponible,
        rango_periodo,
        fecha_inicia,
        fecha_finaliza,
        estado
    )
    SELECT p.id_presupuesto,
           COALESCE(p.valor_global, 0),
           GREATEST(COALESCE(p.valor_global, 0) - COALESCE(p.valor_real, 0), 0),
           p.rango_periodo,
           p.fecha_inicia,
           p.fecha_finaliza,
           p.estado
      FROM public.pst_config_presupuesto p
    ON CONFLICT (id_presupuesto) DO NOTHING;

    INSERT INTO public.pst_config_presupuesto_dtl (
        id_presupuesto,
        con_cuenta_code,
        valor_proyeccion,
        valor_real,
        valor_disponible
    )
    SELECT p.id_presupuesto,
           p.con_cuenta_code,
           p.valor_proyeccion,
           p.valor_real,
           GREATEST(COALESCE(p.valor_proyeccion, 0) - COALESCE(p.valor_real, 0), 0)
      FROM public.pst_config_presupuesto p
    ON CONFLICT (id_presupuesto, con_cuenta_code)
    DO UPDATE
          SET valor_proyeccion = EXCLUDED.valor_proyeccion,
              valor_real = EXCLUDED.valor_real,
              valor_disponible = EXCLUDED.valor_disponible;
END;
$$;
