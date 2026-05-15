# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Source of truth

The current code and `HODSOFT_DEVEXPRESS.sln` are authoritative. `readme.md` and other docs may contradict the live solution — follow the code. The same applies to `docs/` (planning/handoff notes, not specs).

## Build / run / publish

- Solution build: `dotnet build HODSOFT_DEVEXPRESS.sln` (the only VS Code task is `dotnet-build-solution`).
- Run portal locally: `dotnet run --project apc/apc.csproj` (hosts the WASM client at `apc.Client` as a static asset and exposes the API).
- On-prem publish: `./publish-onprem.ps1` (defaults to publishing the portal; `-Solo portal|ws|todos`, `-Output <path>`). Targets `win-x64`, framework-dependent, ReadyToRun.
- Connection string lives in `apc/appsettings.json` under `ConnectionStrings:DefaultConnection` (Npgsql/Azure Postgres). Identity migrations write to schema `identity` (`__IdentityMigrationsHistory`). The SIAD functional DB does **not** use EF migrations — see Database section.
- Default culture is forced to `es-HN` in `apc/Program.cs`; preserve it.

There is no test project in this solution and no lint config beyond default .NET analyzers.

## Solution layout

Six projects, all .NET 9:

| Project        | Role                                                                            |
|----------------|---------------------------------------------------------------------------------|
| `apc`          | ASP.NET Core host. Identity, controllers, DevExpress Reporting, PDF Viewer. Razor Components with **InteractiveServer** render mode and the WASM client mounted as `AdditionalAssemblies`. |
| `apc.Client`   | Blazor WebAssembly. `DevExpress.Blazor`, PDF viewer, Skia PDF renderer. Pages and HTTP clients live here. |
| `SIAD.Core`    | Entities (scaffolded from Postgres), DTOs, constants (`PermissionNames`, `RoleNames`, `PermissionEndpointCatalog`), tenancy contracts. |
| `SIAD.Data`    | `SiadDbContext` (partial across `SiadDbContext.cs`, `*.Tenancy.cs`, `*.Accounting.cs`, `*.Mantenimientos.cs`, `*.NotasCreditoDebito.cs`, `*.SarCompliance.cs`), Npgsql config. |
| `SIAD.Services`| Per-module domain services (Clientes, Cobranza, Contabilidad, Bancos, Tarifario, etc.), AutoMapper profiles (assembly-scanned), DI in `ServiceRegistration.cs`. |
| `SIAD.Reports` | DevExpress Reporting bootstrap, per-company `ReportStorageWebExtension`, SQL validation, templates. |

The Identity DbContext is `apc.Data.ApplicationDbContext` and is separate from `SiadDbContext`. Identity migrations are in `apc/Migrations/`; **do not put functional DB changes there**.

## Architecture: a slice through the stack

A typical module flows: DTO in `SIAD.Core/DTOs/<Modulo>/` → interface + impl in `SIAD.Services/<Modulo>/` → controller in `apc/Controllers/<Modulo>/` (thin: validate, resolve tenant, delegate) → HTTP client in `apc.Client/Services/<Modulo>/` → page in `apc.Client/Pages/<Modulo>/`.

Wiring is centralized:

- Domain services: register in [SIAD.Services/ServiceRegistration.cs](SIAD.Services/ServiceRegistration.cs) (`AddSiadServices`), called from `apc/Program.cs`.
- Client HTTP services: register in [apc.Client/CommonServices.cs](apc.Client/CommonServices.cs), called from both `apc/Program.cs` (server-interactive) and `apc.Client/Program.cs` (WASM).

Because `CommonServices.Configure` runs in both hosts, anything added there must be safe in both. The server-side `HttpClient` named `"ServerAPI"` is custom-built in `apc/Program.cs` and forwards the request `Cookie` so server-rendered components can call the local API; the WASM `Program.cs` registers a plain `HttpClient` against `HostEnvironment.BaseAddress`.

## Multi-tenancy — non-negotiable

Every functional table is scoped to `company_id`. The mechanism:

- Entities that belong to a tenant implement `SIAD.Core.Tenancy.ICompanyScopedEntity` (exposes `long company_id`).
- `SiadDbContext.Tenancy.cs` injects `ICurrentCompanyService`, applies a global query filter `e => e.company_id == CurrentCompanyId` to every `ICompanyScopedEntity`, and overrides `SaveChanges*` to stamp `company_id` on inserts and forbid changing it on updates.
- The model cache key includes the current company (`SiadDbContextModelCacheKeyFactory`) so EF rebuilds the model per tenant.
- The current company is resolved from claims via `TenantCompanyClaimTransformation` (registered as `IClaimsTransformation` in `apc/Program.cs`).

Rules:

- Do not bypass the global filter unless absolutely required and document why (the rare cross-tenant query).
- Do not trust `companyId` from request bodies; resolve via `ICurrentCompanyService` / claims.
- When adding a new tenant-scoped table, include `company_id`, add tenant-safe indexes/constraints, and implement `ICompanyScopedEntity`.
- In Blazor pages, call `TenantState.EnsureCompanyAsync()` before loading tenant data.

## Authorization

