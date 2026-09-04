-- =============================================================================
-- Fase C — Configuración contable de Almacén y Compras
-- Fecha: 2026-08-20
--
-- Base objetivo: siad_v4 @ 172.16.0.9  (la base ACTIVA; NO siad_v3)
--
-- Tres piezas, en este orden (hay FK entre ellas):
--   C.1a  con_diario            18 INVENTARIO · 28 COMPRAS
--   C.1b  con_tipo_transaccion  54 INVENTARIOS · 55 COMPRAS
--   C.1c  con_integracion_asiento  ALMACEN (18/54) · COMPRAS (28/55)
--   C.2   alm_tipo_articulo.cuenta_inventario  ->  cuentas que SÍ existen arriba
--
-- -----------------------------------------------------------------------------
-- POR QUÉ C.2 NO ES "QUITAR LOS GUIONES"
--   En siad_v4 los 9 tipos de artículo apuntan a cuentas con guiones
--   (114-01-01-01-01). El plan de cuentas de siad_v4 NO usa guiones, así que
--   ninguna existe. Pero además **tampoco existen sin guiones**: la rama 114 de
--   producción llega solo a nivel 5 (8 cuentas), mientras que local tiene 19
--   incluidas las de nivel 6 que usa el almacén.
--
--   Decisión tomada (2026-08-20): **no abrir cuentas nuevas en el plan de
--   producción**; remapear cada tipo al ancestro más específico que YA existe
--   arriba y permite posteo. Se pierde detalle contable — 5 tipos comparten
--   cuenta — a cambio de no tocar el plan de una empresa real.
--
--   Nota: para los tipos 04, 06 y 08 el ancestro disponible es de **nivel 4**
--   (11409000000 «Otros Inventarios»), porque la rama 11409 de producción no
--   tiene nivel 5.
-- -----------------------------------------------------------------------------
--
-- ADITIVO salvo el UPDATE acotado de C.2 (9 filas de un catálogo de 9).
-- NO ENCIENDE NADA: con_integracion_config.activo_almacen y .activo_compras
--   quedan como estén (hoy en 'f'). Encenderlos va con el binario que los soporta.
-- IDEMPOTENTE: INSERT … WHERE NOT EXISTS + UPDATE a valores fijos.
--
-- ¿YA APLICADO?
--   SELECT module, journal_id, type_id FROM con_integracion_asiento
--    WHERE company_id = 2 AND module IN ('ALMACEN','COMPRAS');
--
-- REVERSIBLE: bloque de rollback comentado al final.
-- =============================================================================
BEGIN;

-- ---------------------------------------------------------------------------
-- C.1a  Diarios
-- ---------------------------------------------------------------------------
INSERT INTO con_diario
    (journal_id, company_id, code, name, description, sequence_prefix,
     last_sequence, is_active, allows_manual, is_default_manual, created_at, created_by)
SELECT 18, 2, 'INV', 'INVENTARIO', NULL, 'INV1', 0, true, false, false, now(), 'fase_c_2026-08-20'
WHERE NOT EXISTS (SELECT 1 FROM con_diario WHERE company_id = 2 AND journal_id = 18);

INSERT INTO con_diario
    (journal_id, company_id, code, name, description, sequence_prefix,
     last_sequence, is_active, allows_manual, is_default_manual, created_at, created_by)
SELECT 28, 2, 'COM', 'COMPRAS', 'Diario para compras a proveedor', NULL, 0, true, false, false, now(), 'fase_c_2026-08-20'
WHERE NOT EXISTS (SELECT 1 FROM con_diario WHERE company_id = 2 AND journal_id = 28);

-- ---------------------------------------------------------------------------
-- C.1b  Tipos de transacción
-- ---------------------------------------------------------------------------
INSERT INTO con_tipo_transaccion
    (type_id, company_id, code, name, description, category, is_automatic,
     allows_cost_center, allows_third_party, status, type_trans, type_oper, frequency,
     max_entries, allows_cash_flow, allows_account_limit, is_default, status_id,
     document_sequence_start, last_document_number, created_at, created_by)
SELECT 54, 2, 'INV', 'INVENTARIOS', NULL, 'GENERAL', true,
       false, false, 'ACTIVE', 0, 0, 0, 0, false, false, false, 1, 1, 0, now(), 'fase_c_2026-08-20'
WHERE NOT EXISTS (SELECT 1 FROM con_tipo_transaccion WHERE company_id = 2 AND type_id = 54);

INSERT INTO con_tipo_transaccion
    (type_id, company_id, code, name, description, category, is_automatic,
     allows_cost_center, allows_third_party, status, type_trans, type_oper, frequency,
     max_entries, allows_cash_flow, allows_account_limit, is_default, status_id,
     document_sequence_start, last_document_number, created_at, created_by)
SELECT 55, 2, 'COM', 'COMPRAS', 'Para transacciones de compras a proveedor', 'GENERAL', true,
       false, false, 'ACTIVE', 0, 0, 0, 0, false, false, false, 1, 1, 0, now(), 'fase_c_2026-08-20'
