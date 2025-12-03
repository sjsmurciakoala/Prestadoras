# Plan de Migración - Módulo Medidores

## Tablas mapeadas
- `maestro_medidor`: catálogo principal de medidores (número, marca, diámetro, fecha instalación, estado).
- `historicomedicion`: lecturas históricas asociadas a contador/clave.
- `historicosinmedidor`: registro de lecturas manuales cuando no existe medidor físico.
- `configuracion_app_lectura_medidores`: parámetros usados por la app móvil para captura de lecturas.

## Scaffold aplicado
```powershell
dotnet tool run dotnet-ef dbcontext scaffold `
    'Host=3.208.232.209;Port=5432;Database=bdnes;Username=postgres;Password=Koala@2021;Timeout=10;SslMode=Prefer' `
    Npgsql.EntityFrameworkCore.PostgreSQL `
    -p SIAD.Data/SIAD.Data.csproj `
    -s apc/apc.csproj `
    -c SiadDbContext `
    --no-onconfiguring `
    --context-dir . `
    --context-namespace SIAD.Data `
    --namespace SIAD.Core.Entities `
    --use-database-names `
    --output-dir ..\SIAD.Core\Entities `
    --force --no-build `
    -t maestro_medidor `
    -t historicomedicion `
    -t historicosinmedidor `
    -t configuracion_app_lectura_medidores
```

## DTOs y servicio en la capa `SIAD`
- DTOs creados en `SIAD.Core/DTOs/Medidores`:
  - `MedidorFilterDto` (filtros: número, marca, estado, asignación, clave cliente).
  - `MedidorListDto` (listado resumido con cliente asociado).
  - `MedidorDetailDto` (detalle ampliado + historial + configuraciones).
  - `MedidorHistorialDto` (fila para lecturas históricas).
- Servicio registrado en `SIAD.Services`:
  - `IMedidoresService` / `MedidoresService` con métodos:
    - `SearchAsync`: filtra por número/marca/estado/asignación/clave.
    - `GetAsync`: devuelve detalle + cliente vinculado + configuraciones.
    - `GetHistorialAsync`: obtiene lecturas más recientes por número de medidor / claves asociadas.
    - `AssignToClienteAsync`: asigna el medidor al cliente (actualiza `cliente_detalle` y `maestro_medidor`).
    - `RegistrarLecturaSinMedidorAsync`: inserta registro en `historicosinmedidor`.

## Endpoints API (`apc/Controllers/MedidoresController.cs`)
- `GET /api/medidores` → listado con filtros (`MedidorFilterDto` via query string).
- `GET /api/medidores/{id}` → detalle de un medidor.
- `GET /api/medidores/{id}/historial?take=12` → lecturas recientes.
- `POST /api/medidores/asignar` → asigna `{ MedidorId, ClienteId }` al cliente.
- `POST /api/medidores/lecturas-sin-medidor` → registra `{ Clave, Fecha, Lectura, Usuario }`.
- Respuestas documentadas con `ProducesResponseType` + `ProblemDetails` en errores.

## UI Blazor (`apc.Client/Pages/Clientes/ClienteDetail.razor`)
- Nueva pestaña **“Medidores”** con:
  - Botones para cargar listado, abrir asignación y registrar lecturas sin medidor.
  - `DxGrid` con `MedidorListDto` mostrando número, marca, diámetro, fecha instalación, estado y cliente.
  - Acción “Ver detalle” que abre `DxPopup` con información completa y un `DxGrid` de `MedidorHistorialDto`.
  - Popup “Asignar medidor” (combo de medidores disponibles) y formulario “Registrar lectura sin medidor”.
  - Uso de `DxToastProvider` para mensajes de éxito/error.

## Script de semilla
- `Database/2025-10-23_seed_medidores.sql`
  - Inserta/actualiza el medidor demo `MED-DEMO-001`.
  - Lo asigna al cliente `CLI-DEMO-001` (crea `cliente_detalle` si no existe).
  - Marca al cliente con `maestro_cliente_tiene_medidor = true`.
  - Agrega dos lecturas en `historicomedicion`.
- Ejecutar con:  
  `psql -h 3.208.232.209 -U postgres -d bdnes -f Database/2025-10-23_seed_medidores.sql`

## Validación manual
1. Ejecutar el seed anterior.
2. `dotnet build apc.sln` → sin errores; advertencias esperadas de DevExpress/EF.
3. `dotnet run --project apc/apc.csproj`.
4. En la app, iniciar sesión y abrir `Clientes → CLI-DEMO-001`:
   - Botón **Cargar medidores** muestra el medidor demo con columnas completas.
   - **Ver detalle** despliega popup con datos, historial de lecturas y configuraciones.
   - **Asignar medidor** reapunta correctamente al cliente y muestra toast de éxito.
   - **Registrar lectura sin medidor** acepta la captura y responde 204.
5. API comprobada vía navegador/postman:
   - `GET /api/medidores?ClienteClave=CLI-DEMO-001`
   - `GET /api/medidores/{id}`
   - `GET /api/medidores/{id}/historial`

## Checklist
- [x] Scaffold de tablas de medidores.
- [x] DTOs y servicio `IMedidoresService` / `MedidoresService`.
- [x] Endpoints `MedidoresController`.
- [x] UI (pestaña "Medidores" en `ClienteDetail.razor`).
- [x] Script de semilla `Database/2025-10-23_seed_medidores.sql`.
- [x] Actualizar `README_estado_actual.md` con el estado final del módulo.
