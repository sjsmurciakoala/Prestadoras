-- =============================================================================
-- Catálogo de tipos de movimiento de almacén (alm_tipo_movimiento)
-- Fecha: 2026-08-01
-- Regla DB Mirror: aplicar en siad_v3_restore (localhost) antes que en el SRV.
--
-- POR QUÉ
--   Fase 1 del diseño docs/plans/2026-08-01-movimientos-almacen-catalogo-diseno.md.
--   El portal hoy sólo sabe mover stock con el enum compilado TipoMovimientoInventario;
--   agregar "merma por vencimiento", "donación" o "consumo interno" exige recompilar y
--   migrar un CHECK. Este catálogo es el equivalente sano de INV_TIPOSTRANSACC de Centura:
--   el usuario da de alta tipos de negocio sin tocar código, cada uno con su cuenta
--   contable y su bandera de autorización, mientras el motor de posteo (que ejecuta la
--   CLASE, no el tipo) no cambia una sola línea.
--
--   Dos capas (§2 del diseño):
--     · CLASE  (ENTRADA | SALIDA | VALOR) — semántica que el motor sabe ejecutar. Es el
--       mismo vocabulario de ClaseAjusteInventario. Sólo cambia con código + tests.
--     · TIPO   (esta tabla)               — nombre de negocio, configurable por el usuario.
--
-- 100% ADITIVO. Una tabla nueva y su semilla; no altera ni borra nada existente.
-- IDEMPOTENTE (CREATE TABLE IF NOT EXISTS / DO blocks guardados / INSERT ON CONFLICT).
-- REVERSIBLE (bloque de rollback al final).
-- MULTIEMPRESA real: una fila por (company_id, codigo); cada empresa edita el suyo sin
--   afectar a las demás.
-- =============================================================================
BEGIN;

-- -----------------------------------------------------------------------------
-- 1) La tabla
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.alm_tipo_movimiento (
    id                    SERIAL        PRIMARY KEY,
    company_id            BIGINT        NOT NULL,
    codigo                VARCHAR(20)   NOT NULL,             -- estable: referencias, kardex
    nombre                VARCHAR(80)   NOT NULL,             -- editable: se puede retocar
    clase                 VARCHAR(10)   NOT NULL,             -- ENTRADA | SALIDA | VALOR
    requiere_autorizacion BOOLEAN       NOT NULL DEFAULT false,
    cuenta_contable       VARCHAR(20)   NULL,                 -- override; NULL = hereda del tipo de artículo
    activo                BOOLEAN       NOT NULL DEFAULT true, -- se desactiva, NUNCA se borra
    orden                 SMALLINT      NOT NULL DEFAULT 0,   -- orden de despliegue en el combo
    usuariocreacion       VARCHAR(100)  NULL,
    fechacreacion         TIMESTAMP WITHOUT TIME ZONE NULL DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion   VARCHAR(100)  NULL,
    fechamodificacion     TIMESTAMP WITHOUT TIME ZONE NULL,

    CONSTRAINT uq_alm_tipo_movimiento_codigo UNIQUE (company_id, codigo),
    -- Clave alterna tenant-safe: la Fase 2 (alm_movimiento_hdr) declara su FK compuesta
    -- (company_id, tipo_movimiento_id) contra ésta, no REFERENCES ...(id) a secas.
    CONSTRAINT uq_alm_tipo_movimiento_tenant UNIQUE (company_id, id),
    CONSTRAINT ck_alm_tipo_movimiento_clase  CHECK (clase IN ('ENTRADA','SALIDA','VALOR')),
    CONSTRAINT ck_alm_tipo_movimiento_codigo_vacio CHECK (length(btrim(codigo)) > 0),
    CONSTRAINT ck_alm_tipo_movimiento_nombre_vacio CHECK (length(btrim(nombre)) > 0)
);

CREATE INDEX IF NOT EXISTS ix_alm_tipo_movimiento_company
    ON public.alm_tipo_movimiento (company_id);

