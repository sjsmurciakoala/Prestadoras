# Plan de incorporación de `company_id` en tablas faltantes

Este documento inventaria todos los scripts en `Database/ddl_v2` que contienen `CREATE TABLE` sin la columna `company_id`, propone migraciones incrementales (ordenadas por módulo) y define la coordinación de scripts de datos/validaciones para garantizar que DevExpress Reports y los servicios Blazor WebAssembly (DevExpress 25.1) lean la nueva estructura.

> **Automatización**: el inventario se genera ejecutando `python tools/inventory_company_id.py` dentro de `Prestadoras/Database/ddl_v2`. El script recorre cada `.sql`, detecta bloques `CREATE TABLE` y produce la tabla de resultados en Markdown o JSON (`--format json`).

## 1. Inventario detallado de tablas sin `company_id`

| Script (módulo) | Tabla | Referencia sugerida para poblar `company_id` |
| --- | --- | --- |
| 01_configuracion_base.sql (Configuración) | public.cfg_currency | `cfg_company.currency_code` → localizar todas las empresas y asociar la moneda base a cada una. |
| 02_contabilidad_core.sql (Contabilidad) | public.con_plantilla_poliza_linea | `con_plantilla_poliza` contiene `company_id`; propagarlo por `plantilla_id`. |
| 02_contabilidad_core.sql (Contabilidad) | public.con_poliza_linea | `con_poliza` contiene `company_id`; propagarlo por `poliza_id`. |
| 04_ventas_core.sql (Ventas) | public.ven_factura_linea | `ven_factura` contiene `company_id`; propagarlo por `factura_id`. |
| 04_ventas_core.sql (Ventas) | public.ven_nota | `ven_factura` y `cfg_document_type` almacenan `company_id`; usar `ven_factura` como fuente principal. |
| 04_ventas_core.sql (Ventas) | public.ven_cobro_detalle | `ven_cobro` contiene `company_id`; propagarlo por `cobro_id`. |
| 05_compras_core.sql (Compras) | public.com_orden_linea | `com_orden` contiene `company_id`; propagarlo por `orden_id`. |
| 05_compras_core.sql (Compras) | public.com_factura_linea | `com_factura` contiene `company_id`; propagarlo por `factura_id`. |
| 05_compras_core.sql (Compras) | public.com_pago_detalle | `com_pago` contiene `company_id`; propagarlo por `pago_id`. |
| 06_bancos_core.sql (Bancos) | public.ban_conciliacion | `ban_cuenta` contiene `company_id`; propagarlo por `banco_cuenta_id`. |
| 06_bancos_core.sql (Bancos) | public.ban_conciliacion_detalle | `ban_conciliacion` tendrá `company_id`; propagarlo por `conciliacion_id`. |
| 07_administracion_core.sql (Administrativo - catálogos) | public.adm_lista_precio_detalle | `adm_lista_precio` contiene `company_id`; propagarlo por `lista_precio_id`. |
| 07_administracion_core.sql (Administrativo) | public.adm_cliente_contacto | `adm_cliente` contiene `company_id`; propagarlo por `cliente_id`. |
| 07_administracion_core.sql (Administrativo) | public.adm_proveedor_contacto | `adm_proveedor` contiene `company_id`; propagarlo por `proveedor_id`. |
| 07_administracion_core.sql (Administrativo) | public.adm_reporte_parametro | `adm_reporte` contiene `company_id`; propagarlo por `reporte_id`. |
| 07_administracion_core.sql (Administrativo) | public.adm_reporte_programacion | `adm_reporte` contiene `company_id`; propagarlo por `reporte_id`. |
| 07_administracion_core.sql (Administrativo) | public.adm_reporte_ejecucion | `adm_reporte` contiene `company_id`; propagarlo por `reporte_id`. |
| 08_inventarios_core.sql (Inventarios) | public.inv_existencia | `inv_producto` y `cfg_branch` contienen `company_id`; la fuente más estable es `cfg_branch` a través de `almacen_id`. |
| 08_inventarios_core.sql (Inventarios) | public.inv_movimiento_linea | `inv_movimiento` contiene `company_id`; propagarlo por `movimiento_id`. |
| 09_activos_fijos_core.sql (Activos fijos) | public.af_depreciacion | `af_activo` contiene `company_id`; propagarlo por `activo_id`. |
| 09_activos_fijos_core.sql (Activos fijos) | public.af_baja | `af_activo` contiene `company_id`; propagarlo por `activo_id`. |
| 10_administracion_maestros.sql (Administrativo - catálogo extendido) | public.adm_lista_precio_detalle | Duplicada respecto al módulo 07; seguir mismo criterio para ambientes que usan este script legado. |
| 11_administracion_transacciones.sql (Administrativo - transacciones) | public.adm_cxc_movimiento | `adm_cliente` (y `adm_cxc_documento`) tienen `company_id`; propagarlo por `cliente_id`. |
| 11_administracion_transacciones.sql (Administrativo - transacciones) | public.adm_cxp_movimiento | `adm_proveedor` contiene `company_id`; propagarlo por `proveedor_id`. |
| 11_administracion_transacciones.sql (Administrativo - transacciones) | public.adm_interes_mora | `adm_cliente`/`adm_cxc_documento` con `company_id`; usar `cliente_id`. |

