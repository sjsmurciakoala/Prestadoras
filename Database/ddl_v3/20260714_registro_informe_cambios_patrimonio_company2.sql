-- =============================================================================
-- Registro en el catalogo web de reporteria del Estado de Cambios en el
-- Patrimonio (company_id = 2). Registro por datos (sin seeds en codigo),
-- mismo patron que 20260714_registro_informe_flujo_efectivo_company2.sql.
-- Idempotente. Para otra empresa, duplicar cambiando v_company_id.
-- =============================================================================

BEGIN;

DO $$
DECLARE
    v_company_id bigint := 2;
    v_dataset_id bigint;
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM public.cfg_company WHERE company_id = v_company_id
    ) THEN
        RAISE EXCEPTION 'No existe cfg_company.company_id=%', v_company_id;
    END IF;

    IF to_regprocedure('public.rep_estado_cambios_patrimonio(bigint, date, date)') IS NULL THEN
        RAISE EXCEPTION 'No existe public.rep_estado_cambios_patrimonio. Ejecute antes 20260714_rep_estado_cambios_patrimonio.sql';
    END IF;

    -- 1. Dataset.
    SELECT dataset_id INTO v_dataset_id
    FROM public.rep_catalogo_dataset
    WHERE company_id = v_company_id AND codigo = 'estado-cambios-patrimonio';

    IF v_dataset_id IS NULL THEN
        INSERT INTO public.rep_catalogo_dataset (
            company_id, codigo, nombre, descripcion, tipo_origen, origen_clave,
            sql_text, connection_name, is_active, created_at, created_by
        )
        VALUES (
            v_company_id,
            'estado-cambios-patrimonio',
            'Dataset estado de cambios en el patrimonio',
            'Componentes de patrimonio (clase 5 de con_configuracion_balance) con saldo inicial, aumentos, disminuciones y saldo final.',
            'STORED_PROCEDURE',
            'public.rep_estado_cambios_patrimonio',
            NULL,
            'DefaultConnection',
            true,
            now(),
            'cambios-patrimonio-registro'
        )
        RETURNING dataset_id INTO v_dataset_id;
    ELSE
        UPDATE public.rep_catalogo_dataset
        SET nombre = 'Dataset estado de cambios en el patrimonio',
            descripcion = 'Componentes de patrimonio (clase 5 de con_configuracion_balance) con saldo inicial, aumentos, disminuciones y saldo final.',
            tipo_origen = 'STORED_PROCEDURE',
            origen_clave = 'public.rep_estado_cambios_patrimonio',
            sql_text = NULL,
            connection_name = 'DefaultConnection',
            is_active = true,
            updated_at = now(),
            updated_by = 'cambios-patrimonio-registro'
        WHERE dataset_id = v_dataset_id;
    END IF;

    -- 2. Parametros (se reescriben para dejarlos normalizados).
    DELETE FROM public.rep_dataset_parametro
    WHERE company_id = v_company_id AND dataset_id = v_dataset_id;

    INSERT INTO public.rep_dataset_parametro (
        company_id, dataset_id, nombre, nombre_origen, etiqueta, tipo_dato,
        fuente_valor, valor_default, visible, permite_nulo, requerido, orden,
        created_at, created_by
    )
    VALUES
        (v_company_id, v_dataset_id, 'CompanyId', 'p_company_id', 'Empresa actual', 'INT64', 'CURRENT_COMPANY', NULL, false, false, true, 0, now(), 'cambios-patrimonio-registro'),
        (v_company_id, v_dataset_id, 'FechaDesde', 'p_fecha_desde', 'Fecha desde', 'DATE', 'REPORT', NULL, true, false, true, 10, now(), 'cambios-patrimonio-registro'),
        (v_company_id, v_dataset_id, 'FechaHasta', 'p_fecha_hasta', 'Fecha hasta', 'DATE', 'REPORT', NULL, true, false, true, 20, now(), 'cambios-patrimonio-registro');

    -- 3. Informe (categoria Contabilidad, despues del flujo de efectivo).
    IF EXISTS (
        SELECT 1 FROM public.rep_catalogo_informe
        WHERE company_id = v_company_id AND codigo = 'estado-cambios-patrimonio'
    ) THEN
        UPDATE public.rep_catalogo_informe
        SET nombre = 'Estado de cambios en el patrimonio',
            descripcion = 'Reporte financiero ERSAPS derivado de los componentes de patrimonio de con_configuracion_balance.',
            categoria = 'Contabilidad',
            tipo_origen = 'REPORT',
            ruta = '/informes/reportes/estado-cambios-patrimonio/viewer',
            consulta_clave = 'estado-cambios-patrimonio',
            icono_css_class = 'bi bi-safe',
            orden = 56,
            permite_exportar = true,
            permite_imprimir = true,
            is_active = true,
            updated_at = now(),
            updated_by = 'cambios-patrimonio-registro'
        WHERE company_id = v_company_id AND codigo = 'estado-cambios-patrimonio';
    ELSE
        INSERT INTO public.rep_catalogo_informe (
            company_id, codigo, nombre, descripcion, categoria, tipo_origen,
            ruta, consulta_clave, icono_css_class, orden,
            permite_exportar, permite_imprimir, is_active, created_at, created_by
        )
        VALUES (
            v_company_id,
            'estado-cambios-patrimonio',
            'Estado de cambios en el patrimonio',
            'Reporte financiero ERSAPS derivado de los componentes de patrimonio de con_configuracion_balance.',
            'Contabilidad',
            'REPORT',
            '/informes/reportes/estado-cambios-patrimonio/viewer',
            'estado-cambios-patrimonio',
            'bi bi-safe',
            56,
            true,
            true,
            true,
            now(),
            'cambios-patrimonio-registro'
        );
    END IF;
END $$;

COMMIT;
