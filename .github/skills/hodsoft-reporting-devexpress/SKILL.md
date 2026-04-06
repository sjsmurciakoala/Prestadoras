---
name: hodsoft-reporting-devexpress
description: Trabaja sobre DevExpress Reporting, catalogos de reportes, datasets, web designer, web viewer y storage de layouts para HODSOFT. Use this when editing `SIAD.Reports`, reporting controllers/pages/services, report datasets, or report design/publish flows. Before changing DevExpress Reporting behavior or APIs, consult the official docs through `dxdocs`.
---

# HODSOFT Reporting DevExpress

Usa esta skill para cambios en reporteria web, datasets y persistencia de layouts.

## Workflow

1. Consulta primero la documentacion oficial con `$hodsoft-devexpress-docs` o `dxdocs`.
2. Revisa el bootstrap de reporting en `apc/Program.cs`.
3. Ubica el flujo afectado:
   - catalogo de reportes
   - catalogo de datasets
   - draft/published layout
   - viewer/designer
   - SQL validation o compatibilidad de data sources
4. Ajusta backend, UI y SQL del catalogo si el cambio cruza capas.

## Reglas

- Conserva el alcance por empresa en `rep_catalogo_informes`, `rep_catalogo_datasets`, `rep_reporte_layouts` y tablas relacionadas.
- Manten la validacion de SQL custom como solo lectura: `SELECT` o `WITH ... SELECT`.
- No serialices parametros de conexion dentro del XML persistido del layout.
- Si tocas compatibilidad de data sources, revisa tambien la logica de stored functions y migracion de queries.
- Si agregas un nuevo reporte o dataset, confirma que exista el soporte SQL/catalogo correspondiente.

## Archivos clave

- `apc/Program.cs`
- `SIAD.Reports/Reporting/CompanyReportStorageWebExtension.cs`
- `SIAD.Reports/Reporting/ReportingCustomSqlValidation.cs`
- `SIAD.Reports/Reporting/ReportTemplateFactory.cs`
- `apc.Client/Pages/Informes/*`
- `apc/Controllers/Informes/*`

## Evita

- Abrir la puerta a SQL destructiva en reporteria.
- Saltarte `company_id` en catalogos o layouts.
- Cambiar el flujo draft/publish sin revisar el impacto en viewer y designer.

## Referencia rapida

Lee `references/reporting-map.md` para un mapa de tablas, archivos y scripts relacionados.
