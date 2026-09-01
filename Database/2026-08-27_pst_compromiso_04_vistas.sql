-- =============================================================================
-- Control presupuestario con COMPROMISO en la O/C — vistas de consulta y reportes
-- Fecha: 2026-08-27
-- Fase F1 (4 de 4). Requiere: los scripts 01 (estructura), 02 (funciones) y 03 (procedimientos)
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) antes que en el SRV
--
-- POR QUÉ
-- Hoy la única consulta de presupuesto es el grid maestro-detalle de
-- PresupuestoConfiguracionesList.razor y el PDF Rpt_Dev_Presupuesto, ambos SIN drill-down: no
-- hay forma de saber qué documentos consumieron una cuenta. Estas cuatro vistas son la base de
-- los reportes de la fase F5 y, sobre todo, lo que vuelve auditable el saldo.
--
-- Las cuatro son de LECTURA y llevan company_id: el filtrado por empresa lo hace quien consulta.
-- Ninguna reemplaza objetos existentes.
-- =============================================================================
BEGIN;

-- -----------------------------------------------------------------------------
-- 1) vw_pst_compromiso_saldo — compromisos con su saldo derivado
--    Aquí vive el saldo: es columna derivada a propósito, no materializada (evita una columna
--    generada y su desincronización).
-- -----------------------------------------------------------------------------
CREATE OR REPLACE VIEW public.vw_pst_compromiso_saldo AS
SELECT c.id,
       c.company_id,
       c.id_presupuesto,
       c.con_cuenta_code,
       pc.name                        AS cuenta_nombre,
       c.centro_costo_id,
       cc.code                        AS centro_costo_codigo,
       cc.name                        AS centro_costo_nombre,
       c.modulo,
       c.documento_tipo,
       c.documento_id,
       c.documento_numero,
       c.documento_detalle_id,
       c.fecha,
       o.cod_proveedor,
       pr.nombre                      AS proveedor,
       o.estado                       AS orden_estado,
       c.monto_comprometido,
       c.monto_devengado,
       c.monto_liberado,
       (c.monto_comprometido - c.monto_devengado - c.monto_liberado) AS saldo_comprometido,
       c.estado,
       (CURRENT_DATE - c.fecha)       AS dias_antiguedad,
       c.usuariocreacion,
       c.fechacreacion
  FROM public.pst_compromiso c
  LEFT JOIN public.con_plan_cuentas pc
         ON pc.company_id = c.company_id
        AND upper(btrim(pc.code)) = upper(btrim(c.con_cuenta_code))
  LEFT JOIN public.con_centro_costo cc
         ON cc.company_id = c.company_id
        AND cc.cost_center_id = c.centro_costo_id
  LEFT JOIN public.alm_orden_compra o
         ON c.documento_tipo = 'ORDEN_COMPRA'
        AND o.company_id = c.company_id
        AND o.id = c.documento_id
  LEFT JOIN public.prv_proveedores pr
         ON pr.company_id = c.company_id
        AND pr.cod_proveedor = o.cod_proveedor;

COMMENT ON VIEW public.vw_pst_compromiso_saldo IS
    'Compromisos presupuestarios con saldo derivado (comprometido - devengado - liberado). Filtrar estado = 1 para el reporte de compromisos pendientes.';

-- -----------------------------------------------------------------------------
-- 2) vw_pst_ejecucion_presupuestaria — el reporte principal
--    Una fila por partida: presupuesto, comprometido, ejecutado, pagado, disponible.
-- -----------------------------------------------------------------------------
CREATE OR REPLACE VIEW public.vw_pst_ejecucion_presupuestaria AS
SELECT d.company_id,
       d.id_presupuesto,
       h.fecha_inicia,
       h.fecha_finaliza,
       h.estado_aprobado,
       d.con_cuenta_code,
       pc.name                                    AS cuenta_nombre,
       pc.account_type                            AS cuenta_tipo,
       pc.allows_budget                           AS cuenta_presupuestable,
       d.valor_proyeccion                         AS presupuesto,
       d.valor_comprometido                       AS comprometido,
       d.valor_real                               AS ejecutado,
       d.valor_pagado                             AS pagado,
       GREATEST(d.valor_proyeccion - d.valor_comprometido - d.valor_real, 0) AS disponible,
       CASE WHEN d.valor_proyeccion > 0
            THEN round(100.0 * d.valor_real / d.valor_proyeccion, 2) ELSE NULL END       AS pct_ejecucion,
       CASE WHEN d.valor_proyeccion > 0
            THEN round(100.0 * d.valor_comprometido / d.valor_proyeccion, 2) ELSE NULL END AS pct_compromiso,
       CASE WHEN d.valor_proyeccion > 0
            THEN round(100.0 * (d.valor_comprometido + d.valor_real) / d.valor_proyeccion, 2)
            ELSE NULL END                                                                  AS pct_utilizado
  FROM public.pst_config_presupuesto_dtl d
  JOIN public.pst_config_presupuesto_hdr h
    ON h.company_id = d.company_id
   AND h.id_presupuesto = d.id_presupuesto
  LEFT JOIN public.con_plan_cuentas pc
         ON pc.company_id = d.company_id
        AND upper(btrim(pc.code)) = upper(btrim(d.con_cuenta_code));

