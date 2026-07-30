-- =============================================================================
-- Resiembra de `adm_documento_secuencia` tras la migración total de SIMAFI
-- =============================================================================
-- PLAN_UNIFICACION_COBRANZA §3.8 sembró la serie RECIBO_PAGO con
-- `valor_actual = MAX(factura.numrecibo)`, que en su momento era 3,075,230.
-- La migración SIMAFI cambió el panorama:
--
--   factura.numrecibo             llega a  4,194,366  (numeración original SIMAFI)
--   adm_pago.numero_recibo        llega a 15,429,120  (id del movimiento migrado)
--   adm_documento_secuencia       quedó en  3,075,230  ← desfasada
--
-- Los folios nuevos salen con prefijo (`REC-00000001`) y los migrados son
-- dígitos pelados, así que hoy no colisionan por el prefijo. Pero dejar la serie
-- por debajo de lo ya usado es una bomba de tiempo: basta que alguien quite el
-- prefijo, compare como número, o que otro proceso reutilice la serie, para
-- chocar contra `uq_adm_pago_numero_recibo`.
--
-- Se reposiciona por encima del máximo REALMENTE usado en cualquiera de las dos
-- numeraciones, con holgura.
-- =============================================================================

\timing on
\set ON_ERROR_STOP on

\echo '--- antes ---'
SELECT company_id, tipo_documento, canal_id, prefijo, longitud_padding, valor_actual
FROM public.adm_documento_secuencia
WHERE tipo_documento = 'RECIBO_PAGO'
ORDER BY company_id, canal_id;

UPDATE public.adm_documento_secuencia s
   SET valor_actual = GREATEST(
           s.valor_actual,
           COALESCE((SELECT max(f.numrecibo) FROM public.factura f
                      WHERE f.company_id = s.company_id), 0),
           COALESCE((SELECT max(p.numero_recibo::bigint) FROM public.adm_pago p
                      WHERE p.company_id = s.company_id
                        AND p.numero_recibo ~ '^[0-9]+$'), 0)
       ) + 1000
 WHERE s.tipo_documento = 'RECIBO_PAGO';

\echo '--- despues ---'
SELECT company_id, tipo_documento, canal_id, prefijo, longitud_padding, valor_actual
FROM public.adm_documento_secuencia
WHERE tipo_documento = 'RECIBO_PAGO'
ORDER BY company_id, canal_id;

\echo '--- control: la serie queda por encima de todo lo usado ---'
SELECT s.company_id,
       s.valor_actual,
       (SELECT max(f.numrecibo) FROM public.factura f WHERE f.company_id = s.company_id) AS max_factura,
       (SELECT max(p.numero_recibo::bigint) FROM public.adm_pago p
         WHERE p.company_id = s.company_id AND p.numero_recibo ~ '^[0-9]+$')            AS max_pago,
       (s.valor_actual > COALESCE((SELECT max(f.numrecibo) FROM public.factura f WHERE f.company_id = s.company_id), 0)
        AND s.valor_actual > COALESCE((SELECT max(p.numero_recibo::bigint) FROM public.adm_pago p
                                        WHERE p.company_id = s.company_id AND p.numero_recibo ~ '^[0-9]+$'), 0)) AS ok
FROM public.adm_documento_secuencia s
WHERE s.tipo_documento = 'RECIBO_PAGO';
