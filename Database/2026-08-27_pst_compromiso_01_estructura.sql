-- =============================================================================
-- Control presupuestario con COMPROMISO en la aprobación de la O/C — estructura
-- Fecha: 2026-08-27
-- Fase F1 (1 de 4). Diseño: docs/plans/2026-08-27-presupuesto-compromiso-oc-design.md
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) antes que en el SRV
--
-- POR QUÉ
-- El modelo presupuestario NO tiene el concepto de "comprometido": pst_config_presupuesto_dtl
-- solo lleva valor_proyeccion (lo presupuestado), valor_real (lo ejecutado) y valor_disponible
-- (derivado). No hay reserva, ni ciclo de liberación, ni rastro de qué documento movió el saldo.
-- Este script agrega ese cuarto eje y su trazabilidad, para que la aprobación de una orden de
-- compra pueda validar disponibilidad y comprometer presupuesto, y para que anular o cancelar
-- la orden libere EXACTAMENTE el saldo pendiente (no el total pedido).
--
-- QUÉ SE CREA
--   1) pst_linea_afectacion          -> tipo compuesto para pasar renglones a los SP.
--   2) valor_comprometido/_pagado    -> columnas nuevas en pst_config_presupuesto_dtl.
--   3) valor_comprometido            -> columna nueva en pst_config_presupuesto_hdr.
--   4) pst_compromiso                -> saldo VIVO del compromiso por documento y partida.
--   5) pst_compromiso_aplicacion     -> qué documento consumió/liberó qué compromiso.
--   6) pst_movimiento                -> KARDEX presupuestario, inmutable (append-only).
--   7) cfg_presupuesto_control       -> modo del control por empresa y módulo (nace APAGADO).
--   8) alm_orden_compra_detalle      -> cuenta_presupuestaria + centro_costo_id.
--   9) alm_orden_compra              -> CHECK de estado ampliado (5 Rechazada, 6 Cancelada).
--
-- FÓRMULA QUE CAMBIA
--   Antes: valor_disponible = MAX(valor_proyeccion - valor_real, 0)
--   Ahora: valor_disponible = MAX(valor_proyeccion - valor_comprometido - valor_real, 0)
--   Sin impacto al aplicar: valor_comprometido nace en 0 y solo lo mueve el módulo nuevo.
--
-- DECISIONES APLICADAS (ver §13 del diseño)
--   - D3-A: el centro de costo se GUARDA y se reporta, pero NO valida. El presupuesto no
--           tiene ese eje (su PK es (company_id, id_presupuesto, con_cuenta_code)) y agregarlo
--           es un proyecto aparte.
--   - El control nace en modo 0 (apagado) para TODA empresa: aplicar este script no cambia el
--     comportamiento de ninguna pantalla.
--   - saldo_comprometido NO es columna: se deriva en vw_pst_compromiso_saldo (script 04). Evita
--     una columna generada (versión de PostgreSQL del SRV sin confirmar) y su desincronización.
--   - El CHECK de montos no negativos cubre SOLO las columnas nuevas. valor_real queda fuera a
--     propósito: su contenido actual no está medido y un CHECK sobre datos existentes puede fallar.
--
-- FUERA DE ALCANCE DE ESTE SCRIPT (pendiente para F3, con su propia aprobación)
--   - Columna de motivo en alm_orden_compra para Rechazar/Cancelar. Mientras tanto el motivo
--     vive en pst_movimiento.observacion, que solo se escribe si el control está encendido.
--
-- Cambio ADITIVO y reversible: 4 tablas nuevas (vacías), 5 columnas NULL o con DEFAULT 0,
-- y un CHECK que solo AMPLÍA los valores admitidos. No altera ni borra datos existentes.
-- =============================================================================
BEGIN;

-- -----------------------------------------------------------------------------
-- 1) Tipo compuesto de renglón presupuestario
--    Precedente: tipo_linea_partida de sp_pst_aplicar_partida_presupuesto.
-- -----------------------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'pst_linea_afectacion') THEN
        CREATE TYPE public.pst_linea_afectacion AS (
            con_cuenta_code       VARCHAR(20),
            centro_costo_id       BIGINT,
            documento_detalle_id  BIGINT,
            monto                 NUMERIC(18,4)
        );
    END IF;
