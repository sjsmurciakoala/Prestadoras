# Historial de decisiones recientes

## 17 oct 2025 – reinicio del proyecto con pc
1. Se detectó que la solución anterior (DXApplicationAPC) se basaba en la plantilla **ASP.NET Core Blazor Reporting Application** (Blazor Server). Decidimos reiniciar sobre una nueva plantilla **Blazor WebAssembly hospedada** (DevExpress reporting) para tener una arquitectura más acorde al despliegue final.
2. Nueva estructura creada en d:\jesse\Documents\apc:
   - pc.Client (WASM)
   - pc.Server (host + DevExpress reporting)
   - pc.Shared
3. Se añadieron librerías vacías para la migración de dominio:
   - SIAD.Core, SIAD.Data, SIAD.Services, SIAD.Reports
   - Referencias configuradas para pc.Server -> SIAD.Services -> SIAD.Data -> SIAD.Core
4. Documentación (eadme.md, 	rabajo.md) actualizada con la nueva arquitectura y plan.

## Consideraciones acordadas
- El despliegue se hará publicando **apc.Server**; no es necesario publicar APIs adicionales por separado. El servidor hospedará tanto la API como la UI WASM.
- pc.Client consumirá las APIs internas del servidor; no se expondrá la base de datos directamente al navegador.
- eadme_inventario.md sigue vigente para el entorno legacy (ASPNET_Core_3).

## Próximos pasos
1. Poblar SIAD.Core con entidades de Administración (primer módulo a migrar).
2. Configurar SiadDbContext en SIAD.Data y conexión PostgreSQL.
3. Implementar servicios en SIAD.Services y los endpoints correspondientes en pc.Server.
4. Sustituir el layout/base UI de pc.Client conforme a la guía visual.
