-- =============================================================================
-- Proveedores/Compromisos: multi-tenancy + integridad referencial
--
-- PROBLEMA
--   `prv_compromiso_hdr` y `prv_compromiso_dtl` no tienen `company_id` y
--   `OrdenesPagoDirectoService` los consulta sin filtro de compañía, así que
--   los compromisos se ven desde cualquier tenant. CLAUDE.md declara la
--   multi-tenancy como no negociable: toda tabla funcional lleva `company_id`.
--   Además ninguna tabla `prv_*` tenía llave primaria ni foránea; la unicidad
--   de `prv_proveedores(cod_proveedor, company_id)` sólo la asumía la app
--   (`ProveedoresService` con `WHERE NOT EXISTS`).
--
-- QUÉ HACE
--   1. `company_id bigint NOT NULL` en `prv_compromiso_hdr` y `_dtl`
--      (bigint = el tipo que exige ICompanyScopedEntity.company_id).
--      Backfill a la compañía 2, la única con datos.
--   2. `prv_compromiso_hdr.cod_proveedor` varchar(7) -> varchar(20), para que
--      quepan los mismos códigos que `prv_proveedores.cod_proveedor`.
--   3. Llaves primarias:
--        prv_compromiso_hdr   PK (company_id, numero_orden)
--        prv_compromiso_dtl   PK (compromiso_dtl_id)  [surrogate nuevo]
--        prv_tipoproveedor    PK (cod_tipoproveedor)
--      `prv_compromiso_dtl` no tiene clave natural: (numero_orden,
--      cod_presupuestario) se repite (3,261 filas / 3,113 combinaciones),
--      porque una misma partida presupuestaria aparece con distinto
--      programa/actividad. De ahí el surrogate.
--   4. UNIQUE `prv_proveedores (company_id, cod_proveedor)` — la unicidad que
--      la aplicación ya daba por hecha.
--   5. FK `prv_compromiso_dtl -> prv_compromiso_hdr` ON DELETE CASCADE.
--
-- NO HACE (fuera de alcance, anotado)
--   · `prv_proveedores.company_id` sigue siendo `integer` con DEFAULT 1. No se
--     toca para no romper los INSERT de `ProveedoresService`, que ya lo pasa
--     explícito. Por eso NO se crea FK entre hdr.company_id (bigint) y
--     prv_proveedores.company_id (integer).
--   · `prv_kardex` y `prv_tipostransacc` están vacías y sin uso: se dejan igual.
--
-- Idempotente y transaccional. Aplicar en SRV y en el mirror (regla espejo).
-- =============================================================================
BEGIN;

-- ---------------------------------------------------------------------------
-- Guard: no debe haber detalle huérfano, o la FK del paso 5 fallaría a medias.
-- ---------------------------------------------------------------------------
DO $$
DECLARE n bigint;
BEGIN
    SELECT count(*) INTO n
    FROM public.prv_compromiso_dtl d
    WHERE NOT EXISTS (SELECT 1 FROM public.prv_compromiso_hdr h
                      WHERE h.numero_orden = d.numero_orden);
    IF n > 0 THEN
        RAISE EXCEPTION 'ABORTADO: % fila(s) de prv_compromiso_dtl sin cabecera.', n;
    END IF;
END $$;

-- ---------------------------------------------------------------------------
-- 1) company_id en la cabecera
-- ---------------------------------------------------------------------------
ALTER TABLE public.prv_compromiso_hdr ADD COLUMN IF NOT EXISTS company_id bigint;
UPDATE public.prv_compromiso_hdr SET company_id = 2 WHERE company_id IS NULL;
ALTER TABLE public.prv_compromiso_hdr ALTER COLUMN company_id SET NOT NULL;

-- 2) Ampliar cod_proveedor para que admita los códigos de prv_proveedores
DO $$
DECLARE largo int;
BEGIN
    SELECT character_maximum_length INTO largo
    FROM information_schema.columns
    WHERE table_schema='public' AND table_name='prv_compromiso_hdr' AND column_name='cod_proveedor';
    IF largo IS NOT NULL AND largo < 20 THEN
        ALTER TABLE public.prv_compromiso_hdr ALTER COLUMN cod_proveedor TYPE varchar(20);
        RAISE NOTICE 'prv_compromiso_hdr.cod_proveedor ampliado de varchar(%) a varchar(20)', largo;
    END IF;
END $$;

-- 2b) `concepto` a varchar(500). Ya lo hace el script 00 de la migración de
--     proveedores, pero se repite aquí (idempotente) para que el esquema de
--     ambas bases coincida con `HasMaxLength(500)` del SiadDbContext, incluso
--     si esa migración todavía no se aplicó.
DO $$
DECLARE largo int;
BEGIN
    SELECT character_maximum_length INTO largo
    FROM information_schema.columns
    WHERE table_schema='public' AND table_name='prv_compromiso_hdr' AND column_name='concepto';
    IF largo IS NOT NULL AND largo < 500 THEN
        ALTER TABLE public.prv_compromiso_hdr ALTER COLUMN concepto TYPE varchar(500);
        RAISE NOTICE 'prv_compromiso_hdr.concepto ampliado de varchar(%) a varchar(500)', largo;
    END IF;
