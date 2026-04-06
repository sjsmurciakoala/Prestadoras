---
applyTo: "Database/**/*.sql,SIAD.Data/**/*.cs,SIAD.Core/Entities/**/*.cs"
---

- Prefiere cambios aditivos con scripts SQL fechados dentro de `Database/`.
- Trata `SIAD.Data/SiadDbContext.cs` y muchas entidades como codigo scaffolded; evita cambios amplios manuales si una extension parcial basta.
- Si una tabla es multiempresa, manten `company_id`, indices/constraints tenant-safe y la integracion con `ICompanyScopedEntity` cuando corresponda.
- Manten separadas las migraciones de Identity en `apc/Migrations` de los cambios de la BD funcional.
- Si refrescas scaffold, usa el comando base documentado en `readme.md` y valida que no se pierdan tablas ya incluidas.
