-- 2026-06-01_add_cliente_movimientos_indexes.sql
-- Mejora consultas de clientes paginados y api/clientes/{id}/movimientos/paged.

CREATE INDEX IF NOT EXISTS ix_cliente_maestro_company_estado_nombre_id
    ON cliente_maestro(company_id, estado, maestro_cliente_nombre, maestro_cliente_id);

CREATE EXTENSION IF NOT EXISTS pg_trgm;

CREATE INDEX IF NOT EXISTS ix_cliente_maestro_clave_trgm
    ON cliente_maestro USING gin (maestro_cliente_clave gin_trgm_ops);

CREATE INDEX IF NOT EXISTS ix_cliente_maestro_nombre_trgm
    ON cliente_maestro USING gin (maestro_cliente_nombre gin_trgm_ops);

CREATE INDEX IF NOT EXISTS ix_cliente_maestro_identidad_trgm
    ON cliente_maestro USING gin (maestro_cliente_identidad gin_trgm_ops);

CREATE INDEX IF NOT EXISTS ix_cliente_maestro_barrio_trgm
    ON cliente_maestro USING gin (barrio_codigo gin_trgm_ops);

CREATE INDEX IF NOT EXISTS ix_transaccion_abonado_cliente_fecha_ide
    ON transaccion_abonado(cliente_clave, fecha_docu, ide);

CREATE INDEX IF NOT EXISTS ix_factura_clientecodigo_numrecibo
    ON factura(clientecodigo, numrecibo);
