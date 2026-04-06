---
applyTo: "apc/Controllers/**/*.cs,SIAD.Services/**/*.cs,SIAD.Core/**/*.cs,SIAD.Data/**/*.cs"
---

- Respeta la arquitectura por capas: DTOs en `SIAD.Core`, logica en `SIAD.Services`, endpoints en `apc/Controllers`.
- En endpoints tenant-aware, resuelve la empresa con `ICurrentCompanyService` y no rompas el filtro por `company_id`.
- Protege acciones con `ModuleAuthorize` o con politicas basadas en `PermissionNames`.
- Registra servicios nuevos en `SIAD.Services/ServiceRegistration.cs`.
- Prefiere cambios parciales o focalizados en `SiadDbContext`; evita reescribir a mano grandes zonas scaffolded si una extension parcial resuelve el caso.
- Usa `CancellationToken` y patrones async cuando el flujo ya sea asincrono.
