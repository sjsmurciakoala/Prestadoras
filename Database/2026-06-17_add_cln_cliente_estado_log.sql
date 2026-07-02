-- Bitácora de cambios en flags de estado del cliente:
-- No Cortable y Bloqueo de Cobro.
-- El campo 'tipo' diferencia entre ambos: 'NO_CORTABLE' | 'BLOQUEO_COBRO'.

CREATE TABLE IF NOT EXISTS public.cln_cliente_estado_log (
    id              BIGSERIAL PRIMARY KEY,
    company_id      BIGINT NOT NULL,
    codigocliente   VARCHAR(20) NOT NULL,
    tipo            VARCHAR(30) NOT NULL,  -- 'NO_CORTABLE' | 'BLOQUEO_COBRO'
    valor_anterior  BOOLEAN,
    valor_nuevo     BOOLEAN NOT NULL,
    motivo          TEXT,
    usuario         VARCHAR(100) NOT NULL,
    fecha           TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_cln_cliente_estado_log_company_cliente
    ON public.cln_cliente_estado_log (company_id, codigocliente);

CREATE INDEX IF NOT EXISTS ix_cln_cliente_estado_log_tipo
    ON public.cln_cliente_estado_log (company_id, codigocliente, tipo);

COMMENT ON TABLE public.cln_cliente_estado_log IS
    'Registro de auditoría para cambios en No Cortable y Bloqueo de Cobro por cliente.';

COMMENT ON COLUMN public.cln_cliente_estado_log.tipo IS
    'NO_CORTABLE = cambio en flag no_cortable; BLOQUEO_COBRO = cambio en bloqueado_cobranza';