END $$;

COMMENT ON TYPE public.pst_linea_afectacion IS
    'Renglón presupuestario que se pasa a los sp_pst_*. centro_costo_id es informativo (D3-A).';

-- -----------------------------------------------------------------------------
-- 2) Los montos nuevos del detalle presupuestario
-- -----------------------------------------------------------------------------
ALTER TABLE public.pst_config_presupuesto_dtl
    ADD COLUMN IF NOT EXISTS valor_comprometido NUMERIC(18,4) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS valor_pagado       NUMERIC(18,4) NOT NULL DEFAULT 0;

COMMENT ON COLUMN public.pst_config_presupuesto_dtl.valor_comprometido IS
    'Comprometido: O/C aprobadas con saldo sin devengar. = SUMA de los saldos vigentes de pst_compromiso. Resta al disponible.';
COMMENT ON COLUMN public.pst_config_presupuesto_dtl.valor_pagado IS
    'Pagado al proveedor. Informativo para el reporte de ejecución: NO resta al disponible.';
COMMENT ON COLUMN public.pst_config_presupuesto_dtl.valor_disponible IS
    'Derivado: MAX(valor_proyeccion - valor_comprometido - valor_real, 0). La fórmula cambió el 2026-08-27 al incorporar el compromiso.';

-- Solo sobre las columnas nuevas: nacen con DEFAULT 0, así que el CHECK no puede fallar.
-- valor_real queda deliberadamente fuera (su contenido actual no está medido).
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conname = 'ck_pst_dtl_montos_no_negativos'
           AND conrelid = 'public.pst_config_presupuesto_dtl'::regclass
    ) THEN
        ALTER TABLE public.pst_config_presupuesto_dtl
            ADD CONSTRAINT ck_pst_dtl_montos_no_negativos
            CHECK (valor_comprometido >= 0 AND valor_pagado >= 0);
    END IF;
END $$;

ALTER TABLE public.pst_config_presupuesto_hdr
    ADD COLUMN IF NOT EXISTS valor_comprometido NUMERIC(18,4) NOT NULL DEFAULT 0;

COMMENT ON COLUMN public.pst_config_presupuesto_hdr.valor_comprometido IS
    'Suma de valor_comprometido de los detalles. La recalcula fn_pst_recalcular_cabecera.';

-- -----------------------------------------------------------------------------
-- 3) pst_compromiso — saldo VIVO del compromiso
--    Una fila por (documento, renglón, partida). Es lo que permite liberar solo el pendiente:
--    O/C de 100,000 con 60,000 recibidos libera 40,000, no 100,000.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.pst_compromiso (
    id                   BIGSERIAL      PRIMARY KEY,
    company_id           BIGINT         NOT NULL,
    id_presupuesto       VARCHAR(10)    NOT NULL,
    con_cuenta_code      VARCHAR(20)    NOT NULL,
    centro_costo_id      BIGINT         NULL,              -- informativo (D3-A)
    modulo               VARCHAR(20)    NOT NULL,          -- COMPRAS | PROVEEDORES | BANCOS
    documento_tipo       VARCHAR(20)    NOT NULL,          -- ORDEN_COMPRA
    documento_id         BIGINT         NOT NULL,          -- alm_orden_compra.id
    documento_numero     VARCHAR(40)    NULL,
    documento_detalle_id BIGINT         NULL,              -- alm_orden_compra_detalle.id
    fecha                DATE           NOT NULL,          -- con la que se resolvió el presupuesto
    monto_comprometido   NUMERIC(18,4)  NOT NULL DEFAULT 0,
    monto_devengado      NUMERIC(18,4)  NOT NULL DEFAULT 0,
    monto_liberado       NUMERIC(18,4)  NOT NULL DEFAULT 0,
    estado               SMALLINT       NOT NULL DEFAULT 1, -- 1 Vigente · 2 Cerrado · 9 Liberado
    usuariocreacion      VARCHAR(100)   NULL,
    fechacreacion        TIMESTAMP      NULL DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion  VARCHAR(100)   NULL,
    fechamodificacion    TIMESTAMP      NULL,
    -- Idempotencia: un reintento no puede duplicar el compromiso de un renglón.
    CONSTRAINT uq_pst_compromiso_documento
        UNIQUE (company_id, modulo, documento_tipo, documento_id, documento_detalle_id, con_cuenta_code),
    CONSTRAINT ck_pst_compromiso_montos
        CHECK (monto_comprometido >= 0 AND monto_devengado >= 0 AND monto_liberado >= 0),
    -- Invariante I5: no se puede devengar ni liberar más de lo comprometido.
    CONSTRAINT ck_pst_compromiso_saldo
        CHECK (monto_devengado + monto_liberado <= monto_comprometido),
    CONSTRAINT ck_pst_compromiso_estado
        CHECK (estado IN (1, 2, 9))
);