> _Notas_: El inventario se generó con un análisis automatizado (script Python temporal) que revisó cada bloque `CREATE TABLE` dentro de `Database/ddl_v2`. Sólo se listan las tablas que no definen explícitamente `company_id` en su definición actual.

## 2. Migraciones incrementales por módulo

Cada módulo debe desplegarse mediante scripts independientes para minimizar bloqueos y facilitar rollbacks. Todos siguen la misma estructura: agregar columna, poblarla con datos existentes, reforzar restricciones e índices y documentar en `TablasPostgrets`. Ejemplos:

### 2.1 Configuración (01_configuracion_base.sql)

Script listo para despliegue: `migrations/configuracion/20250112_add_company_id_cfg_currency.sql`.

```sql
-- 01_configuracion_base__add_company_to_currency.sql
ALTER TABLE public.cfg_currency
    ADD COLUMN company_id bigint;

UPDATE public.cfg_currency cur
SET company_id = cmp.company_id
FROM public.cfg_company cmp
WHERE cmp.currency_code = cur.currency_code
  AND cur.company_id IS NULL;

-- fallback: asignar a la empresa actual cuando existan múltiples monedas base
UPDATE public.cfg_currency
SET company_id = (SELECT company_id FROM public.cfg_company ORDER BY company_id LIMIT 1)
WHERE company_id IS NULL;

ALTER TABLE public.cfg_currency
    ALTER COLUMN company_id SET NOT NULL,
    ADD CONSTRAINT fk_cfg_currency_company FOREIGN KEY (company_id) REFERENCES public.cfg_company(company_id) ON DELETE CASCADE;

CREATE INDEX IF NOT EXISTS ix_cfg_currency_company ON public.cfg_currency(company_id);
```

### 2.2 Contabilidad (02_contabilidad_core.sql)

Script listo para despliegue: `migrations/contabilidad/20250112_add_company_id_lineas.sql`.

```sql
-- con_plantilla_poliza_linea
ALTER TABLE public.con_plantilla_poliza_linea ADD COLUMN company_id bigint;
UPDATE public.con_plantilla_poliza_linea l
SET company_id = p.company_id
FROM public.con_plantilla_poliza p
WHERE l.plantilla_id = p.plantilla_id;
ALTER TABLE public.con_plantilla_poliza_linea
    ALTER COLUMN company_id SET NOT NULL,
    ADD CONSTRAINT fk_con_plantilla_poliza_linea_company FOREIGN KEY (company_id) REFERENCES public.cfg_company(company_id);
CREATE INDEX IF NOT EXISTS ix_con_plantilla_poliza_linea_company ON public.con_plantilla_poliza_linea(company_id);

-- con_poliza_linea
ALTER TABLE public.con_poliza_linea ADD COLUMN company_id bigint;
UPDATE public.con_poliza_linea l
SET company_id = p.company_id
FROM public.con_poliza p
WHERE l.poliza_id = p.poliza_id;
ALTER TABLE public.con_poliza_linea
    ALTER COLUMN company_id SET NOT NULL,
    ADD CONSTRAINT fk_con_poliza_linea_company FOREIGN KEY (company_id) REFERENCES public.cfg_company(company_id);
CREATE INDEX IF NOT EXISTS ix_con_poliza_linea_company ON public.con_poliza_linea(company_id);
```

### 2.3 Ventas (04_ventas_core.sql)

Script listo para despliegue: `migrations/ventas/20250112_add_company_id_core.sql`.

