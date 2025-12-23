# AI Coding Agent Instructions for HODSOFT SIAD Blazor Migration

## Project Overview
**SIAD (Sistema Integral de Administración Domiciliaria)** is a .NET 9 + Blazor WebAssembly + DevExpress migration from ASP.NET Core 3. The architecture follows a layered approach with clear separation between frontend (apc.Client), backend (apc), core business logic (SIAD.Services), and data access (SIAD.Data).

### Core Architecture Pattern
- **apc.Client**: Blazor WebAssembly (WASM) UI layer with DevExpress components
- **apc** (Server): ASP.NET Core host serving WASM app, exposing REST API, hosting DevExpress Reporting
- **SIAD.Services**: Domain services organized by business module (Contabilidad, Clientes, Medidores, etc.)
- **SIAD.Core**: Entities, DTOs, Constants, and domain interfaces
- **SIAD.Data**: EF Core DbContext (Postgres/SQL Server), migrations, and seeds
- **SIAD.Reports**: DevExpress .repx reports and reporting infrastructure

## Data Flow & Communication Pattern

### API Client Pattern
All Client→Server communication follows this sealed class pattern in `apc.Client/Services/<Module>/<EntityName>Client.cs`:

```csharp
public sealed class EmpresasContabilidadClient
{
    private readonly HttpClient http;
    
    public async Task<DTO> CrearAsync(DTO dto, CancellationToken ct = default) 
        => await http.PostAsJsonAsync("api/<module>/<endpoint>", dto, cancellationToken: ct);
    
    // CRUD methods with explicit error handling via ReadFromJsonAsyncWithAuthCheck
}
```

**Key conventions:**
- All HTTP methods are async with optional `CancellationToken`
- DTOs passed in method parameters; deserialization uses `ReadFromJsonAsyncWithAuthCheck<T>` (custom extension for auth handling)
- Null validation with `ArgumentNullException.ThrowIfNull()`
- Exceptions: throw `HttpRequestException` with descriptive message, re-throw `UnauthorizedAccessException`

### Backend Service Orchestration
Controllers in `apc/Controllers/<Module>/` inject service interfaces from `SIAD.Services` and:
- **Validate ModelState** and return `BadRequest(CrearProblemDetalle(...))`
- **Get current user/tenant** via `ICurrentCompanyService.GetCompanyId()` or `User.Identity.Name`
- **Call domain service methods** (async, with CancellationToken)
- **Return appropriately**: `Created()`, `Ok()`, `BadRequest()` with problem detail responses

### Service Registration
Services are registered in `SIAD.Services/ServiceRegistration.cs` via extension method:
```csharp
public static IServiceCollection AddSiadServices(this IServiceCollection services)
{
    services.AddAutoMapper(typeof(ServiceRegistration).Assembly);
    services.AddScoped<IClientesService, ClientesService>();
    // ... all domain services and tenancy services
}
```

## Code Organization by Module

