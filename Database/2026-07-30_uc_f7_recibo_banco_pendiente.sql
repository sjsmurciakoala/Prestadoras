-- ============================================================================
-- Unificación de cobranza — F7 hito 1 (2026-07-30)
-- Los "recibos para banco" pendientes dejan transaccion_abonado (filas
-- 202/'P') y pasan a su tabla propia adm_recibo_banco_pendiente. Era el único
-- uso de transaccion_abonado sin casa en el modelo nuevo — prerequisito del
-- freeze de F7 (plan docs/PLAN_F7_CORTE_2026-07.md §3 H1).
--
-- * estado_id reusa adm_estado_pago: 2 PENDIENTE (vivo), 1 APLICADO (cobrado
--   en caja — guarda el adm_pago), 3 ANULADO (manual o cubierto por la
--   conciliación automática).
-- * Se migran los 'P' vivos resolviendo la factura por numrecibo + cliente.
--   Las filas legacy quedan como historia congelada (el estado 'P' no cuenta
--   en la vigencia; la tabla se congela en F7 H4).
-- * El trigger de conciliación (factura saldada → pendientes cubiertos,
--   2026-07-27) se reapunta a la tabla nueva.
-- ============================================================================

BEGIN;

CREATE TABLE IF NOT EXISTS public.adm_recibo_banco_pendiente (
    recibo_pendiente_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id          bigint NOT NULL REFERENCES public.cfg_company (company_id),
    cliente_clave       varchar(20) NOT NULL,
    factura_id          integer NOT NULL REFERENCES public.factura (id),
    numrecibo           integer NOT NULL,
    monto               numeric(18,2) NOT NULL CHECK (monto > 0),
    estado_id           smallint NOT NULL DEFAULT 2
        REFERENCES public.adm_estado_pago (estado_pago_id),
    descripcion         varchar(300),
    generado_por        varchar(100) NOT NULL,
    generado_en         timestamptz NOT NULL DEFAULT now(),
    -- Cobro en caja: el documento del motor que lo aplicó.
    cobrado_pago_id     bigint REFERENCES public.adm_pago (pago_id),
    -- Anulación (manual o conciliación automática).
    anulado_por         varchar(100),
    anulado_en          timestamptz,
    motivo_anulacion    varchar(300),
    -- Fila legacy de origen (migración; NULL para recibos nuevos).
    transaccion_abonado_ide integer
);

CREATE INDEX IF NOT EXISTS ix_adm_recibo_pendiente_factura
    ON public.adm_recibo_banco_pendiente (company_id, factura_id, estado_id);
CREATE INDEX IF NOT EXISTS ix_adm_recibo_pendiente_cliente
    ON public.adm_recibo_banco_pendiente (company_id, cliente_clave)
    WHERE estado_id = 2;

COMMENT ON TABLE public.adm_recibo_banco_pendiente IS
'F7 H1: recibos "para pagar en banco" emitidos en ventanilla (antes filas 202/P de transaccion_abonado). NO rebajan la factura; se aplican al cobrarse en caja (cobrado_pago_id) o se anulan (manual o cubiertos por la conciliación al saldarse la factura).';

-- ----------------------------------------------------------------------------
-- Migración de los pendientes vivos (idempotente por transaccion_abonado_ide).
-- ----------------------------------------------------------------------------
INSERT INTO public.adm_recibo_banco_pendiente
    (company_id, cliente_clave, factura_id, numrecibo, monto, estado_id,
     descripcion, generado_por, generado_en, transaccion_abonado_ide)
SELECT t.company_id,
       t.cliente_clave,
       f.id,
       f.numrecibo,
       COALESCE(t.creditos, 0),
       2,
       t.descripcion,
       COALESCE(t.usuario, 'migracion-f7'),
       COALESCE(t.fecha_registro::timestamptz, now()),
       t.ide
FROM public.transaccion_abonado t
JOIN public.factura f
  ON f.company_id = t.company_id
 AND f.clientecodigo = t.cliente_clave
 AND f.numrecibo = t.recibo::integer
WHERE t.tipotransaccion = '202'
  AND t.estado = 'P'
  AND COALESCE(t.creditos, 0) > 0
  AND NOT EXISTS (
      SELECT 1 FROM public.adm_recibo_banco_pendiente r
      WHERE r.transaccion_abonado_ide = t.ide);

-- ----------------------------------------------------------------------------
-- Conciliación automática: al saldarse la factura ('C' por cualquier canal),
-- los pendientes vivos quedan cubiertos — ahora sobre la tabla nueva.
-- ----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.fn_factura_concilia_recibos_pendientes()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE public.adm_recibo_banco_pendiente r
    SET estado_id = 3,
        anulado_en = now(),
        anulado_por = 'sistema',
        motivo_anulacion = 'CUBIERTO: factura saldada'
    WHERE r.company_id = NEW.company_id
      AND r.factura_id = NEW.id
      AND r.estado_id = 2;

    -- Espejo legacy del pendiente (dual-write hasta F7 H2/H4): mantenerlo
    -- coherente mientras exista.
    UPDATE public.transaccion_abonado t
    SET estado = 'A',
        descripcion = 'CUBIERTO: factura saldada — ' || COALESCE(t.descripcion, '')
    WHERE t.company_id = NEW.company_id
      AND t.recibo = NEW.numrecibo
      AND t.tipotransaccion = '202'
      AND t.estado = 'P';
    RETURN NEW;
END;
$$;

-- El trigger existente (2026-07-27) queda apuntando a la función redefinida;
-- si no existe (BD que nunca aplicó ese DDL), se crea.
DROP TRIGGER IF EXISTS trg_factura_concilia_pendientes ON public.factura;
CREATE TRIGGER trg_factura_concilia_pendientes
    AFTER UPDATE OF estado ON public.factura
    FOR EACH ROW
    WHEN (NEW.estado = 'C' AND OLD.estado IS DISTINCT FROM 'C')
    EXECUTE FUNCTION public.fn_factura_concilia_recibos_pendientes();

COMMIT;