COMMENT ON TABLE public.alm_tipo_movimiento IS
 'Catálogo de tipos de movimiento de almacén, configurable por el usuario (equivalente sano de INV_TIPOSTRANSACC de Centura). El nombre es de negocio; la lógica de inventario la gobierna la CLASE (ENTRADA/SALIDA/VALOR), que el motor de posteo ejecuta sin cambios. Multiempresa: una fila por (company_id, codigo).';
COMMENT ON COLUMN public.alm_tipo_movimiento.clase IS
 'ENTRADA suma existencia y recalcula costo promedio · SALIDA resta al promedio vigente sin moverlo · VALOR corrige sólo el costo (cantidad 0). Espejo de ClaseAjusteInventario. NO editable una vez que el tipo tiene movimientos posteados (lo garantiza el servicio).';
COMMENT ON COLUMN public.alm_tipo_movimiento.cuenta_contable IS
 'Override de la cuenta contable del asiento. NULL = el asiento hereda la cuenta del tipo de artículo (alm_tipo_articulo). Sólo se llena cuando el tipo de movimiento exige una cuenta distinta (p. ej. Donación contra una cuenta de gasto).';
COMMENT ON COLUMN public.alm_tipo_movimiento.requiere_autorizacion IS
 'true = usar este tipo en un documento exige el permiso module.inventario.movimientos.autorizar_sensibles (se valida en el servicio, Fase 2). No bifurca un flujo de aprobación.';
COMMENT ON COLUMN public.alm_tipo_movimiento.activo IS
 'false = oculto del combo de captura y rechazado en documentos nuevos, pero el histórico lo sigue resolviendo. Un tipo con movimientos posteados no se puede borrar; se desactiva.';

-- -----------------------------------------------------------------------------
-- 2) Retiro de la semilla genérica anterior (2026-08-01 → 2026-08-03)
--
--    La primera versión de este script sembraba 3 tipos inventados a partir de
--    ClaseAjusteInventario (SOBRANTE_CONTEO / MERMA / CORRECCION_COSTO). NO salían
--    de ningún dato real. Se reemplazan por los tipos reales del legacy (bloque 3).
--
--    ⚠️ DESTRUCTIVO pero acotado: sólo borra esos 3 códigos, sólo si NINGÚN documento
--    los referencia. Cuando exista alm_movimiento_dtl (Fase 2) el guard lo comprueba;
--    mientras no exista, no hay nada que pueda referenciarlos.
--    Aprobado por el usuario el 2026-08-03 (paridad estricta con Centura: se acepta
--    perder la única clase VALOR, que Centura no tiene).
-- -----------------------------------------------------------------------------
DO $$
DECLARE
    v_en_uso  bigint := 0;
    v_borradas bigint;
BEGIN
    IF to_regclass('public.alm_movimiento_dtl') IS NOT NULL THEN
        EXECUTE $q$
            SELECT count(*) FROM public.alm_movimiento_dtl d
            JOIN public.alm_movimiento_hdr h ON h.id = d.movimiento_hdr_id
            JOIN public.alm_tipo_movimiento t ON t.id = h.tipo_movimiento_id
            WHERE t.codigo IN ('SOBRANTE_CONTEO','MERMA','CORRECCION_COSTO')
        $q$ INTO v_en_uso;
    END IF;

    IF v_en_uso > 0 THEN
        RAISE EXCEPTION 'No se retira la semilla vieja: % renglones ya la usan. Desactivar en vez de borrar.', v_en_uso;
    END IF;

    DELETE FROM public.alm_tipo_movimiento
     WHERE codigo IN ('SOBRANTE_CONTEO','MERMA','CORRECCION_COSTO');
    GET DIAGNOSTICS v_borradas = ROW_COUNT;
    RAISE NOTICE 'Semilla genérica retirada: % filas.', v_borradas;
END $$;