### Module Structure (Example: Contabilidad/Accounting)
1. **SIAD.Core/DTOs/Contabilidad/**: `CompanyCreationDto`, `PeriodoContableDto`
2. **SIAD.Services/Contabilidad/**: 
   - `ICompanyManagementService` (interface)
   - `CompanyManagementService` (implementation with AutoMapper profile)
   - `IConfiguracionSistemaService` for system settings
3. **apc/Controllers/Contabilidad/**: `ContabilidadEmpresaController`, `PeriodosContablesController`
4. **apc.Client/Services/Contabilidad/**: `EmpresasContabilidadClient` (HTTP client)
5. **apc.Client/Pages/**: Razor components for UI (organized by feature)

## Critical Conventions & Patterns

### Naming & Style
- **PascalCase** for classes, interfaces, properties; **camelCase** for local variables
- No abbreviations (e.g., `companyadministrationservice` → `CompanyAdministrationService`)
- Sealed classes for non-inherited types (`public sealed class ...`)
- Async methods suffix `Async`; use `CancellationToken ct = default` in signatures

### Entity Modeling
- Entities in `SIAD.Core/Entities/` are scaffold-generated from database (EF Core reverse-engineer)
- DTOs in `SIAD.Core/DTOs/<Module>/` for API contracts
- AutoMapper profiles (`SIAD.Services/<Module>/*Mappings.cs`) define Entity ↔ DTO transformations
- Use Fluent API in `SIAD.Data/Configurations/` for EF Core model configuration

### Security & Tenancy
- **Multi-tenant**: `ICurrentCompanyService` provides tenant context (injected)
- **Authorization policies** in `SIAD.Core/Constants/AuthorizationPolicies.cs` (e.g., `Contabilidad`, `Administrador`)
- Controllers decorated with `[Authorize(Policy = "...")]`
- `TenantCompanyClaimTransformation` adds claims from tenant user context

### Error Handling
- Domain exceptions: `InvalidOperationException`, `ArgumentException` (with descriptive messages)
- HTTP responses: use `CrearProblemDetalle(titulo, detalle)` helper for RFC 7807 Problem Details
- Client-side: catch `UnauthorizedAccessException` separately; re-throw HTTP errors with context

### DevExpress Integration
- **Server-side PDF Viewer**: `AddDevExpressServerSideBlazorPdfViewer()` in `Program.cs`
- **WASM PDF Viewer**: `AddDevExpressWebAssemblyBlazorPdfViewer()` in Client `Program.cs`
- **Reporting**: `.repx` files in `SIAD.Reports/Layouts/`; render via Reporting API

## Database & Migrations

### Connection String Management
- **Local**: Postgres/SQL Server via containers (config in `appsettings.Development.json`)
- **QA/Production**: External config (secrets via Options pattern)
- **Scaffold (EF Core)**:
```powershell
dotnet ef dbcontext scaffold "connection-string" Npgsql.EntityFrameworkCore.PostgreSQL `
  -p SIAD.Data/SIAD.Data.csproj -s apc/apc.csproj -c SiadDbContext `
  --namespace SIAD.Core.Entities --output-dir SIAD.Core/Entities --force
```

### Seeds & Testing
- SQL scripts in `Database/Seeds/` or `Database/Seeds_v2/` executed during schema initialization
- Demo data: `seed_cliente_demo.sql` provides test tenant + clients for UI validation
- Migration policies: use Migrations API, avoid raw SQL for structural changes

## Build, Test & Development Workflow

### Commands
- **Build**: `dotnet build` (solution-level)
- **Run Development**: `dotnet run --project apc` (serves WASM + API on https://localhost:5001)
- **Database Init**: Apply migrations via `dotnet ef database update` or via container startup scripts
- **Trust Dev Certificate**: `dotnet dev-certs https --trust`
- **Unit Tests**: `dotnet test` (in apc.Client.Tests/ for client logic; expand as needed)

### Branching & Versioning
- **main**: production-ready code (monthly semantic version: YYYY.MM)
- **dev**: integration branch for feature merges
- **Feature branches**: `Feature/<module>` (e.g., `Feature/Ordenes`)
- Pull requests require cross-review + automated validation (build + unit tests)

### Local Development Setup
1. Install .NET 9 SDK, Visual Studio 2022, and workloads:
   ```powershell
   dotnet workload install wasm-tools
   dotnet dev-certs https --trust
   dotnet nuget add source https://nuget.devexpress.com/api/v3/index.json -n DXFeed -u DevExpress -p <key>
   ```
2. Create Postgres container or connect to QA instance
3. Update `appsettings.Development.json` with connection strings
4. Run migrations: `dotnet ef database update --project SIAD.Data`
5. Execute seed scripts: `psql -h localhost -U postgres -d bdnes -f Database/Seeds_v2/seed_*.sql`
6. `dotnet run --project apc`

## Key Files & Documentation
- [readme.md](readme.md): Project vision, solution structure, prerequisites
- [docs/modulo_clientes.md](docs/modulo_clientes.md): Clients module inventory & UI flow
- [docs/modulo_solicitudes.md](docs/modulo_solicitudes.md): Service requests (CRUD + catalog)
- [docs/modulo_medidores.md](docs/modulo_medidores.md): Meters & readings
- [docs/modulo_auxiliar_lectura.md](docs/modulo_auxiliar_lectura.md): Reading cycles & bulk import
- [docs/modulo_ordenes.md](docs/modulo_ordenes.md): Work orders (pending full implementation)
- [docs/modulo_rutas.md](docs/modulo_rutas.md): Routes & crews
- [apc.Client/CommonServices.cs](apc.Client/CommonServices.cs): Service registration for DI
- [SIAD.Services/ServiceRegistration.cs](SIAD.Services/ServiceRegistration.cs): Backend service registration

## When Adding a New Feature/Module
1. **Define DTOs** in `SIAD.Core/DTOs/<ModuleName>/`
2. **Create Service Interface** in `SIAD.Services/<ModuleName>/I<Feature>Service.cs`
3. **Implement Service** with AutoMapper profile; register in `ServiceRegistration.cs`
4. **Add Controller** in `apc/Controllers/<ModuleName>/` with authorization policy
5. **Create Client Class** in `apc.Client/Services/<ModuleName>/<Entity>Client.cs`
6. **Register Client** in `apc.Client/CommonServices.cs`
7. **Build Razor Pages/Components** in `apc.Client/Pages/<ModuleName>/` using DevExpress grid/form components
8. **Document** in `docs/modulo_<modulename>.md`
9. **Add Unit Tests** covering service logic and client HTTP interactions
10. **Test locally** before merging to `dev`

---

**Last Updated**: December 2025 | **Framework**: .NET 9, Blazor WebAssembly, DevExpress v25.1
