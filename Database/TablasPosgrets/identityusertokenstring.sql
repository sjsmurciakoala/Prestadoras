-- Table: public.identityusertoken<string>

-- DROP TABLE IF EXISTS public."identityusertoken<string>";

CREATE TABLE IF NOT EXISTS public."identityusertoken<string>"
(
    user_id text COLLATE pg_catalog."default" NOT NULL,
    login_provider text COLLATE pg_catalog."default" NOT NULL,
    name text COLLATE pg_catalog."default" NOT NULL,
    value text COLLATE pg_catalog."default",
    CONSTRAINT pk_user_tokens PRIMARY KEY (user_id, login_provider, name),
    CONSTRAINT fk_user_tokens_asp_net_users_identity_user_id FOREIGN KEY (user_id)
        REFERENCES public.identityuser (id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE CASCADE
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public."identityusertoken<string>"
    OWNER to postgres;