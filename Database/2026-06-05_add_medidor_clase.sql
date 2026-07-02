BEGIN;

-- Catalog table for meter metrological classification (ISO 4064)
CREATE TABLE IF NOT EXISTS medidor_clase (
    medidor_clase_codigo  VARCHAR(1)   NOT NULL,
    descripcion           VARCHAR(200) NOT NULL,
    estado                BOOLEAN      NOT NULL DEFAULT true,
    usuariocreacion       VARCHAR(256),
    fechacreacion         TIMESTAMP,
    usuariomodificacion   VARCHAR(256),
    fechamodificacion     TIMESTAMP,
    CONSTRAINT pk_medidor_clase PRIMARY KEY (medidor_clase_codigo)
);

-- Seed ISO 4064 classes
INSERT INTO medidor_clase (medidor_clase_codigo, descripcion, estado, usuariocreacion, fechacreacion)
VALUES
    ('A', 'Clase A – Baja sensibilidad. Flujos constantes y relativamente altos. Uso típico en zonas rurales.', true, 'sistema', NOW()),
    ('B', 'Clase B – Precisión media. Detecta mejor caudales bajos. Uso típico en viviendas estándar.', true, 'sistema', NOW()),
    ('C', 'Clase C – Alta precisión en caudales bajos. Detecta goteos y fugas leves. Uso típico en zonas urbanas modernas.', true, 'sistema', NOW()),
    ('D', 'Clase D – Muy alta precisión. Excelente en caudales muy bajos. Uso en aplicaciones especiales e industriales.', true, 'sistema', NOW())
ON CONFLICT (medidor_clase_codigo) DO NOTHING;

-- Add FK column to maestro_medidor (nullable — existing meters have no class assigned yet)
ALTER TABLE maestro_medidor
    ADD COLUMN IF NOT EXISTS medidor_clase_codigo VARCHAR(1);

-- Add FK constraint idempotently (ADD CONSTRAINT IF NOT EXISTS is not valid PostgreSQL)
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'fk_maestro_medidor_clase'
          AND conrelid = 'maestro_medidor'::regclass
    ) THEN
        ALTER TABLE maestro_medidor
            ADD CONSTRAINT fk_maestro_medidor_clase
                FOREIGN KEY (medidor_clase_codigo)
                REFERENCES medidor_clase (medidor_clase_codigo)
                ON DELETE RESTRICT
                ON UPDATE CASCADE;
    END IF;
END
$$;

-- Partial index on FK column (meters without a class assigned are excluded)
CREATE INDEX IF NOT EXISTS ix_maestro_medidor_clase_codigo
    ON maestro_medidor (medidor_clase_codigo)
    WHERE medidor_clase_codigo IS NOT NULL;

COMMIT;
