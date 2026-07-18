-- =============================================================================
-- Regla de vigencia única para transaccion_abonado + porcentajes del desglose
-- Fecha: 2026-07-16
-- Diseño: docs/plans/2026-07-16-desglose-abono-porcentajes-design.md
-- Respaldo previo del SP (revertible): Database/2026-07-16_backup_sp_obtener_cliente_saldo.sql
--
-- PROBLEMA: convención de estado invertida entre módulos.
--   * Facturación V3 / lectores de saldo: 'A' = activo, 'N'/'R' = anulado.
--   * Caja / posteos / WS bancario: abono vigente = 'C', recibo pendiente = 'P',
--     y al anular/reversar ponen 'A'.
--   Resultado: los abonos vigentes NO restan al saldo ni al desglose, y los
--   abonos reversados SÍ restan. Además la columna saldo (corrido) de los abonos
--   está corrupta porque se calculaba con el propio SP roto — por eso el SP pasa
--   a sumar (débitos − créditos) en vez de leer esa columna.
--
-- Idempotente. NO modifica datos: solo una función, una vista y una tabla nueva.
-- Aplicar primero al mirror (siad_v3_restore) y después a prod (siad_v3),
-- corriendo antes la auditoría del final para dimensionar el impacto.
-- =============================================================================

BEGIN;

-- 1) La regla de vigencia en un solo lugar, formulada por exclusión de lo muerto:
--    * 'N' (factura anulada V3), 'R' (reversado legacy), 'P' (recibo pendiente)
--      nunca cuentan.
--    * 'A' significa "anulado" SOLO para los pagos de caja/WS (tipotransaccion
--      201/202); para todo lo demás 'A' es "activo".
--    * Cualquier otro estado cuenta: pagos 201/202 con 'C' (vigentes para la
--      caja) y el traslado 'PLAN' con 'C' de los planes de pago (crédito que
--      compensa las cuotas PLAN-CUOTA para no duplicar la deuda trasladada).
CREATE OR REPLACE VIEW public.vw_transaccion_abonado_vigente AS
SELECT ta.*
FROM public.transaccion_abonado ta
WHERE COALESCE(ta.estado, '') NOT IN ('N', 'R', 'P')
  AND NOT (ta.estado = 'A' AND COALESCE(ta.tipotransaccion, '') IN ('201', '202'));

COMMENT ON VIEW public.vw_transaccion_abonado_vigente IS
'Movimientos vigentes de transaccion_abonado bajo la convencion doble de estado:
quedan fuera N (anulada), R (reversado legacy), P (recibo pendiente) y los pagos
201/202 con estado A (la caja/WS marca A al anular/reversar). Todo lo demas cuenta:
cargos/NC/ND/saldo anterior con A, pagos 201/202 con C y el traslado PLAN con C de
los planes de pago. Fix vigencia 2026-07-16.';

-- 2) Saldo del cliente = suma de vigentes (misma firma, mismos callers:
--    estado de cuenta, cobranza, corte, saldo_previo de facturación V3).
CREATE OR REPLACE FUNCTION public.sp_obtener_cliente_saldo(
    p_company_id bigint,
    pcodigocliente character varying
)
RETURNS TABLE(saldo_actual numeric)
LANGUAGE plpgsql
STABLE
AS $function$
BEGIN
  RETURN QUERY
  SELECT COALESCE(SUM(COALESCE(ta.debitos, 0) - COALESCE(ta.creditos, 0)), 0)
  FROM public.vw_transaccion_abonado_vigente ta
  WHERE ta.company_id    = p_company_id
    AND ta.cliente_clave = pcodigocliente;
END
$function$;

COMMENT ON FUNCTION public.sp_obtener_cliente_saldo(bigint, character varying) IS
'Saldo total del cliente = SUM(debitos - creditos) de vw_transaccion_abonado_vigente.
Antes leia el saldo corrido del ultimo movimiento con estado A, que ignoraba los
abonos vigentes (estado C) y contaba los reversados (estado A). Respaldo de la
version anterior: Database/2026-07-16_backup_sp_obtener_cliente_saldo.sql.
Devuelve una fila con 0 si el cliente no tiene movimientos (antes: 0 filas).
La firma de 1 argumento queda intacta (deprecated, cross-company, sin callers).';

