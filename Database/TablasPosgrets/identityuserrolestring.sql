-- Table: public.identityuserrole<string>

-- DROP TABLE IF EXISTS public."identityuserrole<string>";

CREATE TABLE IF NOT EXISTS public."identityuserrole<string>"
(
    user_id text COLLATE pg_catalog."default" NOT NULL,
    role_id text COLLATE pg_catalog."default" NOT NULL,
    CONSTRAINT pk_user_roles PRIMARY KEY (user_id, role_id),
    CONSTRAINT fk_user_roles_asp_net_roles_identity_role_id FOREIGN KEY (role_id)
        REFERENCES public.identityrole (id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE CASCADE,
    CONSTRAINT fk_user_roles_asp_net_users_identity_user_id FOREIGN KEY (user_id)
        REFERENCES public.identityuser (id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE CASCADE
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public."identityuserrole<string>"
    OWNER to postgres;
-- Index: ix_user_roles_role_id

-- DROP INDEX IF EXISTS public.ix_user_roles_role_id;

CREATE INDEX IF NOT EXISTS ix_user_roles_role_id
    ON public."identityuserrole<string>" USING btree
    (role_id COLLATE pg_catalog."default" ASC NULLS LAST)
    TABLESPACE pg_default;