COMMENT ON VIEW public.vw_pst_ejecucion_presupuestaria IS
    'Ejecución presupuestaria por partida: presupuesto, comprometido, ejecutado, pagado y disponible. Disponible = proyeccion - comprometido - real.';

-- -----------------------------------------------------------------------------
-- 3) vw_pst_movimiento_detalle — el KARDEX en pantalla
--    Permite reconstruir la historia completa de una partida: cada fila trae los saldos antes y
--    después, quién, cuándo y con qué documento.
-- -----------------------------------------------------------------------------
CREATE OR REPLACE VIEW public.vw_pst_movimiento_detalle AS
SELECT m.id,
       m.company_id,
       m.id_presupuesto,
       m.con_cuenta_code,
       pc.name                        AS cuenta_nombre,
       m.centro_costo_id,
       cc.code                        AS centro_costo_codigo,
       cc.name                        AS centro_costo_nombre,
       m.tipo_movimiento,
       CASE m.tipo_movimiento
            WHEN  1 THEN 'Compromiso'
            WHEN  2 THEN 'Liberación de compromiso'
            WHEN  3 THEN 'Devengo'
            WHEN  4 THEN 'Reversa de devengo'
            WHEN  5 THEN 'Devengo directo (sin O/C)'
            WHEN  6 THEN 'Reversa de devengo directo'
            WHEN  7 THEN 'Pago'
            WHEN  8 THEN 'Reversa de pago'
            WHEN 10 THEN 'Ampliación de presupuesto'
            WHEN 11 THEN 'Reducción de presupuesto'
            WHEN 12 THEN 'Ajuste de compromiso (aumento)'
            WHEN 13 THEN 'Ajuste de compromiso (disminución)'
            ELSE 'Desconocido'
       END                            AS tipo_movimiento_nombre,
       -- Efecto con signo, para que el reporte no tenga que replicar la tabla de tipos.
       CASE WHEN m.tipo_movimiento IN (1, 12) THEN  m.monto
            WHEN m.tipo_movimiento IN (2, 13) THEN -m.monto
            ELSE 0 END                AS efecto_comprometido,
       CASE WHEN m.tipo_movimiento IN (3, 5) THEN  m.monto
            WHEN m.tipo_movimiento IN (4, 6) THEN -m.monto
            ELSE 0 END                AS efecto_ejecutado,
       CASE WHEN m.tipo_movimiento = 7 THEN  m.monto
            WHEN m.tipo_movimiento = 8 THEN -m.monto
            ELSE 0 END                AS efecto_pagado,
       m.modulo,
       m.documento_tipo,
       m.documento_id,
       m.documento_numero,
       m.orden_compra_id,
       o.numero                       AS orden_compra_numero,
       o.cod_proveedor,
       pr.nombre                      AS proveedor,
       m.compromiso_id,
       m.fecha,
       m.monto,
       m.proyeccion_anterior,  m.comprometido_anterior,  m.ejecutado_anterior,  m.disponible_anterior,
       m.proyeccion_posterior, m.comprometido_posterior, m.ejecutado_posterior, m.disponible_posterior,
       m.excedio,
       m.estado,
       m.movimiento_reversa_id,
       m.observacion,
       m.usuario,
       m.usuario_aprobo,
       m.ip,
       m.fecha_registro
  FROM public.pst_movimiento m
  LEFT JOIN public.con_plan_cuentas pc
         ON pc.company_id = m.company_id
        AND upper(btrim(pc.code)) = upper(btrim(m.con_cuenta_code))
  LEFT JOIN public.con_centro_costo cc
         ON cc.company_id = m.company_id
        AND cc.cost_center_id = m.centro_costo_id
  LEFT JOIN public.alm_orden_compra o
         ON o.company_id = m.company_id
        AND o.id = m.orden_compra_id
  LEFT JOIN public.prv_proveedores pr
         ON pr.company_id = m.company_id
        AND pr.cod_proveedor = o.cod_proveedor;

COMMENT ON VIEW public.vw_pst_movimiento_detalle IS
    'Kardex presupuestario listo para pantalla: tipo con nombre, efecto con signo, documento, O/C, proveedor y saldos antes/después.';

