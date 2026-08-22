-- =============================================================================
-- Almacén: 3 artículos de PRUEBA para ejercicios de entradas y salidas
-- Fecha: 2026-08-20
--
-- Base objetivo: siad_v4 @ 172.16.0.9  (la base ACTIVA)
--
-- ⚠️ ESTO CREA DATOS DE PRUEBA EN PRODUCCIÓN, a pedido del usuario (2026-08-20).
--    Los 3 van con código `PRUEBA-0x` y descripción «ARTICULO DE PRUEBA» para que se
--    distingan a simple vista de los 634 artículos reales. Todos en la **bodega 11**
--    (id 3), que ya se usa para datos de ejemplo.
--
--    Para borrarlos después, ver el bloque de LIMPIEZA al final: el kardex tiene el
--    trigger `trg_alm_kardex_inmutable` (BEFORE DELETE OR UPDATE), así que el asiento
--    del artículo 1 NO se puede borrar sin desactivar el trigger.
--
-- QUÉ CREA
--   PRUEBA-01  tipo 01 (materiales agua potable) · UND · bodega 11
--              **CON existencia inicial: 100 UND a L. 25.0000 = L. 2,500.00**
--              (alta + asiento de kardex de entrada + existencia y costo en la bodega)
--   PRUEBA-02  tipo 03 (útiles de oficina)       · UND · bodega 11 · existencia 0
--   PRUEBA-03  tipo 06 (material eléctrico)      · MTR · bodega 11 · existencia 0
--
--   Con PRUEBA-01 se puede hacer una salida de entrada; con los otros dos, practicar
--   el ciclo completo desde la primera entrada.
--
-- ADITIVO: solo INSERT. No toca ningún artículo, bodega ni asiento existente.
-- IDEMPOTENTE: cada INSERT va con WHERE NOT EXISTS por código de artículo.
--
-- ¿YA APLICADO?
--   SELECT codigo_articulo, existencia FROM alm_articulo
--    WHERE company_id = 2 AND codigo_articulo LIKE 'PRUEBA-%' ORDER BY 1;
-- =============================================================================
BEGIN;

DO $$
DECLARE
    v_company   bigint := 2;
    v_bodega_id bigint := 3;      -- bodega 11
    v_bodega    text   := '11';
    v_art_id    bigint;
    v_cant      numeric := 100.00;
    v_costo     numeric := 25.0000;
BEGIN
    -- Guardias: la bodega y los tipos tienen que existir.
    IF NOT EXISTS (SELECT 1 FROM alm_bodega WHERE company_id = v_company AND id = v_bodega_id) THEN
        RAISE EXCEPTION 'No existe la bodega id=% en company=%.', v_bodega_id, v_company;
    END IF;

    -- -----------------------------------------------------------------------
    -- PRUEBA-01 — CON existencia inicial
    -- -----------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM alm_articulo
                    WHERE company_id = v_company AND codigo_articulo = 'PRUEBA-01') THEN

        INSERT INTO alm_articulo
            (company_id, codigo_articulo, descripcion, fecha_registro, cantidad, existencia,
             existencia_minima, valor_unitario, unidad_medida_id, tipo_articulo_id,
             activo, usuariocreacion, fechacreacion)
        VALUES
            (v_company, 'PRUEBA-01', 'ARTICULO DE PRUEBA 01 - CON EXISTENCIA', now(),
             v_cant, v_cant, 0, v_costo,
             (SELECT id FROM alm_unidad_medida WHERE company_id = v_company AND codigo = 'UND'),
             (SELECT id FROM alm_tipo_articulo WHERE company_id = v_company AND codigo = '01'),
             true, 'prueba_2026-08-20', now())
        RETURNING id INTO v_art_id;

        -- Existencia y costo en la bodega
        INSERT INTO alm_articulo_bodega
            (company_id, articulo_id, bodega_id, existencia, existencia_minima, principal, activo,
             existencia_maxima, existencia_comprometida, existencia_transito,
             costo_promedio, ultimo_costo, punto_reorden, usuariocreacion)
        VALUES
            (v_company, v_art_id, v_bodega_id, v_cant, 0, true, true,
             0, 0, 0, v_costo, v_costo, 0, 'prueba_2026-08-20');

        -- Asiento de kardex de la entrada inicial (el libro es la fuente de verdad
        -- del costo promedio: sin este asiento la existencia quedaría "colgada").
        INSERT INTO alm_kardex
            (company_id, numero_documento, tipo_transaccion, fecha, codigo_articulo, cantidad,
             bodega, ingresos, salidas, valor_unitario, total, debe, haber, cuenta_contable,
             es_ajuste, descripcion, observacion, bodega_id, articulo_id, uuid, documento_tipo,
             existencia_resultante, costo_promedio_resultante, usuariocreacion)
        VALUES
            (v_company, NULL, '102', now()::date, 'PRUEBA-01', v_cant,
             v_bodega, v_cant, 0, v_costo, v_cant * v_costo, v_cant * v_costo, 0,
             (SELECT cuenta_inventario FROM alm_tipo_articulo WHERE company_id = v_company AND codigo = '01'),
             true, 'ARTICULO DE PRUEBA 01 - CON EXISTENCIA', 'INI-PRUEBA-01 - carga inicial para ejercicios',
             v_bodega_id, v_art_id, gen_random_uuid(), 'CARGA_INICIAL',
             v_cant, v_costo, 'prueba_2026-08-20');

        RAISE NOTICE 'PRUEBA-01 creado (id=%) con % UND a L.% en bodega %.', v_art_id, v_cant, v_costo, v_bodega;
    ELSE
        RAISE NOTICE 'PRUEBA-01 ya existia, se omite.';
    END IF;

    -- -----------------------------------------------------------------------
    -- PRUEBA-02 y PRUEBA-03 — en cero
    -- -----------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM alm_articulo
                    WHERE company_id = v_company AND codigo_articulo = 'PRUEBA-02') THEN

        INSERT INTO alm_articulo
            (company_id, codigo_articulo, descripcion, fecha_registro, cantidad, existencia,
             existencia_minima, valor_unitario, unidad_medida_id, tipo_articulo_id,
             activo, usuariocreacion, fechacreacion)
        VALUES
            (v_company, 'PRUEBA-02', 'ARTICULO DE PRUEBA 02 - SIN EXISTENCIA', now(),
             0, 0, 0, 0,
             (SELECT id FROM alm_unidad_medida WHERE company_id = v_company AND codigo = 'UND'),
             (SELECT id FROM alm_tipo_articulo WHERE company_id = v_company AND codigo = '03'),
             true, 'prueba_2026-08-20', now())
        RETURNING id INTO v_art_id;

        INSERT INTO alm_articulo_bodega
            (company_id, articulo_id, bodega_id, existencia, existencia_minima, principal, activo,
             existencia_maxima, existencia_comprometida, existencia_transito,
             costo_promedio, ultimo_costo, punto_reorden, usuariocreacion)
        VALUES
            (v_company, v_art_id, v_bodega_id, 0, 0, true, true, 0, 0, 0, 0, 0, 0, 'prueba_2026-08-20');

        RAISE NOTICE 'PRUEBA-02 creado (id=%) en cero.', v_art_id;
    ELSE
        RAISE NOTICE 'PRUEBA-02 ya existia, se omite.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM alm_articulo
                    WHERE company_id = v_company AND codigo_articulo = 'PRUEBA-03') THEN

        INSERT INTO alm_articulo
            (company_id, codigo_articulo, descripcion, fecha_registro, cantidad, existencia,
             existencia_minima, valor_unitario, unidad_medida_id, tipo_articulo_id,
             activo, usuariocreacion, fechacreacion)
        VALUES
            (v_company, 'PRUEBA-03', 'ARTICULO DE PRUEBA 03 - SIN EXISTENCIA', now(),
             0, 0, 0, 0,
             (SELECT id FROM alm_unidad_medida WHERE company_id = v_company AND codigo = 'MTR'),
             (SELECT id FROM alm_tipo_articulo WHERE company_id = v_company AND codigo = '06'),
             true, 'prueba_2026-08-20', now())
        RETURNING id INTO v_art_id;

        INSERT INTO alm_articulo_bodega
            (company_id, articulo_id, bodega_id, existencia, existencia_minima, principal, activo,
             existencia_maxima, existencia_comprometida, existencia_transito,
             costo_promedio, ultimo_costo, punto_reorden, usuariocreacion)
        VALUES
            (v_company, v_art_id, v_bodega_id, 0, 0, true, true, 0, 0, 0, 0, 0, 0, 'prueba_2026-08-20');

        RAISE NOTICE 'PRUEBA-03 creado (id=%) en cero.', v_art_id;
    ELSE
        RAISE NOTICE 'PRUEBA-03 ya existia, se omite.';
    END IF;
