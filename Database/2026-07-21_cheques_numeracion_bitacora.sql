-- =============================================================================
-- Bancos: numeracion de cheques por cuenta y bitacora de emision/anulacion
-- Fecha: 2026-07-21
-- Regla DB Mirror: aplicar tambien en siad_v3_restore (localhost) antes que en SRV
--
-- POR QUE
-- Las cuentas tipo CHEQUES ya traen ban_cuenta.proximo_cheque (migrado de SIMAFI
-- ctacheques.ncheque el 2026-07-09) pero ningun flujo lo usa: hoy se paga con
-- metodo CHEQUE sin asignar numero. Se agrega:
--   1) ban_cuenta.cheque_maximo  -> ultimo numero autorizado del talonario
--                                    (0 = sin limite, no se valida agotamiento)
--   2) tabla ban_cheque          -> libro: una fila por cheque, con numero
--                                    unico por cuenta y estado vigente E/A.
--   3) tabla ban_cheque_bitacora -> bitacora de EVENTOS append-only: una fila
--                                    por evento (EMITIDO/ANULADO); nunca se
--                                    actualiza ni se borra.
-- Lo consumen ChequesService.EmitirChequeAsync / AnularPorKardexAsync /
-- AnularSiguienteNumeroAsync (SIAD.Services/Bancos), enganchados en
-- OrdenesPagoDirectoService (procesar/abonar compromisos con metodo CHEQUE) y
-- BanTransaccionesService (transaccion manual con tipo emite_cheque='S';
-- anulacion de movimientos).
--
-- CRITERIO (definido con el usuario 2026-07-21): numeracion automatica no
-- editable al pagar; todas las vias de emision; anulacion automatica al
-- reversar el movimiento + anulacion manual de un numero (cheque danado).
--
-- ESTADO 'E'/'A' (convencion NO invertida):
--   'E' = EMITIDO   'A' = ANULADO
--
-- Cambio ADITIVO y reversible: una columna nueva con DEFAULT y una tabla nueva.
-- No altera datos existentes.
-- =============================================================================
BEGIN;

ALTER TABLE public.ban_cuenta
    ADD COLUMN IF NOT EXISTS cheque_maximo NUMERIC(28,0) NOT NULL DEFAULT 0;

COMMENT ON COLUMN public.ban_cuenta.cheque_maximo IS
    'Ultimo numero de cheque autorizado del talonario (0 = sin limite). Se valida contra proximo_cheque al emitir.';

CREATE TABLE IF NOT EXISTS public.ban_cheque (
    cheque_id            BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id           BIGINT        NOT NULL,
    banco_cuenta_id      BIGINT        NOT NULL,
    numero_cheque        NUMERIC(28,0) NOT NULL,
    fecha_emision        TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    monto                NUMERIC(15,2) NOT NULL DEFAULT 0,
    beneficiario         VARCHAR(200),
    concepto             VARCHAR(250),
    origen               VARCHAR(20)   NOT NULL,
    origen_documento     VARCHAR(50),
    ban_kardex_id        BIGINT,
    partida_id           BIGINT,
    ban_kardex_id_reverso BIGINT,
    estado               CHAR(1)       NOT NULL DEFAULT 'E',
    usuario_emision      VARCHAR(100)  NOT NULL,
    fecha_creacion       TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT now(),
    motivo_anulacion     VARCHAR(250),
    usuario_anulacion    VARCHAR(100),
    fecha_anulacion      TIMESTAMP WITHOUT TIME ZONE,
    rowid                UUID          NOT NULL DEFAULT gen_random_uuid(),

    CONSTRAINT ck_ban_cheque_estado CHECK (estado IN ('E', 'A')),
    CONSTRAINT ck_ban_cheque_origen CHECK (origen IN ('PROCESAR', 'ABONO', 'TRANSACCION', 'MANUAL')),
    CONSTRAINT ck_ban_cheque_numero CHECK (numero_cheque > 0),

    -- RESTRICT: no se borra una cuenta con cheques registrados.
    -- Tenant-safe: el cheque vive SIEMPRE en la misma empresa que su cuenta
    -- (convencion del modulo bancos: FK compuesta contra la AK uq_ban_cuenta_company_id).
    CONSTRAINT fk_ban_cheque_cuenta
        FOREIGN KEY (company_id, banco_cuenta_id)
        REFERENCES public.ban_cuenta (company_id, banco_cuenta_id)
        ON DELETE RESTRICT,

    -- Numeros irrepetibles por cuenta (defensa en BD; el FOR UPDATE del
    -- servicio serializa, esto es el respaldo ante carreras).
    CONSTRAINT uq_ban_cheque_numero
        UNIQUE (company_id, banco_cuenta_id, numero_cheque),

    -- AK para la FK compuesta tenant-safe de ban_cheque_bitacora.
    CONSTRAINT uq_ban_cheque_company_cheque
        UNIQUE (company_id, cheque_id)
);