-- -----------------------------------------------------------------------------
-- 3) Semilla real: importada de dbo.INV_TIPOSTRANSACC de MERENDON (SQL Server)
--    el 2026-08-03. Son las 12 filas del catálogo del legacy, verbatim en código y
--    nombre. Se siembra por cada empresa con bodegas.
--
--    MAPEO APLICADO
--      · ENTRA_SALE 'E' → clase ENTRADA · 'S' → clase SALIDA. Centura no tiene
--        equivalente de la clase VALOR (su motor no sabe corregir sólo el costo).
--      · CAMBIA_COSTO NO se importa: en el legacy no vive por tipo sino en
--        INV_TRANSACC_AXL por (AREA_AFECTADA, ENTRA_SALE), y su contenido real es
--        uniforme — toda entrada cambia costo, ninguna salida lo cambia. Eso es
--        exactamente lo que ya hace el motor de posteo, así que no hace falta columna.
--      · CUENTA_CONTABLE NO se importa: las 7 cuentas del legacy (199100, 115010101,
--        1105010100, 641135, 114405, 114305, 1100501) son de Merendón y NINGUNA existe
--        en con_plan_cuentas de SIAD (verificado 2026-08-03). Importarlas dejaría
--        referencias colgadas. Quedan NULL = hereda del tipo de artículo (lo diseñado).
--        Se documentan aquí por trazabilidad, en la columna de comentario de cada fila.
--      · CORRELATIVO no se importa: la numeración de SIAD vive en su propia tabla.
--      · AREA_AFECTADA no se importa (SIAD no la necesita), pero determinó el `activo`:
--        'D' = interno → activo · 'C' = clientes y 'P' = proveedores → inactivo, porque
--        en SIAD esos movimientos los postea automáticamente su propio flujo
--        (facturación, recepción de compra) y no deben capturarse a mano.
--      · TFE/TFS entran INACTIVOS pese a ser los más usados del histórico (25.434 y
--        37.165 asientos): necesitan la clase TRASLADO, que es la Fase 5. Activarlos
--        hoy permitiría registrar media transferencia.
--
--    ON CONFLICT DO NOTHING: re-ejecutable y respeta lo que el usuario ya haya editado.
-- -----------------------------------------------------------------------------
INSERT INTO public.alm_tipo_movimiento
       (company_id, codigo, nombre, clase, activo, orden, usuariocreacion, fechacreacion)
SELECT c.company_id, s.codigo, s.nombre, s.clase, s.activo, s.orden, 'seed-centura', (now() AT TIME ZONE 'utc')
FROM   (SELECT DISTINCT company_id FROM public.alm_bodega) c
CROSS JOIN (VALUES
    -- código, nombre (verbatim del legacy), clase, activo, orden   -- área · cuenta legacy · asientos en INV_KARDEX
    ('AIE', 'AJUSTE DE INVENTARIO -- ENTRADA', 'ENTRADA', true,   1::smallint),  -- D · 115010101  ·    942
    ('AIS', 'AJUSTE DE INVENTARIO -- SALIDA',  'SALIDA',  true,   2::smallint),  -- D · 115010101  ·  1.667
    ('NPG', 'PUBLICIDAD',                      'SALIDA',  true,   3::smallint),  -- D · 641135     ·    182
    ('APL', 'APLICACION DE REQUISICIONES',     'SALIDA',  true,   4::smallint),  -- D · 1100501    ·      0
    ('TFE', 'ENTRADAS POR TRANSFERENCIA',      'ENTRADA', false,  5::smallint),  -- D · 114405     · 25.434  (Fase 5)
    ('TFS', 'SALIDAS POR TRANSFERENCIA',       'SALIDA',  false,  6::smallint),  -- D · 114305     · 37.165  (Fase 5)
    ('COM', 'COMPRAS A PROVEEDORES',           'ENTRADA', false,  7::smallint),  -- P · 199100     ·    264  (lo postea RecepcionCompraService)
    ('TTR', 'DEVOLUCIONES A PROVEEDORES',      'ENTRADA', false,  8::smallint),  -- P · (null)     ·      0
    ('FAC', 'FACTURACION',                     'SALIDA',  false,  9::smallint),  -- C · 1105010100 · 24.417  (lo postea facturación)
    ('CAN', 'CANCELACION DE FACTURACION',      'ENTRADA', false, 10::smallint),  -- C · 199100     ·    130
    ('DPI', 'DEVOLUCIONES DE PRODUCTO',        'ENTRADA', false, 11::smallint),  -- C · 199100     · 11.731
    ('DEP', 'DEVOLUCIONES A CLIENTES',         'ENTRADA', false, 12::smallint)   -- C · 115010101  ·      0
) AS s(codigo, nombre, clase, activo, orden)
ON CONFLICT (company_id, codigo) DO NOTHING;

