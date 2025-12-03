# Plan de Migracion - Modulo Solicitudes de Servicio

## Tablas mapeadas
- solicitud_servicio
- categoria_servicio

## Scaffold mas reciente
`dotnet tool run dotnet-ef dbcontext scaffold "Host=3.208.232.209;Port=5432;Database=bdnes;Username=postgres;Password=Koala@2021;Timeout=10;SslMode=Prefer" Npgsql.EntityFrameworkCore.PostgreSQL -p SIAD.Data/SIAD.Data.csproj -s apc/apc.csproj -c SiadDbContext --no-onconfiguring --context-dir TempScaffold --context-namespace TempScaffold --namespace TempScaffold.Entities --use-database-names --output-dir TempScaffold/Entities --force --no-build -t solicitud_servicio -t categoria_servicio`
- Copiar `TempScaffold/Entities/*.cs` a `SIAD.Core/Entities/` y eliminar la carpeta temporal.
- Ajustar los namespaces a `SIAD.Core.Entities`.

## Servicios y DTOs
- `SIAD.Core.DTOs.Solicitudes` ahora expone `SolicitudListDto`, `SolicitudDetailDto` (incluye nombre de categoria y campos opcionales de empresa/negocio) y `SolicitudCategoriaDto`.
- `SIAD.Services.Solicitudes.SolicitudesService` implementa:
  - `GetSolicitudesAsync(clienteIdentidad)` devolviendo lista con nombre de categoria.
  - `GetSolicitudAsync(id)` con detalle completo y campos opcionales.
  - `GetCategoriasAsync()` con categorias activas ordenadas alfabeticamente.
  - `CreateSolicitudAsync(dto)` guardando telefono, direccion, categoria y datos opcionales.
- `ServiceRegistration.AddSiadServices()` registra `ISolicitudesService`.

## API disponible
- `GET /api/solicitudes?clienteIdentidad={identidad}` lista solicitudes filtradas por identidad (opcional).
- `GET /api/solicitudes/{id}` devuelve el detalle de una solicitud.
- `GET /api/solicitudes/categorias` entrega las categorias activas para combos en UI.
- `POST /api/solicitudes` crea una nueva solicitud (requiere identidad, nombre, categoria, telefono, direccion y correo válido).

## UI actual (ClienteDetail.razor)
- Nueva pestana "Solicitudes":
  - Boton "Cargar solicitudes" que invoca `LoadSolicitudesAsync()`.
  - Grid DevExpress ahora muestra `Solicitud #`, Identidad, Nombre, Categoría, Fecha (formateada) y Estado; cada fila tiene botón "Ver detalle" que abre un `DxPopup`.
  - El popup muestra el número de solicitud junto con datos de contacto, categoría, notas y campos opcionales de empresa/negocio cuando existen.
  - Formulario "Registrar nueva solicitud": utiliza `DxComboBox` sobre `SolicitudesService.GetCategoriasAsync()`, resalta campos obligatorios (teléfono/dirección/correo), valida formato de correo y dispara toasts (`DxToastProvider` + `IToastNotificationService`) con el número recién creado o mensajes de error.
- Los catalogos de categoria se cargan una unica vez (`EnsureCategoriasAsync`) y se reutilizan para el detalle y el formulario.

## Script de semilla
Ruta: `Database/2025-10-20_seed_solicitud_servicio.sql`.
- Limpia solicitudes previas del cliente demo (`DELETE ... WHERE clave_sure = 'CLI-DEMO-001'`).
- Inserta dos solicitudes de ejemplo para la identidad `0801199000001`.
- Ejecutar desde psql: `psql -h 3.208.232.209 -U postgres -d bdnes -f Database/2025-10-20_seed_solicitud_servicio.sql`.

## Validacion manual sugerida
1. Ejecutar el script de semilla para asegurar datos de prueba.
2. Levantar la solucion (`dotnet build apc.sln` y luego `dotnet run --project apc/apc.csproj`).
3. Autenticarse en la aplicacion y abrir `Clientes` > seleccionar `CLI-DEMO-001`.
4. En la pestana "Solicitudes":
   - Presionar "Cargar solicitudes" y confirmar que se muestran los dos registros seed (con nombre de categoria y columna `Solicitud #`).
   - Usar "Ver detalle" y verificar que el encabezado incluya el número de solicitud y los campos opcionales (telefono, correo, empresa, negocio).
   - Completar el formulario y guardar una solicitud nueva; validar que el toast indique `Solicitud #N` y que el registro aparece en la grilla con fecha actual.
5. Revisar los mensajes de estado (toast y alertas) para errores (falta de telefono/direccion/correo, categorias vacias, formato de correo inválido, etc.).

## Proximos pasos
- Ajustar internacionalizacion/acentos segun decida el equipo (actualmente se usa ASCII por consistencia).
- Definir reglas de negocio adicionales (validaciones de categoria, estados, asignaciones).
- Integrar reporteria o exportacion cuando se definan requerimientos (RDLC heredados).
