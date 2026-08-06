-- =============================================================================
-- Almacén: el stock se muda a la bodega donde vive su kardex (PRIN -> 01)
-- Fecha: 2026-07-31
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) antes que en SRV
--
-- POR QUÉ HACE FALTA ESTE SCRIPT
-- El kardex y el stock quedaron en bodegas distintas:
--
--   * alm_kardex (histórico SIMAFI): 47,213 de 47,215 asientos en bodega_id = 2
--     (código '01'), de 587 artículos.
--   * alm_articulo_bodega: 634 filas en bodega_id = 1 (código 'PRIN') y 1 sola
--     en la 2.
--
-- Son la MISMA bodega física con dos identidades. La prueba: el saldo del
-- histórico de la bodega 2 coincide EXACTAMENTE con la existencia que hay en la
-- bodega 1 en 585 de 587 artículos (99.7%). Las 2 excepciones son el artículo
-- 0002 (histórico -58 vs existencia 42) y el 0147 (histórico 0 vs existencia -6,
-- que además es una de las existencias negativas pendientes de saneo).
--
-- El causante probable es 2026-07-07_alm_articulo_bodega_backfill_existencia.sql,
-- que sembró las filas por bodega desde la cabecera del artículo sin mirar de qué
-- bodega hablaba el kardex.
--
-- QUÉ SE ROMPE SI NO SE ARREGLA
-- El punto de corte de la carga inicial (KardexService) empareja POR PAR
-- (artículo, bodega). Con la apertura en la bodega 1 y el histórico en la 2 no
-- empareja NUNCA:
--   * Filtrando por bodega, la pantalla calla el descuadre (no hay fila del par,
--     así que no hay cifra comparable): falso negativo.
--   * SIN filtrar bodega, el saldo corrido SUMA histórico + apertura y DUPLICA la
--     existencia. Medido en el mirror el 2026-07-31 tras un ensayo del corte: el
--     artículo 0001 mostraba saldo 572.00 contra una existencia de 286.00, y 8 de
--     los 12 artículos con más histórico quedaron descuadrados.
--
-- QUÉ HACE
--   1. Mueve las filas de alm_articulo_bodega de la bodega 1 a la 2. Conservan
--      id, existencia, costos, mínimos, máximos, punto de reorden, ubicación y
--      la marca de principal: SOLO cambia bodega_id.
--   2. Resuelve la única colisión con el índice único (company, artículo, bodega):
--      el artículo 0030 ya tiene fila en la bodega 2 (existencia 0, comprometida 0,
--      tránsito 0, costos 0, creada el 2026-07-29 por admin@siad-demo.com — una
--      fila de prueba). Se conserva el mayor de los dos mínimos y se elimina la
--      fila duplicada.
--   3. Rehace el rollup de cabecera de los artículos que quedaron desalineados.
--      Hace falta por el punto 2: al eliminar una fila de bodega cambia la Σ de
--      mínimos, y sin esto alm_articulo.existencia_minima queda stale (medido:
--      el artículo 0030 quedaba con 50 en la cabecera contra 30 en sus bodegas).
--
-- QUÉ **NO** HACE — a propósito
--   * NO toca alm_kardex. Sus 47,215 asientos quedan intactos y NO se deshabilita
--     ningún trigger. Mover el histórico habría exigido apagar
--     trg_alm_kardex_inmutable, es decir reescribir el libro.
--   * NO crea ni usa disparadores de ningún tipo.
--   * NO toca compras, descargos ni requisiciones: sus 90,107 filas tienen
--     bodega_id en NULL (verificado), así que no hay nada que realinear.
--   * NO desactiva la bodega PRIN, que queda sin artículos. Si se decide
--     desactivarla, va en un script aparte.
--   * NO es DDL: ni ALTER, ni DROP, ni índices, ni constraints.
--
-- IDEMPOTENTE: si ya se aplicó, los tres pasos afectan 0 filas (el WHERE exige
-- bodega_id = 1, y después del cambio no queda ninguna).
-- REVERSIBLE en cuanto al movimiento (ver ROLLBACK al final). La fila duplicada
-- que se elimina NO se recupera: es una fila de prueba en cero.
--
-- ⚠️ ANTES DE APLICAR EN SRV: correr el bloque «¿YA APLICADO?» y confirmar que
-- los ids de bodega son los mismos (1 = PRIN, 2 = '01'). Los ids se asignan por
-- secuencia y NO tienen por qué coincidir entre mirror y producción.
-- =============================================================================