CREATE INDEX IF NOT EXISTS ix_pst_compromiso_documento
    ON public.pst_compromiso (company_id, documento_tipo, documento_id);
CREATE INDEX IF NOT EXISTS ix_pst_compromiso_partida
    ON public.pst_compromiso (company_id, id_presupuesto, con_cuenta_code);
CREATE INDEX IF NOT EXISTS ix_pst_compromiso_vigentes
    ON public.pst_compromiso (company_id, id_presupuesto, con_cuenta_code) WHERE estado = 1;

COMMENT ON TABLE  public.pst_compromiso IS 'Saldo vivo del compromiso presupuestario por documento, renglón y partida. Lo crea la aprobación de la O/C; lo consume la factura y lo libera la anulación/cancelación.';
COMMENT ON COLUMN public.pst_compromiso.fecha IS 'Fecha con la que se resolvió el presupuesto. La LIBERACIÓN usa esta fecha, no la del día: devuelve el monto al mismo presupuesto que lo consumió.';
COMMENT ON COLUMN public.pst_compromiso.centro_costo_id IS 'Centro de costo del renglón. INFORMATIVO: el presupuesto no tiene eje de centro de costo (decisión D3-A).';

-- -----------------------------------------------------------------------------
-- 4) pst_compromiso_aplicacion — qué documento consumió qué compromiso
--    Responde "esta O/C se consumió con estas 3 facturas y se le liberaron 40,000 al cancelarla".
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.pst_compromiso_aplicacion (
    id               BIGSERIAL      PRIMARY KEY,
    company_id       BIGINT         NOT NULL,
    compromiso_id    BIGINT         NOT NULL,
    movimiento_id    BIGINT         NOT NULL,
    tipo             SMALLINT       NOT NULL,   -- 1 Devengo · 2 Liberación · 3 Reversa de devengo
    documento_tipo   VARCHAR(20)    NOT NULL,
    documento_id     BIGINT         NOT NULL,
    documento_numero VARCHAR(40)    NULL,
    monto            NUMERIC(18,4)  NOT NULL,   -- con signo
    usuario          VARCHAR(100)   NULL,
    fecha_registro   TIMESTAMP      NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    CONSTRAINT fk_pst_compromiso_aplicacion_compromiso
        FOREIGN KEY (compromiso_id) REFERENCES public.pst_compromiso (id) ON DELETE CASCADE,
    CONSTRAINT ck_pst_compromiso_aplicacion_tipo CHECK (tipo IN (1, 2, 3))
);

CREATE INDEX IF NOT EXISTS ix_pst_compromiso_aplicacion_compromiso
    ON public.pst_compromiso_aplicacion (compromiso_id);
CREATE INDEX IF NOT EXISTS ix_pst_compromiso_aplicacion_documento
    ON public.pst_compromiso_aplicacion (company_id, documento_tipo, documento_id);

COMMENT ON TABLE public.pst_compromiso_aplicacion IS 'Aplicaciones sobre un compromiso: qué factura lo devengó, qué evento lo liberó. Traza O/C -> factura.';

