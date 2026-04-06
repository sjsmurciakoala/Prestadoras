DROP TABLE IF EXISTS public.ban_tipos_transacciones;

CREATE TABLE public.ban_tipos_transacciones (
    ban_tipo_transaccion_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id              bigint       NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    tipo_transaccion        varchar(3)   NOT NULL,
    cod_tipopartida         bpchar(3)    NOT NULL,
    correlativo             varchar(6)   NOT NULL,
    cod_centrocosto         int8         NULL,
    cuenta_contable         varchar(13)  NULL,
    destino                 varchar(9)   NULL,
    nombre                  varchar(40)  NOT NULL,
    observaciones           text         NULL,
    entra_sale              bpchar(1)    NOT NULL,
    del_sistema             bpchar(1)    NOT NULL,
    emite_cheque            bpchar(1)    NULL,
    "pad"                   bpchar(1)    NULL,
    pda                     bpchar(1)    NULL,
    rel_empleados           bpchar(1)    NULL,
    trn_prestamo            bpchar(1)    NULL,
    filtro                  int2         NULL,
    cuenta_alterna          bool         NOT NULL DEFAULT false,
    estado                  varchar(20)  NOT NULL DEFAULT 'ACTIVE',
    created_at              timestamptz  NOT NULL DEFAULT now(),
    created_by              varchar(100) NOT NULL DEFAULT current_user,
    updated_at              timestamptz  NULL,
    updated_by              varchar(100) NULL,
    UNIQUE (company_id, tipo_transaccion, cod_tipopartida, correlativo, entra_sale)
);

CREATE INDEX IF NOT EXISTS ix_ban_tipos_transacciones_company
  ON public.ban_tipos_transacciones (company_id);

CREATE INDEX IF NOT EXISTS ix_ban_tipos_transacciones_centrocosto
  ON public.ban_tipos_transacciones (cod_centrocosto);

CREATE INDEX IF NOT EXISTS ix_ban_tipos_transacciones_tipopartida
  ON public.ban_tipos_transacciones (cod_tipopartida);

ALTER TABLE public.ban_tipos_transacciones
  ADD CONSTRAINT fk_ban_tipos_transacciones_centrocosto
  FOREIGN KEY (cod_centrocosto)
  REFERENCES public.con_centro_costo (cost_center_id);