CREATE INDEX IF NOT EXISTS ix_ban_cheque_cuenta_estado
    ON public.ban_cheque (company_id, banco_cuenta_id, estado);

-- Anulacion por reverso: localizar el cheque vigente de un movimiento.
CREATE INDEX IF NOT EXISTS ix_ban_cheque_kardex
    ON public.ban_cheque (company_id, ban_kardex_id);

COMMENT ON TABLE  public.ban_cheque IS
    'Libro/bitacora de cheques por cuenta bancaria. Una fila por cheque: emision (estado=''E'') y anulacion (estado=''A'', por reverso del movimiento o manual/danado). Numero unico por (company, cuenta).';
COMMENT ON COLUMN public.ban_cheque.origen IS
    'PROCESAR = procesar compromiso | ABONO = abono a compromiso | TRANSACCION = transaccion bancaria manual | MANUAL = numero anulado sin pago (cheque danado).';
COMMENT ON COLUMN public.ban_cheque.ban_kardex_id IS
    'Movimiento bancario (ban_kardex) que emitio el cheque. NULL solo en origen MANUAL.';
COMMENT ON COLUMN public.ban_cheque.ban_kardex_id_reverso IS
    'ban_kardex del reverso que anulo el cheque. NULL si esta vigente o si la anulacion fue manual.';