-- -----------------------------------------------------------------------------
-- 5) pst_movimiento — KARDEX presupuestario (inmutable)
--    Hoy valor_real es un acumulado sin historia: nadie puede responder "¿por qué esta cuenta
--    está al 90%?". Esta tabla es la que lo vuelve auditable.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.pst_movimiento (
    id                     BIGSERIAL      PRIMARY KEY,
    company_id             BIGINT         NOT NULL,
    id_presupuesto         VARCHAR(10)    NOT NULL,
    con_cuenta_code        VARCHAR(20)    NOT NULL,
    centro_costo_id        BIGINT         NULL,
    tipo_movimiento        SMALLINT       NOT NULL,   -- ver CHECK y COMMENT
    modulo                 VARCHAR(20)    NOT NULL,
    documento_tipo         VARCHAR(20)    NOT NULL,   -- ORDEN_COMPRA | FACTURA_COMPRA | ABONO_CXP | PRESUPUESTO
    documento_id           BIGINT         NOT NULL,   -- el documento que CAUSA el movimiento
    documento_numero       VARCHAR(40)    NULL,
    documento_detalle_id   BIGINT         NULL,
    orden_compra_id        BIGINT         NULL,       -- siempre la O/C relacionada, aunque el doc sea la factura
    compromiso_id          BIGINT         NULL,
    fecha                  DATE           NOT NULL,   -- fecha de efecto presupuestario
    monto                  NUMERIC(18,4)  NOT NULL,   -- SIEMPRE positivo; el signo lo da tipo_movimiento
    -- Saldos ANTES (reconstrucción completa de la historia)
    proyeccion_anterior    NUMERIC(18,4)  NOT NULL DEFAULT 0,
    comprometido_anterior  NUMERIC(18,4)  NOT NULL DEFAULT 0,
    ejecutado_anterior     NUMERIC(18,4)  NOT NULL DEFAULT 0,
    disponible_anterior    NUMERIC(18,4)  NOT NULL DEFAULT 0,
    -- Saldos DESPUÉS
    proyeccion_posterior   NUMERIC(18,4)  NOT NULL DEFAULT 0,
    comprometido_posterior NUMERIC(18,4)  NOT NULL DEFAULT 0,
    ejecutado_posterior    NUMERIC(18,4)  NOT NULL DEFAULT 0,
    disponible_posterior   NUMERIC(18,4)  NOT NULL DEFAULT 0,
    excedio                BOOLEAN        NOT NULL DEFAULT FALSE,
    estado                 SMALLINT       NOT NULL DEFAULT 1,  -- 1 Vigente · 9 Reversado
    movimiento_reversa_id  BIGINT         NULL,
    observacion            VARCHAR(500)   NULL,
    usuario                VARCHAR(100)   NOT NULL,
    usuario_aprobo         VARCHAR(100)   NULL,
    ip                     VARCHAR(45)    NULL,
    fecha_registro         TIMESTAMP      NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    CONSTRAINT ck_pst_movimiento_tipo
        CHECK (tipo_movimiento IN (1, 2, 3, 4, 5, 6, 7, 8, 10, 11, 12, 13)),
    CONSTRAINT ck_pst_movimiento_monto  CHECK (monto >= 0),
    CONSTRAINT ck_pst_movimiento_estado CHECK (estado IN (1, 9))
);

CREATE INDEX IF NOT EXISTS ix_pst_movimiento_partida
    ON public.pst_movimiento (company_id, id_presupuesto, con_cuenta_code, fecha);
CREATE INDEX IF NOT EXISTS ix_pst_movimiento_documento
    ON public.pst_movimiento (company_id, documento_tipo, documento_id);
CREATE INDEX IF NOT EXISTS ix_pst_movimiento_orden_compra
    ON public.pst_movimiento (company_id, orden_compra_id) WHERE orden_compra_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_pst_movimiento_compromiso
    ON public.pst_movimiento (compromiso_id) WHERE compromiso_id IS NOT NULL;

-- Idempotencia: funciona porque documento_id es SIEMPRE el documento que causa el movimiento
-- (la O/C para el compromiso, la factura para el devengo, el abono para el pago). Dos recepciones
-- parciales de la misma O/C son dos facturas distintas -> dos filas legítimas; un reintento de la
-- misma factura choca contra el índice.
--
-- Los tipos 10-13 quedan FUERA del índice a propósito: ampliar/reducir un presupuesto y ajustar
-- una O/C aprobada son eventos REPETIBLES por naturaleza (una O/C se puede modificar tres veces).
-- Incluirlos haría que el segundo ajuste chocara contra el índice y se perdiera en silencio.
CREATE UNIQUE INDEX IF NOT EXISTS uq_pst_movimiento_idempotencia
    ON public.pst_movimiento (company_id, tipo_movimiento, documento_tipo, documento_id,
                              con_cuenta_code, COALESCE(documento_detalle_id, 0))
    WHERE estado = 1 AND tipo_movimiento NOT IN (10, 11, 12, 13);

