-- Table: public.identityuser

-- DROP TABLE IF EXISTS public.identityuser;

CREATE TABLE IF NOT EXISTS public.identityuser
(
    id text COLLATE pg_catalog."default" NOT NULL,
    user_name character varying(256) COLLATE pg_catalog."default",
    normalized_user_name character varying(256) COLLATE pg_catalog."default",
    email character varying(256) COLLATE pg_catalog."default",
    normalized_email character varying(256) COLLATE pg_catalog."default",
    email_confirmed boolean NOT NULL,
    password_hash text COLLATE pg_catalog."default",
    security_stamp text COLLATE pg_catalog."default",
    concurrency_stamp text COLLATE pg_catalog."default",
    phone_number text COLLATE pg_catalog."default",
    phone_number_confirmed boolean NOT NULL,
    two_factor_enabled boolean NOT NULL,
    lockout_end timestamp with time zone,
    lockout_enabled boolean NOT NULL,
    access_failed_count integer NOT NULL,
    CONSTRAINT pk_users PRIMARY KEY (id)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.identityuser
    OWNER to postgres;
-- Index: EmailIndex

-- DROP INDEX IF EXISTS public."EmailIndex";

CREATE INDEX IF NOT EXISTS "EmailIndex"
    ON public.identityuser USING btree
    (normalized_email COLLATE pg_catalog."default" ASC NULLS LAST)
    TABLESPACE pg_default;
-- Index: UserNameIndex

-- DROP INDEX IF EXISTS public."UserNameIndex";

CREATE UNIQUE INDEX IF NOT EXISTS "UserNameIndex"
    ON public.identityuser USING btree
    (normalized_user_name COLLATE pg_catalog."default" ASC NULLS LAST)
    TABLESPACE pg_default;