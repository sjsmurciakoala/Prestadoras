---
name: hodsoft-siad-backend
description: Implementa o refactoriza backend en la arquitectura por capas de HODSOFT: controllers en `apc`, servicios en `SIAD.Services`, contratos en `SIAD.Core` y acceso a datos en `SIAD.Data`. Use this when adding endpoints, DTOs, services, permissions, tenancy-aware flows, or module wiring. Respect `company_id`, `ICurrentCompanyService`, `ModuleAuthorize`, and the existing registration patterns.
---

# HODSOFT SIAD Backend

Usa esta skill para tocar backend sin mezclar responsabilidades ni romper tenancy.

## Workflow

1. Traza el slice completo del modulo:
   - DTOs en `SIAD.Core`
   - interfaz e implementacion en `SIAD.Services`
   - controller en `apc/Controllers`
   - cliente HTTP en `apc.Client/Services` si hay consumo desde UI
2. Revisa si el modulo ya tiene permisos, rutas y servicios equivalentes.
3. Implementa el cambio en la capa correcta.
4. Registra el servicio nuevo en `SIAD.Services/ServiceRegistration.cs`.
5. Si hay nueva API para la UI, agrega o ajusta el cliente HTTP y registralo en `apc.Client/CommonServices.cs`.

## Reglas

- Los controllers deben ser delgados: validar entrada, resolver contexto, delegar al servicio, devolver respuesta.
- Para datos tenant-aware, resuelve la empresa con `ICurrentCompanyService`.
- Usa `ModuleAuthorize` o politicas basadas en `PermissionNames`.
- Manten las firmas async con `CancellationToken` donde el modulo ya las usa.
- Usa `SiadDbContext` como via principal de acceso a datos; usa Dapper solo cuando el propio flujo ya siga ese patron y el caso lo justifique.
- No dupliques logica de negocio entre controller y servicio.

## Tenancy

- Respeta `ICompanyScopedEntity`, `ApplyCompanyInformation()` y los filtros configurados en `SiadDbContext.Tenancy.cs`.
- No permitas que una actualizacion cambie `company_id` en entidades ya existentes.
- Si una consulta necesita comportamiento cross-company, documenta y justifica explicitamente el escape del filtro.

## Seguridad

- Reusa `PermissionModules`, `PermissionNames`, `PermissionEndpointCatalog` y `AuthorizationPolicies`.
- Si agregas un endpoint nuevo, confirma que su permiso encaje en el modulo correcto y que el nombre sea consistente con el catalogo existente.

## Referencia rapida

Lee `references/architecture-map.md` para ver rutas y archivos guia del backend actual.
