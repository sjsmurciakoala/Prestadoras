# Módulo Solicitudes de Servicio - Migración Completa

## Estado: ✅ COMPLETAMENTE IMPLEMENTADO

### Tablas Base de Datos
- `solicitud_servicio` - Almacena todas las solicitudes con campos completos (solicitante, empresa, negocio, auditoría)
- `categoria_servicio` - Catálogo de categorías (Doméstica, Comercial, Industrial, Pública, Industrial ENP)

### Modelo de Datos (DTOs)

#### SIAD.Core/DTOs/Solicitudes/
1. **SolicitudListDto** - Para listados
   - Id, IdentificacionCliente, NombreCliente, CategoriaServicioId, CategoriaServicioNombre, Fecha, Estado, Asignada

2. **SolicitudDetailDto** - Detalle completo (lectura)
   - Todos los campos de solicitante (DNI, RTN, nombre, teléfono, móvil, email, dirección, color casa, fecha nacimiento, clave SURE)
   - Todos los campos de empresa (nombre, teléfono, dirección)
   - Todos los campos de negocio (nombre, teléfono, clave catastral)
   - Auditoría (estado, asignada, fecha creación)

3. **SolicitudCreateDto** - Para crear nueva solicitud
   - Todos los campos requeridos para inserción inicial

4. **SolicitudUpdateDto** - Para actualizar solicitud existente
   - Permite editar todos los campos excepto identificación del cliente

5. **SolicitudCategoriaDto** - Catálogo de categorías
   - Id, Nombre (Descripción), Estado

### Servicios y Lógica de Negocio

#### ISolicitudesService (Interface)
```csharp
// Lectura
Task<IReadOnlyList<SolicitudListDto>> GetSolicitudesAsync(string? clienteIdentidad = null, CancellationToken ct = default);
Task<SolicitudDetailDto?> GetSolicitudAsync(int id, CancellationToken ct = default);
Task<IReadOnlyList<SolicitudCategoriaDto>> GetCategoriasAsync(CancellationToken ct = default);

// CRUD Completo
Task<int> CreateSolicitudAsync(SolicitudCreateDto dto, string usuarioCreacion, CancellationToken ct = default);
Task UpdateSolicitudAsync(SolicitudUpdateDto dto, string usuarioModificacion, CancellationToken ct = default);
Task InactivateSolicitudAsync(int id, string usuarioModificacion, CancellationToken ct = default);

// Gestión de Asignación
Task AsignarSolicitudAsync(int id, string usuarioModificacion, CancellationToken ct = default);
Task DesasignarSolicitudAsync(int id, string usuarioModificacion, CancellationToken ct = default);
```

#### SolicitudesService (Implementación)
- Ubicación: `SIAD.Services/Solicitudes/SolicitudesService.cs`
- Incluye AutoMapper para mapeo Entity ↔ DTOs
- Validaciones completas (null checks, existencia de registros)
- Auditoría automática (usuario/fecha de creación y modificación)

#### SolicitudMappings (AutoMapper Profiles)
- Ubicación: `SIAD.Services/Solicitudes/SolicitudMappings.cs`
- Mapea Entity ↔ SolicitudDetailDto, SolicitudListDto, SolicitudCreateDto, SolicitudUpdateDto

### API (REST Endpoints)

#### SolicitudesController
- Ruta base: `/api/solicitudes`

**Lectura:**
- `GET /api/solicitudes` - Listado completo (con filtro opcional por clienteIdentidad)
- `GET /api/solicitudes/{id}` - Detalle de una solicitud
- `GET /api/solicitudes/categorias` - Catálogo de categorías activas

**Escritura (CRUD):**
- `POST /api/solicitudes` - Crear nueva solicitud (body: SolicitudCreateDto)
- `PUT /api/solicitudes/{id}` - Actualizar solicitud (body: SolicitudUpdateDto)
- `DELETE /api/solicitudes/{id}` - Inactivar solicitud (cambia estado a false)

**Acciones Especiales:**
- `POST /api/solicitudes/{id}/asignar` - Marcar como asignada
- `POST /api/solicitudes/{id}/desasignar` - Marcar como no asignada

### Cliente HTTP

#### SolicitudesClient
- Ubicación: `apc.Client/Services/Solicitudes/SolicitudesClient.cs`
- Métodos:
  - `ObtenerAsync(clienteIdentidad?)` - GET listado
  - `ObtenerPorIdAsync(id)` - GET detalle
  - `ObtenerCategoriasAsync()` - GET categorías
  - `CrearAsync(dto)` - POST crear
  - `UpdateAsync(id, dto)` - PUT actualizar
  - `InactivarAsync(id)` - DELETE inactivar
  - `AsignarAsync(id)` - POST asignar
  - `DesasignarAsync(id)` - POST desasignar

### UI/Frontend

#### Página Principal: SolicitudesIndex.razor
- Ubicación: `apc.Client/Pages/Solicitudes/SolicitudesIndex.razor`
- Ruta: `/solicitudes`
- Características:
  - **Filtros**: Identificación del cliente, estado (solo activas)
  - **DataGrid** con columnas: ID, Identidad, Nombre, Categoría, Fecha, Estado, Asignada, Acciones
  - **Botones por fila**: Ver, Editar, Eliminar
  - **Modales**:
    - Detalle (vista en popup con todos los campos)
    - Formulario de creación/edición con 3 pestañas