Permission catalog lives in [SIAD.Core/Constants/](SIAD.Core/Constants/):

- `PermissionModules` — top-level modules (`ventas`, `bancos`, `compras`, `contabilidad`, `reporteria`, `configuracion`, `inventario`).
- `PermissionResources` — submodules per module.
- `PermissionNames` — string constants like `"module.ventas.clientes.edit"` plus a `Policies` table consumed by `AddAuthorization` in both `apc/Program.cs` and `apc.Client/Program.cs`.
- `PermissionEndpointCatalog` — endpoint → resource mapping used by `ModuleAuthorize` to derive a resource from the route pattern.
- `RoleNames.SuperAdministrador` is the global bypass; the auth assertion is `IsInRole(SuperAdministrador) || HasClaim(permission)`.

On controllers/actions use `[ModuleAuthorize(module)]`, `[ModuleAuthorize(module, action)]`, `[ModuleAuthorize(module, resource)]` or `[ModuleAuthorize(module, resource, action)]` — see [apc/Security/ModuleAuthorizeAttribute.cs](apc/Security/ModuleAuthorizeAttribute.cs). HTTP method is mapped to `PermissionAction` (`GET→View`, `POST→Create`, `PUT/PATCH→Edit`, `DELETE→Delete`). The attribute walks fallbacks: endpoint-specific → base resource → module-level → legacy `module.<name>` (View only).

When adding a new endpoint, register its permission name in `PermissionNames` and (if endpoint-specific) in `PermissionEndpointCatalog`.

## Blazor / DevExpress UI conventions

- DevExpress version is **25.1.7** on .NET 9. Configured size mode: `Small`.
- Pages live in `apc.Client/Pages/<Modulo>/`. Honor existing `.razor` / `.razor.cs` / `.razor.css` / `*GridDataSource.cs` separation when a module already uses it.
- For HTTP, use the auth-aware extensions in [apc.Client/Services/HttpClientExtensions.cs](apc.Client/Services/HttpClientExtensions.cs): `ReadFromJsonAsyncWithAuthCheck`, `GetFromJsonAsyncWithAuthCheck`, `PostAsJsonAsyncWithAuthCheck`, `PutAsJsonAsyncWithAuthCheck`, and `ObtenerMensajeErrorAsync`. They throw `UnauthorizedAccessException` on 401 / login redirects.
- Keep business rules in services, not pages — pages orchestrate state and HTTP.
- **DevExpress API workflow is mandatory**: before changing any DevExpress component behavior or API surface, query the `dxdocs` MCP server (`devexpress_docs_search` → `devexpress_docs_get_content`) configured in [.vscode/mcp.json](.vscode/mcp.json). Do not invent properties/events.

## Reporting

DevExpress Web Designer + Web Viewer + PDF Viewer are bootstrapped in [apc/Program.cs](apc/Program.cs):

- `ReportingRuntimeBootstrap.Initialize` is called after `Build()`.
- Custom SQL is enabled but validated read-only (`SELECT` / `WITH ... SELECT`) via `ICustomQueryValidator` → `ReportingCustomQueryValidator`.
- Per-company report storage: `CompanyReportStorageWebExtension`.
- Connection string providers: `ReportingConnectionProviderFactory` / `ReportingConnectionProviderService`.
- Catalog tables `rep_catalogo_informes`, `rep_catalogo_datasets`, `rep_reporte_layouts` are tenant-scoped — preserve `company_id` on any change.
- Do not serialize connection params into the persisted layout XML.

## Database / scaffold

Functional schema changes are applied as **timestamped SQL scripts** in `Database/` (e.g., `2026-03-04_add_company_cont_account_to_servicios.sql`) — there are no EF Core migrations for the SIAD context. Identity migrations are the only EF migrations and live under `apc/Migrations/`.

When refreshing the scaffold, use the command in [readme.md](readme.md) §2.7 (`dotnet ef dbcontext scaffold` with `Npgsql.EntityFrameworkCore.PostgreSQL`, `--context-dir .`, `--namespace SIAD.Core.Entities`, `--output-dir SIAD.Core/Entities`, `--use-database-names`). Always include the full `-t` list of tables already in the context — partial scaffolds will drop them. Entity bodies in `SIAD.Core/Entities/*` and the body of `SIAD.Data/SiadDbContext.cs` are generated; put custom behavior in partial files (`SiadDbContext.Tenancy.cs`, `SiadDbContext.Accounting.cs`, etc.).

Backup/restore PowerShell helpers: `Database/backup_bd_simple.ps1`, `Database/restore_bd.ps1`.

## Repo skills

Five skills under [.github/skills/](.github/skills/) — each carries a SKILL.md plus `agents/` and `references/`:

- `hodsoft-devexpress-docs` — answers DevExpress questions via the `dxdocs` MCP.
- `hodsoft-blazor-devexpress-ui` — for `apc.Client` UI work.
- `hodsoft-siad-backend` — for controller/service/DTO slices.
- `hodsoft-reporting-devexpress` — for `SIAD.Reports` and report flows.
- `hodsoft-postgres-ef-scaffold` — for `Database/`, EF scaffold, partial context files.

Prefer these skills when the task matches.
