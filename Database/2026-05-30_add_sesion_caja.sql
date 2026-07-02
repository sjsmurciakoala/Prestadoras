-- 2026-05-30_add_sesion_caja.sql
-- Tabla de sesiones diarias de caja (apertura/cierre)
-- Cada registro representa un turno de caja de un usuario.

CREATE TABLE sesion_caja (
    id                 SERIAL        PRIMARY KEY,
    company_id         BIGINT        NOT NULL,
    usuario_apertura   VARCHAR(100)  NOT NULL,
    fecha_apertura     TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    usuario_cierre     VARCHAR(100),
    fecha_cierre       TIMESTAMPTZ,
    estado             VARCHAR(20)   NOT NULL DEFAULT 'ABIERTA',
    total_cobrado      NUMERIC(18,2),
    observacion        VARCHAR(500)
);

-- Filtrado por tenant
CREATE INDEX ix_sesion_caja_company ON sesion_caja(company_id);

-- Solo una sesión ABIERTA por usuario por empresa
CREATE UNIQUE INDEX ix_sesion_caja_unica_abierta
    ON sesion_caja(company_id, usuario_apertura)
    WHERE estado = 'ABIERTA';

COMMENT ON TABLE sesion_caja IS 'Sesiones diarias de apertura y cierre de caja por empresa';
COMMENT ON COLUMN sesion_caja.estado IS 'ABIERTA | CERRADA';

-- Soft-link de transacciones a la sesión de caja que las registró.
-- Sin FK: campo de referencia libre, nullable.
ALTER TABLE transaccion_abonado
    ADD COLUMN caja_id INTEGER;

CREATE INDEX ix_transaccion_abonado_caja ON transaccion_abonado(caja_id)
    WHERE caja_id IS NOT NULL;
