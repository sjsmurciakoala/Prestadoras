-- =============================================================================
-- Registro en el catalogo web de reporteria del Estado de Flujos de Efectivo
-- (company_id = 2). Sustituye el seed en codigo: el dataset y el informe se
-- registran por datos, como el resto de cambios funcionales de BD.
--
-- - Dataset 'estado-flujo-efectivo' -> public.rep_estado_flujo_efectivo
--   (si existe el dataset manual 'sp-flujo-efectivo' se consolida renombrandolo,
--   para no dejar dos fuentes apuntando a la misma funcion).
-- - Informe 'estado-flujo-efectivo' en categoria Contabilidad, con
--   consulta_clave apuntando al dataset. La plantilla DevExpress inicial la
--   construye ReportTemplateFactory al abrir el viewer/designer; el diseno
--   editado se persiste por empresa en rep_reporte_layout.
--
-- Idempotente: se puede ejecutar varias veces. Para otra empresa, duplicar
-- cambiando v_company_id (el catalogo es por tenant).
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

    IF to_regprocedure('public.rep_estado_flujo_efectivo(bigint, date, date)') IS NULL THEN
        RAISE EXCEPTION 'No existe public.rep_estado_flujo_efectivo. Ejecute antes 20260714_rep_estado_flujo_efectivo.sql';
    END IF;

    -- 1. Dataset: consolidar el manual 'sp-flujo-efectivo' si existe, o crear.
    SELECT dataset_id INTO v_dataset_id
    FROM public.rep_catalogo_dataset
    WHERE company_id = v_company_id AND codigo = 'estado-flujo-efectivo';

    IF v_dataset_id IS NULL THEN
        SELECT dataset_id INTO v_dataset_id
        FROM public.rep_catalogo_dataset
        WHERE company_id = v_company_id AND codigo = 'sp-flujo-efectivo';
    END IF;

    IF v_dataset_id IS NULL THEN
        INSERT INTO public.rep_catalogo_dataset (
            company_id, codigo, nombre, descripcion, tipo_origen, origen_clave,
            sql_text, connection_name, is_active, created_at, created_by
        )
        VALUES (
            v_company_id,
            'estado-flujo-efectivo',
            'Dataset estado de flujos de efectivo',
            'Fuente configurada por empresa para el estado de flujos de efectivo desde con_configuracion_flujo_efectivo.',
            'STORED_PROCEDURE',
            'public.rep_estado_flujo_efectivo',
            NULL,
            'DefaultConnection',
            true,
            now(),
            'flujo-efectivo-registro'
        )
        RETURNING dataset_id INTO v_dataset_id;
    ELSE
        UPDATE public.rep_catalogo_dataset
        SET codigo = 'estado-flujo-efectivo',
            nombre = 'Dataset estado de flujos de efectivo',
            descripcion = 'Fuente configurada por empresa para el estado de flujos de efectivo desde con_configuracion_flujo_efectivo.',
            tipo_origen = 'STORED_PROCEDURE',
            origen_clave = 'public.rep_estado_flujo_efectivo',
            sql_text = NULL,
            connection_name = 'DefaultConnection',
            is_active = true,
            updated_at = now(),
            updated_by = 'flujo-efectivo-registro'
        WHERE dataset_id = v_dataset_id;
    END IF;

    -- 2. Parametros del dataset (se reescriben para dejarlos normalizados).
    DELETE FROM public.rep_dataset_parametro
    WHERE company_id = v_company_id AND dataset_id = v_dataset_id;

    INSERT INTO public.rep_dataset_parametro (
        company_id, dataset_id, nombre, nombre_origen, etiqueta, tipo_dato,
        fuente_valor, valor_default, visible, permite_nulo, requerido, orden,
        created_at, created_by
    )
    VALUES
        (v_company_id, v_dataset_id, 'CompanyId', 'p_company_id', 'Empresa actual', 'INT64', 'CURRENT_COMPANY', NULL, false, false, true, 0, now(), 'flujo-efectivo-registro'),
        (v_company_id, v_dataset_id, 'FechaDesde', 'p_fecha_desde', 'Fecha desde', 'DATE', 'REPORT', NULL, true, false, true, 10, now(), 'flujo-efectivo-registro'),
        (v_company_id, v_dataset_id, 'FechaHasta', 'p_fecha_hasta', 'Fecha hasta', 'DATE', 'REPORT', NULL, true, false, true, 20, now(), 'flujo-efectivo-registro');

    -- 3. Informe en el catalogo (categoria Contabilidad, entre ER y Transacciones).
    IF EXISTS (
        SELECT 1 FROM public.rep_catalogo_informe
        WHERE company_id = v_company_id AND codigo = 'estado-flujo-efectivo'
    ) THEN
        UPDATE public.rep_catalogo_informe
        SET nombre = 'Estado de flujos de efectivo',
            descripcion = 'Reporte financiero ERSAPS configurado por empresa a partir de con_configuracion_flujo_efectivo.',
            categoria = 'Contabilidad',
            tipo_origen = 'REPORT',
            ruta = '/informes/reportes/estado-flujo-efectivo/viewer',
            consulta_clave = 'estado-flujo-efectivo',
            icono_css_class = 'bi bi-cash-stack',
            orden = 55,
            permite_exportar = true,
            permite_imprimir = true,
            is_active = true,
            updated_at = now(),
            updated_by = 'flujo-efectivo-registro'
        WHERE company_id = v_company_id AND codigo = 'estado-flujo-efectivo';
    ELSE
        INSERT INTO public.rep_catalogo_informe (
            company_id, codigo, nombre, descripcion, categoria, tipo_origen,
            ruta, consulta_clave, icono_css_class, orden,
            permite_exportar, permite_imprimir, is_active, created_at, created_by
        )
        VALUES (
            v_company_id,
            'estado-flujo-efectivo',
            'Estado de flujos de efectivo',
            'Reporte financiero ERSAPS configurado por empresa a partir de con_configuracion_flujo_efectivo.',
            'Contabilidad',
            'REPORT',
            '/informes/reportes/estado-flujo-efectivo/viewer',
            'estado-flujo-efectivo',
            'bi bi-cash-stack',
            55,
            true,
            true,
            true,
            now(),
            'flujo-efectivo-registro'
        );
    END IF;
END $$;

COMMIT;
