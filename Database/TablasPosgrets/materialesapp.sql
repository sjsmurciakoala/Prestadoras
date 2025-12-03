CREATE TABLE IF NOT EXISTS public.orden_trabajo_detalle
(
    orden_trabajo_detalle_id serial PRIMARY KEY,
    orden_numero             numeric(18,0) REFERENCES public.orden_trabajo(orden_numero),
    descripcion              varchar(500),
    fecha_registro           timestamp without time zone DEFAULT now(),
    usuario_registro         varchar(50)
);
relation "orden_trabajo" already exists, skipping
