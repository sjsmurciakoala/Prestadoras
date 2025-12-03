# Modulo Ordenes de trabajo / corte

## Objetivo
Migrar el flujo legacy de ordenes (agua, alcantarillado, corte y ubicacion en tiempo real) desde `ASPNET_Core_3` hacia la solucion Blazor/DevExpress (`apc`, `SIAD.*`) manteniendo asignaciones, seguimiento de cuadrillas y registro de adjuntos.

## Estado 2025-11-12

El módulo ya se encuentra desarrollado en la solución principal:

| Componente | Ruta |
| --- | --- |
| DTOs | `SIAD.Core/DTOs/Ordenes/*` |
| Servicio + contrato | `SIAD.Services/Ordenes/IOrdenesService.cs`, `SIAD.Services/Ordenes/OrdenesService.cs` |
| Registro DI | `SIAD.Services/ServiceRegistration.cs` (`services.AddScoped<IOrdenesService, OrdenesService>();`) |
| API REST | `apc/Controllers/OrdenesController.cs` |
| Cliente WASM | `apc.Client/Services/Ordenes/OrdenesClient.cs` |
| Páginas DevExpress | `apc.Client/Pages/Ordenes/OrdenesList.razor`, `apc.Client/Pages/Ordenes/OrdenTrabajoDetail.razor` |
| Navegación | `apc.Client/Layout/NavMenu.razor` ya incluye `/ordenes` |

El flujo soporta:
- Listado con filtros (tipo/estado/rango de fechas) de órdenes de agua, alcantarillado y corte.
- Vista detalle con propietario, materiales, adjuntos e historial.
- Creación de orden (`POST /api/ordenes`) y asignación a cuadrillas (`POST /api/ordenes/{numero}/asignaciones`).
- Búsqueda de tipos (catálogo `tipo_d`), propietarios (`cliente_maestro/detalle`), estados (`orden_trabajo_estado`) y usuarios `miorden`.
- Mapa con coordenadas de cuadrillas (`coordenadas_empleado`).

> Para probar rápidamente el flujo se añadió el seed `Database/Seeds/2025-11-20_ordenes_corte.sql`, que genera órdenes demo (agua y corte), adjuntos, materiales y coordenadas. Ejecutarlo después de los seeds de clientes, usuarios `miorden` y estados.

Lo que sigue pendiente es validar estos componentes contra el DDL definitivo de `orden_trabajo` y funciones legacy. Si cambian nombres/columnas deberemos ajustar el `DbContext` y las consultas EF antes de ir a producción.

## Inventario heredado
Los archivos relevantes fueron copiados a `Prestadoras/modulo_ordenesdecorte/`:

| Componente | Ruta legacy | Notas |
| --- | --- | --- |
| Controlador MVC | `Areas/OrdenesArea/Controllers/OrdenesController.cs` | Acciones para listas (agua/alcantarillado/corte), detalle, asignacion, creacion de orden, ubicacion, registro de pagos. |
| Repositorio / contrato | `Repositories/Repositorios/OrdenesRepository.cs`, `IOrdenesRepository.cs` | Dapper + procedimientos `sp_obtener_orden_trabajo`, funcion `fn_try_update_ot`, consultas en `Helpers/Queries/OrdenesQuery.cs`, `OrdenesdePagoQuery.cs`. |
| Vistas Razor | `Views/Ordenes/*.cshtml` | `OrdenesTrabajoAgua`, `OrdenesAlcantarillado`, `OrdenesCorte`, `AsignarOrdenes`, `AgregarUsuario`, `RegistroOrdenesDePago`, `UbicacionRealOrdenes`, `CrearOrden`. |
| ViewModels/DTOs | `Models/OrdenesViewModelPostgres.cs`, `RegistroOrdenesDePagoDTO.cs`, clases embebidas (`AgregarOrdenesUsuario`, `AsignarOrdenesViewModel`, etc.). |
| Scripts de BD | `Database/TablasPosgrets/ordent_mate.sql`, `orden_trabajo_adjunto.sql`, `coordenadas_empleado.sql`, `usuarios_miorden.sql`, `orden_trabajo.sql` (duplicado de `ops_compromisos`, requiere correccion). |

## Resumen funcional legacy
- **Ordenes Agua / Alcantarillado / Corte**  
  Grids filtrables (DataTables) alimentados por `ObtenerOrdenesTrabajoPostgres`, `ObtenerOrdenesTrabajoAlcantarilladoPostgres` y `ObtenerOrdenesCortePostgres`. Cada fila abre `VerOrden` (detalle con datos del cliente).
- **Asignar ordenes**  
  Mapas y popups para elegir usuario `miorden` (cuadrillas) y asignar usando `fn_try_update_ot`.
- **Crear orden**  
  Formulario que obtiene datos del propietario (`ObtenerInformacionPropietarioPostgres`), tipos (`tipo_d`) y empleados (`usuarios_miorden`); inserta en `orden_trabajo` y genera el numero correlativo.
- **Ubicacion en tiempo real**  
  `UbicacionRealOrdenes.cshtml` renderiza un mapa con coordenadas almacenadas en `coordenadas_empleado`.
- **Registro de ordenes de pago**  
  Vista `RegistroOrdenesDePago.cshtml` consume `OrdenesdePagoQuery` para mostrar compromisos/ordenes financieras.

