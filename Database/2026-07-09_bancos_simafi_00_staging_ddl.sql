-- =============================================================================
-- Migración Bancos SIMAFI  ·  Script 00/4 : STAGING DDL + mapa de bancos
-- Origen MySQL bdsimafi → Postgres siad_v3 (familia ban_*, landing fiel).
-- Crea tablas stg_simafi_* (idempotente) y siembra stg_simafi_banco_map.
-- Aplicar en mirror (localhost/siad_v3_restore) y SRV (172.16.0.9/siad_v3).
-- Ver docs/MIGRACION_BANCOS_SIMAFI_MAPEO_2026-07-09.md
-- =============================================================================
BEGIN;

-- ---------------------------------------------------------------------------
-- 1) Cuentas de cheques (cuentas bancarias)                     ctacheques (22)
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.stg_simafi_ctacheques (
    numero         text,
    cuenta         text,
    banco          text,
    contable       text,
    ncheque        numeric,
    ndepo          numeric,
    ncredito       numeric,
    ndebito        numeric,
    vou            numeric,
    ordenp         numeric,
    activa         int,
    saldobancoant  numeric,
    contableant    text
);

-- ---------------------------------------------------------------------------
-- 2) Saldos por cuenta                                          saldobancos (27)
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.stg_simafi_saldobancos (
    numero         text,
    cuenta         text,
    banco          text,
    activa         int,
    saldobancoant  numeric,
    ingresos       numeric,
    egresos        numeric,
    saldoactual    numeric
);

-- ---------------------------------------------------------------------------
-- 3) Libro banco (detalle partida doble de vouchers CK)       detalleck (14312)
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.stg_simafi_detalleck (
    cuenta         text,   -- cuenta contable (GL), no el nº de cuenta bancaria
    ano            int,
    codgrupo       text,
    csgrupo        text,
    cmayor         text,
    cscuenta       text,
    referencia     text,
    vou            text,
    debe           numeric,
    haber          numeric,
    concepto       text,
    fecha          date,
    docu           text,
    mayoriza       text,
    usuario        text,
    empre          text
);

-- ---------------------------------------------------------------------------
-- 4) Registro de movimientos (cheques/notas)                  maestroche (43615)
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.stg_simafi_maestroche (
    cuenta         text,   -- = ctacheques.numero (char4)
    fecha          date,
    debe           numeric,
    haber          numeric,
    beneficiar     text,
    docu           text,
    concepto       text,
    fechaent       date,
    entregado      text,
    borr           text,
    pos            text,
    mesconc        int,
    ordenp         text,
    fechapos       date,
    id             numeric,
    docud          text
);

-- ---------------------------------------------------------------------------
-- 5) Mapa de normalización de bancos (texto libre → canónico)
--    Curado a mano. El transform hace COALESCE al nombre crudo si no está aquí.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.stg_simafi_banco_map (
    nombre_libre   text PRIMARY KEY,   -- trim() del banco original
    canonico       text NOT NULL       -- nombre canónico para ban_banco.nombre
);

TRUNCATE public.stg_simafi_banco_map;
INSERT INTO public.stg_simafi_banco_map (nombre_libre, canonico) VALUES
  ('BANCO ATLANTIDA',                'BANCO ATLANTIDA'),
  ('CTA. CHEQUES ATLANTIDA',         'BANCO ATLANTIDA'),
  ('CUENTA DE AHORRO BANCO ATLANTI', 'BANCO ATLANTIDA'),
  ('BANCO CUSCATLAN',                'BANCO CUSCATLAN'),
  ('BANCO DE LOS TRABAJADORES',      'BANCO DE LOS TRABAJADORES'),
  ('BANCO DEL PAIS',                 'BANCO DEL PAIS'),
  ('BANCO DEL PAIS S. A.',           'BANCO DEL PAIS'),
  ('BANCO FICOHSA',                  'BANCO FICOHSA'),
  ('BANCO LAFISE',                   'BANCO LAFISE'),
  ('BANCO PROMERICA',                'BANCO PROMERICA'),
  ('CTA. CHEQUES BANCO OCCIDENTE',   'BANCO DE OCCIDENTE'),
  ('BANCO DE OCCIDENTE',             'BANCO DE OCCIDENTE'),
  ('CTA. CHEQUES HSBC',              'BANCO HSBC'),
  ('BANCO LA CONSTANCIA S. A.',      'BANCO LA CONSTANCIA'),
  ('BANCO CENTRAL',                  'BANCO CENTRAL'),
  ('Cuenta transitoria',            'CUENTA TRANSITORIA'),
  ('Cuenta Trnasitoria',            'CUENTA TRANSITORIA'),
  ('Cuneta transitoria',            'CUENTA TRANSITORIA'),
  ('Efecto presupuestario y contab', 'EFECTO PRESUPUESTARIO Y CONTABLE');

COMMIT;