-- -----------------------------------------------------------------------------
-- Bitacora de EVENTOS (ampliacion 2026-07-21, aprobada por el usuario):
-- tabla APPEND-ONLY aparte del libro ban_cheque. Una fila por evento
-- (EMITIDO/ANULADO); nunca se actualiza ni se borra. La escriben
-- ChequesService.EmitirChequeAsync (EMITIDO), AnularPorKardexAsync (ANULADO) y
-- AnularSiguienteNumeroAsync (ANULADO manual), siempre dentro de la misma
-- transaccion de la operacion.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.ban_cheque_bitacora (
    bitacora_id          BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id           BIGINT        NOT NULL,
    cheque_id            BIGINT        NOT NULL,
    banco_cuenta_id      BIGINT        NOT NULL,
    numero_cheque        NUMERIC(28,0) NOT NULL,
    accion               VARCHAR(10)   NOT NULL,
    fecha                TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT now(),
    usuario              VARCHAR(100)  NOT NULL,
    monto                NUMERIC(15,2) NOT NULL DEFAULT 0,
    beneficiario         VARCHAR(200),
    concepto             VARCHAR(250),
    motivo               VARCHAR(250),
    origen               VARCHAR(20)   NOT NULL,
    origen_documento     VARCHAR(50),
    ban_kardex_id        BIGINT,
    rowid                UUID          NOT NULL DEFAULT gen_random_uuid(),

    CONSTRAINT ck_ban_cheque_bitacora_accion CHECK (accion IN ('EMITIDO', 'ANULADO')),

    -- RESTRICT: no se borra un cheque con eventos registrados.
    -- Tenant-safe: el evento vive SIEMPRE en la misma empresa que su cheque
    -- (FK compuesta contra la AK uq_ban_cheque_company_cheque).
    CONSTRAINT fk_ban_cheque_bitacora_cheque
        FOREIGN KEY (company_id, cheque_id)
        REFERENCES public.ban_cheque (company_id, cheque_id)
        ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS ix_ban_cheque_bitacora_cuenta_fecha
    ON public.ban_cheque_bitacora (company_id, banco_cuenta_id, fecha);

-- Append-only por CONVENCION de la capa de servicio: ChequesService solo hace
-- INSERT sobre esta tabla (nunca UPDATE ni DELETE). No se usan disparadores.
COMMENT ON TABLE  public.ban_cheque_bitacora IS
    'Bitacora de eventos de cheques, append-only por convencion de la capa de servicio (solo INSERT): una fila por evento (EMITIDO/ANULADO). El estado vigente vive en ban_cheque.';
COMMENT ON COLUMN public.ban_cheque_bitacora.accion IS
    'EMITIDO = asignacion del numero al emitir | ANULADO = anulacion (por reverso del movimiento o manual/danado).';
COMMENT ON COLUMN public.ban_cheque_bitacora.motivo IS
    'Motivo de la anulacion (solo eventos ANULADO).';
COMMENT ON COLUMN public.ban_cheque_bitacora.ban_kardex_id IS
    'EMITIDO: ban_kardex que emitio el cheque (NULL en origen MANUAL). ANULADO por reverso: ban_kardex del reverso. ANULADO manual: NULL.';

COMMIT;

-- =============================================================================
-- VERIFICACION (correr a mano tras aplicar)
-- =============================================================================
-- 1) Columna nueva:
-- SELECT column_name, data_type, column_default FROM information_schema.columns
--  WHERE table_name='ban_cuenta' AND column_name='cheque_maximo';
-- 2) Tabla y constraints:
-- SELECT conname, contype FROM pg_constraint WHERE conrelid='ban_cheque'::regclass ORDER BY contype, conname;
--   -> ck_ban_cheque_estado(c), ck_ban_cheque_origen(c), ck_ban_cheque_numero(c),
--      fk_ban_cheque_cuenta(f), ban_cheque_pkey(p), uq_ban_cheque_company_cheque(u),
--      uq_ban_cheque_numero(u)
-- 3) Indices:
-- SELECT indexname FROM pg_indexes WHERE tablename='ban_cheque' ORDER BY indexname;
-- 4) El CHECK de estado debe FALLAR:
-- INSERT INTO ban_cheque (company_id, banco_cuenta_id, numero_cheque, fecha_emision, origen, usuario_emision)
-- VALUES (2, 1, 1, now(), 'PROCESAR', 'test');  -- ok si la cuenta 1 existe; luego:
-- UPDATE ban_cheque SET estado='X' WHERE numero_cheque=1;  -- ERROR ck_ban_cheque_estado
-- 5) Bitacora de eventos:
-- SELECT conname, contype FROM pg_constraint WHERE conrelid='ban_cheque_bitacora'::regclass ORDER BY contype, conname;
--   -> ck_ban_cheque_bitacora_accion(c), fk_ban_cheque_bitacora_cheque(f),
--      ban_cheque_bitacora_pkey(p)
-- SELECT indexname FROM pg_indexes WHERE tablename='ban_cheque_bitacora' ORDER BY indexname;
--   -> ban_cheque_bitacora_pkey, ix_ban_cheque_bitacora_cuenta_fecha
-- 6) El CHECK de accion debe FALLAR:
-- INSERT INTO ban_cheque_bitacora (company_id, cheque_id, banco_cuenta_id, numero_cheque, accion, usuario, origen)
-- VALUES (2, 1, 1, 1, 'OTRO', 'test', 'MANUAL');  -- ERROR ck_ban_cheque_bitacora_accion
-- =============================================================================
