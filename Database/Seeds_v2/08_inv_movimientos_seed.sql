-- ================================================
-- 08_inv_movimientos_seed.sql
-- Seeds demo para Inventarios:
--   * Categorías/productos/almacén básicos
--   * Existencias iniciales
--   * Movimientos de ingreso (desde compras) y egreso (ventas)
-- Requiere: seeds 01-06 y DDL 08_inventarios_core.sql.
-- ================================================

DO $$
DECLARE
    v_company_id        bigint;
    v_branch_id         bigint;
    v_inv_cat_id        bigint;
    v_inv_prod_id       bigint;
    v_almacen_id        bigint;
    v_com_factura_id    bigint;
    v_ven_factura_id    bigint;
    v_mov_ingreso_id    bigint;
    v_mov_egreso_id     bigint;
BEGIN
    SELECT company_id
      INTO v_company_id
      FROM public.cfg_company
     WHERE code = 'SIAD-DEMO'
     LIMIT 1;

    IF v_company_id IS NULL THEN
        RAISE EXCEPTION 'No existe compañía SIAD-DEMO. Ejecuta seeds previos antes de Inventarios.';
    END IF;

    SELECT branch_id
      INTO v_branch_id
      FROM public.cfg_branch
     WHERE company_id = v_company_id
       AND code = 'MATRIZ'
     LIMIT 1;

    INSERT INTO public.inv_categoria (
        company_id, code, nombre, descripcion)
    VALUES (
        v_company_id, 'INV-TUB', 'Materiales Tubería', 'Categoría general de tuberías y accesorios')
    ON CONFLICT (company_id, code) DO UPDATE
        SET nombre = EXCLUDED.nombre,
            descripcion = EXCLUDED.descripcion,
            updated_at = now(),
            updated_by = current_user
    RETURNING categoria_id INTO v_inv_cat_id;

    INSERT INTO public.inv_producto (
        company_id, categoria_id, code, nombre, descripcion,
        unidad_medida, es_inventariable, metodo_costeo,
        costo_promedio, costo_ultimo, estado)
    VALUES (
        v_company_id, v_inv_cat_id, 'MAT-TUB-50',
        'Tubería PVC 1/2"', 'Tubería estándar para acometidas',
        'UNIDAD', true, 'PROMEDIO',
        40.00, 40.00, 'ACTIVE')
    ON CONFLICT (company_id, code) DO UPDATE
        SET categoria_id = EXCLUDED.categoria_id,
            nombre = EXCLUDED.nombre,
            descripcion = EXCLUDED.descripcion,
            unidad_medida = EXCLUDED.unidad_medida,
            es_inventariable = EXCLUDED.es_inventariable,
            metodo_costeo = EXCLUDED.metodo_costeo,
            costo_promedio = EXCLUDED.costo_promedio,
            costo_ultimo = EXCLUDED.costo_ultimo,
            estado = EXCLUDED.estado,
            updated_at = now(),
            updated_by = current_user
    RETURNING producto_id INTO v_inv_prod_id;

    INSERT INTO public.inv_almacen (
        company_id, branch_id, code, nombre, direccion, responsable, estado)
    VALUES (
        v_company_id, v_branch_id, 'ALM-CENTRAL', 'Almacén Central Inventarios',
        'Col. Centroamérica, Bodega Inventarios', 'Carlos Mendoza', 'ACTIVE')
    ON CONFLICT (company_id, code) DO UPDATE
        SET branch_id = EXCLUDED.branch_id,
            nombre = EXCLUDED.nombre,
            direccion = EXCLUDED.direccion,
            responsable = EXCLUDED.responsable,
            estado = EXCLUDED.estado,
            updated_at = now(),
            updated_by = current_user
    RETURNING almacen_id INTO v_almacen_id;

    INSERT INTO public.inv_existencia (
        producto_id, almacen_id, cantidad_disponible, cantidad_reservada, costo_promedio)
    VALUES (
        v_inv_prod_id, v_almacen_id, 180.00, 0, 40.00)
    ON CONFLICT (producto_id, almacen_id) DO UPDATE
        SET cantidad_disponible = EXCLUDED.cantidad_disponible,
            cantidad_reservada = EXCLUDED.cantidad_reservada,
            costo_promedio = EXCLUDED.costo_promedio,
            updated_at = now();

    SELECT factura_id
      INTO v_com_factura_id
      FROM public.com_factura
     WHERE company_id = v_company_id
       AND numero_documento = 'FACPROV-2025-0001'
     LIMIT 1;

    SELECT factura_id
      INTO v_ven_factura_id
      FROM public.ven_factura
     WHERE company_id = v_company_id
       AND numero_documento = 'FV-2025-0002'
     LIMIT 1;

    -- Ingreso por compra
    INSERT INTO public.inv_movimiento (
        company_id, document_type_id, document_series_id, numero_documento,
        tipo_movimiento, fecha_movimiento, almacen_destino_id,
        referencia, origen_modulo, origen_documento_id,
        estado, monto_total)
    VALUES (
        v_company_id, NULL, NULL, 'ING-OC-2025-0001',
        'INGRESO', current_date - 8, v_almacen_id,
        'Ingreso por FACPROV-2025-0001', 'COMPRAS', v_com_factura_id,
        'POSTED', 9200.00)
    ON CONFLICT DO NOTHING
    RETURNING movimiento_id INTO v_mov_ingreso_id;

    IF v_mov_ingreso_id IS NULL THEN
        SELECT movimiento_id
          INTO v_mov_ingreso_id
          FROM public.inv_movimiento
         WHERE company_id = v_company_id
           AND origen_modulo = 'COMPRAS'
           AND origen_documento_id = v_com_factura_id
         LIMIT 1;
    END IF;

    INSERT INTO public.inv_movimiento_linea (
        movimiento_id, line_number, producto_id,
        almacen_id, cantidad, costo_unitario, costo_total, descripcion)
    VALUES (
        v_mov_ingreso_id, 1, v_inv_prod_id,
        v_almacen_id, 200, 40.00, 8000.00, 'Ingreso compra tubería')
    ON CONFLICT (movimiento_id, line_number) DO UPDATE
        SET cantidad = EXCLUDED.cantidad,
            costo_unitario = EXCLUDED.costo_unitario,
            costo_total = EXCLUDED.costo_total,
            descripcion = EXCLUDED.descripcion;

    -- Egreso por venta
    INSERT INTO public.inv_movimiento (
        company_id, document_type_id, document_series_id, numero_documento,
        tipo_movimiento, fecha_movimiento, almacen_origen_id,
        referencia, origen_modulo, origen_documento_id,
        estado, monto_total)
    VALUES (
        v_company_id, NULL, NULL, 'EGR-VEN-2025-0001',
        'EGRESO', current_date - 3, v_almacen_id,
        'Salida por FV-2025-0002', 'VENTAS', v_ven_factura_id,
        'POSTED', 800.00)
    ON CONFLICT DO NOTHING
    RETURNING movimiento_id INTO v_mov_egreso_id;

    IF v_mov_egreso_id IS NULL THEN
        SELECT movimiento_id
          INTO v_mov_egreso_id
          FROM public.inv_movimiento
         WHERE company_id = v_company_id
           AND origen_modulo = 'VENTAS'
           AND origen_documento_id = v_ven_factura_id
         LIMIT 1;
    END IF;

    INSERT INTO public.inv_movimiento_linea (
        movimiento_id, line_number, producto_id,
        almacen_id, cantidad, costo_unitario, costo_total, descripcion)
    VALUES (
        v_mov_egreso_id, 1, v_inv_prod_id,
        v_almacen_id, 20, 40.00, 800.00, 'Despacho a cliente empresarial')
    ON CONFLICT (movimiento_id, line_number) DO UPDATE
        SET cantidad = EXCLUDED.cantidad,
            costo_unitario = EXCLUDED.costo_unitario,
            costo_total = EXCLUDED.costo_total,
            descripcion = EXCLUDED.descripcion;

    -- Ajustar existencia final (200 ingreso - 20 egreso = 180)
    UPDATE public.inv_existencia
       SET cantidad_disponible = 180.00,
           costo_promedio = 40.00,
           updated_at = now()
     WHERE producto_id = v_inv_prod_id
       AND almacen_id = v_almacen_id;

    RAISE NOTICE 'Inventario demo actualizado para compañía %', v_company_id;
END
$$;