COMMENT ON TABLE  public.pst_movimiento IS 'Kardex presupuestario: historia completa e inmutable de cada partida. No se purga — es el libro de la ejecución y sobrevive al cierre del ejercicio.';
COMMENT ON COLUMN public.pst_movimiento.tipo_movimiento IS '1 Compromiso inicial (aprobación de la O/C) · 2 Liberación · 3 Devengo · 4 Reversa de devengo · 5 Devengo directo (sin O/C) · 6 Reversa de devengo directo · 7 Pago · 8 Reversa de pago · 10 Ampliación de presupuesto · 11 Reducción de presupuesto · 12 Ajuste de compromiso (aumento) · 13 Ajuste de compromiso (disminución). Los tipos 10-13 son repetibles y quedan fuera del índice de idempotencia.';
COMMENT ON COLUMN public.pst_movimiento.monto IS 'Siempre POSITIVO. El signo del efecto lo determina tipo_movimiento.';
COMMENT ON COLUMN public.pst_movimiento.documento_id IS 'El documento que CAUSA el movimiento: la O/C para el compromiso, la factura para el devengo, el abono para el pago. De esto depende la idempotencia.';
COMMENT ON COLUMN public.pst_movimiento.fecha IS 'Fecha de efecto presupuestario. Puede diferir de fecha_registro (cuándo se digitó).';
COMMENT ON COLUMN public.pst_movimiento.ip IS 'IP del usuario. NULL mientras no se pase el HttpContext desde el controlador (decisión D7).';

-- La inmutabilidad se impone en la BD, no por convención.
-- Precedente en el repo: trg_transaccion_abonado_congelada.
CREATE OR REPLACE FUNCTION public.fn_pst_movimiento_solo_insert()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'pst_movimiento es un libro inmutable: no se permite DELETE (id=%). Registre un movimiento de reversa.', OLD.id
            USING ERRCODE = 'P0001';
    END IF;

    -- El único campo que puede cambiar después del INSERT es el enlace a su reversa
    -- (y el estado que lo acompaña).
    IF ROW(NEW.*) IS DISTINCT FROM ROW(OLD.*) THEN
        IF NEW.company_id             IS DISTINCT FROM OLD.company_id
        OR NEW.id_presupuesto         IS DISTINCT FROM OLD.id_presupuesto
        OR NEW.con_cuenta_code        IS DISTINCT FROM OLD.con_cuenta_code
        OR NEW.tipo_movimiento        IS DISTINCT FROM OLD.tipo_movimiento
        OR NEW.documento_tipo         IS DISTINCT FROM OLD.documento_tipo
        OR NEW.documento_id           IS DISTINCT FROM OLD.documento_id
        OR NEW.fecha                  IS DISTINCT FROM OLD.fecha
        OR NEW.monto                  IS DISTINCT FROM OLD.monto
        OR NEW.proyeccion_anterior    IS DISTINCT FROM OLD.proyeccion_anterior
        OR NEW.comprometido_anterior  IS DISTINCT FROM OLD.comprometido_anterior
        OR NEW.ejecutado_anterior     IS DISTINCT FROM OLD.ejecutado_anterior
        OR NEW.disponible_anterior    IS DISTINCT FROM OLD.disponible_anterior
        OR NEW.proyeccion_posterior   IS DISTINCT FROM OLD.proyeccion_posterior
        OR NEW.comprometido_posterior IS DISTINCT FROM OLD.comprometido_posterior
        OR NEW.ejecutado_posterior    IS DISTINCT FROM OLD.ejecutado_posterior
        OR NEW.disponible_posterior   IS DISTINCT FROM OLD.disponible_posterior
        OR NEW.usuario                IS DISTINCT FROM OLD.usuario
        OR NEW.fecha_registro         IS DISTINCT FROM OLD.fecha_registro
        THEN
            RAISE EXCEPTION 'pst_movimiento es un libro inmutable: solo se pueden actualizar estado y movimiento_reversa_id (id=%).', OLD.id
                USING ERRCODE = 'P0001';
        END IF;
    END IF;

    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_pst_movimiento_solo_insert ON public.pst_movimiento;