COMMIT;

-- =============================================================================
-- VERIFICACIÓN (correr a mano tras aplicar)
-- =============================================================================
-- V1) La tabla y sus constraints existen (esperado: 1 tabla, 3 CHECK, 2 UNIQUE):
-- SELECT conname, contype FROM pg_constraint
--  WHERE conrelid = 'public.alm_tipo_movimiento'::regclass ORDER BY contype, conname;
--
-- V2) La semilla quedó por empresa con bodega (esperado hoy: company_id 2 → 12 filas):
-- SELECT company_id, count(*), string_agg(codigo, ', ' ORDER BY orden)
--   FROM alm_tipo_movimiento GROUP BY company_id ORDER BY company_id;
--   esperado: 2 | 12 | AIE, AIS, NPG, APL, TFE, TFS, COM, TTR, FAC, CAN, DPI, DEP
--
-- V2b) La semilla genérica vieja YA NO existe (esperado: 0 filas):
-- SELECT codigo FROM alm_tipo_movimiento
--  WHERE codigo IN ('SOBRANTE_CONTEO','MERMA','CORRECCION_COSTO');
--
-- V2c) Sólo los 4 internos quedan activos (esperado: AIE, AIS, NPG, APL):
-- SELECT codigo, clase FROM alm_tipo_movimiento WHERE activo ORDER BY orden;
--
-- V3) Sólo hay ENTRADA y SALIDA: Centura no tiene equivalente de la clase VALOR.
--     Esperado por empresa: ENTRADA 7 · SALIDA 5 · VALOR 0.
--     (7 = AIE, TFE, COM, TTR, CAN, DPI, DEP — es el conteo literal de ENTRA_SALE='E'
--      en INV_TIPOSTRANSACC; verificado contra el origen el 2026-08-03.)
-- SELECT clase, count(*) FROM alm_tipo_movimiento GROUP BY clase ORDER BY clase;
--
-- V4) PRUEBA NEGATIVA — una clase inválida debe FALLAR (ck_alm_tipo_movimiento_clase):
-- BEGIN; INSERT INTO alm_tipo_movimiento (company_id, codigo, nombre, clase)
--   VALUES (2, 'ZZ_TEST', 'Prueba', 'TRASLADO'); ROLLBACK;   -- espera 23514
--
-- V5) PRUEBA NEGATIVA — código duplicado en la misma empresa debe FALLAR (uq_..._codigo):
-- BEGIN; INSERT INTO alm_tipo_movimiento (company_id, codigo, nombre, clase)
--   VALUES (2, 'MERMA', 'Otra', 'SALIDA'); ROLLBACK;         -- espera 23505
--
-- V6) Re-ejecutar el script NO duplica la semilla (esperado: mismos conteos que V2):
--   (volver a correr este archivo entero y comparar V2)
-- =============================================================================

-- =============================================================================
-- ROLLBACK
-- =============================================================================
-- BEGIN;
-- DROP TABLE IF EXISTS public.alm_tipo_movimiento;
-- COMMIT;
-- =============================================================================