-- ---------------------------------------------------------------------------
-- ¿YA APLICADO? (correr ANTES, fuera de la transacción)
-- Esperado antes de aplicar : bodega 1 -> 634 filas, bodega 2 -> 1 fila.
-- Esperado después          : bodega 1 -> 0 filas,   bodega 2 -> 634 filas.
-- ---------------------------------------------------------------------------
-- SELECT b.id, b.codigo, b.nombre, count(ab.id) AS filas,
--        count(ab.id) FILTER (WHERE ab.existencia <> 0) AS con_existencia
-- FROM alm_bodega b
-- LEFT JOIN alm_articulo_bodega ab ON ab.company_id = b.company_id AND ab.bodega_id = b.id
-- WHERE b.company_id = 2
-- GROUP BY b.id, b.codigo, b.nombre ORDER BY b.id;

BEGIN;

-- ---------------------------------------------------------------------------
-- 0) Guardas: si el terreno no es el esperado, el script se detiene sin escribir.
-- ---------------------------------------------------------------------------
DO $$
DECLARE
    v_origen  INT;
    v_destino INT;
BEGIN
    SELECT id INTO v_origen  FROM public.alm_bodega WHERE company_id = 2 AND codigo = 'PRIN';
    SELECT id INTO v_destino FROM public.alm_bodega WHERE company_id = 2 AND codigo = '01';

    IF v_origen IS NULL OR v_destino IS NULL THEN
        RAISE EXCEPTION 'No se encontraron las bodegas PRIN y/o 01 en la empresa 2. Revise los códigos antes de aplicar.';
    END IF;

    IF v_origen <> 1 OR v_destino <> 2 THEN
        RAISE EXCEPTION 'Los ids de bodega no son los esperados (PRIN=%, 01=%). Ajuste el script a los ids de ESTA base antes de aplicarlo.', v_origen, v_destino;
    END IF;
END $$;

-- ---------------------------------------------------------------------------
-- 1) Colisión con uq (company, artículo, bodega): conservar el mayor mínimo.
--    Solo aplica a los artículos que tienen fila en LAS DOS bodegas.
-- ---------------------------------------------------------------------------
UPDATE public.alm_articulo_bodega origen
   SET existencia_minima = GREATEST(origen.existencia_minima, destino.existencia_minima)
  FROM public.alm_articulo_bodega destino
 WHERE origen.company_id  = 2
   AND destino.company_id = 2
   AND origen.bodega_id   = 1
   AND destino.bodega_id  = 2
   AND destino.articulo_id = origen.articulo_id;

-- ---------------------------------------------------------------------------
-- 2) Eliminar la fila duplicada de la bodega destino.
--    La guarda de existencia/comprometida/tránsito en CERO es deliberada: si
--    alguna trajera saldo, el script falla en el paso 3 por el índice único en
--    vez de perder inventario en silencio.
-- ---------------------------------------------------------------------------
DELETE FROM public.alm_articulo_bodega destino
 WHERE destino.company_id = 2
   AND destino.bodega_id  = 2
   AND destino.existencia = 0
   AND destino.existencia_comprometida = 0
   AND destino.existencia_transito = 0
   AND EXISTS (
        SELECT 1 FROM public.alm_articulo_bodega origen
         WHERE origen.company_id  = 2
           AND origen.bodega_id   = 1
           AND origen.articulo_id = destino.articulo_id);

-- ---------------------------------------------------------------------------
-- 3) La mudanza. Cambia bodega_id y nada más.
-- ---------------------------------------------------------------------------
UPDATE public.alm_articulo_bodega
   SET bodega_id           = 2,
       usuariomodificacion = 'script-2026-07-31',
       fechamodificacion   = now() AT TIME ZONE 'UTC'
 WHERE company_id = 2
   AND bodega_id  = 1;

