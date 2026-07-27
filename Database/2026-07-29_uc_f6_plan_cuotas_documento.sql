-- ============================================================================
-- Unificación de cobranza — F6 hito 1 (2026-07-29)
-- Las cuotas de plan de pago se vuelven DOCUMENTOS cobrables por la caja
-- única (documento_tipo = 2 en adm_pago_aplicacion, columna plan_cuota_id que
-- existe desde F2).
--
-- 1) Multi-tenancy (regla #1 del repo — gap heredado): cln_plan_pago_hdr/dtl
--    no tenían company_id. Se agrega con backfill por clienteid →
--    cliente_maestro (las bases locales están vacías; en 0.9/prod el backfill
--    resuelve los planes históricos).
-- 2) Estados numéricos (regla del repo — nada de strings nuevos):
--    * hdr.estado_id → adm_estado_plan (1 ACTIVO, 2 COMPLETADO, 3 ANULADO).
--    * dtl.estado_id → cfg_estado_documento_comercial (mismo catálogo que la
--      factura: 1 Activa, 4 Abonada parcial, 2 Cobrada, 3 Anulada) — la cuota
--      ES un documento comercial cobrable.
--    * estadopago (varchar) queda de solo-lectura legacy y se retira en F7.
-- 3) dtl.saldo_cuota: saldo vivo de la cuota (el motor lo rebaja al aplicar
--    pagos, igual que factura_detalle.montovalor_saldo).
-- 4) cln_plan_pago_traslado: registro del traslado de deuda al crear el plan
--    (qué líneas de factura se compensaron y por cuánto) — es la contraparte
--    documental del viejo movimiento 'PLAN' y lo que permite reversar/anular
--    un plan restituyendo saldos exactos.
-- 5) sp_obtener_cliente_saldo v4: + cuotas pendientes de planes ACTIVOS.
--    El saldo legacy ya incluía las cuotas como débitos PLAN-CUOTA vigentes,
--    así que el valor NO cambia para planes viejos; para planes nuevos (F6)
--    la equivalencia dual-write se sostiene porque las facturas compensadas
--    salen de documentos pero su espejo legacy queda vigente (mismo total).
-- ============================================================================

BEGIN;

-- ----------------------------------------------------------------------------
-- Catálogo de estados del plan (cabecera).
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.adm_estado_plan (
    estado_plan_id smallint PRIMARY KEY,
    codigo         varchar(20) NOT NULL UNIQUE,
    nombre         varchar(60) NOT NULL
);

INSERT INTO public.adm_estado_plan (estado_plan_id, codigo, nombre) VALUES
    (1, 'ACTIVO',     'Activo'),
    (2, 'COMPLETADO', 'Completado'),
    (3, 'ANULADO',    'Anulado')
ON CONFLICT (estado_plan_id) DO NOTHING;

-- ----------------------------------------------------------------------------
-- cln_plan_pago_hdr: tenancy + estado numérico.
-- ----------------------------------------------------------------------------
ALTER TABLE public.cln_plan_pago_hdr
    ADD COLUMN IF NOT EXISTS company_id bigint,
    ADD COLUMN IF NOT EXISTS estado_id  smallint;

UPDATE public.cln_plan_pago_hdr h
SET company_id = cm.company_id
FROM public.cliente_maestro cm
WHERE h.company_id IS NULL
  AND cm.maestro_cliente_id = h.clienteid;

UPDATE public.cln_plan_pago_hdr
SET estado_id = CASE
        WHEN upper(COALESCE(estadopago, '')) IN ('COMPLETADO', 'PAGADO', 'FINALIZADO') THEN 2
        WHEN upper(COALESCE(estadopago, '')) IN ('ANULADO', 'CANCELADO') THEN 3
        ELSE 1
    END
WHERE estado_id IS NULL;

ALTER TABLE public.cln_plan_pago_hdr
    ALTER COLUMN company_id SET NOT NULL,
    ALTER COLUMN estado_id SET NOT NULL,
    ALTER COLUMN estado_id SET DEFAULT 1;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_cln_plan_pago_hdr_estado') THEN
        ALTER TABLE public.cln_plan_pago_hdr
            ADD CONSTRAINT fk_cln_plan_pago_hdr_estado
                FOREIGN KEY (estado_id) REFERENCES public.adm_estado_plan (estado_plan_id),
            ADD CONSTRAINT fk_cln_plan_pago_hdr_company
                FOREIGN KEY (company_id) REFERENCES public.cfg_company (company_id);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_cln_plan_pago_hdr_company_estado
    ON public.cln_plan_pago_hdr (company_id, estado_id);

-- ----------------------------------------------------------------------------
-- cln_plan_pago_dtl: tenancy + estado de documento + saldo vivo de la cuota.
-- ----------------------------------------------------------------------------
ALTER TABLE public.cln_plan_pago_dtl
    ADD COLUMN IF NOT EXISTS company_id  bigint,
    ADD COLUMN IF NOT EXISTS estado_id   smallint,
    ADD COLUMN IF NOT EXISTS saldo_cuota numeric(18,2);

UPDATE public.cln_plan_pago_dtl d
SET company_id = h.company_id
FROM public.cln_plan_pago_hdr h
WHERE d.company_id IS NULL
  AND h.id = d.idhdr;

