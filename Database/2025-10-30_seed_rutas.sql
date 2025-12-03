-- Demo data for Rutas module
-- Creates reference ciclos (if missing) and inserts sample rutas

DO $$
DECLARE
    v_ciclo_centro integer;
    v_ciclo_norte integer;
    v_ciclo_corte integer;
BEGIN
    -- Ciclo Agua Centro
    SELECT ciclos_id
      INTO v_ciclo_centro
      FROM public.ciclos
     WHERE ciclos_codigo = 'CIC-AGUA-001'
     LIMIT 1;

    IF v_ciclo_centro IS NULL THEN
        INSERT INTO public.ciclos (ciclos_codigo, ciclos_descripcioncorta, ciclos_descripcionlarga, estado, usuariocreacion, fechacreacion)
        VALUES ('CIC-AGUA-001', 'Agua Centro', 'Ciclo de rutas para cuadrillas de agua zona centro', true, 'seed-rutas', NOW())
        RETURNING ciclos_id INTO v_ciclo_centro;
    END IF;

    -- Ciclo Agua Norte
    SELECT ciclos_id
      INTO v_ciclo_norte
      FROM public.ciclos
     WHERE ciclos_codigo = 'CIC-AGUA-002'
     LIMIT 1;

    IF v_ciclo_norte IS NULL THEN
        INSERT INTO public.ciclos (ciclos_codigo, ciclos_descripcioncorta, ciclos_descripcionlarga, estado, usuariocreacion, fechacreacion)
        VALUES ('CIC-AGUA-002', 'Agua Norte', 'Ciclo de rutas para cuadrillas de agua zona norte', true, 'seed-rutas', NOW())
        RETURNING ciclos_id INTO v_ciclo_norte;
    END IF;

    -- Ciclo Corte Sur
    SELECT ciclos_id
      INTO v_ciclo_corte
      FROM public.ciclos
     WHERE ciclos_codigo = 'CIC-CORTE-001'
     LIMIT 1;

    IF v_ciclo_corte IS NULL THEN
        INSERT INTO public.ciclos (ciclos_codigo, ciclos_descripcioncorta, ciclos_descripcionlarga, estado, usuariocreacion, fechacreacion)
        VALUES ('CIC-CORTE-001', 'Corte Sur', 'Ciclo operativo para ordenes de corte zona sur', true, 'seed-rutas', NOW())
        RETURNING ciclos_id INTO v_ciclo_corte;
    END IF;

    -- Rutas demo
    IF NOT EXISTS (SELECT 1 FROM public.rutas WHERE codruta = 'A01') THEN
        INSERT INTO public.rutas (codciclo, codruta, descripcion)
        VALUES (v_ciclo_centro, 'A01', 'Ruta Agua Centro');
    END IF;

    IF NOT EXISTS (SELECT 1 FROM public.rutas WHERE codruta = 'A02') THEN
        INSERT INTO public.rutas (codciclo, codruta, descripcion)
        VALUES (v_ciclo_centro, 'A02', 'Ruta Agua Centro Oriente');
    END IF;

    IF NOT EXISTS (SELECT 1 FROM public.rutas WHERE codruta = 'A10') THEN
        INSERT INTO public.rutas (codciclo, codruta, descripcion)
        VALUES (v_ciclo_norte, 'A10', 'Ruta Agua Norte');
    END IF;

    IF NOT EXISTS (SELECT 1 FROM public.rutas WHERE codruta = 'C01') THEN
        INSERT INTO public.rutas (codciclo, codruta, descripcion)
        VALUES (v_ciclo_corte, 'C01', 'Ruta Corte Residencial Sur');
    END IF;
END
$$;
