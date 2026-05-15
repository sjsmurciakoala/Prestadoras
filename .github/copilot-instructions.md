# Instrucciones de IA para HODSOFT SIAD

## Fuente de verdad
- Toma como fuente principal el codigo actual y la solucion `HODSOFT_DEVEXPRESS.sln`.
- Si `readme.md` o documentos antiguos contradicen la solucion real, sigue el codigo vigente.
- Para DevExpress, consulta primero la documentacion oficial mediante el MCP `dxdocs` configurado en `.vscode/mcp.json`.
- Usa la documentacion local del repo como contexto secundario, no como sustituto de la fuente oficial para APIs DevExpress.

## DevExpress MCP — Workflow obligatorio

When replying to **ANY** question about DevExpress components, use the dxdocs server to construct your answer.

1. **Call `devexpress_docs_search`** to obtain help topics related to the user's question.
2. **Call `devexpress_docs_get_content`** to fetch and read the most relevant help topics.
3. **Reflect on the obtained content** and how it relates to the question.
4. **Provide a comprehensive answer** based solely on retrieved information.

### Constraints

- **Use `devexpress_docs_search` only once** per question to avoid redundant queries.
- **Answer questions based solely** on information obtained from MCP server tools.
- If relevant code examples are available in documentation, **include those code examples**.
- **Reference specific DevExpress controls and properties** mentioned in the docs.
- If a user specifies a version (such as v24.2 or 24.2), invoke MCP server tools corresponding to that version (for example, `dxdocs24_2`).
- Be specific about components: mention exact DevExpress class names (`DxGrid`, `DxPopup`, `DxComboBox`, etc.).
- Include the technology stack: Blazor Server/WASM, ASP.NET Core, .NET 9, DevExpress 25.1.7.

## Stack real del proyecto
- `apc`: host ASP.NET Core .NET 9, Identity, controllers, DevExpress Reporting y PDF Viewer.
- `apc.Client`: Blazor WebAssembly .NET 9 con `DevExpress.Blazor`.
- `SIAD.Core`: entidades, DTOs, constantes, permisos, tenancy.
- `SIAD.Data`: `SiadDbContext`, extensiones parciales, Npgsql y reglas multiempresa.
- `SIAD.Services`: servicios por modulo, AutoMapper y algunos flujos con Dapper.
- `SIAD.Reports`: storage, bootstrap, validacion SQL, templates y soporte de reporteria web.
- `Database`: scripts SQL, DDL, seeds y cambios incrementales de la base de datos.

## Reglas no negociables
- Respeta la arquitectura por capas y por modulo.
- Respeta siempre la multiempresa: `company_id`, `ICurrentCompanyService`, `TenantState` e `ICompanyScopedEntity`.
- No inventes APIs de DevExpress ni asumas propiedades/eventos sin revisar la fuente oficial.
- No metas logica de negocio pesada dentro de paginas Razor si puede vivir en servicios o API.
- No hagas ediciones masivas en `SIAD.Data/SiadDbContext.cs` ni en entidades scaffolded salvo necesidad clara.
- Si agregas un servicio HTTP cliente, registralo en `apc.Client/CommonServices.cs`.
- Si agregas un servicio de dominio, registralo en `SIAD.Services/ServiceRegistration.cs`.
- Protege endpoints con `ModuleAuthorize` o politicas construidas desde `PermissionNames`.
- Manten patrones async con `CancellationToken` cuando el modulo ya los usa.

## Convenciones por capa

### UI Blazor y DevExpress
- Coloca pantallas en `apc.Client/Pages/<Modulo>/`.
- Si la pantalla ya usa `.razor.cs` o clases auxiliares `*GridDataSource.cs`, conserva esa separacion.
- Para llamadas HTTP, prefiere `ReadFromJsonAsyncWithAuthCheck`, `PostAsJsonAsyncWithAuthCheck`, `PutAsJsonAsyncWithAuthCheck` y `ObtenerMensajeErrorAsync`.
- Para datos tenant-aware, resuelve la empresa con `TenantState.EnsureCompanyAsync()` antes de cargar informacion.
- Reutiliza los patrones ya presentes con `DxGrid`, `DxFormLayout`, `DxPopup`, `DxLoadingPanel`, `DxButton`, `DxComboBox` y componentes similares.

### API y servicios
- Sigue el flujo normal: DTO en `SIAD.Core` -> interfaz e implementacion en `SIAD.Services` -> controller en `apc/Controllers` -> cliente HTTP en `apc.Client/Services` si aplica.
- Los controllers no deben concentrar reglas de negocio; solo validan, resuelven contexto y delegan al servicio.
- Cuando el modulo es tenant-aware, no confies en un `companyId` arbitrario enviado por el cliente si ya puedes resolverlo desde claims/sesion.

### Tenancy y seguridad
- `SiadDbContext.Tenancy.cs` aplica filtros por `company_id` y rellena ese campo al guardar entidades con `ICompanyScopedEntity`.
- No rompas esos filtros salvo que el cambio lo requiera de forma explicita y este muy justificado.
- Usa `PermissionModules`, `PermissionNames`, `PermissionEndpointCatalog` y `ModuleAuthorizeAttribute` como base de permisos.

### Reporteria DevExpress
- Revisa `apc/Program.cs` y `SIAD.Reports/Reporting/*` antes de tocar designer/viewer/storage.
- Los datasets y layouts son por empresa; cualquier cambio debe mantener `company_id`.
- La SQL custom de reporteria debe seguir siendo solo lectura.

### Base de datos y scaffold
- Para cambios de esquema del negocio, prefiere scripts SQL incrementales en `Database/`.
- Manten separadas las migraciones de Identity (`apc/Migrations`) de los cambios de la BD funcional.
- Si necesitas refrescar scaffold, parte del comando documentado en `readme.md` y evita perder tablas ya incluidas en el contexto.

## Skills del repo
- Usa las skills de `.github/skills/` cuando el trabajo encaje con ellas.
- Skills disponibles en este repo:
  - `hodsoft-devexpress-docs`
  - `hodsoft-blazor-devexpress-ui`
  - `hodsoft-siad-backend`
  - `hodsoft-reporting-devexpress`
  - `hodsoft-postgres-ef-scaffold`