END $$;

-- ---------------------------------------------------------------------------
-- 3) company_id en el detalle (heredado de su cabecera)
-- ---------------------------------------------------------------------------
ALTER TABLE public.prv_compromiso_dtl ADD COLUMN IF NOT EXISTS company_id bigint;

UPDATE public.prv_compromiso_dtl d
SET company_id = h.company_id
FROM public.prv_compromiso_hdr h
WHERE h.numero_orden = d.numero_orden
  AND d.company_id IS DISTINCT FROM h.company_id;

ALTER TABLE public.prv_compromiso_dtl ALTER COLUMN company_id SET NOT NULL;

-- Surrogate para el detalle (no tiene clave natural)
ALTER TABLE public.prv_compromiso_dtl
    ADD COLUMN IF NOT EXISTS compromiso_dtl_id bigint GENERATED BY DEFAULT AS IDENTITY;

-- ---------------------------------------------------------------------------
-- 4) Llaves primarias y unicidad
-- ---------------------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                   WHERE conrelid='public.prv_compromiso_hdr'::regclass AND contype='p') THEN
        ALTER TABLE public.prv_compromiso_hdr
            ADD CONSTRAINT prv_compromiso_hdr_pkey PRIMARY KEY (company_id, numero_orden);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                   WHERE conrelid='public.prv_compromiso_dtl'::regclass AND contype='p') THEN
        ALTER TABLE public.prv_compromiso_dtl
            ADD CONSTRAINT prv_compromiso_dtl_pkey PRIMARY KEY (compromiso_dtl_id);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                   WHERE conrelid='public.prv_tipoproveedor'::regclass AND contype='p') THEN
        ALTER TABLE public.prv_tipoproveedor
            ADD CONSTRAINT prv_tipoproveedor_pkey PRIMARY KEY (cod_tipoproveedor);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                   WHERE conrelid='public.prv_proveedores'::regclass
                     AND conname='uq_prv_proveedores_company_cod') THEN
        ALTER TABLE public.prv_proveedores
            ADD CONSTRAINT uq_prv_proveedores_company_cod UNIQUE (company_id, cod_proveedor);
    END IF;
END $$;

-- ---------------------------------------------------------------------------
-- 5) Integridad referencial detalle -> cabecera
-- ---------------------------------------------------------------------------
CREATE INDEX IF NOT EXISTS ix_prv_compromiso_dtl_company_orden
    ON public.prv_compromiso_dtl (company_id, numero_orden);

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                   WHERE conrelid='public.prv_compromiso_dtl'::regclass
                     AND conname='fk_prv_compromiso_dtl_hdr') THEN
        ALTER TABLE public.prv_compromiso_dtl
            ADD CONSTRAINT fk_prv_compromiso_dtl_hdr
            FOREIGN KEY (company_id, numero_orden)
            REFERENCES public.prv_compromiso_hdr (company_id, numero_orden)
            ON DELETE CASCADE;
    END IF;
END $$;

-- ---------------------------------------------------------------------------
-- 6) Verificación dentro de la transacción
-- ---------------------------------------------------------------------------
DO $$
DECLARE n_hdr bigint; n_dtl bigint; n_pk int;
BEGIN
    SELECT count(*) INTO n_hdr FROM public.prv_compromiso_hdr WHERE company_id <> 2;
    SELECT count(*) INTO n_dtl FROM public.prv_compromiso_dtl WHERE company_id <> 2;
    IF n_hdr > 0 OR n_dtl > 0 THEN
        RAISE EXCEPTION 'ABORTADO: filas fuera de la compañía 2 (hdr=%, dtl=%).', n_hdr, n_dtl;
    END IF;

    SELECT count(*) INTO n_pk FROM pg_constraint
    WHERE contype='p' AND conrelid IN (
        'public.prv_compromiso_hdr'::regclass,
        'public.prv_compromiso_dtl'::regclass,
        'public.prv_tipoproveedor'::regclass);
    IF n_pk <> 3 THEN
        RAISE EXCEPTION 'ABORTADO: se esperaban 3 llaves primarias nuevas, hay %.', n_pk;
    END IF;

    RAISE NOTICE 'OK · hdr=% filas · dtl=% filas · PKs=% · FK dtl->hdr y UNIQUE de proveedores creados',
        (SELECT count(*) FROM public.prv_compromiso_hdr),
        (SELECT count(*) FROM public.prv_compromiso_dtl),
        n_pk;
END $$;

COMMIT;