CREATE TRIGGER trg_pst_movimiento_solo_insert
    BEFORE UPDATE OR DELETE ON public.pst_movimiento
    FOR EACH ROW EXECUTE FUNCTION public.fn_pst_movimiento_solo_insert();

-- -----------------------------------------------------------------------------
-- 6) cfg_presupuesto_control — el interruptor
--    El modo 1 (Advertencia) no es decorativo: permite encender el control en producción,
--    observar un mes de datos reales y detectar cuentas mal presupuestadas SIN bloquear la
--    operación. Sin él, el primer día de bloqueo es una fila de O/C que no se pueden aprobar.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.cfg_presupuesto_control (
    company_id                 BIGINT        NOT NULL,
    modulo                     VARCHAR(30)   NOT NULL,
    modo                       SMALLINT      NOT NULL DEFAULT 0,
    exige_presupuesto_aprobado BOOLEAN       NOT NULL DEFAULT TRUE,
    tolerancia_pct             NUMERIC(5,2)  NOT NULL DEFAULT 0,
    permite_devengo_sin_oc     SMALLINT      NOT NULL DEFAULT 1,
    usuariomodificacion        VARCHAR(100)  NULL,
    fechamodificacion          TIMESTAMP     NULL,
    CONSTRAINT pk_cfg_presupuesto_control PRIMARY KEY (company_id, modulo),
    CONSTRAINT ck_cfg_presupuesto_control_modo         CHECK (modo IN (0, 1, 2)),
    CONSTRAINT ck_cfg_presupuesto_control_sin_oc       CHECK (permite_devengo_sin_oc IN (0, 1, 2)),
    CONSTRAINT ck_cfg_presupuesto_control_tolerancia   CHECK (tolerancia_pct >= 0 AND tolerancia_pct <= 100),
    CONSTRAINT ck_cfg_presupuesto_control_modulo
        CHECK (modulo IN ('COMPRAS_OC', 'COMPRAS_FACTURA', 'PROVEEDORES', 'BANCOS'))
);

COMMENT ON TABLE  public.cfg_presupuesto_control IS 'Modo del control presupuestario por empresa y módulo. Nace en 0 (apagado): aplicar los scripts no cambia el comportamiento de nadie.';
COMMENT ON COLUMN public.cfg_presupuesto_control.modo IS '0 Apagado (no consulta presupuesto) · 1 Advertencia (registra y deja pasar) · 2 Bloqueo (rechaza si excede).';
COMMENT ON COLUMN public.cfg_presupuesto_control.tolerancia_pct IS 'Variación admitida entre el compromiso de la O/C y el devengo de la factura antes de exigir disponible por el exceso.';
COMMENT ON COLUMN public.cfg_presupuesto_control.permite_devengo_sin_oc IS '0 Prohíbe la compra directa · 1 Consume disponible directamente · 2 Solo advierte. Cierra el hueco de que alm_compra_hdr.orden_compra_id sea nullable.';

-- Semilla: una fila por empresa y módulo, todas APAGADAS. Idempotente.
INSERT INTO public.cfg_presupuesto_control (company_id, modulo, modo)
SELECT c.company_id, m.modulo, 0
  FROM public.cfg_company c
 CROSS JOIN (VALUES ('COMPRAS_OC'), ('COMPRAS_FACTURA'), ('PROVEEDORES'), ('BANCOS')) AS m(modulo)
ON CONFLICT (company_id, modulo) DO NOTHING;

-- -----------------------------------------------------------------------------
-- 7) La partida presupuestaria del renglón de la O/C
--    Snapshot INDISPENSABLE: sin él, cambiar la cuenta del tipo de artículo reescribiría
--    retroactivamente contra qué partida se comprometió una O/C ya aprobada.
-- -----------------------------------------------------------------------------
ALTER TABLE public.alm_orden_compra_detalle
    ADD COLUMN IF NOT EXISTS cuenta_presupuestaria VARCHAR(20) NULL,
    ADD COLUMN IF NOT EXISTS centro_costo_id       BIGINT      NULL;