-- 3) Mantenimiento "Distribución de abonos": porcentaje con que un abono se
--    reparte entre los ítems del desglose por servicio del estado de cuenta.
CREATE TABLE IF NOT EXISTS public.adm_desglose_abono_porcentaje (
    desglose_abono_id  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint NOT NULL,
    item_codigo        varchar(50) NOT NULL,
    porcentaje         numeric(5,2) NOT NULL CHECK (porcentaje > 0 AND porcentaje <= 100),
    usuario            varchar(100),
    fecha_modificacion timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_desglose_abono UNIQUE (company_id, item_codigo)
);

COMMENT ON TABLE public.adm_desglose_abono_porcentaje IS
'Porcentaje con que los abonos se reparten entre los items del desglose por servicio
del estado de cuenta (item_codigo = adm_servicio.codigo o SALDO_ANTERIOR). La suma
por empresa debe ser 100.00; sin filas la distribucion esta desactivada y los pagos
quedan en la fila "Pagos y ajustes". Mantenimiento: /tarifario/desglose-abonos.';

COMMIT;

-- =============================================================================
-- AUDITORÍA (solo lectura; correr en prod ANTES de aplicar y de nuevo DESPUÉS)
-- Compara el saldo viejo (último movimiento 'A') contra el nuevo (SUM vigentes).
-- Requiere la vista ya creada; para correrla ANTES de aplicar, sustituir la
-- subconsulta de "nuevo" por transaccion_abonado con el WHERE de la vista.
--
-- En el mirror (2026-07-16, 881 clientes) dio 20 con diferencia, todas explicadas:
--   * 14 de ±0.01: arrastre de redondeo de la columna saldo corrida (la suma es
--     la aritméticamente correcta).
--   * abonos vigentes 'C' que por fin restan (p.ej. 090808251: 38,182.93 -> 0.00;
--     090807336: 14,094.61 -> 3,094.61).
--   * 090807219: 9,929.71 -> 10,101.65 — su pago estaba REVERSADO por el WS
--     (202 estado 'A') y aun así restaba; deja de restar.
--   * 090807355: 171.94 -> 722.78 — plan de pago; el saldo viejo leía el corrido
--     interno de la última cuota (171.94), que ni siquiera era el saldo real.
-- =============================================================================
-- WITH clientes AS (
--   SELECT DISTINCT company_id, cliente_clave FROM transaccion_abonado
-- ),
-- viejo AS (
--   SELECT c.company_id, c.cliente_clave,
--          (SELECT t.saldo FROM transaccion_abonado t
--            WHERE t.company_id = c.company_id AND t.cliente_clave = c.cliente_clave
--              AND t.estado = 'A'
--            ORDER BY t.ide DESC LIMIT 1) AS saldo_viejo
--   FROM clientes c
-- ),
-- nuevo AS (
--   SELECT c.company_id, c.cliente_clave,
--          (SELECT COALESCE(SUM(COALESCE(t.debitos,0) - COALESCE(t.creditos,0)), 0)
--             FROM public.vw_transaccion_abonado_vigente t
--            WHERE t.company_id = c.company_id AND t.cliente_clave = c.cliente_clave) AS saldo_nuevo
--   FROM clientes c
-- )
-- SELECT v.company_id, v.cliente_clave,
--        COALESCE(v.saldo_viejo, 0) AS saldo_viejo,
--        n.saldo_nuevo,
--        n.saldo_nuevo - COALESCE(v.saldo_viejo, 0) AS delta
-- FROM viejo v
-- JOIN nuevo n USING (company_id, cliente_clave)
-- WHERE COALESCE(v.saldo_viejo, 0) <> n.saldo_nuevo
-- ORDER BY v.company_id, abs(n.saldo_nuevo - COALESCE(v.saldo_viejo, 0)) DESC;
