BEGIN;

INSERT INTO public.usuarios_miorden (nombre, usuario, clave, tipo, estado)
SELECT 'Equipo Agua Norte', 'agua.norte', 'agua.norte', 1, B'1'
WHERE NOT EXISTS (SELECT 1 FROM public.usuarios_miorden WHERE usuario = 'agua.norte');

INSERT INTO public.usuarios_miorden (nombre, usuario, clave, tipo, estado)
SELECT 'Cuadrilla Corte Centro', 'corte.centro', 'corte.centro', 2, B'1'
WHERE NOT EXISTS (SELECT 1 FROM public.usuarios_miorden WHERE usuario = 'corte.centro');

INSERT INTO public.usuarios_miorden (nombre, usuario, clave, tipo, estado)
SELECT 'Cuadrilla Alcantarillado', 'alcantarillado.ops', 'alcantarillado.ops', 3, B'1'
WHERE NOT EXISTS (SELECT 1 FROM public.usuarios_miorden WHERE usuario = 'alcantarillado.ops');

COMMIT;
