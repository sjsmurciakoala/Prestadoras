# Estado actual de Clientes (Blazor + DevExpress)

## Resumen funcional
- Cliente demo `CLI-DEMO-001` sembrado con tarifas, movimientos y consumos.  
  Script: `Database/2025-10-18_seed_cliente_demo.sql`  
  Ejecucion: `psql -h 3.208.232.209 -U postgres -d bdnes -f Database/2025-10-18_seed_cliente_demo.sql`.
- UI (`apc.Client/Pages/Clientes/ClienteDetail.razor`) con pestanas Datos generales, Tarifas, Estado de cuenta, Solicitudes, Medidores y Auxiliar de Lectura.
- Servicios listos:
  - `ClientesService` (listado, detalle, filtros, tarifas, estado de cuenta, movimientos).
  - `SolicitudesService` (listado por identidad, detalle, alta y catalogos).
  - `MedidoresService` (busqueda, asignacion, historial, lecturas sin medidor).
  - `AuxiliarLecturaService` (periodos, cierre, eliminacion, carga masiva).
- API disponible (`apc/Controllers/*`):
  - `ClientesController`: `GET /api/clientes*`, `GET /api/clientes/{id}/tarifas`, `/estado-cuenta`, `/movimientos`.
  - `SolicitudesController`: `GET /api/solicitudes`, `/api/solicitudes/{id}`, `POST /api/solicitudes`.
  - `MedidoresController`: `GET /api/medidores`, `/api/medidores/{id}`, `/historial`, `POST /api/medidores/asignar`, `POST /api/medidores/lecturas-sin-medidor`.
  - `AuxiliarLecturaController`: `GET /api/auxiliarlectura*`, `POST /api/auxiliarlectura`, `/cierre`, `/masivo`, `DELETE /api/auxiliarlectura`.
- Documentacion viva:
  - `docs/modulo_clientes.md`
  - `docs/modulo_solicitudes.md`
  - `docs/modulo_medidores.md`
  - `docs/modulo_auxiliar_lectura.md`

## Como validar la semilla
1. Ejecutar el script indicado.
2. `dotnet run --project apc` y navegar a `https://localhost:5001/clientes`.
3. Abrir **Cliente Demo Blazor**:
   - pestana *Tarifas* -> *Cargar tarifas* (grilla con catalogo demo).
   - pestana *Estado de cuenta* -> *Cargar estado de cuenta* y *Cargar movimientos*.
   - pestana *Auxiliar de Lectura* -> aplicar filtros y probar generar/cerrar periodo, carga masiva.

## Proximo bloque sugerido
- Auxiliar de Lectura completado.
- Inventario de **Ordenes de trabajo/corte** replicado en `docs/modulo_ordenes.md` (fuentes en `modulo_ordenesdecorte/`).  
  Antes de implementar se necesita:
  - Confirmar el DDL real de `orden_trabajo` y tablas relacionadas (`orden_trabajo_detalle`, `orden_trabajo_adjunto`, `usuarios_miorden`, `coordenadas_empleado`).
  - Documentar los procedimientos/funciones usados (`sp_obtener_orden_trabajo`, `fn_try_update_ot`, `sp_insertar_orden`, etc.).
  - Definir seeds demo (ordenes, cuadrillas, usuarios movil, ubicaciones).
  - Alinearse con UX esperado (listados por tipo, asignaciones, mapa en tiempo real).
- **Rutas/Cuadrillas** ya dispone de CRUD completo (ver `docs/modulo_rutas.md`). Pendiente integrar asignacion de cuadrillas/geocercas y validar dependencias con Auxiliar/Ordenes antes de ampliar el modelo.

## Notas
- Las advertencias CS8981 y WASM0001 provienen del scaffold EF/DevExpress y se aceptan como conocido.
- Manten este documento sincronizado cuando se cierre el seed de medidores restante o cuando Ordenes avance a desarrollo activo.

---

# Estado actual de Captación de Pagos (Blazor + DevExpress)

## Resumen funcional
- Código legado migrado a la solución oficial:
  - DTOs: `SIAD.Core/DTOs/CaptacionPagos/CaptacionPagosDtos.cs`.
  - Servicio EF Core: `SIAD.Services/CaptacionPagos/CaptacionPagosService.cs` (cajas, arqueos, consulta, alta manual y reverso).
  - API: `apc/Controllers/CaptacionPagosController.cs` con endpoints para catálogo de cajas, arqueos, detalle de pago, registro y reverso.
  - Cliente WASM: `apc.Client/Services/CaptacionPagos/CaptacionPagosClient.cs`.
  - UI: `apc.Client/Pages/Facturacion/CaptacionPagos/Caja.razor` (listado/arquéo/registro) y `Reverso.razor` (consultar y reversar pagos, misceláneos).
