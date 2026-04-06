# Backend Architecture Map

## Capas

- `apc/Controllers/`: endpoints HTTP.
- `SIAD.Services/<Modulo>/`: logica y orquestacion de negocio.
- `SIAD.Core/DTOs/`: contratos compartidos.
- `SIAD.Core/Constants/`: permisos, politicas y llaves comunes.
- `SIAD.Data/`: `SiadDbContext`, tenancy y configuraciones relacionadas.

## Archivos guia

- Registro de servicios: `SIAD.Services/ServiceRegistration.cs`
- Tenancy en EF Core: `SIAD.Data/SiadDbContext.Tenancy.cs`
- Controller tenant-aware: `apc/Controllers/Contabilidad/PolizasController.cs`
- Permisos: `SIAD.Core/Constants/PermissionNames.cs`
- Claims de empresa: `apc/Security/TenantCompanyClaimTransformation.cs`

## Patron de trabajo recomendado

1. Crear o ajustar DTO en `SIAD.Core`.
2. Crear o ajustar interfaz en `SIAD.Services/<Modulo>/I*.cs`.
3. Implementar la logica en `SIAD.Services/<Modulo>/*Service.cs`.
4. Exponer endpoint en `apc/Controllers/<Modulo>/`.
5. Registrar el servicio.
6. Si la UI lo necesita, agregar cliente HTTP correspondiente.

## Dapper vs EF Core

- EF Core es la ruta normal para CRUD y reglas con entidades.
- Dapper ya se usa en algunos flujos intensivos o de reporteria; no lo expandas por inercia si EF cubre bien el caso.