-- -----------------------------------------------------------------------------
-- 4) vw_pst_ejecucion_centro_costo — consumo por área
--    ⚠️ INFORMATIVA. El presupuesto NO tiene eje de centro de costo (decisión D3-A): esta vista
--    agrega lo CONSUMIDO por centro de costo, pero no existe un "presupuesto asignado al centro
--    de costo" contra el cual compararlo. La columna de presupuesto se omite a propósito en vez
--    de inventar un reparto que nadie autorizó.
-- -----------------------------------------------------------------------------
CREATE OR REPLACE VIEW public.vw_pst_ejecucion_centro_costo AS
SELECT m.company_id,
       m.id_presupuesto,
       m.centro_costo_id,
       cc.code                        AS centro_costo_codigo,
       cc.name                        AS centro_costo_nombre,
       m.con_cuenta_code,
       pc.name                        AS cuenta_nombre,
       SUM(CASE WHEN m.tipo_movimiento IN (1, 12) THEN  m.monto
                WHEN m.tipo_movimiento IN (2, 13) THEN -m.monto ELSE 0 END) AS comprometido,
       SUM(CASE WHEN m.tipo_movimiento IN (3, 5)  THEN  m.monto
                WHEN m.tipo_movimiento IN (4, 6)  THEN -m.monto ELSE 0 END) AS ejecutado,
       SUM(CASE WHEN m.tipo_movimiento = 7 THEN  m.monto
                WHEN m.tipo_movimiento = 8 THEN -m.monto ELSE 0 END)        AS pagado
  FROM public.pst_movimiento m
  LEFT JOIN public.con_centro_costo cc
         ON cc.company_id = m.company_id
        AND cc.cost_center_id = m.centro_costo_id
  LEFT JOIN public.con_plan_cuentas pc
         ON pc.company_id = m.company_id
        AND upper(btrim(pc.code)) = upper(btrim(m.con_cuenta_code))
 WHERE m.estado = 1
 GROUP BY m.company_id, m.id_presupuesto, m.centro_costo_id, cc.code, cc.name,
          m.con_cuenta_code, pc.name;

COMMENT ON VIEW public.vw_pst_ejecucion_centro_costo IS
    'Consumo presupuestario agregado por centro de costo. INFORMATIVA: el presupuesto no tiene eje de centro de costo (D3-A), por eso no hay columna de presupuesto asignado.';

COMMIT;

-- =============================================================================
-- VERIFICACIÓN (ejecutar después del COMMIT; empresa 2 = MERENDON)
-- =============================================================================
-- a) Las 4 vistas existen y responden
-- SELECT table_name FROM information_schema.views
--  WHERE table_schema = 'public'
--    AND table_name IN ('vw_pst_compromiso_saldo', 'vw_pst_ejecucion_presupuestaria',
--                       'vw_pst_movimiento_detalle', 'vw_pst_ejecucion_centro_costo')
--  ORDER BY table_name;
-- Esperado: 4 filas.
--
-- b) La ejecución presupuestaria responde con los datos de hoy (comprometido y pagado deben
--    salir en 0 hasta que el control se encienda)
-- SELECT count(*) AS partidas,
--        SUM(presupuesto) AS presupuesto,
--        SUM(comprometido) AS comprometido,
--        SUM(ejecutado) AS ejecutado,
--        SUM(pagado) AS pagado,
--        SUM(disponible) AS disponible
--   FROM public.vw_pst_ejecucion_presupuestaria
--  WHERE company_id = 2 AND estado_aprobado;
--
-- c) Las 3 vistas nuevas sobre tablas vacías devuelven 0 filas (aún no hay movimientos)
-- SELECT (SELECT count(*) FROM public.vw_pst_compromiso_saldo       WHERE company_id = 2) AS compromisos,
--        (SELECT count(*) FROM public.vw_pst_movimiento_detalle     WHERE company_id = 2) AS movimientos,
--        (SELECT count(*) FROM public.vw_pst_ejecucion_centro_costo WHERE company_id = 2) AS por_centro;
--
-- d) ⚠️ EL DATO QUE DECIDE SI EL CONTROL SERVIRÁ (decisión D1):
--    ¿están presupuestadas las cuentas de inventario de los tipos de artículo?
-- SELECT t.codigo, t.nombre, t.cuenta_inventario,
--        pc.allows_budget                       AS marcada_presupuestable,
--        e.presupuesto                          AS presupuestada_en
--   FROM public.alm_tipo_articulo t
--   LEFT JOIN public.con_plan_cuentas pc
--          ON pc.company_id = t.company_id
--         AND upper(btrim(pc.code)) = upper(btrim(t.cuenta_inventario))
--   LEFT JOIN public.vw_pst_ejecucion_presupuestaria e
--          ON e.company_id = t.company_id
--         AND upper(btrim(e.con_cuenta_code)) = upper(btrim(t.cuenta_inventario))
--         AND e.estado_aprobado
--  WHERE t.company_id = 2
--  ORDER BY t.codigo;
--
--    Si marcada_presupuestable sale FALSE o presupuestada_en sale NULL en todas las filas,
--    encender el control contra la cuenta de inventario NO bloquearía nada. Es exactamente el
--    riesgo R1 del diseño y lo que hay que resolver con el contador (D1) antes de la fase F7.
--
-- =============================================================================
-- ROLLBACK
-- =============================================================================
-- DROP VIEW IF EXISTS public.vw_pst_ejecucion_centro_costo;
-- DROP VIEW IF EXISTS public.vw_pst_movimiento_detalle;
-- DROP VIEW IF EXISTS public.vw_pst_ejecucion_presupuestaria;
-- DROP VIEW IF EXISTS public.vw_pst_compromiso_saldo;