WHERE NOT EXISTS (SELECT 1 FROM con_tipo_transaccion WHERE company_id = 2 AND type_id = 55);

-- ---------------------------------------------------------------------------
-- C.1c  Asientos de integración  (FK a los dos bloques anteriores)
-- ---------------------------------------------------------------------------
INSERT INTO con_integracion_asiento
    (integracion_asiento_id, company_id, journal_id, module, type_id, created_at, created_by)
SELECT 801, 2, 18, 'ALMACEN', 54, now(), 'fase_c_2026-08-20'
WHERE NOT EXISTS (SELECT 1 FROM con_integracion_asiento WHERE company_id = 2 AND module = 'ALMACEN');

INSERT INTO con_integracion_asiento
    (integracion_asiento_id, company_id, journal_id, module, type_id, created_at, created_by)
SELECT 851, 2, 28, 'COMPRAS', 55, now(), 'fase_c_2026-08-20'
WHERE NOT EXISTS (SELECT 1 FROM con_integracion_asiento WHERE company_id = 2 AND module = 'COMPRAS');

-- ---------------------------------------------------------------------------
-- C.2  Cuenta de inventario de cada tipo de artículo
--
--      Mapeo explícito (auditable por el contador). Cada destino se verifica
--      antes de aplicar: debe existir en con_plan_cuentas y permitir posteo.
-- ---------------------------------------------------------------------------
DO $$
DECLARE
    v_faltan text;
BEGIN
    SELECT string_agg(DISTINCT m.cuenta, ', ')
      INTO v_faltan
      FROM (VALUES ('11401010000'), ('11401020000'), ('11409000000')) AS m(cuenta)
     WHERE NOT EXISTS (
             SELECT 1 FROM con_plan_cuentas c
              WHERE c.company_id = 2 AND c.code = m.cuenta AND c.allows_posting = true);

    IF v_faltan IS NOT NULL THEN
        RAISE EXCEPTION 'Cuentas destino inexistentes o que no permiten posteo: %. Revisar el plan de cuentas antes de continuar.', v_faltan;
    END IF;
END $$;

UPDATE alm_tipo_articulo t
   SET cuenta_inventario   = m.cuenta,
       usuariomodificacion = 'fase_c_2026-08-20',
       fechamodificacion   = now()
  FROM (VALUES
            ('01', '11401010000'),   -- Materiales y accesorios agua potable
            ('02', '11401010000'),   -- Materiales y accesorios alcantarillado
            ('03', '11401010000'),   -- Útiles y accesorios de oficina
            ('04', '11409000000'),   -- Herramientas menores      (rama 11409: solo nivel 4)
            ('05', '11401010000'),   -- Medidores y válvulas en consignación
            ('06', '11409000000'),   -- Material eléctrico         (rama 11409: solo nivel 4)
            ('07', '11401020000'),   -- Producto químico
            ('08', '11409000000'),   -- Producto municipal         (rama 11409: solo nivel 4)
            ('09', '11401010000')    -- Materiales de polietileno potable
       ) AS m(codigo, cuenta)
 WHERE t.company_id = 2
   AND t.codigo     = m.codigo
   AND t.cuenta_inventario IS DISTINCT FROM m.cuenta;

-- ---------------------------------------------------------------------------
-- Verificación
-- ---------------------------------------------------------------------------
DO $$
DECLARE
    v_asientos int;
    v_rotas    int;
BEGIN
    SELECT count(*) INTO v_asientos
      FROM con_integracion_asiento WHERE company_id = 2 AND module IN ('ALMACEN','COMPRAS');

    SELECT count(*) INTO v_rotas
      FROM alm_tipo_articulo t
     WHERE t.company_id = 2 AND t.cuenta_inventario IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM con_plan_cuentas c
                        WHERE c.company_id = 2 AND c.code = t.cuenta_inventario
                          AND c.allows_posting = true);

    RAISE NOTICE 'Asientos ALMACEN/COMPRAS: % de 2.', v_asientos;
    RAISE NOTICE 'Tipos de articulo con cuenta invalida: % (esperado 0).', v_rotas;

    IF v_asientos <> 2 OR v_rotas <> 0 THEN
        RAISE EXCEPTION 'Verificacion fallida: asientos=% rotas=%. Se revierte.', v_asientos, v_rotas;
    END IF;
END $$;

COMMIT;

-- =============================================================================
-- ROLLBACK (si hiciera falta revertir):
--
--   BEGIN;
--   DELETE FROM con_integracion_asiento WHERE company_id=2 AND integracion_asiento_id IN (801,851);
--   DELETE FROM con_tipo_transaccion    WHERE company_id=2 AND type_id    IN (54,55);
--   DELETE FROM con_diario              WHERE company_id=2 AND journal_id IN (18,28);
--   -- las cuentas de alm_tipo_articulo quedaban antes con guiones y NO existian
--   -- en el plan: revertirlas volveria a dejar la configuracion rota.
--   COMMIT;
-- =============================================================================
