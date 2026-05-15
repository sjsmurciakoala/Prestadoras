-- Tabla de mapeo de roles de servicios usados por WS/app.
-- Permite cambiar códigos sin tocar SPs.

CREATE TABLE IF NOT EXISTS public.servicios_roles_ws
(
    rol character varying(50) NOT NULL,
    servicios_codigo character varying(50) NOT NULL,
    activo boolean NOT NULL DEFAULT true,
    descripcion text,
    CONSTRAINT servicios_roles_ws_pkey PRIMARY KEY (rol, servicios_codigo)
);

-- Seeds (ajustables). Si un rol no aplica, elimina la fila correspondiente.
INSERT INTO public.servicios_roles_ws (rol, servicios_codigo, descripcion)
VALUES
    ('agua', 'SRV001', 'Agua'),
    ('alcantarillado', 'SRV002', 'Alcantarillado'),
    ('ambiental', 'SRV003', 'Ambiental'),
    ('ersaps', 'SRV004', 'ERSAPS'),
    ('gestion_legal', 'SRV005', 'Gestion legal'),
    ('sdo_ser6', 'SRV006', 'Ser6'),
    ('sdo_ser7', 'SRV007', 'Ser7'),
    ('sdo_ser8', 'SRV008', 'Ser8'),
    ('sdo_ser9', 'SRV009', 'Ser9'),
    ('sdo_ser10', 'SRV010', 'Ser10'),
    ('otros', 'SRV010', 'Otros cargos'),

    ('ser1', 'SRV001', 'Ser1'),
    ('ser2', 'SRV002', 'Ser2'),
    ('ser3', 'SRV003', 'Ser3'),
    ('ser4', 'SRV004', 'Ser4'),
    ('ser5', 'SRV005', 'Ser5'),
    ('ser6', 'SRV006', 'Ser6'),
    ('ser7', 'SRV007', 'Ser7'),
    ('ser8', 'SRV008', 'Ser8'),
    ('ser9', 'SRV009', 'Ser9'),
    ('ser10', 'SRV010', 'Ser10')
ON CONFLICT (rol, servicios_codigo) DO NOTHING;
