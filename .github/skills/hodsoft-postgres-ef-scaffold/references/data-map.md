# Data Map

## Directorios principales

- `Database/`: cambios SQL incrementales.
- `Database/ddl_v2/`: DDL base por area funcional.
- `Database/Seeds/` y `Database/Seeds_v2/`: seeds y datos base.
- `SIAD.Data/`: contexto EF Core y extensiones parciales.
- `SIAD.Core/Entities/`: entidades reverse-engineered.

## Archivos clave

- `SIAD.Data/SiadDbContext.cs`
- `SIAD.Data/SiadDbContext.Tenancy.cs`
- `SIAD.Data/SiadDbContext.Accounting.cs`
- `SIAD.Data/SiadDbContextModelCacheKeyFactory.cs`
- `readme.md` para el comando scaffold base

## Reglas practicas

- Si el cambio es schema o seed del negocio, primero piensa en un script SQL nuevo.
- Si el cambio es comportamiento EF o tenancy, primero piensa en un archivo parcial.
- Si el cambio requiere regenerar entidades, prepara el scaffold y luego revisa manualmente tenancy, constraints y nombres.
- Las migraciones en `apc/Migrations` corresponden al esquema de Identity, no al modelo funcional principal.
