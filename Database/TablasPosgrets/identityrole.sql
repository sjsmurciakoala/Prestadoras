-- Table: public.identityrole

-- DROP TABLE IF EXISTS public.identityrole;

CREATE TABLE IF NOT EXISTS public.identityrole
(
    id text COLLATE pg_catalog."default" NOT NULL,
    name character varying(256) COLLATE pg_catalog."default",
    normalized_name character varying(256) COLLATE pg_catalog."default",
    concurrency_stamp text COLLATE pg_catalog."default",
    CONSTRAINT pk_roles PRIMARY KEY (id)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.identityrole
    OWNER to postgres;
-- Index: RoleNameIndex

-- DROP INDEX IF EXISTS public."RoleNameIndex";

CREATE UNIQUE INDEX IF NOT EXISTS "RoleNameIndex"
    ON public.identityrole USING btree
    (normalized_name COLLATE pg_catalog."default" ASC NULLS LAST)
    TABLESPACE pg_default;