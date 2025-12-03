## Facturación y Cobranza – Plan de migración

> Objetivo: portar los módulos de captación de pagos, facturación miscelánea, notas crédito/débito y cobranza del proyecto legado `ASPNET_Core_3` hacia la solución Blazor/DevExpress (`apc`, `SIAD.*`).
>
> Estado (2025-11-11): Captación de pagos ya vive en la solución principal (`SIAD.Core/DTOs/CaptacionPagos`, `SIAD.Services/CaptacionPagos`, `apc/Controllers/CaptacionPagosController`, `apc.Client/Pages/Facturacion/CaptacionPagos/*`, `Database/Seeds/2025-10-23_captacion_pagos.sql`). Registrar cualquier validación adicional en `modulo_captacionpagos/modulo_captacion_pagos.md`.
>
> **Actualización 2025-11-11:** Se migró el flujo de *Facturación Miscelánea* respetando la estructura vigente:
> - DTOs compartidos: `SIAD.Core/DTOs/Common/ResponseModelDto.cs` y `SIAD.Core/DTOs/FacturacionMiscelaneos/*`.
> - Servicio de dominio: `SIAD.Services/FacturacionMiscelaneos/FacturacionMiscelaneosService.cs` registrado vía `ServiceRegistration`.
> - API: `apc/Controllers/FacturacionMiscelaneosController.cs`.
> - Cliente + UI DevExpress: `apc.Client/Services/Facturacion/FacturacionMiscelaneosClient.cs` y `apc.Client/Pages/Facturacion/Miscelaneos/Facturacion.razor` (menú principal actualizado).
> - Semilla dummy `Database/Seeds/2025-11-11_facturacion_miscelaneos.sql` basada en el cliente `CLI-DEMO-001`.
> - El legado MVC (`modulo_facturacion/*`) permanece únicamente como referencia funcional.

---

### 1. Inventario del legado

| Módulo | Controlador/Área | Alcance resumido | Dependencias (tablas/vistas/consultas) |
| --- | --- | --- | --- |
| Captación de pagos | `Areas/CaptacionPagosArea/CaptacionPagosController` (confirmar nombre exacto) | Recepción de pagos (lector óptico + manual), reversos, catálogo de cajas | `pagos_hdr/dtl`, `pagos_miscelaneos`, funciones de lector, queries Dapper |
| Facturación misceláneos | `Areas/FacturacionMiscelaneosArea/FacturacionMiscelaneosController` | Emisión de recibos misceláneos, búsqueda por código, catálogos auxiliares | `factura_miscelaneos_hdr/dtl`, catálogos de conceptos/tasas |
| Notas crédito/débito | `Areas/NotasCreditoDebitoArea/NotasCreditoDebitoController` | Registro de notas, validación de tasas/configuraciones | `notas_credito`, `notas_debito`, vistas de configuración por cliente |
| Cobranza | `Areas/CobranzaArea/CobranzaController` | Planes de pago, acciones de cobranza, bloqueo de cuentas, reportes | `planes_pago_hdr/dtl`, `acciones_cobranza`, `Helpers/Queries/CobranzaQuery.cs`, RDLC `RptPlanPago`, `RptPagare`, `RptReciboPrima` |

> Nota: Los nombres exactos se confirmarán con un listado del directorio `ASPNET_Core_3/Areas`.

---

### 2. Secuencia de migración propuesta

1. **Captación de pagos**
   - Scaffold de tablas base relacionadas con pagos y reversos.
   - Servicio `ICaptacionPagosService` + implementación.
   - Endpoints `CaptacionPagosController` en `apc`.
   - Componentes Blazor (pantalla de caja, reverso).
   - Análisis de reporte/constancia si aplica.
2. **Facturación miscelánea**
   - Scaffold `factura_miscelaneos` y catálogos.
   - Servicio `IFacturacionMiscelaneosService`.
   - API + vista Blazor (emisión y consulta).
3. **Notas crédito/débito**
   - Scaffold entidades notas.
   - Servicio `INotasCreditoDebitoService`.
   - UI + API para registro/aprobación.
   - Verificar reportes o comprobantes.
4. **Cobranza**
   - Scaffold `planes_pago`, `acciones_cobranza`, tablas auxiliares.
   - Servicios para planes y acciones (`ICobranzaService`).
   - Endpoints y UI (plan de pago, gestión de acciones).
   - Portar reportes `RptPlanPago`, `RptPagare`, `RptReciboPrima` a DevExpress.

---

### 3. Checklist por módulo

Para cada uno de los cuatro bloques repetir:

1. **Scaffold**: comando `dotnet tool run dotnet-ef dbcontext scaffold` documentado aquí.
2. **DTOs** en `SIAD.Core/DTOs/<Módulo>`.
3. **Servicio** en `SIAD.Services/<Módulo>` + registro en `ServiceRegistration.cs`.
4. **API** en `apc/Controllers`.
5. **UI** en `apc.Client/Pages`.
6. **Reportes** (si aplica) en `SIAD.Reports`.
7. **Seeds** (scripts SQL en `Database/Seeds`) + documentación.
8. **Pruebas manuales** y anotaciones de resultados/pending fixes.

