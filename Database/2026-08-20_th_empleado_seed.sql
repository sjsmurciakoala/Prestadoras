-- =============================================================================
-- Talento Humano: semilla del catálogo de empleados (th_empleado)
-- Fecha: 2026-08-20
-- Regla DB Mirror: el mirror local (siad_v3_restore) YA tiene estos datos —
--                  este script es para llevarlos al servidor.
--
-- Base objetivo: siad_v4 @ 172.16.0.9  (la base ACTIVA de producción; NO siad_v3)
--
-- Los 34 empleados se capturaron por pantalla en local y no existían en ningún
-- script. Esta semilla los reproduce tal cual, con su texto de cargo y
-- departamento, para que el script de catálogos pueda derivarlos.
--
-- -----------------------------------------------------------------------------
-- ⚠️ ORDEN OBLIGATORIO — los tres pasos de la Fase A, en este orden:
--
--   1. 2026-08-19_th_empleado.sql            (crea la tabla, vacía)
--   2. 2026-08-20_th_empleado_seed.sql       (ESTE — siembra los 34 empleados)
--   3. 2026-08-19_th_cargo_departamento.sql  (deriva th_cargo/th_departamento
--                                             del texto y enlaza los _id)
--
-- El nombre del paso 3 es ANTERIOR por fecha, pero va DE ÚLTIMO. Si se corre
-- antes de esta semilla, los catálogos quedan vacíos y los empleados sin
-- cargo_id/departamento_id. Este script detecta ese caso y lo avisa al final.
-- -----------------------------------------------------------------------------
--
-- ADITIVO / bajo riesgo: solo INSERT sobre una tabla del propio módulo. No toca
-- ninguna tabla existente. Nada externo referencia th_empleado (verificado).
--
-- IDEMPOTENTE: INSERT ... ON CONFLICT (company_id, codigo) DO NOTHING.
-- Repetirlo no duplica ni pisa cambios hechos arriba.
--
-- ¿YA APLICADO?
--   SELECT count(*) FROM th_empleado WHERE company_id = 2;   -- esperado: 34
--
-- REVERSIBLE:
--   DELETE FROM th_empleado WHERE company_id = 2;            -- (borra TODOS)
-- =============================================================================
BEGIN;

-- Guardia: la tabla tiene que existir (paso 1 antes que este).
DO $$
BEGIN
    IF to_regclass('public.th_empleado') IS NULL THEN
        RAISE EXCEPTION 'Falta th_empleado. Corré primero 2026-08-19_th_empleado.sql (paso 1).';
    END IF;
END $$;

INSERT INTO th_empleado
    (company_id, codigo, codigo_simafi, nombre, identidad, cargo, departamento, activo, usuariocreacion)