```sql
-- ven_factura_linea
ALTER TABLE public.ven_factura_linea ADD COLUMN company_id bigint;
UPDATE public.ven_factura_linea l
SET company_id = f.company_id
FROM public.ven_factura f
WHERE l.factura_id = f.factura_id;
ALTER TABLE public.ven_factura_linea
    ALTER COLUMN company_id SET NOT NULL,
    ADD CONSTRAINT fk_ven_factura_linea_company FOREIGN KEY (company_id) REFERENCES public.cfg_company(company_id);
CREATE INDEX IF NOT EXISTS ix_ven_factura_linea_company ON public.ven_factura_linea(company_id);

-- ven_nota
ALTER TABLE public.ven_nota ADD COLUMN company_id bigint;
UPDATE public.ven_nota n
SET company_id = f.company_id
FROM public.ven_factura f
WHERE n.factura_id = f.factura_id;
ALTER TABLE public.ven_nota
    ALTER COLUMN company_id SET NOT NULL,
    ADD CONSTRAINT fk_ven_nota_company FOREIGN KEY (company_id) REFERENCES public.cfg_company(company_id);
CREATE INDEX IF NOT EXISTS ix_ven_nota_company ON public.ven_nota(company_id);

-- ven_cobro_detalle
ALTER TABLE public.ven_cobro_detalle ADD COLUMN company_id bigint;
UPDATE public.ven_cobro_detalle d
SET company_id = c.company_id
FROM public.ven_cobro c
WHERE d.cobro_id = c.cobro_id;
ALTER TABLE public.ven_cobro_detalle
    ALTER COLUMN company_id SET NOT NULL,
    ADD CONSTRAINT fk_ven_cobro_detalle_company FOREIGN KEY (company_id) REFERENCES public.cfg_company(company_id);
CREATE INDEX IF NOT EXISTS ix_ven_cobro_detalle_company ON public.ven_cobro_detalle(company_id);
```

### 2.4 Compras (05_compras_core.sql)

Script listo para despliegue: `migrations/compras/20250112_add_company_id_core.sql`.

```sql
ALTER TABLE public.com_orden_linea ADD COLUMN company_id bigint;
UPDATE public.com_orden_linea l
SET company_id = o.company_id
FROM public.com_orden o
WHERE l.orden_id = o.orden_id;
ALTER TABLE public.com_orden_linea
    ALTER COLUMN company_id SET NOT NULL,
    ADD CONSTRAINT fk_com_orden_linea_company FOREIGN KEY (company_id) REFERENCES public.cfg_company(company_id);
CREATE INDEX IF NOT EXISTS ix_com_orden_linea_company ON public.com_orden_linea(company_id);

ALTER TABLE public.com_factura_linea ADD COLUMN company_id bigint;
UPDATE public.com_factura_linea l
SET company_id = f.company_id
FROM public.com_factura f
WHERE l.factura_id = f.factura_id;
ALTER TABLE public.com_factura_linea
    ALTER COLUMN company_id SET NOT NULL,
    ADD CONSTRAINT fk_com_factura_linea_company FOREIGN KEY (company_id) REFERENCES public.cfg_company(company_id);
CREATE INDEX IF NOT EXISTS ix_com_factura_linea_company ON public.com_factura_linea(company_id);

ALTER TABLE public.com_pago_detalle ADD COLUMN company_id bigint;
UPDATE public.com_pago_detalle d
SET company_id = p.company_id
FROM public.com_pago p
WHERE d.pago_id = p.pago_id;
ALTER TABLE public.com_pago_detalle
    ALTER COLUMN company_id SET NOT NULL,
    ADD CONSTRAINT fk_com_pago_detalle_company FOREIGN KEY (company_id) REFERENCES public.cfg_company(company_id);
CREATE INDEX IF NOT EXISTS ix_com_pago_detalle_company ON public.com_pago_detalle(company_id);
```

### 2.5 Bancos (06_bancos_core.sql)

Script listo para despliegue: `migrations/bancos/20250112_add_company_id_core.sql`.