#### Evidencias Facturación Miscelánea (2025-11-11)

- [x] DTOs + ResponseModel compartido (`SIAD.Core/DTOs/Common`, `SIAD.Core/DTOs/FacturacionMiscelaneos`).
- [x] Servicio `IFacturacionMiscelaneosService` + implementación EF (`SIAD.Services/FacturacionMiscelaneos`).
- [x] Endpoints `api/facturacion/miscelaneos/*` en `apc`.
- [x] Cliente HTTP y página DevExpress `/facturacion/miscelaneos` con búsqueda de clientes, selección de catálogo y generación de recibo.
- [x] Script dummy `Database/Seeds/2025-11-11_facturacion_miscelaneos.sql` (usa `CLI-DEMO-001`).
- [ ] Pendiente: migrar reportes RDLC (recibo impreso) al paquete DevExpress de reportes.
- [ ] Pendiente: pruebas automatizadas del servicio (incluir casos de acumulación de saldo y validaciones de catálogo).

**Semilla demo**

```bash
psql "host=<host> user=<user> dbname=bdnes password=<pwd>" -f Database/Seeds/2025-11-11_facturacion_miscelaneos.sql
```

El script crea/actualiza dos conceptos en `miscelaneos_catalogo`, genera un recibo tipo "R" con referencia `MIS-DEMO-SEED` y mueve el saldo del cliente `CLI-DEMO-001`. Ejecutar después del seed `2025-10-18_seed_cliente_demo.sql` para garantizar la existencia del cliente.

#### Evidencias Notas Crédito/Débito (2025-11-12)

- [x] DTOs dedicados `SIAD.Core/DTOs/NotasCreditoDebito/*`.
- [x] Servicio EF `SIAD.Services/NotasCreditoDebito/NotasCreditoDebitoService` (búsqueda, motivos y registro de notas actualizando `ajustes`, `ajustes_detalle` y `transaccion_abonado`).
- [x] API `apc/Controllers/NotasCreditoDebitoController`.
- [x] Cliente WASM `apc.Client/Services/Facturacion/NotasCreditoDebitoClient`.
- [x] UI `/facturacion/notas` (`apc.Client/Pages/Facturacion/Notas/NotasCreditoDebito.razor`) con selector de conceptos y sumatoria automática.
- [x] Seed `Database/Seeds/2025-11-12_notas_cobranza.sql` genera motivo demo y nota para `CLI-DEMO-001`.

#### Evidencias Cobranza (2025-11-12)

- [x] DTOs `SIAD.Core/DTOs/Cobranza/*`.
- [x] Servicio EF `SIAD.Services/Cobranza/CobranzaService` (saldos, bloqueo, vista previa y persistencia de planes).
- [x] API `apc/Controllers/CobranzaController`.
- [x] Cliente WASM `apc.Client/Services/Facturacion/CobranzaClient`.
- [x] UI `/facturacion/cobranza` (`apc.Client/Pages/Facturacion/Cobranza/Cobranza.razor`) con pestañas para registro y consulta.
- [x] Seed `Database/Seeds/2025-11-12_notas_cobranza.sql` crea plan demo correlativo `000101`.

**Recomendaciones de modelo**

1. Declarar `UNIQUE (codigo)` en `causa_refacturacion` para evitar motivos duplicados al ejecutar seeds.
2. Agregar `FOREIGN KEY` opcional desde `transaccion_abonado.docufuente` hacia `ajustes.documento` y `cln_plan_pago_hdr.id` (hoy el campo sólo se usa como referencia libre).
3. Crear un índice compuesto `transaccion_abonado (cliente_clave, tipotransaccion)` para acelerar la búsqueda de la transacción `101` utilizada como recibo base en notas y cobranza.

---

### 4. Próximas acciones inmediatas

1. Listar contenido de `ASPNET_Core_3/Areas` y confirmar nombres de controladores para cada bloque.
2. Levantar inventario detallado de entidades/tablas necesarias para **Captación de pagos**.
3. Ejecutar el primer scaffold parcial y registrar el comando aquí.
4. Crear interfaz/servicio base (`ICaptacionPagosService`) con el primer método (p.ej. `ListarPagosAsync`).
5. Exponer endpoint GET en `CaptacionPagosController` y validar en Postman/Swagger.
6. Actualizar esta documentación después de cada paso.

---

### 5. Referencias

- `readme_inventario.md` – mapa general del legado.
- `README_estado_actual.md` – estado y pendientes de módulos ya migrados.
- Scripts SQL: revisar `ASPNET_Core_3/INSERTS`, `FUNCTIONS`, `bdnes.sql` para dependencias financieras.

