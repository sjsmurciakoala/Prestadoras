identityuserlogin-- Table: public.identityuserlogin<string>

-- DROP TABLE IF EXISTS public."identityuserlogin<string>";

CREATE TABLE IF NOT EXISTS public."identityuserlogin<string>"
(
    login_provider text COLLATE pg_catalog."default" NOT NULL,
    provider_key text COLLATE pg_catalog."default" NOT NULL,
    provider_display_name text COLLATE pg_catalog."default",
    user_id text COLLATE pg_catalog."default" NOT NULL,
    CONSTRAINT pk_user_logins PRIMARY KEY (login_provider, provider_key),
    CONSTRAINT fk_user_logins_asp_net_users_identity_user_id FOREIGN KEY (user_id)
        REFERENCES public.identityuser (id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE CASCADE
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public."identityuserlogin<string>"
    OWNER to postgres;
-- Index: ix_user_logins_user_id

-- DROP INDEX IF EXISTS public.ix_user_logins_user_id;

CREATE INDEX IF NOT EXISTS ix_user_logins_user_id
    ON public."identityuserlogin<string>" USING btree
    (user_id COLLATE pg_catalog."default" ASC NULLS LAST)
    TABLESPACE pg_default;