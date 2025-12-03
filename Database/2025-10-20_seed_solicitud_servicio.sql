-- Demo de solicitudes para CLI-DEMO-001
DELETE FROM public.solicitud_servicio
WHERE clave_sure = 'CLI-DEMO-001';

INSERT INTO public.solicitud_servicio (
    cliente_identidad,
    categoria_servicio_id,
    cliente_rtn,
    cliente_nombre,
    cliente_telefono,
    cliente_movil,
    cliente_email,
    cliente_direccion,
    cliente_color_casa,
    observacion,
    empresa_nombre,
    empresa_telefono,
    empresa_direccion,
    negocio_nombre,
    negocio_telefono,
    negocio_clave_catastral,
    estado,
    usuariocreacion,
    fechacreacion,
    usuariomodificacion,
    fechamodificacion,
    asiginada,
    fechanacimiento,
    clave_sure
)
VALUES
    ('0801199000001', 1, '08011990000012', 'Cliente Demo Blazor',
     '22223333', '99998888', 'cliente.demo@apc.test', 'Colonia Demo #123',
     'Blanca', 'Solicitud de conexion demo',
     NULL, NULL, NULL,
     NULL, NULL, NULL,
     TRUE, 'seed', NOW(), NULL, NULL, FALSE, NULL, 'CLI-DEMO-001'),
    ('0801199000001', 1, '08011990000012', 'Cliente Demo Blazor',
     '22223333', '99998888', 'cliente.demo@apc.test', 'Colonia Demo #123',
     'Blanca', 'Cambio de medidor demo',
     NULL, NULL, NULL,
     NULL, NULL, NULL,
     TRUE, 'seed', NOW() - INTERVAL '5 days', NULL, NULL, TRUE, NULL, 'CLI-DEMO-001');
