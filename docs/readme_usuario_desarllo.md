# README para Desarrollo – Módulo de Órdenes

## 1. Arquitectura general
- **Front-end**: `apc.Client/Pages/Ordenes/OrdenesList.razor`
  - Tabs, filtros, grilla y popups en un solo componente Razor.
  - Estilos locales para plantillas de combos y badges de estado.
- **Servicios cliente**: `apc.Client/Services/OrdenesClient.cs`
  - Wrapper sobre la API REST (`api/ordenes/*`).
- **Backend**: `SIAD.Services/Ordenes/OrdenesService.cs`
  - Reglas de negocio, consultas EF Core y validaciones.
- **DTOs**: `SIAD.Core/DTOs/Ordenes`
- **Entidad EF**: `SIAD.Core/Entities/orden_trabajo` (incluye `fecha_creacion`).
- **Script SQL**: `Database/TablasPosgrets/2025-02-14_add_fecha_creacion_orden_trabajo.sql`.

## 2. Flujo de datos
1. El usuario interactúa con los filtros o popups.
2. Los manejadores (`OnEstadoFiltroTextChanged`, `OnTipoFiltroTextChanged`, etc.) actualizan los textos y disparan las recargas asíncronas.
3. `OrdenesClient` comunica con la API; `OrdenesService` ejecuta las consultas/actualizaciones en la base.
4. La UI se refresca mediante `StateHasChanged` cuando llegan los catálogos o las órdenes.

## 3. Búsqueda en vivo (combos DevExpress)
- Cada `DxComboBox` usa:
  ```razor
  Text="@tipoFiltroTexto"
  TextChanged="OnTipoFiltroTextChanged"
  ```
- El handler llama a `RecargarTiposAsync(texto)`.
- `RecargarTiposAsync` cancela solicitudes previas (`CancellationTokenSource`), llama al servicio, actualiza `tipos` y limpia el valor si ya no existe.
- El patrón se replica para estados, clientes y usuarios.

## 4. Validaciones en la UI
`ValidarNuevaOrdenAsync` debe pasar antes de llamar al backend:
- Cliente y Tipo existen (`propietarios.Any`, `tipos.Any`).
- Empleado es opcional, pero si se ingresó debe existir en `usuarios`.
- Fecha no es default (y el UI ya restringe con `MinDate`).
- Concepto no es vacío.
- Si algo falla, se arma el listado de campos en el toast y se enfoca el memo si falta el concepto.

## 5. Backend destacado
- `GetOrdenesAsync`: aplica filtros por departamento, tipo, estado, cliente, texto, fechas, año/mes. Proyecta a `OrdenTrabajoListItemDto`, mapea descripciones y obtiene dirección del cliente.
- `CrearOrdenAsync`: verifica cliente, calcula el siguiente número, fija `fecha_creacion = DateTime.SpecifyKind(DateTime.UtcNow, Unspecified)` y guarda.
- `AsignarOrdenesAsync`: actualiza usuario/empleado/estado en lote según los números seleccionados.
- Catálogos (`BuscarTiposAsync`, `BuscarPropietariosAsync`, `BuscarEstadosAsync`) aceptan texto (`q`) y `take`.

## 6. Estilos y UX
- Clases `.combo-item*` en el archivo Razor para mostrar descripción y clave en dos columnas.
- Badges de estado definidos con CSS local (`.estado-badge-*`) y `GetEstadoBadgeCss`.
- `DxGridSelectionColumn` solo define `Width="45px"` porque la propiedad `SelectAllCheckboxMode` no está disponible en la versión actual.

## 7. Scripts y migraciones
- Ejecuta `Database/TablasPosgrets/2025-02-14_add_fecha_creacion_orden_trabajo.sql` en entornos que aún no tengan la columna `fecha_creacion`.
- Asegúrate de actualizar la entidad y DTOs si agregas nuevos campos en la tabla.

## 8. Build y dependencias
- Proyecto Blazor WebAssembly (`apc.Client/apc.Client.csproj`) con DevExpress 25.1.
- `dotnet build HODSOFT_DEVEXPRESS.sln` compila sin errores; los warnings actuales provienen de `ClienteDetail.razor` por `@using`.
- Si agregas más componentes DevExpress, confirma que `apc.Client/_Imports.razor` tenga los `@using DevExpress.Blazor` necesarios.

## 9. Buenas prácticas al extender
1. Reutiliza `OrdenesClient` para nuevas operaciones (no llames `HttpClient` directo desde la UI).
2. Mantén la cancelación de peticiones (`CancellationTokenSource`) para evitar race conditions.
3. Limpia los valores (`@bind-Value`) si el catálogo ya no contiene la opción seleccionada.
4. Si agregas campos obligatorios en la creación/asignación, actualiza tanto las validaciones del front como del servicio.

Con esta guía puedes entender y extender el módulo sin romper la búsqueda en vivo, las validaciones ni la experiencia del usuario.