## Dependencias de datos identificadas
- Tablas: `orden_trabajo`, `orden_trabajo_adjunto`, `ordent_mate`, `usuarios_miorden`, `coordenadas_empleado`, `cliente_maestro`, `cliente_detalle`, `tipo_d`.
- Procedimientos/funciones: `sp_obtener_orden_trabajo(tipo)`, `sp_insertar_orden_trabajo`, `fn_try_update_ot(orden, empleado)`, `sp_obtener_ordenes_pago`, `sp_obtener_usuario_miorden`.
- Catalogos: tipos de orden (`tipo_d.depto_appmitrabajo = 'OT'`), usuarios de cuadrilla (app Mi Orden), propietarios (clientes), materiales (productos).

## Informacion pendiente antes de codificar
1. **DDL real de `orden_trabajo`** (el script en `Database/TablasPosgrets/orden_trabajo.sql` es incorrecto). Se debe exportar desde la base real (`pg_dump -t orden_trabajo`) junto con cualquier tabla relacionada (`orden_trabajo_detalle`, `orden_trabajo_evento`, etc.).
2. **Definicion de stored procedures / funciones** (`sp_obtener_orden_trabajo`, `fn_try_update_ot`, `sp_insertar_orden_trabajo`, `sp_obtener_orden_trabajo_pago`). Documentar parametros, consultas internas y efectos secundarios.
3. **Reglas de negocio por tipo** (agua, alcantarillado, corte): estados validos, transiciones, dependencia de facturacion/cobranza.
4. **Requerimientos de UX** para la nueva UI Blazor: filtros obligatorios, columnas por tipo, comportamiento del mapa de ubicaciones y popups de asignacion.
5. **Dataset demo**: definir un seed `Database/2025-10-30_seed_ordenes_trabajo.sql` con ordenes de cada tipo, usuarios `miorden` activos, coordenadas historicas, adjuntos e imagenes de ejemplo.

Sin esta informacion no se puede reproducir la logica de asignacion ni validar la creacion de ordenes en la nueva capa EF Core.

## Plan de migracion
1. **DTOs (`SIAD.Core/DTOs/Ordenes/`)**
   - `OrdenTrabajoListItemDto`, `OrdenTrabajoDetailDto`, `OrdenTrabajoOwnerDto`.
   - `OrdenTrabajoFilterDto` (tipo, fecha, ciclo, estado, clave).
   - `CrearOrdenDto`, `AsignarOrdenDto`, `OrdenAdjuntoDto`, `OrdenMaterialDto`, `UsuarioMiOrdenDto`, `CoordenadaDto`.
2. **Servicio (`SIAD.Services/Ordenes/`)**
   - `IOrdenesService` / `OrdenesService` usando `SiadDbContext`.
   - Query EF para tablas y vistas relevantes.
   - Reemplazar la logica de `OrdenesRepository`: listar, ver detalle, crear, asignar, consultar usuarios, traer coordenadas, registrar adjuntos.
3. **API (`apc/Controllers/OrdenesController.cs`)**
   - `GET /api/ordenes` (filtros).
   - `GET /api/ordenes/{numero}`.
   - `POST /api/ordenes` (crear).
   - `POST /api/ordenes/{numero}/asignaciones`.
   - `GET /api/ordenes/usuarios`, `/api/ordenes/tipos`, `/api/ordenes/propietarios`, `/api/ordenes/coordenadas`.
4. **Cliente Blazor (`apc.Client`)**
   - Crear `Services/Ordenes/OrdenesClient.cs`.
   - Paginas: `Pages/Ordenes/OrdenesList.razor` (tabs por tipo, filtros, popup ver orden) y `Pages/Ordenes/OrdenDetail.razor`.
   - Componentes para crear/asignar orden y para el mapa de ubicaciones.
   - Actualizar `NavMenu.razor`, `Routes.razor` y registrar el servicio HTTP.
5. **Seeds y scripts**
   - Corregir `orden_trabajo.sql`.
   - Agregar seed con datos demo (ordenes + usuarios + coordenadas + adjuntos).
   - Documentar comandos `psql` para ejecutarlos.
6. **Pruebas**
   - Unit tests para `OrdenesService`.
   - Pruebas manuales similares a las vistas legacy.

## Checklist
- [ ] Exportar DDL real de tablas de ordenes y subirlo a `Database/TablasPosgrets`.
- [ ] Documentar procedimientos/funciones y parametrizacion.
- [ ] Crear DTOs en `SIAD.Core`.
- [ ] Implementar `IOrdenesService` y registrarlo.
- [ ] Construir API REST en `apc`.
- [ ] Disenar UI Blazor (listas, detalle, crear, asignar, mapa).
- [ ] Preparar seed `2025-10-30_seed_ordenes_trabajo.sql`.
- [ ] Validar flujo end-to-end y actualizar este documento con resultados.

## Preguntas abiertas
1. Existe integracion con dispositivos moviles (app Mi Orden)? Si depende de una API adicional, documentar contratos y credenciales.
2. Como se calcula el numero de orden (correlativo por tipo, por ciclo o global)? Necesitamos confirmar la regla para reproducirla.
3. Las ordenes de pago interactuan con modulos financieros (compromisos, proveedores)? Determinar si forman parte del alcance inmediato.
4. El mapa de ubicacion real lee coordenadas en vivo (SignalR) o solo consulta `coordenadas_empleado`? Definir estrategia en Blazor.

Capturar estas respuestas antes de iniciar el desarrollo nos permite mantener alineado el plan de migracion con el legado y evitar retrabajo.
