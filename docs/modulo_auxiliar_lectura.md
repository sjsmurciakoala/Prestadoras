## Auxiliar de Lectura – estado al 23/10/2025

### Inventario migrado
- **API** (`apc/Controllers/AuxiliarLecturaController.cs`):
  - `GET /api/auxiliarlectura?anio&mes&ciclo&soloPendientes` – filtra lecturas desde `historicomedicion`.
  - `GET /api/auxiliarlectura/periodo-actual` – devuelve el último periodo abierto (`historialmes`).
  - `POST /api/auxiliarlectura` – genera un nuevo periodo con roll-over de lecturas.
  - `POST /api/auxiliarlectura/cierre` – valida que no existan lecturas pendientes y cierra el periodo.
  - `DELETE /api/auxiliarlectura` – elimina el periodo siempre que no existan lecturas confirmadas.
  - `POST /api/auxiliarlectura/masivo` – registra lecturas en lote reutilizando `SIAD.Services`.
- **Servicio** (`SIAD.Services/AuxiliarLectura/AuxiliarLecturaService`):
  - Implementa las operaciones anteriores usando `SiadDbContext` ya scaffoldeado.
  - En la carga masiva respeta opcionalmente el ciclo y recalcula consumo.
- **DTOs** (`SIAD.Core/DTOs/AuxiliarLectura`):
  - `AuxiliarLecturaDto`, `AuxiliarLecturaFilterDto`, `AuxiliarLecturaPeriodoDto`, `LecturaMasivaDto` + `LecturaMasivaItemDto`.
- **UI Blazor** (`apc.Client/Pages/Clientes/ClienteDetail.razor` – pestaña “Auxiliar de Lectura”):
  - Filtros por año/mes/ciclo/solo pendientes.
  - Indicadores de carga, mensajes de error y estado “sin resultados”.
  - Popups para generar periodo y carga masiva (entrada semicolon separada, validación y feedback).

### Cómo probar
1. **Listado y filtros**  
   Desde `/clientes/{id}` abrir la pestaña *Auxiliar de Lectura*, ajustar filtros y presionar *Cargar lecturas*.
2. **Generar periodo**  
   Usar el popup “Generar periodo”, confirmar que aparece toast de éxito y que se refresca la grilla.
3. **Cerrar/Eliminar periodo**  
   Probar botones correspondientes verificando mensajes de validación cuando hay lecturas pendientes.
4. **Carga masiva**  
   - Abrir el popup, pegar líneas tipo `CLI-001;MED-001;120;135;USR01`.  
   - Pulsar *Enviar*, revisar toast de éxito y que las lecturas cambien en la grilla.
   - Validar manualmente vía Postman con `POST /api/auxiliarlectura/masivo`.

### Seeds recomendados
- Preparar un script de datos ficticios (`Database/Seeds/2025-10-23_auxiliar_lectura.sql`) con:  
  - Periodo abierto (`historialmes`).  
  - Lecturas base en `historicomedicion` vinculadas a clientes demo.  
  - Usuarios de cuadrilla para las lecturas masivas (opcional).
- Documentar ejecución (`psql -h ... -f Database/Seeds/...sql`).

### Pendiente / próximos pasos
- Resolver advertencias RZ10012 (registrar los componentes DevExpress en `_Imports.razor` de la app host).
- Agregar pruebas automatizadas para `AuxiliarLecturaService` (generar/cerrar/eliminar periodo y carga masiva).
- Integrar el módulo siguiente del backlog legado: **Órdenes de corte/trabajo** (`modulo_ordenes.md` por crear).