```sql
ALTER TABLE public.ban_conciliacion ADD COLUMN company_id bigint;
UPDATE public.ban_conciliacion c
SET company_id = cuenta.company_id
FROM public.ban_cuenta cuenta
WHERE c.banco_cuenta_id = cuenta.banco_cuenta_id;
ALTER TABLE public.ban_conciliacion
    ALTER COLUMN company_id SET NOT NULL,
    ADD CONSTRAINT fk_ban_conciliacion_company FOREIGN KEY (company_id) REFERENCES public.cfg_company(company_id);
CREATE INDEX IF NOT EXISTS ix_ban_conciliacion_company ON public.ban_conciliacion(company_id);

ALTER TABLE public.ban_conciliacion_detalle ADD COLUMN company_id bigint;
UPDATE public.ban_conciliacion_detalle d
SET company_id = conc.company_id
FROM public.ban_conciliacion conc
WHERE d.conciliacion_id = conc.conciliacion_id;
ALTER TABLE public.ban_conciliacion_detalle
    ALTER COLUMN company_id SET NOT NULL,
    ADD CONSTRAINT fk_ban_conciliacion_detalle_company FOREIGN KEY (company_id) REFERENCES public.cfg_company(company_id);
CREATE INDEX IF NOT EXISTS ix_ban_conciliacion_detalle_company ON public.ban_conciliacion_detalle(company_id);
```

### 2.6 Administración (07_administracion_core.sql, 10_administracion_maestros.sql y 11_administracion_transacciones.sql)

Script listo para despliegue: `migrations/administracion/20250112_add_company_id_core.sql`.

```sql
-- adm_lista_precio_detalle
ALTER TABLE public.adm_lista_precio_detalle ADD COLUMN company_id bigint;
UPDATE public.adm_lista_precio_detalle d
SET company_id = lp.company_id
FROM public.adm_lista_precio lp
WHERE d.lista_precio_id = lp.lista_precio_id;
ALTER TABLE public.adm_lista_precio_detalle
    ALTER COLUMN company_id SET NOT NULL,
    ADD CONSTRAINT fk_adm_lista_precio_detalle_company FOREIGN KEY (company_id) REFERENCES public.cfg_company(company_id);
CREATE INDEX IF NOT EXISTS ix_adm_lista_precio_detalle_company ON public.adm_lista_precio_detalle(company_id);

-- adm_cliente_contacto
ALTER TABLE public.adm_cliente_contacto ADD COLUMN company_id bigint;
UPDATE public.adm_cliente_contacto c
SET company_id = cli.company_id
FROM public.adm_cliente cli
WHERE c.cliente_id = cli.cliente_id;
ALTER TABLE public.adm_cliente_contacto
    ALTER COLUMN company_id SET NOT NULL,
    ADD CONSTRAINT fk_adm_cliente_contacto_company FOREIGN KEY (company_id) REFERENCES public.cfg_company(company_id);
CREATE INDEX IF NOT EXISTS ix_adm_cliente_contacto_company ON public.adm_cliente_contacto(company_id);

-- adm_proveedor_contacto
ALTER TABLE public.adm_proveedor_contacto ADD COLUMN company_id bigint;
UPDATE public.adm_proveedor_contacto c
SET company_id = prov.company_id
FROM public.adm_proveedor prov
WHERE c.proveedor_id = prov.proveedor_id;
ALTER TABLE public.adm_proveedor_contacto
    ALTER COLUMN company_id SET NOT NULL,
    ADD CONSTRAINT fk_adm_proveedor_contacto_company FOREIGN KEY (company_id) REFERENCES public.cfg_company(company_id);
CREATE INDEX IF NOT EXISTS ix_adm_proveedor_contacto_company ON public.adm_proveedor_contacto(company_id);

-- adm_reporte_* (parametro, programacion, ejecucion)
ALTER TABLE public.adm_reporte_parametro ADD COLUMN company_id bigint;
UPDATE public.adm_reporte_parametro p SET company_id = r.company_id FROM public.adm_reporte r WHERE p.reporte_id = r.reporte_id;
ALTER TABLE public.adm_reporte_programacion ADD COLUMN company_id bigint;
UPDATE public.adm_reporte_programacion p SET company_id = r.company_id FROM public.adm_reporte r WHERE p.reporte_id = r.reporte_id;
ALTER TABLE public.adm_reporte_ejecucion ADD COLUMN company_id bigint;
UPDATE public.adm_reporte_ejecucion e SET company_id = r.company_id FROM public.adm_reporte r WHERE e.reporte_id = r.reporte_id;
ALTER TABLE public.adm_reporte_parametro ALTER COLUMN company_id SET NOT NULL;
ALTER TABLE public.adm_reporte_programacion ALTER COLUMN company_id SET NOT NULL;
ALTER TABLE public.adm_reporte_ejecucion ALTER COLUMN company_id SET NOT NULL;
ALTER TABLE public.adm_reporte_parametro ADD CONSTRAINT fk_adm_reporte_parametro_company FOREIGN KEY (company_id) REFERENCES public.cfg_company(company_id);
ALTER TABLE public.adm_reporte_programacion ADD CONSTRAINT fk_adm_reporte_programacion_company FOREIGN KEY (company_id) REFERENCES public.cfg_company(company_id);
ALTER TABLE public.adm_reporte_ejecucion ADD CONSTRAINT fk_adm_reporte_ejecucion_company FOREIGN KEY (company_id) REFERENCES public.cfg_company(company_id);
CREATE INDEX IF NOT EXISTS ix_adm_reporte_parametro_company ON public.adm_reporte_parametro(company_id);
CREATE INDEX IF NOT EXISTS ix_adm_reporte_programacion_company ON public.adm_reporte_programacion(company_id);
CREATE INDEX IF NOT EXISTS ix_adm_reporte_ejecucion_company ON public.adm_reporte_ejecucion(company_id);

-- adm_cxc_movimiento
ALTER TABLE public.adm_cxc_movimiento ADD COLUMN company_id bigint;
UPDATE public.adm_cxc_movimiento m
SET company_id = cli.company_id
FROM public.adm_cliente cli
WHERE m.cliente_id = cli.cliente_id;
ALTER TABLE public.adm_cxc_movimiento
    ALTER COLUMN company_id SET NOT NULL,
    ADD CONSTRAINT fk_adm_cxc_mov_company FOREIGN KEY (company_id) REFERENCES public.cfg_company(company_id);
CREATE INDEX IF NOT EXISTS ix_adm_cxc_mov_company ON public.adm_cxc_movimiento(company_id);

-- adm_cxp_movimiento
ALTER TABLE public.adm_cxp_movimiento ADD COLUMN company_id bigint;
UPDATE public.adm_cxp_movimiento m
SET company_id = prov.company_id
FROM public.adm_proveedor prov
WHERE m.proveedor_id = prov.proveedor_id;
ALTER TABLE public.adm_cxp_movimiento
    ALTER COLUMN company_id SET NOT NULL,
    ADD CONSTRAINT fk_adm_cxp_mov_company FOREIGN KEY (company_id) REFERENCES public.cfg_company(company_id);
CREATE INDEX IF NOT EXISTS ix_adm_cxp_mov_company ON public.adm_cxp_movimiento(company_id);

-- adm_interes_mora
ALTER TABLE public.adm_interes_mora ADD COLUMN company_id bigint;
UPDATE public.adm_interes_mora m
SET company_id = cli.company_id
FROM public.adm_cliente cli
WHERE m.cliente_id = cli.cliente_id;
ALTER TABLE public.adm_interes_mora
    ALTER COLUMN company_id SET NOT NULL,
    ADD CONSTRAINT fk_adm_interes_mora_company FOREIGN KEY (company_id) REFERENCES public.cfg_company(company_id);
CREATE INDEX IF NOT EXISTS ix_adm_interes_mora_company ON public.adm_interes_mora(company_id);
```

