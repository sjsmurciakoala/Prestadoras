-- =============================================================================
-- Migración Proveedores SIMAFI · Script 00/3 : DDL (staging + ajuste de destino)
--
-- Origen: MySQL bdsimafi @ 172.16.0.3.  Destino: prv_* en siad_v3 (company 2).
-- Ver Prestadoras/docs/MIGRACION_PROVEEDORES_SIMAFI_MAPEO_2026-07-10.md
--
-- Idempotente. Aplicar en SRV y mirror antes del 01_landing.
-- =============================================================================
BEGIN;

-- ---------------------------------------------------------------------------
-- 1) Staging fiel del origen (todo texto salvo montos/fechas).
--    Se conservan también las columnas sin destino (codigo2, contableant,
--    representantel, observa) para no perder información.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.stg_simafi_proveedor (
    codigo          text,
    proveedor       text,
    direccion       text,
    rtn             text,
    telefono        text,
    fecha           date,
    razonsocial     text,
    email           text,
    fax             text,
    contable        text,
    codigo2         text,
    representantel  text,
    solvencia       text,
    percontacto     text,
    paginaweb       text,
    observa         text,
    prodsumi        text,
    dirfoto1        text,
    dirfoto2        text,
    tipoprov        text,
    descritipoprov  text,
    pais            text,
    ciudad          text,
    contableant     text
);

CREATE TABLE IF NOT EXISTS public.stg_simafi_ordenesp (
    ano         text,
    codpro      text,
    codact      text,
    renglon     text,
    codigo      text,
    beneficiar  text,
    valorp      numeric(14,2),
    ordenp      text,
    ordenc      text,
    vou         text,
    cuentac     text,
    debe        numeric(14,2),
    haber       numeric(14,2),
    codproy     text,
    fechap      date,
    docu        text,
    concepto    text,
    fecha       date,
    referencia  text,
    fondo       text,
    contable    text
);

CREATE INDEX IF NOT EXISTS ix_stg_simafi_ordenesp_ordenp  ON public.stg_simafi_ordenesp (ordenp);
CREATE INDEX IF NOT EXISTS ix_stg_simafi_proveedor_codigo ON public.stg_simafi_proveedor (codigo);

-- ---------------------------------------------------------------------------
-- 2) Destino: ampliar `concepto` a varchar(500).
--    En el origen llega hasta 435 caracteres; 704 de las 4,297 órdenes de la
--    ventana exceden los 150 actuales. Decisión del usuario (D8): ampliar,
--    no truncar.  Idempotente: solo actúa si aún está corto.
-- ---------------------------------------------------------------------------
DO $$
DECLARE largo int;
BEGIN
    SELECT character_maximum_length INTO largo
    FROM information_schema.columns
    WHERE table_schema='public' AND table_name='prv_compromiso_hdr' AND column_name='concepto';

    IF largo IS NULL THEN
        RAISE EXCEPTION 'No existe prv_compromiso_hdr.concepto';
    ELSIF largo < 500 THEN
        ALTER TABLE public.prv_compromiso_hdr ALTER COLUMN concepto TYPE varchar(500);
        RAISE NOTICE 'prv_compromiso_hdr.concepto ampliado de varchar(%) a varchar(500)', largo;
    ELSE
        RAISE NOTICE 'prv_compromiso_hdr.concepto ya es varchar(%), sin cambios', largo;
    END IF;
END $$;

COMMIT;
