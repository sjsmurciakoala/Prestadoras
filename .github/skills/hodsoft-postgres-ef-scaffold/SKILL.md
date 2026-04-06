---
name: hodsoft-postgres-ef-scaffold
description: Cambia scripts PostgreSQL, reglas de scaffold, entidades y acceso a datos con EF Core para HODSOFT, respetando la estructura real del proyecto y la multiempresa. Use this when editing `Database/**/*.sql`, `SIAD.Data`, reverse-engineered entities, `SiadDbContext`, scaffold commands, or Npgsql configuration. Preserve scaffolded code carefully and keep `company_id` rules consistent.
---

# HODSOFT Postgres EF Scaffold

Usa esta skill cuando el trabajo cae en SQL, schema, EF Core o codigo sensible al scaffold.

## Workflow

1. Decide si el cambio pertenece a:
   - script SQL incremental
   - ajuste parcial de `SiadDbContext`
   - entidad o mapping derivado del scaffold
   - refresh de scaffold
2. Para cambios de BD funcional, prefiere scripts fechados en `Database/`.
3. Para cambios de comportamiento EF, intenta resolverlos en archivos parciales antes de tocar el cuerpo scaffolded.
4. Si haces refresh de scaffold, valida despues tenancy, relaciones e indices relevantes.

## Reglas

- Trata `SIAD.Data/SiadDbContext.cs` y gran parte de `SIAD.Core/Entities/*` como codigo generado.
- Prefiere extensiones parciales como `SiadDbContext.Tenancy.cs` y `SiadDbContext.Accounting.cs`.
- Si agregas una tabla multiempresa, incluye `company_id`, indices/constraints tenant-safe y el contrato `ICompanyScopedEntity` cuando corresponda.
- No mezcles migraciones de Identity con cambios de la BD funcional.
- Si una modificacion requiere nuevo scaffold, parte del comando documentado en `readme.md` y evita omitir tablas ya incluidas en el contexto.

## Tenancy

- Los filtros por empresa y la asignacion automatica de `company_id` viven en `SiadDbContext.Tenancy.cs`.
- No rompas `IModelCacheKeyFactory` ni `TenantModelKey` sin revisar el impacto sobre los modelos por empresa.

## Referencia rapida

Lee `references/data-map.md` para rutas, scripts y archivos clave de datos.
