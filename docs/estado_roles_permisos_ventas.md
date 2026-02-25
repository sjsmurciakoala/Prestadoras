# Estado del trabajo - Roles y permisos por endpoint (Ventas)

Fecha: 2026-02-25

## Alcance actual
Se implemento control de permisos por endpoint para el modulo **Ventas**. Incluye:

- **Opciones** dentro de Ventas: Clientes, Captacion de Pagos, Cobranza, Facturacion Miscelaneos, Notas Credito/Debito.
- **Permisos CRUD por endpoint** (cada ruta tiene su propio permiso, con fallback a permiso por opcion y por modulo).
- **Vista de roles**: muestra endpoints con descripcion amigable (sin GET/POST) y permite marcar permisos.
- **Semilla SQL** actualizada para permisos nuevos.

## Archivos clave

### Catalogo de endpoints y permisos
- `SIAD.Core/Constants/PermissionEndpointCatalog.cs`
  - Define los 56 endpoints de Ventas con: modulo, opcion, recurso, accion, metodo y ruta.
  - Genera el permiso final con `PermissionKeyBuilder`.

### Builder y enums
- `SIAD.Core/Constants/PermissionKeyBuilder.cs`
  - Normaliza la ruta y construye recursos `opcion__ruta_normalizada`.
  - Construye permisos de modulo, opcion y endpoint.
- `SIAD.Core/Constants/PermissionAction.cs`
  - Enum CRUD: View, Create, Edit, Delete.

### Permisos globales y politicas
- `SIAD.Core/Constants/PermissionNames.cs`
  - `All` incluye permisos de modulo, de opcion y de endpoint (Ventas).
  - `Policies` agrega policies por endpoint con fallback:
    - Endpoint -> Opcion -> Modulo -> Legacy (solo View).

### Autorizacion en API
- `apc/Security/ModuleAuthorizeAttribute.cs`
  - Genera el permiso de endpoint en base a la ruta real.
  - Si hay permiso endpoint, pasa.
  - Si no, intenta con permiso de opcion y luego modulo.
  - Super Admin bypass.

### Controladores Ventas
- `apc/Controllers/CaptacionPagosController.cs`
- `apc/Controllers/ClientesController.cs`
- `apc/Controllers/CobranzaController.cs`
- `apc/Controllers/FacturacionMiscelaneosController.cs`
- `apc/Controllers/NotasCreditoDebitoController.cs`

Todos usan `ModuleAuthorize` a nivel de clase y algunos overrides de accion.

### UI Roles
- `apc.Client/Pages/Parametros/RolesPortalForm.razor`
  - Muestra endpoints (descripcion amigable) y permisos CRUD.
  - Guarda permisos por endpoint.
  - No muestra GET/POST ni ruta tecnica.

- `apc.Client/Pages/Parametros/RolesPortalList.razor`
  - Formatea permisos para mostrar opcion + detalle.

## Inventario y descripcion de endpoints
- `docs/ventas_endpoints_descripcion.md`
  - Lista los 56 endpoints con descripcion amigable.
- `docs/ventas_endpoints_detallados.md`
  - Lista endpoints con metodo HTTP, ruta y action.

## Base de datos
- `apc/Data/seed_manual.sql`
  - Incluye permisos de modulo, opcion y endpoint (Ventas).
  - Ejecutar solo si se quiere que el Super Admin tenga todos los permisos ya marcados en UI.

## Comportamiento actual
- Backend restringe el acceso si no hay permiso.
- UI de roles permite asignar permisos por endpoint.
- Menu y paginas **no** ocultan opciones aun (solo la API bloquea).

## Pendiente (si se decide mas adelante)
- Ocultar elementos de menu y paginas segun permisos.
- Agregar endpoint-permissions para otros modulos (Bancos, Compras, Inventario, Contabilidad, Reporteria, Configuracion).

