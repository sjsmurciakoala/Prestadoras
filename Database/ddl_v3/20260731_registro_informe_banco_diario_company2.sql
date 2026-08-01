-- =============================================================================
-- Registro en el catalogo web de reporteria del Informe de banco diario
-- (company_id = 2). Mismo patron que el estado de flujos de efectivo: el
-- dataset y el informe se registran por datos, nunca en seeds C#.
--
-- - Dataset 'banco-diario' -> public.rep_banco_diario
-- - Informe 'banco-diario' en categoria Cobranza.
--   La plantilla DevExpress inicial la construye ReportTemplateFactory al
--   abrir el viewer/designer; el diseno editado se persiste por empresa en
--   rep_reporte_layout.
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

    IF to_regprocedure('public.rep_banco_diario(bigint, date, date)') IS NULL THEN
        RAISE EXCEPTION 'No existe public.rep_banco_diario. Ejecute antes 2026-07-31_rep_banco_diario.sql';
    END IF;

    -- 1. Dataset
    SELECT dataset_id INTO v_dataset_id
    FROM public.rep_catalogo_dataset
    WHERE company_id = v_company_id AND codigo = 'banco-diario';

    IF v_dataset_id IS NULL THEN
        INSERT INTO public.rep_catalogo_dataset (
            company_id, codigo, nombre, descripcion, tipo_origen, origen_clave,
            sql_text, connection_name, is_active, created_at, created_by
        )
        VALUES (
            v_company_id,
            'banco-diario',
            'Dataset informe de banco diario',
            'Recaudacion vigente del dia desde adm_pago (modelo nuevo): recibo, cliente, canal, forma de pago, banco/cuenta, caja y cajero.',
            'STORED_PROCEDURE',
            'public.rep_banco_diario',
            NULL,
            'DefaultConnection',
            true,
            now(),
            'banco-diario-registro'
        )
        RETURNING dataset_id INTO v_dataset_id;
    ELSE
        UPDATE public.rep_catalogo_dataset
        SET nombre = 'Dataset informe de banco diario',
            descripcion = 'Recaudacion vigente del dia desde adm_pago (modelo nuevo): recibo, cliente, canal, forma de pago, banco/cuenta, caja y cajero.',
            tipo_origen = 'STORED_PROCEDURE',
            origen_clave = 'public.rep_banco_diario',
            sql_text = NULL,
            connection_name = 'DefaultConnection',
            is_active = true,
            updated_at = now(),
            updated_by = 'banco-diario-registro'
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
        (v_company_id, v_dataset_id, 'CompanyId', 'p_company_id', 'Empresa actual', 'INT64', 'CURRENT_COMPANY', NULL, false, false, true, 0, now(), 'banco-diario-registro'),
        (v_company_id, v_dataset_id, 'FechaDesde', 'p_fecha_desde', 'Fecha desde', 'DATE', 'REPORT', NULL, true, true, false, 10, now(), 'banco-diario-registro'),
        (v_company_id, v_dataset_id, 'FechaHasta', 'p_fecha_hasta', 'Fecha hasta', 'DATE', 'REPORT', NULL, true, true, false, 20, now(), 'banco-diario-registro');

    -- 3. Informe en el catalogo.
    IF EXISTS (
        SELECT 1 FROM public.rep_catalogo_informe
        WHERE company_id = v_company_id AND codigo = 'banco-diario'
    ) THEN
        UPDATE public.rep_catalogo_informe
        SET nombre = 'Informe de banco diario',
            descripcion = 'Recaudacion vigente del dia para cuadrar el deposito: recibo, cliente, canal, forma de pago, banco/cuenta, caja y cajero.',
            categoria = 'Cobranza',
            tipo_origen = 'REPORT',
            ruta = '/informes/reportes/banco-diario/viewer',
            consulta_clave = 'banco-diario',
            icono_css_class = 'bi bi-bank',
            orden = 30,
            permite_exportar = true,
            permite_imprimir = true,
            is_active = true,
            updated_at = now(),
            updated_by = 'banco-diario-registro'
        WHERE company_id = v_company_id AND codigo = 'banco-diario';
    ELSE
        INSERT INTO public.rep_catalogo_informe (
            company_id, codigo, nombre, descripcion, categoria, tipo_origen,
            ruta, consulta_clave, icono_css_class, orden,
            permite_exportar, permite_imprimir, is_active, created_at, created_by
        )
        VALUES (
            v_company_id,
            'banco-diario',
            'Informe de banco diario',
            'Recaudacion vigente del dia para cuadrar el deposito: recibo, cliente, canal, forma de pago, banco/cuenta, caja y cajero.',
            'Cobranza',
            'REPORT',
            '/informes/reportes/banco-diario/viewer',
            'banco-diario',
            'bi bi-bank',
            30,
            true,
            true,
            true,
            now(),
            'banco-diario-registro'
        );
    END IF;
END $$;

COMMIT;