### 2.7 Inventarios (08_inventarios_core.sql)

Script listo para despliegue: `migrations/inventarios/20250112_add_company_id_core.sql`.

```sql
ALTER TABLE public.inv_existencia ADD COLUMN company_id bigint;
UPDATE public.inv_existencia e
SET company_id = alm.company_id
FROM public.inv_almacen alm
WHERE e.almacen_id = alm.almacen_id;
ALTER TABLE public.inv_existencia
    ALTER COLUMN company_id SET NOT NULL,
    ADD CONSTRAINT fk_inv_existencia_company FOREIGN KEY (company_id) REFERENCES public.cfg_company(company_id);
CREATE INDEX IF NOT EXISTS ix_inv_existencia_company ON public.inv_existencia(company_id);

ALTER TABLE public.inv_movimiento_linea ADD COLUMN company_id bigint;
UPDATE public.inv_movimiento_linea l
SET company_id = mov.company_id
FROM public.inv_movimiento mov
WHERE l.movimiento_id = mov.movimiento_id;
ALTER TABLE public.inv_movimiento_linea
    ALTER COLUMN company_id SET NOT NULL,
    ADD CONSTRAINT fk_inv_movimiento_linea_company FOREIGN KEY (company_id) REFERENCES public.cfg_company(company_id);
CREATE INDEX IF NOT EXISTS ix_inv_movimiento_linea_company ON public.inv_movimiento_linea(company_id);
```

