-- Pruebas operativas jul-2026 (backlog, suelto final): partida contable por
-- cambio de categoría del cliente (p.ej. Doméstico → Comercial).
--
-- Cuando la integración contable lleva la CxC en modo POR_SERVICIO_CATEGORIA,
-- el saldo pendiente del cliente está debitado en las cuentas de su categoría
-- anterior. Al recategorizarlo se reclasifica ese saldo (DEBE CxC nueva /
-- HABER CxC vieja, por servicio) y las facturas VIVAS actualizan su snapshot
-- de categoría para que los cobros futuros acrediten la cuenta nueva; las
-- pagadas y anuladas conservan su historia.
--
-- Esta tabla es la bitácora del evento y su id es el document_id del
-- comprobante (module VENTAS / document_type RECLASIFICACION_CXC):
-- sp_con_generar_comprobante_config es idempotente por documento y un mismo
-- cliente puede recategorizarse varias veces.

BEGIN;

CREATE TABLE IF NOT EXISTS public.cln_cliente_recategorizacion (
    id                     bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id             bigint        NOT NULL,
    maestro_cliente_id     integer       NOT NULL,
    cliente_clave          varchar(20)   NOT NULL,
    categoria_anterior_id  integer       NULL,
    categoria_nueva_id     integer       NULL,
    monto_reclasificado    numeric(14,2) NOT NULL DEFAULT 0,
    facturas_actualizadas  integer       NOT NULL DEFAULT 0,
    poliza_id              bigint        NULL,
    usuario                varchar(100)  NOT NULL,
    fecha                  timestamptz   NOT NULL DEFAULT now()
);

COMMENT ON TABLE public.cln_cliente_recategorizacion IS
    'Bitácora de cambios de categoría de cliente con reclasificación contable de CxC (module VENTAS / document_type RECLASIFICACION_CXC). El id es el document_id del comprobante.';

CREATE INDEX IF NOT EXISTS ix_cln_cliente_recateg_cliente
    ON public.cln_cliente_recategorizacion (company_id, maestro_cliente_id);

COMMIT;