VALUES
    (2, '0001', NULL, 'Juan Carlos Pérez', '0801-1995-04567', 'Bodeguero', 'Almacén', true, 'admin@siad-demo.com'),
    (2, '0002', NULL, 'María Elena Rodríguez', '0501-1988-11223', 'Asistente contable', 'Contabilidad', true, 'admin@siad-demo.com'),
    (2, '0003', 'SIMAFI-1001', 'Carlos Alberto Mejía', '1201-1999-69369', 'Bodeguero', 'Almacén', true, 'admin@siad-demo.com'),
    (2, '0004', 'SIMAFI-1002', 'Ana Lucía Fuentes', '0801-1996-55281', 'Vendedor', 'Ventas', true, 'admin@siad-demo.com'),
    (2, '0005', 'SIMAFI-1003', 'José Manuel Cárcamo', '0101-1977-16423', 'Cajero', 'Contabilidad', true, 'admin@siad-demo.com'),
    (2, '0006', 'SIMAFI-1004', 'Sandra Patricia Rivera', '0501-1976-32805', 'Motorista', 'Transporte', true, 'admin@siad-demo.com'),
    (2, '0007', 'SIMAFI-1005', 'Luis Fernando Zelaya', '0101-1999-83816', 'Contador', 'Administración', true, 'admin@siad-demo.com'),
    (2, '0008', 'SIMAFI-1006', 'Karla Yolanda Discua', '0501-1998-99088', 'Auxiliar contable', 'Compras', true, 'admin@siad-demo.com'),
    (2, '0009', 'SIMAFI-1007', 'Óscar René Munguía', '1201-1992-80626', 'Supervisor', 'Cobranza', true, 'admin@siad-demo.com'),
    (2, '0010', 'SIMAFI-1008', 'Gabriela Isabel Paz', '1201-1988-53055', 'Gerente de tienda', 'Recursos Humanos', true, 'admin@siad-demo.com'),
    (2, '0011', 'SIMAFI-1009', 'Marvin Josué Andino', '0101-1986-29680', 'Asistente administrativo', 'Sistemas', true, 'admin@siad-demo.com'),
    (2, '0012', 'SIMAFI-1010', 'Wendy Carolina Lagos', '0501-1992-57708', 'Recursos humanos', 'Gerencia', true, 'admin@siad-demo.com'),
    (2, '0013', 'SIMAFI-1011', 'Elmer David Cruz', '0501-1995-17078', 'Comprador', 'Almacén', false, 'admin@siad-demo.com'),
    (2, '0014', 'SIMAFI-1012', 'Dilcia María Herrera', '0801-1983-57498', 'Facturador', 'Ventas', true, 'admin@siad-demo.com'),
    (2, '0015', 'SIMAFI-1013', 'Roberto Antonio Bonilla', '0301-1997-24811', 'Cobrador', 'Contabilidad', true, 'admin@siad-demo.com'),
    (2, '0016', 'SIMAFI-1014', 'Iris Nohemí Castellanos', '0801-1977-36539', 'Vigilante', 'Transporte', true, 'admin@siad-demo.com'),
    (2, '0017', 'SIMAFI-1015', 'Franklin Omar Salgado', '0101-1981-93207', 'Conserje', 'Administración', true, 'admin@siad-demo.com'),
    (2, '0018', 'SIMAFI-1016', 'Yensi Adaluz Maradiaga', '0501-1977-59953', 'Analista de sistemas', 'Compras', true, 'admin@siad-demo.com'),
    (2, '0019', 'SIMAFI-1017', 'Héctor Orlando Reyes', '1201-1998-67140', 'Bodeguero', 'Cobranza', true, 'admin@siad-demo.com'),
    (2, '0020', 'SIMAFI-1018', 'Suyapa del Carmen Flores', '0501-1982-29290', 'Vendedor', 'Recursos Humanos', true, 'admin@siad-demo.com'),
    (2, '0021', 'SIMAFI-1019', 'Nelson Geovanny Padilla', '0501-1988-34711', 'Cajero', 'Sistemas', true, 'admin@siad-demo.com'),
    (2, '0022', 'SIMAFI-1020', 'Claudia Vanessa Ortega', '1201-1977-59467', 'Motorista', 'Gerencia', true, 'admin@siad-demo.com'),
    (2, '0023', 'SIMAFI-1021', 'Edwin Alexander Cálix', '0101-2002-46316', 'Contador', 'Almacén', true, 'admin@siad-demo.com'),
    (2, '0024', 'SIMAFI-1022', 'Rosa Amelia Interiano', '0301-1994-70235', 'Auxiliar contable', 'Ventas', false, 'admin@siad-demo.com'),
    (2, '0025', 'SIMAFI-1023', 'Jorge Luis Banegas', '0301-2001-44745', 'Supervisor', 'Contabilidad', true, 'admin@siad-demo.com'),
    (2, '0026', 'SIMAFI-1024', 'Mirna Elizabeth Aguilar', '0301-1981-84693', 'Gerente de tienda', 'Transporte', true, 'admin@siad-demo.com'),
    (2, '0027', 'SIMAFI-1025', 'Denis Rolando Sabillón', '0301-1976-38987', 'Asistente administrativo', 'Administración', true, 'admin@siad-demo.com'),
    (2, '0028', 'SIMAFI-1026', 'Fanny Julissa Espinoza', '0301-1979-57524', 'Recursos humanos', 'Compras', true, 'admin@siad-demo.com'),
    (2, '0029', 'SIMAFI-1027', 'Walter Noé Guevara', '0501-1981-83723', 'Comprador', 'Cobranza', true, 'admin@siad-demo.com'),
    (2, '0030', 'SIMAFI-1028', 'Lesly Marcela Turcios', '0501-1984-82507', 'Facturador', 'Recursos Humanos', true, 'admin@siad-demo.com'),
    (2, '0031', 'SIMAFI-1029', 'Byron Alcides Varela', '0501-1979-28526', 'Cobrador', 'Sistemas', true, 'admin@siad-demo.com'),
    (2, '0032', 'SIMAFI-1030', 'Glenda Patricia Núñez', '1201-1998-90532', 'Vigilante', 'Gerencia', true, 'admin@siad-demo.com'),
    (2, '0033', 'SIMAFI-1031', 'Selvin Adonay Cárcamo', '0501-1990-15511', 'Conserje', 'Almacén', true, 'admin@siad-demo.com'),
    (2, '0034', 'SIMAFI-1032', 'Delmy Suyapa Ramos', '0801-1988-69735', 'Analista de sistemas', 'Ventas', true, 'admin@siad-demo.com')
ON CONFLICT (company_id, codigo) DO NOTHING;

-- Verificación + aviso sobre el paso 3.
DO $$
DECLARE
    v_total    bigint;
    v_sin_id   bigint;
BEGIN
    SELECT count(*) INTO v_total FROM th_empleado WHERE company_id = 2;
    RAISE NOTICE 'th_empleado (company_id=2): % filas.', v_total;

    IF to_regclass('public.th_cargo') IS NOT NULL THEN
        SELECT count(*) INTO v_sin_id
          FROM th_empleado
         WHERE company_id = 2 AND (cargo_id IS NULL OR departamento_id IS NULL);
        IF v_sin_id > 0 THEN
            RAISE NOTICE 'ATENCION: % empleados sin cargo_id/departamento_id. El paso 3 ya corrio antes que esta semilla: volve a correr 2026-08-19_th_cargo_departamento.sql para enlazarlos.', v_sin_id;
        END IF;
    ELSE
        RAISE NOTICE 'Siguiente: correr 2026-08-19_th_cargo_departamento.sql (paso 3) para crear los catalogos y enlazar los _id.';
    END IF;
END $$;

COMMIT;
