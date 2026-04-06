---
name: hodsoft-devexpress-docs
description: Consulta oficial de DevExpress Blazor, Reporting, PDF Viewer y Data Access mediante el MCP `dxdocs`. Use this when a task mentions DevExpress namespaces, `Dx*` components, `XtraReport`, `ReportStorageWebExtension`, `SqlDataSource`, report designer/viewer, PDF viewer, or any change that depends on DevExpress API details. Search official DevExpress docs first and use repo docs only as secondary context.
---

# HODSOFT DevExpress Docs

Usa esta skill para forzar una disciplina simple: primero la documentacion oficial de DevExpress, despues el codigo local.

## Workflow

1. Extrae los nombres tecnicos reales del problema.
   Ejemplos: `DxGrid`, `DxPopup`, `DxComboBox`, `DevExpress.Blazor.Reporting`, `ReportStorageWebExtension`, `ICustomQueryValidator`, `SqlDataSource`.
2. Consulta `dxdocs` antes de proponer cambios o afirmar que una API existe.
3. Abre el articulo oficial mas cercano al componente o servicio que vas a tocar.
4. Cruza lo encontrado con el uso local en este repo.
5. Solo despues redacta la solucion o implementa el cambio.

## Reglas

- Trata la documentacion oficial como fuente primaria para DevExpress.
- Usa la version del repo como referencia de compatibilidad: DevExpress `25.1.7` sobre `.NET 9`.
- Si la doc oficial no cubre exactamente el caso, dilo explicitamente y apoya la implementacion en el codigo ya existente del repo.
- No sustituyas la fuente oficial con blogs, foros o respuestas inventadas.

## Donde mirar despues de la doc oficial

- `apc/Program.cs`
- `apc.Client/Program.cs`
- `apc.Client/Pages/**/*`
- `SIAD.Reports/Reporting/*`
- `SIAD.Reports/Templates/*`

## Referencia rapida

Lee `references/search-map.md` para un mapa de namespaces, componentes y archivos locales utiles.
