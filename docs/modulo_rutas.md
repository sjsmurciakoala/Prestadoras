# Modulo Rutas y Cuadrillas

## Objetivo
Migrar a la arquitectura Blazor/DevExpress el mantenimiento legacy de rutas de lectura/captacion (catalogo, asignacion a ciclos y soporte a cuadrillas/Mi Orden) que vivia en `ASPNET_Core_3`.

## Inventario heredado (`Prestadoras/modulo_rutascuadrillas/`)
| Artefacto | Descripcion |
| --- | --- |
| `RutasController.cs` (Area `RutasArea`) | Acciones `RutasIndex`, `getrutas`, `GetRutasForm`, `Guardar`. |
| `RutasRepository.cs` / `IRutasRepository.cs` | CRUD Dapper contra tabla `rutas` + `ListaCiclos()` (combo). |
| DTOs | `RutasDTO` (Id, Codciclo, Codruta, Descripcion). |
| Vistas Razor | `RutasIndex.cshtml` (DataTables) y `_FormRutas.cshtml` (Select2 + validaciones). |

### Tablas disponibles en `Database/TablasPosgrets/`
- `rutas.sql`: id, codciclo (FK `ciclos`), codruta, descripcion.
- `ciclos.sql`: codigo, descripcion corta/larga, estado y metadatos.

## Flujo legacy resumido
1. `RutasIndex` mostraba tabla + botones que llamaban a `RutasController.getrutas`.
2. El controlador hablaba con `_unitOfWork.rutas` (`RutasRepository`) para listar/editar usando SQL directo.
3. El modal cargaba combos de ciclos (`ListaCiclos`) y los datos a editar.
4. Guardar disparaba `_unitOfWork.rutas.SAVEUPDATE`, que hacia INSERT/UPDATE interpolando cadenas.

## Estado actual (11/11/2025)
- CRUD migrado end-to-end: DTOs (`SIAD.Core`), servicio (`SIAD.Services.Rutas`), API (`apc/Controllers`), cliente HTTP (`apc.Client/Services`) y UI (`apc.Client/Pages/Rutas`).
- Navegacion Blazor actualizada con la opcion **Rutas**.
- Seed `Database/2025-10-30_seed_rutas.sql` crea 3 ciclos demo y 4 rutas, listo para pruebas manuales.
- Documentacion y README general sincronizados con el avance.

## Arquitectura actual

### DTOs (`SIAD.Core/DTOs/Rutas/`)
- `RutaListItemDto`, `RutaDetailDto`, `RutaUpsertDto`, `RutaFilterDto`, `CicloLookupDto`.
- `RutaUpsertDto` usa DataAnnotations (`[Range]`, `[Required]`, `[StringLength]`) para reutilizar reglas en la UI y validacion de API.

### Servicios (`SIAD.Services/Rutas/`)
- `IRutasService` define operaciones para listar, obtener, crear, actualizar y obtener ciclos.
- `RutasService` usa `SiadDbContext` (`Npgsql`). Filtros usan `EF.Functions.ILike` con fallback para proveedores in-memory (necesario para pruebas).
- Alta/edicion normalizan cadenas, validan nulos y lanzan `KeyNotFoundException` si el id no existe.
- Registrado en `ServiceRegistration` (`services.AddScoped<IRutasService, RutasService>();`).

### API (`apc/Controllers/RutasController.cs`)
| Metodo | Ruta | Descripcion |
| --- | --- | --- |
| `GET` | `/api/rutas?codCiclo=&codRuta=` | Lista con filtros opcionales. |
| `GET` | `/api/rutas/{id}` | Devuelve `RutaDetailDto`. |
| `POST` | `/api/rutas` | Alta, responde `201 Created`. |
| `PUT` | `/api/rutas/{id}` | Actualiza en sitio. |
| `GET` | `/api/rutas/ciclos` | Combo de ciclos activos (`CicloLookupDto`). |

### Cliente Blazor (`apc.Client`)
- `Services/Rutas/RutasClient.cs`: encapsula llamadas REST (`GetAsync`, `GetById`, `Create`, `Update`, `GetCiclos`).
- `Pages/Rutas/RutasList.razor`: pagina `[Authorize]` con filtros (`DxFormLayout`), grid (`DxGrid`), botones y `DxPopup`.
- `Pages/Rutas/RutaForm.razor`: componente reutilizable con `EditForm`, validaciones y combos DevExpress.
- `Layout/NavMenu.razor`: agrega `DxMenuItem` -> `/rutas`.

### Base de datos y seed
- Ejecutar el demo:
  ```bash
  psql -h <host> -U <usuario> -d <bd> -f Database/2025-10-30_seed_rutas.sql
  ```
- El script crea/asegura ciclos (`CIC-AGUA-001/002`, `CIC-CORTE-001`) y rutas `A01`, `A02`, `A10`, `C01` solo si no existen.

## Pruebas

### Unit tests
- Por ahora no hay proyecto de pruebas automatizadas activo (se retiraron a solicitud del equipo). Si en el futuro se reanudan, la prioridad es cubrir filtros, altas y actualizaciones del `RutasService`.

### Checklist manual (pendiente)
1. Cargar seed `2025-10-30_seed_rutas.sql`.
2. Ejecutar `dotnet run --project apc` y navegar a `https://localhost:5001/rutas`.
3. Validar:
   - Grid inicial con 4 rutas demo (ordenadas por ciclo/codigo).
   - Filtro por ciclo (Agua Centro) muestra `A01` y `A02`.
   - Crear ruta `A20` (Agua Norte) y confirmar aparicion sin recargar.
   - Editar `C01` cambiando descripcion y verificar persistencia.
4. Estado: **Pendiente** (requiere sesion interactiva en UI/DevExpress).

## Checklist
- [ ] Documentar tablas adicionales (cuadrillas, rutas_detalle, zonas) si aplica.
- [x] Crear DTOs en `SIAD.Core`.
- [x] Implementar `IRutasService` y registrarlo en `ServiceRegistration`.
- [x] Exponer API `RutasController`.
- [x] Construir UI Blazor (`RutasList` + `RutaForm`) y agregar la opcion al menu.
- [x] Preparar seed `2025-10-30_seed_rutas.sql`.
- [ ] Agregar pruebas unitarias + checklist manual en este documento.

## Preguntas abiertas
1. Que perfiles/permisos deben operar el CRUD de rutas (alinear con Mi Orden/Ordenes)?
2. Se requerira integrar asignacion de cuadrillas, geocercas o mapas en la misma pantalla?
3. Se debe imponer un patron especifico para `codruta` (prefijo por ciclo, longitud fija, unicidad)?
4. Existen procesos batch/auxiliar que dependan de `rutas` (ej. generar lecturas) que debamos considerar antes de ampliar el modelo?