#### Componentes de Formulario

**SolicitudForm.razor** (Crear Nueva)
- Ubicación: `apc.Client/Pages/Solicitudes/Components/SolicitudForm.razor`
- Parámetros: Solicitud (SolicitudCreateDto), Categorias, OnGuardar (callback)
- 3 Pestañas:
  1. **Datos del Solicitante**: Identificación*, RTN, Nombre*, Categoría*, Teléfono*, Móvil*, Correo, Dirección*, Color Casa, Fecha Nacimiento, Clave SURE, Observaciones
  2. **Datos de Empresa**: Nombre, Teléfono, Dirección
  3. **Datos de Negocio**: Nombre, Teléfono, Clave Catastral

**SolicitudFormEdicion.razor** (Editar)
- Ubicación: `apc.Client/Pages/Solicitudes/Components/SolicitudFormEdicion.razor`
- Igual que SolicitudForm pero:
  - Identificación cliente es de solo lectura
  - Recibe SolicitudUpdateDto en lugar de SolicitudCreateDto

### Menú de Navegación

#### NavMenu.razor actualizado
- Se agregó menú "Servicios" (padre)
- Submenú "Solicitudes" → `/solicitudes`
- Ubicación en menú: Después de "Rutas", antes de "Facturación"

### Registro en DI (Dependency Injection)

#### CommonServices.cs (Cliente)
- ✅ SolicitudesClient ya registrado

#### ServiceRegistration.cs (Servidor)
- ✅ ISolicitudesService, SolicitudesService ya registrados
- ✅ AutoMapper profile registrado

### Data Flow Completo

```
Usuario → NavMenu (/solicitudes)
    ↓
SolicitudesIndex.razor (carga datos)
    ↓
SolicitudesClient.ObtenerAsync()
    ↓
GET /api/solicitudes
    ↓
SolicitudesController.Get()
    ↓
ISolicitudesService.GetSolicitudesAsync()
    ↓
SiadDbContext (EF Core → PostgreSQL)
    ↓
SolicitudListDto[] (mapper)
    ↓
JSON response
    ↓
Grid DataTable
```

### Validaciones Implementadas

**Cliente (Frontend):**
- Campos obligatorios resaltados con "*"
- Validación de estructura de datos en componentes
- Toast notifications para éxito/error

**Servidor (Backend):**
- ModelState validation en controllers
- ArgumentNullException para nulos
- InvalidOperationException para registros no encontrados
- EnsureSuccessStatusCode() en cliente HTTP

### Auditoría

**Campos automáticos en cada solicitud:**
- `usuariocreacion` (del Identity del usuario)
- `fechacreacion` (DateTime.UtcNow)
- `usuariomodificacion` (al actualizar/inactivar)
- `fechamodificacion` (al actualizar/inactivar)

### Testing Manual Recomendado

1. **Crear solicitud nueva:**
   - Navegar a Servicios → Solicitudes
   - Click en "Nueva Solicitud"
   - Completar formulario 3 pestañas
   - Verificar éxito y aparición en grid

2. **Listar y filtrar:**
   - Verificar listado completo
   - Filtrar por identificación del cliente
   - Verificar estado (activa/inactiva)
   - Verificar asignación (asignada/no asignada)

3. **Ver detalle:**
   - Click en "Ver" en cualquier fila
   - Verificar popup con todos los datos
   - Verificar separación por pestañas en formulario

4. **Editar solicitud:**
   - Click en "Editar"
   - Modificar campos
   - Guardar y verificar cambios

5. **Inactivar solicitud:**
   - Click en "Eliminar"
   - Confirmar eliminación
   - Verificar que desaparece del filtro "Solo activas"

6. **Asignar/Desasignar:**
   - Ver detalle y verificar campo "Asignada"
   - Llamar endpoints `/asignar` y `/desasignar`
   - Verificar cambio en estado

### Scripts de Datos de Prueba

**Ubicación:** `Database/2025-10-20_seed_solicitud_servicio.sql`
- Crea 2 solicitudes de ejemplo para cliente demo
- Ejecutar: `psql -h localhost -U postgres -d bdnes -f Database/2025-10-20_seed_solicitud_servicio.sql`

### Próximos Pasos / Mejoras Futuras

1. Integración con módulo de Clientes (crear cliente desde solicitud)
2. Integración con módulo de Medidores (crear medidor desde solicitud)
3. Reportería/exportación a PDF
4. Workflow de estados (nueva → asignada → completada → cerrada)
5. Notificaciones por email al cliente
6. Historial de cambios/auditoría detallada
7. Búsqueda full-text
8. Bulk actions (asignar múltiples, inactivar múltiples)

---

**Última actualización:** Enero 2026 | **Estado:** ✅ PRODUCCIÓN

## Proximos pasos
- Ajustar internacionalizacion/acentos segun decida el equipo (actualmente se usa ASCII por consistencia).
- Definir reglas de negocio adicionales (validaciones de categoria, estados, asignaciones).
- Integrar reporteria o exportacion cuando se definan requerimientos (RDLC heredados).