-- ---------------------------------------------------------------------------
-- 4) Rehacer el rollup de cabecera. Es OBLIGATORIO, no cosmético: el paso 2
--    eliminó una fila de bodega, así que la Σ de mínimos del artículo afectado
--    cambió y alm_articulo.existencia_minima quedaría stale. (La mudanza en sí
--    no mueve existencia — las filas siguen activas —, pero se recalculan los
--    tres campos por coherencia con el contrato del rollup: Σ sobre bodegas
--    ACTIVAS, y cantidad = existencia.)
--    Solo toca los artículos que realmente quedaron desalineados: en una
--    re-ejecución afecta 0 filas.
-- ---------------------------------------------------------------------------
WITH sumas AS (
    SELECT a.id,
           COALESCE(SUM(ab.existencia)        FILTER (WHERE ab.activo), 0) AS existencia,
           COALESCE(SUM(ab.existencia_minima) FILTER (WHERE ab.activo), 0) AS minima
      FROM public.alm_articulo a
      LEFT JOIN public.alm_articulo_bodega ab
             ON ab.company_id = a.company_id AND ab.articulo_id = a.id
     WHERE a.company_id = 2
     GROUP BY a.id
)
UPDATE public.alm_articulo a
   SET existencia        = s.existencia,
       existencia_minima = s.minima,
       cantidad          = s.existencia
  FROM sumas s
 WHERE a.company_id = 2
   AND a.id = s.id
   AND (a.existencia        IS DISTINCT FROM s.existencia
     OR a.existencia_minima IS DISTINCT FROM s.minima
     OR a.cantidad          IS DISTINCT FROM s.existencia);

COMMIT;

-- =============================================================================
-- VERIFICACIÓN (correr DESPUÉS del COMMIT)
-- =============================================================================

-- 1. Reparto por bodega. Esperado: PRIN 0 filas, '01' 634 filas / 244 con existencia.
-- SELECT b.id, b.codigo, count(ab.id) AS filas,
--        count(ab.id) FILTER (WHERE ab.existencia <> 0) AS con_existencia
-- FROM alm_bodega b
-- LEFT JOIN alm_articulo_bodega ab ON ab.company_id = b.company_id AND ab.bodega_id = b.id
-- WHERE b.company_id = 2 GROUP BY b.id, b.codigo ORDER BY b.id;

-- 2. Nada se perdió: la cabecera sigue cuadrada contra la suma de bodegas activas.
--    Esperado: 0 filas.
-- SELECT a.id, a.codigo_articulo, a.existencia, COALESCE(SUM(ab.existencia), 0) AS suma_bodegas
-- FROM alm_articulo a
-- LEFT JOIN alm_articulo_bodega ab ON ab.company_id = a.company_id AND ab.articulo_id = a.id AND ab.activo
-- WHERE a.company_id = 2
-- GROUP BY a.id, a.codigo_articulo, a.existencia
-- HAVING a.existencia <> COALESCE(SUM(ab.existencia), 0);

-- 3. El objetivo del script: histórico y stock en el mismo par. Ahora el saldo del
--    kardex de la bodega 2 sí tiene contra qué compararse. Esperado: ~585 de 587
--    artículos coinciden (las 2 excepciones conocidas son 0002 y 0147).
-- WITH hist AS (
--   SELECT articulo_id, SUM(COALESCE(ingresos,0) - COALESCE(salidas,0)) AS saldo_hist
--   FROM alm_kardex WHERE company_id = 2 AND uuid IS NULL AND bodega_id = 2 AND articulo_id IS NOT NULL
--   GROUP BY articulo_id
-- ), stock AS (
--   SELECT articulo_id, existencia FROM alm_articulo_bodega WHERE company_id = 2 AND bodega_id = 2
-- )
-- SELECT count(*) AS comparados,
--        count(*) FILTER (WHERE h.saldo_hist = s.existencia) AS coinciden
-- FROM hist h JOIN stock s USING (articulo_id);

-- 4. Exactamente una bodega principal por artículo (la mudanza no cambió la marca).
--    Esperado: 0 filas.
-- SELECT articulo_id, count(*) FROM alm_articulo_bodega
-- WHERE company_id = 2 AND activo AND principal
-- GROUP BY articulo_id HAVING count(*) <> 1;

-- =============================================================================
-- ROLLBACK (devuelve el stock a PRIN; NO recrea la fila duplicada eliminada)
-- =============================================================================
-- BEGIN;
-- UPDATE public.alm_articulo_bodega
--    SET bodega_id = 1, usuariomodificacion = 'rollback-2026-07-31', fechamodificacion = now() AT TIME ZONE 'UTC'
--  WHERE company_id = 2 AND bodega_id = 2;
-- COMMIT;