COMMENT ON COLUMN public.alm_orden_compra_detalle.cuenta_presupuestaria IS
    'Cuenta del plan contra la que este renglón compromete presupuesto. Se propone desde alm_tipo_articulo.cuenta_inventario, es editable y se CONGELA al aprobar la O/C.';
COMMENT ON COLUMN public.alm_orden_compra_detalle.centro_costo_id IS
    'Centro de costo del catálogo con_centro_costo. Sustituye a la columna de texto libre centro_costo, que queda DEPRECADA. Informativo: no valida (D3-A).';
COMMENT ON COLUMN public.alm_orden_compra_detalle.centro_costo IS
    'DEPRECADA (2026-08-27). Texto libre sin catálogo. Se conserva por el histórico; el código nuevo usa centro_costo_id.';

-- Clave alterna tenant-safe en con_centro_costo, requerida por la FK compuesta. No puede
-- fallar: cost_center_id ya es PK, así que el par (company_id, cost_center_id) es único
-- por construcción.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conname = 'uq_con_centro_costo_tenant'
           AND conrelid = 'public.con_centro_costo'::regclass
    ) THEN
        ALTER TABLE public.con_centro_costo
            ADD CONSTRAINT uq_con_centro_costo_tenant UNIQUE (company_id, cost_center_id);
    END IF;
END $$;

-- FK COMPUESTA tenant-safe: el renglón no puede apuntar al centro de costo de OTRA empresa.
-- Es la convención del módulo (ver Database/2026-07-30_alm_orden_compra.sql).
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conname = 'fk_alm_orden_compra_detalle_centro_costo'
           AND conrelid = 'public.alm_orden_compra_detalle'::regclass
    ) THEN
        ALTER TABLE public.alm_orden_compra_detalle
            ADD CONSTRAINT fk_alm_orden_compra_detalle_centro_costo
                FOREIGN KEY (company_id, centro_costo_id)
                REFERENCES public.con_centro_costo (company_id, cost_center_id)
                ON DELETE RESTRICT;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_alm_orden_compra_detalle_cuenta_pst
    ON public.alm_orden_compra_detalle (company_id, cuenta_presupuestaria)
    WHERE cuenta_presupuestaria IS NOT NULL;

-- -----------------------------------------------------------------------------
-- 8) Estados nuevos de la orden de compra
--    5 Rechazada  -> desde Borrador. No genera movimiento presupuestario.
--    6 Cancelada  -> desde Aprobada o Recibida parcial. LIBERA el saldo comprometido pendiente.
--
--    AnularAsync hoy PROHÍBE anular una O/C con recepciones, y eso se mantiene: el caso
--    "O/C con recepción parcial que ya no se va a completar" es exactamente Cancelada, la
--    operación que faltaba.
--
--    El CHECK solo AMPLÍA el conjunto admitido: ninguna fila existente queda inválida.
-- -----------------------------------------------------------------------------
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conname = 'ck_alm_orden_compra_estado'
           AND conrelid = 'public.alm_orden_compra'::regclass
    ) THEN
        ALTER TABLE public.alm_orden_compra DROP CONSTRAINT ck_alm_orden_compra_estado;
    END IF;

    ALTER TABLE public.alm_orden_compra
        ADD CONSTRAINT ck_alm_orden_compra_estado CHECK (estado IN (1, 2, 3, 4, 5, 6, 9));
END $$;

COMMENT ON COLUMN public.alm_orden_compra.estado IS
    '1 Borrador · 2 Aprobada · 3 Recibida parcial · 4 Cerrada · 5 Rechazada · 6 Cancelada · 9 Anulada. 5 y 6 se agregaron el 2026-08-27 con el control presupuestario: Cancelada libera el saldo comprometido pendiente.';

COMMIT;

