-- Migration to create letras table
-- Generated automatically

CREATE TABLE IF NOT EXISTS public.letras (
    letras character(1) NOT NULL,
    num numeric(1,0),
    fechacreacion timestamp without time zone,
    usuariocreacion character varying(256),
    fechamodificacion timestamp without time zone,
    usuariomodificacion character varying(256)
);

-- Add primary key
ALTER TABLE ONLY public.letras
    ADD CONSTRAINT letra_pkey PRIMARY KEY (letras);

-- Create index if needed
CREATE INDEX IF NOT EXISTS ix_letras_num ON public.letras(num);