- Documentación viva:
  - `docs/modulo_facturacion.md` (sección Captación).
  - `modulo_captacionpagos/modulo_captacion_pagos.md` (inventario, checklist y pruebas manuales).

## Semillas y datos mínimos
- Script: `Database/Seeds/2025-10-23_captacion_pagos.sql`.
- El bloque DO detecta automáticamente si la tabla `pagos_dtl` usa las columnas `monto`, `montovalor` o `monto_valor` (y si no existen las crea) antes de insertar el detalle, evitando errores por diferencias de esquema.
- Ejecución sugerida:
  ```
  psql -h <host> -U <user> -d bdnes -f Database/Seeds/2025-10-23_captacion_pagos.sql
  ```

## Cómo validar
1. Levantar `apc` (`dotnet run --project apc`) y navegar a `https://localhost:5001/facturacion/captacion/caja`.
2. Confirmar:
   - Grilla de arqueos carga usando filtros de caja y fechas (los filtros normalizan a UTC para evitar errores Npgsql).
   - Registro rápido genera el pago y refresca el arqueo.
   - Consulta muestra header + detalle del pago recién creado.
3. Navegar a `https://localhost:5001/facturacion/captacion/reverso`:
   - Buscar la factura creada, revisar que aparezca el detalle.
   - Ejecutar reverso y validar mensaje de éxito + actualización de estado.
   - Revisar listado de misceláneos (filtrar por clave si es necesario).

## Pendientes / próximos pasos
- Registrar capturas y resultados de pruebas en `modulo_captacionpagos/modulo_captacion_pagos.md`.
- Integrar lector óptico y flujos misceláneos avanzados cuando se confirmen dependencias (sp/funciones legacy).
- Coordinar con Cobranza para reutilizar reportes/recibos una vez que se porten a DevExpress.

---

# Estado actual de Notas y Cobranza (Blazor + DevExpress)

## Resumen funcional
- Notas crédito/débito migradas end-to-end:
  - DTOs `SIAD.Core/DTOs/NotasCreditoDebito`.
  - Servicio EF `NotasCreditoDebitoService` con validaciones de saldo y escritura en `ajustes`, `ajustes_detalle` y `transaccion_abonado`.
  - API `api/facturacion/notas/*`.
  - Cliente WASM + UI DevExpress `/facturacion/notas` con buscador de clientes, catálogo de conceptos y sumatoria dinámica.
  - Datos demo en `Database/Seeds/2025-11-12_notas_cobranza.sql`.
- Cobranza de planes de pago:
  - DTOs `SIAD.Core/DTOs/Cobranza`.
  - Servicio EF `CobranzaService` (saldos, bloqueo, cálculo, persistencia y consulta).
  - API `api/cobranza/*`.
  - Cliente WASM + UI `/facturacion/cobranza` con pestañas para creación y consulta.
  - Seed compartido con plan correlativo `000101`.

## Cómo validar
1. Ejecutar el seed `Database/Seeds/2025-11-12_notas_cobranza.sql` (requiere `CLI-DEMO-001`).
2. Levantar `apc` (`dotnet run --project apc`).
3. Navegar a `/facturacion/notas`:
   - Buscar *Cliente Demo Blazor* y cargar conceptos.
   - Agregar montos y registrar nota, validar toast de confirmación.
4. Navegar a `/facturacion/cobranza`:
   - Buscar el mismo cliente, revisar saldos y generar vista previa.
   - Guardar plan (usa recibo 101 del seed) y verificar que aparezca en la pestaña de consulta.

## Recomendaciones y pendientes
- **Integridad**: crear `UNIQUE (codigo)` en `causa_refacturacion` y `FK` opcionales desde `transaccion_abonado.docufuente` hacia `ajustes`/`cln_plan_pago_hdr` para rastrear origen de movimientos.
- **Índices**: agregar índice compuesto `transaccion_abonado (cliente_clave, tipotransaccion)`; hoy cada nota y plan buscan la transacción `101` secuencialmente.
- **Reportes**: portar `RptPlanPago`, `RptPagare` y `RptReciboPrima` a DevExpress o generar equivalentes en PDF.
- **Validaciones automáticas**: agendar pruebas unitarias para `NotasCreditoDebitoService` (cálculo de saldo y bloqueo por recibo inexistente) y `CobranzaService` (flujo de prima y saldo acumulado).