-- =============================================================================
-- VERIFICACIÓN (ejecutar después del COMMIT)
-- =============================================================================
-- a) Las 4 tablas nuevas existen y están vacías
-- SELECT 'pst_compromiso'             AS tabla, count(*) FROM public.pst_compromiso
-- UNION ALL SELECT 'pst_compromiso_aplicacion', count(*) FROM public.pst_compromiso_aplicacion
-- UNION ALL SELECT 'pst_movimiento',            count(*) FROM public.pst_movimiento
-- UNION ALL SELECT 'cfg_presupuesto_control',   count(*) FROM public.cfg_presupuesto_control;
--
-- b) El control nace APAGADO en todas las empresas (modo debe ser 0 en todas las filas)
-- SELECT modo, count(*) FROM public.cfg_presupuesto_control GROUP BY modo;
--
-- c) Las columnas nuevas existen y están en 0 / NULL
-- SELECT count(*) AS filas,
--        count(*) FILTER (WHERE valor_comprometido <> 0) AS comprometido_no_cero,
--        count(*) FILTER (WHERE valor_pagado <> 0)       AS pagado_no_cero
--   FROM public.pst_config_presupuesto_dtl;
--
-- SELECT count(*) AS renglones,
--        count(cuenta_presupuestaria) AS con_cuenta,
--        count(centro_costo_id)       AS con_centro_costo
--   FROM public.alm_orden_compra_detalle;
--
-- d) El CHECK de estado admite los 7 valores
-- SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'ck_alm_orden_compra_estado';
--
-- e) El kardex es realmente inmutable (ambas deben fallar con P0001)
-- INSERT INTO public.pst_movimiento (company_id, id_presupuesto, con_cuenta_code, tipo_movimiento,
--        modulo, documento_tipo, documento_id, fecha, monto, usuario)
--   VALUES (2, 'PRUEBA', '00000000000', 1, 'COMPRAS', 'ORDEN_COMPRA', -1, CURRENT_DATE, 1, 'prueba');
-- UPDATE public.pst_movimiento SET monto = 999 WHERE documento_id = -1;   -- debe FALLAR
-- DELETE FROM public.pst_movimiento WHERE documento_id = -1;              -- debe FALLAR
-- -- limpieza de la prueba (requiere desactivar el trigger a propósito):
-- ALTER TABLE public.pst_movimiento DISABLE TRIGGER trg_pst_movimiento_solo_insert;
-- DELETE FROM public.pst_movimiento WHERE documento_id = -1;
-- ALTER TABLE public.pst_movimiento ENABLE TRIGGER trg_pst_movimiento_solo_insert;
--
-- =============================================================================
-- ROLLBACK
-- =============================================================================
-- DROP TRIGGER  IF EXISTS trg_pst_movimiento_solo_insert ON public.pst_movimiento;
-- DROP FUNCTION IF EXISTS public.fn_pst_movimiento_solo_insert();
-- DROP TABLE    IF EXISTS public.pst_compromiso_aplicacion;
-- DROP TABLE    IF EXISTS public.pst_movimiento;
-- DROP TABLE    IF EXISTS public.pst_compromiso;
-- DROP TABLE    IF EXISTS public.cfg_presupuesto_control;
-- ALTER TABLE public.alm_orden_compra_detalle
--     DROP CONSTRAINT IF EXISTS fk_alm_orden_compra_detalle_centro_costo,
--     DROP COLUMN IF EXISTS cuenta_presupuestaria,
--     DROP COLUMN IF EXISTS centro_costo_id;
-- ALTER TABLE public.pst_config_presupuesto_dtl
--     DROP CONSTRAINT IF EXISTS ck_pst_dtl_montos_no_negativos,
--     DROP COLUMN IF EXISTS valor_comprometido,
--     DROP COLUMN IF EXISTS valor_pagado;
-- ALTER TABLE public.pst_config_presupuesto_hdr DROP COLUMN IF EXISTS valor_comprometido;
-- ALTER TABLE public.alm_orden_compra DROP CONSTRAINT IF EXISTS ck_alm_orden_compra_estado;
-- ALTER TABLE public.alm_orden_compra
--     ADD CONSTRAINT ck_alm_orden_compra_estado CHECK (estado IN (1, 2, 3, 4, 9));
-- DROP TYPE IF EXISTS public.pst_linea_afectacion;
-- (uq_con_centro_costo_tenant se puede conservar: es una clave alterna inocua.)
