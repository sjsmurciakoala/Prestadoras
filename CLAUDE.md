# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Source of truth

The current code and `HODSOFT_DEVEXPRESS.sln` are authoritative. `readme.md` and other docs may contradict the live solution — follow the code. The same applies to `docs/` (planning/handoff notes, not specs).

## Build / run / publish

- Solution build: `dotnet build HODSOFT_DEVEXPRESS.sln` (the only VS Code task is `dotnet-build-solution`).
- Run portal locally: `dotnet run --project apc/apc.csproj` (hosts the WASM client at `apc.Client` as a static asset and exposes the API).
- On-prem publish: `./publish-onprem.ps1` (`-Solo portal|ws|bancosws|mobileapi|todos`, default `todos`; `-Output <path>`). Targets `win-x64`, framework-dependent, ReadyToRun. For a real deploy follow [docs/RUNBOOK_DEPLOY_2026-07.md](docs/RUNBOOK_DEPLOY_2026-07.md) and publish each host explicitly rather than relying on `-Solo todos`.
- Connection string lives in `apc/appsettings.json` under `ConnectionStrings:DefaultConnection` (Npgsql/Azure Postgres). Identity migrations write to schema `identity` (`__IdentityMigrationsHistory`). The SIAD functional DB does **not** use EF migrations — see Database section.
- Default culture is forced to `es-HN` in `apc/Program.cs`; preserve it.
- Run tests: `$env:SIAD_TEST_DB = '<connection string>'; dotnet test SIAD.Tests/SIAD.Tests.csproj` (without `SIAD_TEST_DB` the integration tests are `Skipped`). Optional `$env:SIAD_TEST_COMPANY_ID` (default `2`). Each test wraps its work in `BEGIN ... ROLLBACK` so the target DB stays clean.
- Run a single test class/method: append `--filter "FullyQualifiedName~NotaCreditoTests"` (or `~NotaCreditoTests.SP_emitir_nota_credito_rechaza_factura_inexistente`).
- Point `SIAD_TEST_DB` at a **test** database (a `siad_v3` DB with `Database/ddl_v3/*` applied) — never prod. See [SIAD.Tests/README.md](SIAD.Tests/README.md) for coverage and caveats (e.g., rollback would not cover SPs using dblink/autonomous transactions).

Tests: see `SIAD.Tests/` (xUnit integration tests against a real Postgres). No lint config beyond default .NET analyzers.

## Solution layout

Nine projects, all .NET 9:

| Project        | Role                                                                            |
|----------------|---------------------------------------------------------------------------------|
| `apc`          | ASP.NET Core host. Identity, controllers, DevExpress Reporting, PDF Viewer. Razor Components with **InteractiveServer** render mode and the WASM client mounted as `AdditionalAssemblies`. |
| `apc.Client`   | Blazor WebAssembly. `DevExpress.Blazor`, PDF viewer, Skia PDF renderer. Pages and HTTP clients live here. |
| `SIAD.Core`    | Entities (scaffolded from Postgres), DTOs, constants (`PermissionNames`, `RoleNames`, `PermissionEndpointCatalog`), tenancy contracts. |
| `SIAD.Data`    | `SiadDbContext`, partial across one file per module (`SiadDbContext.cs` plus `*.Tenancy.cs`, `*.Accounting.cs`, `*.Almacen.cs`, `*.BancosWs.cs`, `*.Cobranza.cs`, `*.CodigoCliente.cs`, `*.CondicionesLectura.cs`, `*.Impuestos.cs`, `*.IntegracionContable.cs`, `*.Libretas.cs`, `*.Mantenimientos.cs`, `*.NotasCreditoDebito.cs`, `*.PeriodoComercial.cs`, `*.SarCompliance.cs`), Npgsql config. New module tables go in a new/matching partial, not in the scaffolded body. |
| `SIAD.Services`| Per-module domain services (Clientes, Cobranza, Contabilidad, Bancos, Tarifario, etc.), AutoMapper profiles (assembly-scanned), DI in `ServiceRegistration.cs`. |
| `SIAD.Reports` | DevExpress Reporting bootstrap, per-company `ReportStorageWebExtension`, SQL validation, templates. |
| `SIAD.Tests`   | xUnit integration tests using Npgsql + Dapper. Each test runs inside `BEGIN ... ROLLBACK` so the DB stays clean. Requires the `SIAD_TEST_DB` env var with a Postgres connection string; without it the tests are marked `Skipped`. Covers billing V3 (emission, idempotency, mora, tercera edad, CAI), NC/ND, integración contable (asientos, lote, saldos oficiales, periodos/cierres F7), WS bancario (golden files), apertura de ciclo, condiciones de lectura, snapshots offline, SAR. |
| `apc.BancosWs` | Standalone net9 WS host (WS bancario SIMAFI, F8). No Identity/Blazor; own auth middleware and credential-based `ICurrentCompanyService`. Deployed separately (`publish-onprem.ps1 -Solo bancosws`). |
| `apc.MobileApi`| Standalone net9 WS host (REST API for the Flutter reader app, L3). Same pattern as `apc.BancosWs`: auth via `adm_lector_credencial`/`adm_lector_sesion`, deployed with `-Solo mobileapi`. |

The Identity DbContext is `apc.Data.ApplicationDbContext` and is separate from `SiadDbContext`. Identity migrations are in `apc/Migrations/`; **do not put functional DB changes there**.

Other directories in this folder (`apc.Client.Tests/`, `VerificadorUsuarios/`, `tools/`, `temp_excel_*`, `publish_*`, `artifacts/`, `*.log`) are untracked scratch/publish leftovers — they are **not** part of the solution; do not edit or reference them.