END $$;

-- ---------------------------------------------------------------------------
-- Verificación
-- ---------------------------------------------------------------------------
DO $$
DECLARE
    v_arts int; v_bod int; v_kdx int;
BEGIN
    SELECT count(*) INTO v_arts FROM alm_articulo
     WHERE company_id = 2 AND codigo_articulo LIKE 'PRUEBA-%';
    SELECT count(*) INTO v_bod FROM alm_articulo_bodega ab
      JOIN alm_articulo a ON a.id = ab.articulo_id
     WHERE ab.company_id = 2 AND a.codigo_articulo LIKE 'PRUEBA-%';
    SELECT count(*) INTO v_kdx FROM alm_kardex
     WHERE company_id = 2 AND codigo_articulo LIKE 'PRUEBA-%';

    RAISE NOTICE 'articulos=% filas_bodega=% asientos_kardex=%', v_arts, v_bod, v_kdx;

    IF v_arts <> 3 OR v_bod <> 3 OR v_kdx <> 1 THEN
        RAISE EXCEPTION 'Verificacion fallida: articulos=% bodega=% kardex=% (esperado 3/3/1). Se revierte.',
              v_arts, v_bod, v_kdx;
    END IF;
END $$;

COMMIT;

-- =============================================================================
-- LIMPIEZA (para borrar los artículos de prueba cuando ya no se necesiten)
--
--   ⚠️ El asiento de kardex NO se puede borrar con el trigger activo. Hay que
--      desactivarlo, borrar, y volver a activarlo — todo en una transacción:
--
--   BEGIN;
--   ALTER TABLE alm_kardex DISABLE TRIGGER trg_alm_kardex_inmutable;
--   DELETE FROM alm_kardex WHERE company_id=2 AND codigo_articulo LIKE 'PRUEBA-%';
--   ALTER TABLE alm_kardex ENABLE  TRIGGER trg_alm_kardex_inmutable;
--   DELETE FROM alm_articulo_bodega WHERE company_id=2 AND articulo_id IN
--          (SELECT id FROM alm_articulo WHERE company_id=2 AND codigo_articulo LIKE 'PRUEBA-%');
--   DELETE FROM alm_articulo WHERE company_id=2 AND codigo_articulo LIKE 'PRUEBA-%';
--   COMMIT;
--
--   Si para entonces ya registraste movimientos sobre estos artículos, los DELETE
--   van a fallar por FK: primero hay que quitar esos documentos.
-- =============================================================================
