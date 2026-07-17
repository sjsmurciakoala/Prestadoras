-- =============================================================================
-- Registro en el catalogo web de reporteria del formato de factura (ticket)
-- (company_id = 2). Mismo patron que 20260714_registro_informe_flujo_efectivo:
--
-- - Dataset 'factura-ticket' -> public.rep_factura_ticket (STORED_PROCEDURE).
--   Parametros: CompanyId (CURRENT_COMPANY, oculto) y FacturaId (REPORT,
--   visible/requerido). El visor acepta el deep-link
--   /informes/reportes/factura-ticket/viewer?FacturaId=<id> desde la vista
--   Facturas App (el storage aplica parametros pasados en el nombre del
--   reporte 'factura-ticket?FacturaId=...').
-- - Informe 'factura-ticket' en categoria Facturacion. La plantilla DevExpress
--   inicial la construye ReportTemplateFactory al abrir el viewer/designer;
--   el diseno editado se persiste por empresa en rep_reporte_layout.
--
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

    IF to_regprocedure('public.rep_factura_ticket(bigint, bigint)') IS NULL THEN
        RAISE EXCEPTION 'No existe public.rep_factura_ticket. Ejecute antes 20260717_rep_factura_ticket.sql';
    END IF;

    -- 1. Dataset
    SELECT dataset_id INTO v_dataset_id
    FROM public.rep_catalogo_dataset
    WHERE company_id = v_company_id AND codigo = 'factura-ticket';

    IF v_dataset_id IS NULL THEN
        INSERT INTO public.rep_catalogo_dataset (
            company_id, codigo, nombre, descripcion, tipo_origen, origen_clave,
            sql_text, connection_name, is_active, created_at, created_by
        )
        VALUES (
            v_company_id,
            'factura-ticket',
            'Dataset factura (ticket)',
            'Formato de factura de servicio: una fila por linea con cabecera repetida (emisor, CAI SAR, cliente, lectura, total).',
            'STORED_PROCEDURE',
            'public.rep_factura_ticket',
            NULL,
            'DefaultConnection',
            true,
            now(),
            'factura-ticket-registro'
        )
        RETURNING dataset_id INTO v_dataset_id;
    ELSE
        UPDATE public.rep_catalogo_dataset
        SET nombre = 'Dataset factura (ticket)',
            descripcion = 'Formato de factura de servicio: una fila por linea con cabecera repetida (emisor, CAI SAR, cliente, lectura, total).',
            tipo_origen = 'STORED_PROCEDURE',
            origen_clave = 'public.rep_factura_ticket',
            sql_text = NULL,
            connection_name = 'DefaultConnection',
            is_active = true,
            updated_at = now(),
            updated_by = 'factura-ticket-registro'
        WHERE dataset_id = v_dataset_id;
    END IF;

    -- 2. Parametros del dataset
    DELETE FROM public.rep_dataset_parametro
    WHERE company_id = v_company_id AND dataset_id = v_dataset_id;

    INSERT INTO public.rep_dataset_parametro (
        company_id, dataset_id, nombre, nombre_origen, etiqueta, tipo_dato,
        fuente_valor, valor_default, visible, permite_nulo, requerido, orden,
        created_at, created_by
    )
    VALUES
        (v_company_id, v_dataset_id, 'CompanyId', 'p_company_id', 'Empresa actual', 'INT64', 'CURRENT_COMPANY', NULL, false, false, true, 0, now(), 'factura-ticket-registro'),
        (v_company_id, v_dataset_id, 'FacturaId', 'p_factura_id', 'Factura (id interno)', 'INT64', 'REPORT', NULL, true, false, true, 10, now(), 'factura-ticket-registro');

    -- 3. Informe en el catalogo (categoria Facturacion)
    IF EXISTS (
        SELECT 1 FROM public.rep_catalogo_informe
        WHERE company_id = v_company_id AND codigo = 'factura-ticket'
    ) THEN
        UPDATE public.rep_catalogo_informe
        SET nombre = 'Factura (ticket)',
            descripcion = 'Reimpresion de una factura de servicio en formato ticket, con CAI y datos fiscales SAR. Se abre desde la vista Facturas App o indicando el id de la factura.',
            categoria = 'Facturacion',
            tipo_origen = 'REPORT',
            ruta = '/informes/reportes/factura-ticket/viewer',
            consulta_clave = 'factura-ticket',
            icono_css_class = 'bi bi-receipt',
            orden = 130,
            permite_exportar = true,
            permite_imprimir = true,
            is_active = true,
            updated_at = now(),
            updated_by = 'factura-ticket-registro'
        WHERE company_id = v_company_id AND codigo = 'factura-ticket';
    ELSE
        INSERT INTO public.rep_catalogo_informe (
            company_id, codigo, nombre, descripcion, categoria, tipo_origen,
            ruta, consulta_clave, icono_css_class, orden,
            permite_exportar, permite_imprimir, is_active, created_at, created_by
        )
        VALUES (
            v_company_id,
            'factura-ticket',
            'Factura (ticket)',
            'Reimpresion de una factura de servicio en formato ticket, con CAI y datos fiscales SAR. Se abre desde la vista Facturas App o indicando el id de la factura.',
            'Facturacion',
            'REPORT',
            '/informes/reportes/factura-ticket/viewer',
            'factura-ticket',
            'bi bi-receipt',
            130,
            true,
            true,
            true,
            now(),
            'factura-ticket-registro'
        );
    END IF;
END $$;

COMMIT;