-- Cuotas históricas: pagada si su estadopago lo dice; si no, activa con el
-- valor completo como saldo (nunca hubo cobro parcial de cuotas: el flujo no
-- existía — plan §F6).
UPDATE public.cln_plan_pago_dtl
SET estado_id = CASE
        WHEN upper(COALESCE(estadopago, '')) IN ('PAGADO', 'PAGADA', 'COMPLETADO') THEN 2
        WHEN upper(COALESCE(estadopago, '')) IN ('ANULADO', 'ANULADA', 'CANCELADO') THEN 3
        ELSE 1
    END,
    saldo_cuota = CASE
        WHEN upper(COALESCE(estadopago, '')) IN ('PAGADO', 'PAGADA', 'COMPLETADO', 'ANULADO', 'ANULADA', 'CANCELADO')
            THEN 0
        ELSE COALESCE(valorcuota, 0)
    END
WHERE estado_id IS NULL;

ALTER TABLE public.cln_plan_pago_dtl
    ALTER COLUMN company_id SET NOT NULL,
    ALTER COLUMN estado_id SET NOT NULL,
    ALTER COLUMN estado_id SET DEFAULT 1,
    ALTER COLUMN saldo_cuota SET NOT NULL,
    ALTER COLUMN saldo_cuota SET DEFAULT 0;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_cln_plan_pago_dtl_estado') THEN
        ALTER TABLE public.cln_plan_pago_dtl
            ADD CONSTRAINT fk_cln_plan_pago_dtl_estado
                FOREIGN KEY (estado_id) REFERENCES public.cfg_estado_documento_comercial (estado_id),
            ADD CONSTRAINT fk_cln_plan_pago_dtl_company
                FOREIGN KEY (company_id) REFERENCES public.cfg_company (company_id),
            ADD CONSTRAINT ck_cln_plan_pago_dtl_saldo
                CHECK (saldo_cuota >= 0);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_cln_plan_pago_dtl_company_hdr
    ON public.cln_plan_pago_dtl (company_id, idhdr);
-- Para el saldo del cliente y el listado de caja: cuotas vivas por plan.
CREATE INDEX IF NOT EXISTS ix_cln_plan_pago_dtl_pendientes
    ON public.cln_plan_pago_dtl (company_id, idhdr, estado_id)
    WHERE estado_id IN (1, 4);

-- ----------------------------------------------------------------------------
-- Traslado de deuda del plan: qué líneas de factura se compensaron al crear
-- el plan y por cuánto (contraparte documental del viejo asiento 'PLAN').
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.cln_plan_pago_traslado (
    traslado_id        bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint NOT NULL REFERENCES public.cfg_company (company_id),
    plan_id            integer NOT NULL REFERENCES public.cln_plan_pago_hdr (id),
    factura_id         integer NOT NULL REFERENCES public.factura (id),
    factura_detalle_id integer NOT NULL REFERENCES public.factura_detalle (id),
    monto_trasladado   numeric(18,2) NOT NULL CHECK (monto_trasladado > 0),
    creado_en          timestamptz NOT NULL DEFAULT now(),
    creado_por         varchar(100) NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_cln_plan_pago_traslado_plan
    ON public.cln_plan_pago_traslado (company_id, plan_id);

COMMENT ON TABLE public.cln_plan_pago_traslado IS
'F6: traslado de deuda al crear un plan de pago — compensación de líneas de factura por derrame FIFO (montoFinanciar). Permite anular el plan restituyendo saldos exactos. Reemplaza al movimiento legacy PLAN (crédito).';

-- ----------------------------------------------------------------------------
-- Saldo del cliente v4: documentos pendientes + CUOTAS DE PLAN ACTIVAS +
-- residuo migrado. Misma firma desde 2026-05-09.
-- ----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.sp_obtener_cliente_saldo(
    p_company_id     bigint,
    pcodigocliente   character varying)
RETURNS TABLE(saldo_actual numeric)
LANGUAGE plpgsql
STABLE
AS $function$
BEGIN
  RETURN QUERY
  SELECT
      COALESCE((
          SELECT SUM(COALESCE(d.montovalor_saldo, d.montovalor, 0))
          FROM public.factura f
          JOIN public.factura_detalle d ON d.factura_id = f.id
          WHERE f.company_id    = p_company_id
            AND f.clientecodigo = pcodigocliente
            AND f.estado IN ('A','B')
      ), 0)
    + COALESCE((
          -- F6: cuotas vivas de planes ACTIVOS (1 Activa, 4 Abonada parcial).
          -- El saldo legacy ya las sumaba como débitos PLAN-CUOTA vigentes.
          SELECT SUM(dt.saldo_cuota)
          FROM public.cln_plan_pago_dtl dt
          JOIN public.cln_plan_pago_hdr h ON h.id = dt.idhdr
          JOIN public.cliente_maestro cm ON cm.maestro_cliente_id = h.clienteid
                                        AND cm.company_id = h.company_id
          WHERE h.company_id = p_company_id
            AND h.estado_id  = 1
            AND cm.maestro_cliente_clave = pcodigocliente
            AND dt.estado_id IN (1, 4)
      ), 0)
    + COALESCE((
          -- Residuo migrado sin documento (cartera SIMAFI). Se retira en F7.
          SELECT SUM(COALESCE(ta.debitos, 0) - COALESCE(ta.creditos, 0))
          FROM public.vw_transaccion_abonado_vigente ta
          WHERE ta.company_id      = p_company_id
            AND ta.cliente_clave   = pcodigocliente
            AND ta.tipotransaccion IN ('SALDO_ANTERIOR', 'SALDO_INICIAL')
      ), 0);
END
$function$;

COMMENT ON FUNCTION public.sp_obtener_cliente_saldo(bigint, character varying) IS
'F6 (2026-07-29): saldo = documentos pendientes (facturas A/B) + cuotas vivas de planes activos + residuo migrado SALDO_ANTERIOR/SALDO_INICIAL. Misma firma desde 2026-05-09. El residuo desaparece en F7.';

COMMIT;
