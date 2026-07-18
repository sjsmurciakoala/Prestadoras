-- =============================================================================
-- Proveedores: abonos parciales a compromisos (ordenes de pago directo)
-- Fecha: 2026-07-17
-- Regla DB Mirror: aplicar tambien en siad_v3_restore (localhost) antes que en SRV
--
-- POR QUE ESTA TABLA
-- Un compromiso (prv_compromiso_hdr) se pagaba de UNA sola vez (MarkAsProcessedAsync
-- ponia status_transacc=true). Esta tabla es el LIBRO DE ABONOS: cada fila es un pago
-- parcial (o total) con su metodo, su contra-cuenta, su partida contable y, si es
-- bancario, su movimiento de banco vinculado.
--
-- EL SALDO NO SE GUARDA - SE DERIVA
--   saldo = prv_compromiso_hdr.monto - SUM(monto de abonos con estado 'V')
-- Cuando el saldo llega a 0 la capa de servicio marca status_transacc=true (Pagado);
-- al anular el ultimo abono, si estaba Pagado, status_transacc vuelve a false.
--
-- COMPATIBILIDAD: un compromiso legacy ya procesado (status_transacc=true) SIN filas
-- de abono se interpreta como saldo 0 / pagado y no admite nuevos abonos.
--
-- ESTADO 'V'/'A' (convencion NO invertida, distinta a proposito de transaccion_abonado)
--   'V' = VIGENTE  -> suma al total abonado, resta del saldo
--   'A' = ANULADO  -> no cuenta para el saldo
--
-- ANULACION: solo el ULTIMO abono vigente (mayor numero_abono con 'V'). Al anularse se
-- registra su partida inversa (partida_reverso_id) y, si tenia movimiento bancario, su
-- reverso (ban_kardex_id_reverso).
--
-- Cambio aditivo: tabla nueva. No altera ninguna tabla ni dato existente.
-- =============================================================================
BEGIN;

CREATE TABLE IF NOT EXISTS prv_compromiso_abono (
    abono_id             BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id           BIGINT       NOT NULL,
    numero_orden         INT          NOT NULL,
    numero_abono         INT          NOT NULL,
    fecha                TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    monto                NUMERIC(15,2) NOT NULL,
    metodo_pago          VARCHAR(20)  NOT NULL,
    cuenta_contra_id     BIGINT,
    banco_cuenta_id      BIGINT,
    ban_kardex_id        BIGINT,
    partida_id           BIGINT,
    partida_reverso_id   BIGINT,
    ban_kardex_id_reverso BIGINT,
    estado               CHAR(1)      NOT NULL DEFAULT 'V',
    motivo_anulacion     VARCHAR(250),
    usuario_creo         VARCHAR(100) NOT NULL,
    fecha_creacion       TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT now(),
    usuario_anulacion    VARCHAR(100),
    fecha_anulacion      TIMESTAMP WITHOUT TIME ZONE,
    usuario_modifica     VARCHAR(100),
    fecha_modificacion   TIMESTAMP WITHOUT TIME ZONE,
    rowid                UUID         NOT NULL DEFAULT gen_random_uuid(),

    CONSTRAINT ck_prv_compromiso_abono_monto  CHECK (monto > 0),
    CONSTRAINT ck_prv_compromiso_abono_estado CHECK (estado IN ('V', 'A')),

    -- El hijo vive SIEMPRE en la misma empresa y contra un compromiso existente
    -- (contraparte en BD del query filter tenant; defensa en profundidad).
    -- RESTRICT: no se borra un compromiso que tenga abonos registrados.
    CONSTRAINT fk_prv_compromiso_abono_hdr
        FOREIGN KEY (company_id, numero_orden)
        REFERENCES prv_compromiso_hdr (company_id, numero_orden)
        ON DELETE RESTRICT,

    -- Garantiza la secuencia 1,2,3... por compromiso (y sirve al lookup por prefijo).
    CONSTRAINT uq_prv_compromiso_abono_numero
        UNIQUE (company_id, numero_orden, numero_abono)
);

-- Suma de vigentes / ultimo vigente por compromiso.
CREATE INDEX IF NOT EXISTS ix_prv_compromiso_abono_estado
    ON prv_compromiso_abono (company_id, numero_orden, estado);

COMMENT ON TABLE  prv_compromiso_abono IS
    'Libro de abonos (pagos parciales) de prv_compromiso_hdr. Saldo NO almacenado: saldo = hdr.monto - SUM(monto WHERE estado=''V''). Al llegar a 0 el servicio marca status_transacc=true.';
COMMENT ON COLUMN prv_compromiso_abono.numero_abono IS
    'Secuencia 1,2,3... por compromiso (company_id, numero_orden). Unica por uq_prv_compromiso_abono_numero.';
COMMENT ON COLUMN prv_compromiso_abono.cuenta_contra_id IS
    'AccountId de la contra-cuenta (origen del pago) resuelta por NormalizeContraProcessingLinesAsync.';
COMMENT ON COLUMN prv_compromiso_abono.partida_id IS
    'poliza_id de la partida contable del abono (stage ABO). NumeroPartida se deriva por JOIN a con_partida_hdr.';
COMMENT ON COLUMN prv_compromiso_abono.partida_reverso_id IS
    'poliza_id de la partida inversa generada al anular (stage RAB). NULL mientras este vigente.';
COMMENT ON COLUMN prv_compromiso_abono.ban_kardex_id IS
    'ban_kardex del movimiento bancario del abono (metodos bancarios). NULL si es contable.';
COMMENT ON COLUMN prv_compromiso_abono.ban_kardex_id_reverso IS
    'ban_kardex del reverso bancario generado al anular el abono. NULL mientras este vigente.';
COMMENT ON COLUMN prv_compromiso_abono.estado IS
    'V = vigente (suma al abonado) | A = anulado (no cuenta). Convencion NO invertida.';

COMMIT;

-- =============================================================================
-- VERIFICACION (correr a mano tras aplicar)
-- =============================================================================
-- 1) Columnas:
-- SELECT column_name, data_type, character_maximum_length, is_nullable, column_default
--   FROM information_schema.columns WHERE table_name='prv_compromiso_abono' ORDER BY ordinal_position;
-- 2) Constraints:
-- SELECT conname, contype FROM pg_constraint WHERE conrelid='prv_compromiso_abono'::regclass ORDER BY contype, conname;
--   -> fk_prv_compromiso_abono_hdr(f), prv_compromiso_abono_pkey(p), uq_prv_compromiso_abono_numero(u),
--      ck_prv_compromiso_abono_monto(c), ck_prv_compromiso_abono_estado(c)
-- 3) Indices:
-- SELECT indexname FROM pg_indexes WHERE tablename='prv_compromiso_abono' ORDER BY indexname;
--   -> ix_prv_compromiso_abono_estado, prv_compromiso_abono_pkey, uq_prv_compromiso_abono_numero
-- 4) El CHECK de monto debe FALLAR:
-- INSERT INTO prv_compromiso_abono (company_id, numero_orden, numero_abono, fecha, monto, metodo_pago, usuario_creo)
-- VALUES (2, 1, 1, now(), 0, 'CONTABLE', 'test');   -- ERROR ck_prv_compromiso_abono_monto
-- =============================================================================