## Architecture: a slice through the stack

A typical module flows: DTO in `SIAD.Core/DTOs/<Modulo>/` → interface + impl in `SIAD.Services/<Modulo>/` → controller in `apc/Controllers/<Modulo>/` (thin: validate, resolve tenant, delegate) → HTTP client in `apc.Client/Services/<Modulo>/` → page in `apc.Client/Pages/<Modulo>/`.

Wiring is centralized:

- Domain services: register in [SIAD.Services/ServiceRegistration.cs](SIAD.Services/ServiceRegistration.cs) (`AddSiadServices`), called from `apc/Program.cs`.
- Client HTTP services: register in [apc.Client/CommonServices.cs](apc.Client/CommonServices.cs), called from both `apc/Program.cs` (server-interactive) and `apc.Client/Program.cs` (WASM).

Keep the async patterns of the module you touch — if its services already accept `CancellationToken`, thread it through new methods too.

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

Permission catalog lives in [SIAD.Core/Constants/PermissionNames.cs](SIAD.Core/Constants/PermissionNames.cs) (all three classes in one file):

- `PermissionModules` — top-level modules (`ventas`, `bancos`, `compras`, `proveedores`, `inventario`, `contabilidad`, `reporteria`, `configuracion`).
- `PermissionResources` — submodules per module.
- `PermissionNames` — string constants like `"module.ventas.clientes.edit"` plus a `Policies` table consumed by `AddAuthorization` in both `apc/Program.cs` and `apc.Client/Program.cs`.
- `PermissionEndpointCatalog` — endpoint → resource mapping used by `ModuleAuthorize` to derive a resource from the route pattern.
- `RoleNames.SuperAdministrador` is the global bypass; the auth assertion is `IsInRole(SuperAdministrador) || HasClaim(permission)`.

On controllers/actions use `[ModuleAuthorize(module)]`, `[ModuleAuthorize(module, action)]`, `[ModuleAuthorize(module, resource)]` or `[ModuleAuthorize(module, resource, action)]` — see [apc/Security/ModuleAuthorizeAttribute.cs](apc/Security/ModuleAuthorizeAttribute.cs). HTTP method is mapped to `PermissionAction` (`GET→View`, `POST→Create`, `PUT/PATCH→Edit`, `DELETE→Delete`). The attribute walks fallbacks: endpoint-specific → base resource → module-level → legacy `module.<name>` (View only).

When adding a new endpoint, register its permission name in `PermissionNames` and (if endpoint-specific) in `PermissionEndpointCatalog`.

## Statuses and codes

String-based statuses are being migrated to numeric lookups — see [SIAD.Core/Constants/EstadosNumericos.cs](SIAD.Core/Constants/EstadosNumericos.cs). Do not introduce new string-state columns or compare against magic status strings; use the numeric constants.

## Blazor / DevExpress UI conventions

- DevExpress version is **25.2.4** on .NET 9 (check the csproj, not docs — it gets bumped). Configured size mode: `Small`.
- Pages live in `apc.Client/Pages/<Modulo>/`. Honor existing `.razor` / `.razor.cs` / `.razor.css` / `*GridDataSource.cs` separation when a module already uses it.
- For HTTP, use the auth-aware extensions in [apc.Client/Services/HttpClientExtensions.cs](apc.Client/Services/HttpClientExtensions.cs): `ReadFromJsonAsyncWithAuthCheck`, `GetFromJsonAsyncWithAuthCheck`, `PostAsJsonAsyncWithAuthCheck`, `PutAsJsonAsyncWithAuthCheck`, and `ObtenerMensajeErrorAsync`. They throw `UnauthorizedAccessException` on 401 / login redirects.
- Keep business rules in services, not pages — pages orchestrate state and HTTP.
- **Views with a `DxGrid` follow the grid standard** documented in [.github/skills/hodsoft-blazor-devexpress-ui/references/grid-standard.md](.github/skills/hodsoft-blazor-devexpress-ui/references/grid-standard.md). Reference implementation: `apc.Client/Pages/Clientes/ClientesList.razor` (the whole `Almacen` module is migrated). The shared look lives **once** in [apc/wwwroot/css/siad-grid.css](apc/wwwroot/css/siad-grid.css) (loaded from `apc/Components/App.razor`) — do not copy that block into a page's `.razor.css`; a `.razor.css` only carries what is specific to that page. Contabilidad and Facturación are out of scope.
- **DevExpress API workflow is mandatory**: before changing any DevExpress component behavior or API surface, query the `dxdocs` MCP server (`devexpress_docs_search` → `devexpress_docs_get_content`) configured in [.vscode/mcp.json](.vscode/mcp.json). Do not invent properties/events.

## Reporting

DevExpress Web Designer + Web Viewer + PDF Viewer are bootstrapped in [apc/Program.cs](apc/Program.cs):

- `ReportingRuntimeBootstrap.Initialize` is called after `Build()`.
- Custom SQL is enabled but validated read-only (`SELECT` / `WITH ... SELECT`) via `ICustomQueryValidator` → `ReportingCustomQueryValidator`.
- Per-company report storage: `CompanyReportStorageWebExtension`.
- Connection string providers: `ReportingConnectionProviderFactory` / `ReportingConnectionProviderService`.
- Catalog tables `rep_catalogo_informes`, `rep_catalogo_datasets`, `rep_reporte_layouts` are tenant-scoped — preserve `company_id` on any change.
- Register new catalog datasets/reports with timestamped SQL scripts in `Database/`, **not** in C# seed code.
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
