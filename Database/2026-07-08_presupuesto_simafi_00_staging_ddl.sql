-- =============================================================================
-- Migración Presupuesto SIMAFI (MySQL bdsimafi) -> Postgres siad_v3
-- Script 00 / 4 : DDL de tablas de staging (aditivo; NO toca el modelo vivo)
-- Convención stg_simafi_* ya usada en esta BD (cuenta_map, detalle, etc.).
-- Idempotente: CREATE TABLE IF NOT EXISTS. Sin PKs -> landing tolerante a duplicados.
-- Aplicar en SRV (172.16.0.9/siad_v3) y en mirror (localhost/siad_v3_restore).
-- =============================================================================

BEGIN;

-- --- Hechos (presupuesto base) -------------------------------------------------

-- baseing = presupuesto de INGRESOS (origen: bdsimafi.baseing)
CREATE TABLE IF NOT EXISTS public.stg_simafi_baseing (
    empre           text,
    ano             integer,
    cuenta          text,
    cuenta2         text,
    renglon         text,
    descrip         text,
    aproba          numeric(18,2),
    contable        text,           -- código contable legacy (411-xx); único monto vivo = aproba
    proyeccion      numeric(18,2),
    nuevoaprobado   numeric(18,2),
    ingresoscobrar  numeric(18,2),
    valor           numeric(18,2)
);

-- basepri = presupuesto de EGRESOS / partidas (origen: bdsimafi.basepri)
CREATE TABLE IF NOT EXISTS public.stg_simafi_basepri (
    empre           text,
    ano             integer,
    cuenta          text,
    codigo          text,
    codpro          text,           -- PROGRAMA (no proyecto) -> stg_simafi_programa
    codsubpro       text,           -- muerto (constante '0')
    codact          text,           -- ACTIVIDAD (depende de codpro) -> stg_simafi_actividad
    renglon         text,           -- objeto del gasto -> stg_simafi_renglon
    descrip         text,
    aproba          numeric(18,2),  -- monto principal
    compro          numeric(18,2),  -- muerto (0)
    pagado          numeric(18,2),  -- muerto (0)
    obs             text,           -- muerto (vacío)
    valor           numeric(18,2),  -- muerto (0)
    ampl            numeric(18,2),  -- muerto (0)
    saldo           numeric(18,2),  -- muerto (0)
    mov             integer,        -- muerto (0)
    transfe         numeric(18,2),  -- muerto (0)
    contable        text,           -- código contable legacy (5xx/7xx)
    fondo           text,           -- código suelto 1/2 (sin catálogo en origen)
    proyeccion      numeric(18,2),  -- poblado sólo 2018 y 2025
    nuevoaprobado   numeric(18,2),  -- = proyeccion
    tipo            text            -- muerto (vacío)
);

-- --- Catálogos de clasificación (Opción 1: sólo referencia en staging) ---------

-- Programa presupuestario (origen: bdsimafi.programa) -- codpro
CREATE TABLE IF NOT EXISTS public.stg_simafi_programa (
    empre           text,
    codpro          text,
    descriprogra    text
);

-- Actividad (origen: bdsimafi.actividad) -- depende de (codpro, codact)
CREATE TABLE IF NOT EXISTS public.stg_simafi_actividad (
    empre           text,
    codpro          text,
    codsubpro       text,
    codact          text,
    descriacti      text,
    numeroc         numeric(18,0)
);

-- Renglón objeto del gasto - EGRESOS (origen: bdsimafi.renglon) -- codrenglon único
CREATE TABLE IF NOT EXISTS public.stg_simafi_renglon (
    codgrupo        text,
    codsgrupo       text,
    codrenglon      text,
    desrenglon      text
);

-- Renglón - INGRESOS (origen: bdsimafi.rengloningre)
CREATE TABLE IF NOT EXISTS public.stg_simafi_rengloningre (
    codgrupo        text,
    codsubgrupo     text,
    codigo          text,
    empre           text,
    decripcion      text            -- (sic) nombre de columna tal cual en el origen
);

-- Jerarquía padre del renglón de egresos (origen: bdsimafi.grupop / subgrupop)
CREATE TABLE IF NOT EXISTS public.stg_simafi_grupop (
    codgrupo        text,
    codgrupo2       text,
    desgrupo        text
);

CREATE TABLE IF NOT EXISTS public.stg_simafi_subgrupop (
    codgrupo        text,
    codsgrupo       text,
    codsgrupo2      text,
    desubgrupo      text
);

COMMIT;
