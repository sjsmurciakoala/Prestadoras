-- =============================================================================
-- Activos Fijos F1 — semilla de arranque de los catálogos
-- Fecha: 2026-09-08
-- Depende de: 2026-09-08_af_activos_fijos_f1_registro.sql
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost)
--
-- QUÉ HACE
--   Llena af_tipo_activo y af_ubicacion, que el script de estructura deja vacíos
--   a propósito. Sin al menos un tipo no se puede registrar ningún activo, porque
--   de ahí salen la vida útil, el método y las cuentas contables.
--
--   Los siete tipos siguen las clases de Propiedad, Planta y Equipo del plan de
--   cuentas ERSAPS que la empresa ya tiene cargado (grupos 12302 a 12308), y sus
--   cuentas son CÓDIGOS REALES de ese plan, todos verificados como cuentas de
--   detalle que admiten movimiento:
--
--     clase                       activo        deprec. acum.   gasto
--     Edificios                   12302010501   12309010501     62104000000
--     Instalaciones               12303010501   12309020501     62104000000
--     Equipos de oficina          12304010501   12309030501     62104010101
--     Equipos de transporte       12305010501   12309040501     62104010102
--     Equipos de producción       12306010501   12309050501     62104010104
--     Equipos de comunicaciones   12307010501   12309060501     62104010103
--     Equipos de informática      12308010501   12309070501     62104000000
--
--   Se eligió la rama "Comunes / Adquirido con Recursos Propios" por ser la más
--   neutra. Un activo donado o transferido, o uno que pertenezca solo a Agua
--   Potable o a Alcantarillado, va a otra hoja del mismo grupo: eso se ajusta por
--   tipo o activo por activo desde la pantalla.
--
-- ⚠️ ESTOS VALORES SON UN PUNTO DE PARTIDA, NO UNA DEFINICIÓN CONTABLE.
--   Las vidas útiles y los porcentajes residuales son los usuales del sector y
--   deben validarse con el contador antes de depreciar (F2). Cambiarlos desde
--   /activos-fijos/tipos NO recalcula los activos ya registrados.
--
-- ADITIVO: solo INSERT. No borra ni actualiza nada.
-- IDEMPOTENTE: ON CONFLICT DO NOTHING sobre (company_id, codigo). Correrlo dos
--   veces no duplica ni pisa lo que el usuario haya editado a mano.
-- =============================================================================

\set COMPANY 2

BEGIN;

-- ── 1. Tipos de activo ───────────────────────────────────────────────────────
INSERT INTO af_tipo_activo
    (company_id, codigo, nombre, descripcion, prefijo_codigo, vida_util_anios,
     metodo_depreciacion_id, porcentaje_residual,
     cuenta_activo, cuenta_depreciacion_acumulada, cuenta_gasto_depreciacion,
     cuenta_perdida_baja, activo, usuariocreacion)
VALUES
    (:COMPANY, 'EDIF', 'Edificios',
     'Edificaciones e inmuebles de la prestadora.', 'EDIF', 40.0, 1, 0,
     '12302010501', '12309010501', '62104000000', '62105000000', true, 'semilla'),

    (:COMPANY, 'INST', 'Instalaciones',
     'Redes, tanques, pozos y obra civil del sistema.', 'INST', 20.0, 1, 0,
     '12303010501', '12309020501', '62104000000', '62105000000', true, 'semilla'),

    (:COMPANY, 'MOB', 'Equipos de oficina',
     'Mobiliario y equipo de oficina.', 'MOB', 10.0, 1, 0,
     '12304010501', '12309030501', '62104010101', '62105000000', true, 'semilla'),

    (:COMPANY, 'VEH', 'Equipos de transporte',
     'Vehículos livianos y pesados de la flota.', 'VEH', 5.0, 1, 10.00,
     '12305010501', '12309040501', '62104010102', '62105000000', true, 'semilla'),

    (:COMPANY, 'MAQ', 'Equipos de producción',
     'Maquinaria y equipo de captación, bombeo y tratamiento.', 'MAQ', 10.0, 1, 0,
     '12306010501', '12309050501', '62104010104', '62105000000', true, 'semilla'),

    (:COMPANY, 'COMU', 'Equipos de comunicaciones',
     'Radios, telefonía y equipo de telemetría.', 'COMU', 5.0, 1, 0,
     '12307010501', '12309060501', '62104010103', '62105000000', true, 'semilla'),

    (:COMPANY, 'INFO', 'Equipos de informática',
     'Computadoras, servidores, impresoras y redes de datos.', 'INFO', 3.0, 1, 0,
     '12308010501', '12309070501', '62104000000', '62105000000', true, 'semilla')
ON CONFLICT (company_id, codigo) DO NOTHING;

-- ── 2. Ubicaciones, nivel raíz ───────────────────────────────────────────────
INSERT INTO af_ubicacion (company_id, codigo, nombre, padre_id, direccion, activo, usuariocreacion)
VALUES
    (:COMPANY, 'OFC',  'Oficina central',           NULL, 'Puerto Cortés', true, 'semilla'),
    (:COMPANY, 'BOD',  'Bodega principal',          NULL, NULL,            true, 'semilla'),
    (:COMPANY, 'SAP',  'Sistema de agua potable',   NULL, NULL,            true, 'semilla'),
    (:COMPANY, 'SAL',  'Sistema de alcantarillado', NULL, NULL,            true, 'semilla')
ON CONFLICT (company_id, codigo) DO NOTHING;

-- ── 3. Ubicaciones hijas ─────────────────────────────────────────────────────
-- El padre se resuelve por código, no por id: el SERIAL no es estable entre bases.
INSERT INTO af_ubicacion (company_id, codigo, nombre, padre_id, activo, usuariocreacion)
SELECT :COMPANY, h.codigo, h.nombre, p.id, true, 'semilla'
  FROM (VALUES
        ('OFC-PB',   'Planta baja',                            'OFC'),
        ('OFC-PA',   'Planta alta',                            'OFC'),
        ('OFC-CAJA', 'Área de caja y atención al cliente',     'OFC'),
        ('SAP-PTAP', 'Planta potabilizadora',                  'SAP'),
        ('SAP-EB',   'Estación de bombeo',                     'SAP'),
        ('SAP-TQ',   'Tanque de almacenamiento',               'SAP'),
        ('SAL-PTAR', 'Planta de tratamiento de aguas residuales', 'SAL')
       ) AS h(codigo, nombre, codigo_padre)
  JOIN af_ubicacion p ON p.company_id = :COMPANY AND p.codigo = h.codigo_padre
ON CONFLICT (company_id, codigo) DO NOTHING;

COMMIT;

-- Verificación
--   SELECT codigo, nombre, vida_util_anios, porcentaje_residual, cuenta_activo
--     FROM public.fn_af_tipo_activo_listar(2, true, NULL) ORDER BY codigo;
--   SELECT codigo, ruta FROM public.fn_af_ubicacion_listar(2, true, NULL);