### 2.8 Activos fijos (09_activos_fijos_core.sql)

Script listo para despliegue: `migrations/activos_fijos/20250112_add_company_id_core.sql`.

```sql
ALTER TABLE public.af_depreciacion ADD COLUMN company_id bigint;
UPDATE public.af_depreciacion d
SET company_id = act.company_id
FROM public.af_activo act
WHERE d.activo_id = act.activo_id;
ALTER TABLE public.af_depreciacion
    ALTER COLUMN company_id SET NOT NULL,
    ADD CONSTRAINT fk_af_depreciacion_company FOREIGN KEY (company_id) REFERENCES public.cfg_company(company_id);
CREATE INDEX IF NOT EXISTS ix_af_depreciacion_company ON public.af_depreciacion(company_id);

ALTER TABLE public.af_baja ADD COLUMN company_id bigint;
UPDATE public.af_baja b
SET company_id = act.company_id
FROM public.af_activo act
WHERE b.activo_id = act.activo_id;
ALTER TABLE public.af_baja
    ALTER COLUMN company_id SET NOT NULL,
    ADD CONSTRAINT fk_af_baja_company FOREIGN KEY (company_id) REFERENCES public.cfg_company(company_id);
CREATE INDEX IF NOT EXISTS ix_af_baja_company ON public.af_baja(company_id);
```

## 3. Coordinación de datos, reportes y servicios

1. **Orden de despliegue**: ejecutar módulos en secuencia (Configuración → Contabilidad → Ventas → Compras → Bancos → Administración → Inventarios → Activos fijos). Los archivos ya creados en `Database/ddl_v2/migrations/<módulo>/20250112_add_company_id_*.sql` siguen el versionado incremental y deben ejecutarse individualmente dentro de una transacción.
2. **Scripts de datos**: 
   - Generar respaldos `pg_dump` por tabla antes de tocar columnas.
   - Para ambientes multiempresa, validar que los `JOIN` propuestos no dupliquen filas. Si existen registros huérfanos (sin cabecera), registrar en `Database/ddl_v2/logs/2025-xx-company-id.csv` y resolver manualmente.
   - Registrar en `Seeds_v2` scripts de inicialización que ahora requieran `company_id` (por ejemplo, `cfg_currency`).
3. **Validaciones para DevExpress Reports**:
   - Actualizar los `ObjectDataSource` usados en `SIAD.Reports` para agregar el filtro `company_id = @CompanyId` y comprobar en el diseñador DevExpress 25.1 que el nuevo parámetro está marcado como requerido.
   - Regenerar `XRReports` relacionados a ventas/compras/administrativo usando el Preview Data con el mismo `company_id` para comprobar que los datasets incorporan la nueva columna.
   - Ejecutar los reportes automatizados desde `Prestadoras/SIAD.Reports` con `dotnet test /p:DXVersion=25.1` validando que los `ObjectDataSource` actualizados siguen resolviendo datos.
4. **Servicios Blazor WebAssembly (SIAD.Services / SIAD.Core)**:
   - Exponer el `company_id` del contexto actual (propagado desde el JWT o selección UI) y anexarlo a todos los comandos SQL generados por Dapper/EF Core.
   - Actualizar los DTOs compartidos (`SIAD.Core/DTOs/*`) agregando la propiedad `long CompanyId { get; set; }` donde corresponda; realizar migración de AutoMapper para mapear la nueva propiedad.
   - Ajustar validaciones de UI (DevExpress DataGrid, FormLayout) para incluir `company_id` como campo oculto pero obligatorio al momento de crear/editar registros dependientes.
   - Validar en `SIAD.Services` (REST y SignalR) que todos los repositorios aplican `company_id` en sus filtros y devuelven 403 cuando el usuario intenta acceder a otra empresa.
5. **Pruebas regresivas**:
   - Ejecutar suites de integración existentes (si aplica) enfocadas en ciclo Ventas→Cobros, Compras→Pagos e Inventarios→Contabilidad verificando que `company_id` viaja por toda la transacción.
   - Validar que `company_id` se incluye en auditorías (`created_by`, `updated_by`) y que filtros multiempresa funcionan en componentes reutilizables (Lookup DevExpress y reportes).

Con este plan queda cubierto el inventario completo, las migraciones incrementales por módulo y la coordinación requerida para asegurar que la solución DevExpress/Blazor WebAssembly opere sobre la estructura multiempresa.
