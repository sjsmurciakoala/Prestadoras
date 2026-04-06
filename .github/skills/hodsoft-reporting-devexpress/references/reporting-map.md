# Reporting Map

## Bootstrap y runtime

- Registro principal: `apc/Program.cs`
- Bootstrap de runtime: `SIAD.Reports/ReportingRuntimeBootstrap.cs`
- Registro DI de reporteria: `SIAD.Reports/ServiceCollectionExtensions.cs`

## Persistencia y compatibilidad

- Storage por empresa: `SIAD.Reports/Reporting/CompanyReportStorageWebExtension.cs`
- Validacion SQL: `SIAD.Reports/Reporting/ReportingCustomSqlValidation.cs`
- Helper de stored functions: `SIAD.Reports/Reporting/ReportingStoredFunctionSqlHelper.cs`
- Fabrica de layouts: `SIAD.Reports/Reporting/ReportTemplateFactory.cs`

## UI y API asociadas

- Paginas UI: `apc.Client/Pages/Informes/*`
- Cliente HTTP: `apc.Client/Services/Informes/InformesClient.cs`
- Controllers: `apc/Controllers/Informes/*`

## Scripts SQL relacionados

- `Database/2026-03-21_add_rep_catalogo_dataset.sql`
- `Database/2026-03-21_add_rep_reporte_layout.sql`
- `Database/2026-03-25_add_rep_balance_comprobacion_dataset.sql`

## Regla de SQL custom

La validacion actual bloquea comentarios, `;`, DDL/DML y sentencias peligrosas. Si el cambio toca datasets o custom SQL, conserva ese nivel de restriccion.
