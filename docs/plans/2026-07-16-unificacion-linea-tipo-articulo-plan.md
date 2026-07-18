# Plan: unificar "Grupo" (alm_linea) con "Tipo de artículo" (alm_tipo_articulo)

**Fecha:** 2026-07-16 · **Estado:** Fases 0, 1 y 2 ejecutadas (Fase 2: código completo, build 0 errores;
saneo aplicado en mirror — **pendiente aplicar `2026-07-16_alm_articulo_saneo_sin_tipo.sql` en SRV**). Sigue Fase 3.

## Contexto

El 2026-07-16 se sembró `alm_tipo_articulo` con los 9 grupos de `alm_linea` (mismos códigos 01–09)
y se hizo backfill de `alm_articulo.tipo_articulo_id` desde `linea_id` (631 artículos, mirror y SRV).
Desde entonces conviven dos catálogos gemelos: el combo "Grupo" del artículo (`alm_linea`) y el
catálogo "Tipos de artículo" (`alm_tipo_articulo`, con pantalla de mantenimiento, 5 cuentas contables
y `maneja_inventario`). Este plan elimina `alm_linea` y deja el tipo como única clasificación,
re-colgando las categorías (`alm_grupo`, 164 filas) del tipo.

Base del análisis: barrido multi-agente del 2026-07-16 (110 hallazgos + crítica de completitud).
Verificado en mirror y SRV: **cero** SPs, vistas, datasets o layouts de reportes dependen de
`alm_linea`/`alm_grupo`; no hay AutoMapper ni tests que las toquen; permisos solo a nivel módulo
(nada que limpiar); MobileApi/BancosWs sin impacto.

## Decisiones asumidas (confirmar antes de Fase 2)

1. **La "Categoría" (`alm_grupo`) SE CONSERVA**, re-colgada del tipo (`tipo_articulo_id` en vez de `linea_id`).
2. El combo del artículo conserva el caption **"Tipo de artículo"** (coincide con el menú y el catálogo).
3. Los textos legacy `alm_articulo.linea`/`grupo` y `alm_kardex.linea`/`linea_desc` (rastro SIMAFI)
   se conservan congelados; su limpieza es la Fase 4 opcional.

## Fase 0 — HECHA (2026-07-16)

- `Database/2026-07-16_alm_tipo_articulo_seed_desde_lineas.sql` (seed 9 grupos → tipos).
- `Database/2026-07-16_alm_articulo_backfill_tipo_desde_linea.sql` (backfill 631 artículos).
- Aplicados y verificados en mirror y SRV.

## Fase 1 — Preparar el tipo como catálogo definitivo (aditivo, sin romper nada)

**BD (script nuevo, tarjeta verde de guardia):**
- `ALTER alm_tipo_articulo`: `nombre` varchar(60)→varchar(100) y `cuenta_inventario` varchar(20)→varchar(25)
  (asimetría heredada de la línea; el tipo 02 quedó truncado a 60 con el nombre real de 62).
- `UPDATE` del tipo 02 con su nombre íntegro desde `descripcion`.
- `ALTER alm_grupo ADD COLUMN tipo_articulo_id int NULL REFERENCES alm_tipo_articulo(id) ON DELETE SET NULL`
  + índice; **backfill** desde `linea_id` emparejando códigos por company. `linea_id` sigue vivo.

**Código (sincronizar límites):**
- `SiadDbContext.Almacen.cs`: HasMaxLength nombre 100 / cuenta_inventario 25; mapear `alm_grupo.tipo_articulo_id` + navegación (colección `grupos` en el tipo si se quiere inversa).
- `TipoArticuloEditDto`: StringLength 60→100 (nombre); `TipoArticuloService`: máximos hardcodeados en Create/Update (l.92-93, 105-109, 129-130, 140-144).
- Entidad `alm_grupo.cs`: agregar `tipo_articulo_id` + navegación (manteniendo `linea_id` temporalmente).

**Resultado:** todo sigue funcionando igual; el terreno queda listo.

## Fase 2 — HECHA (2026-07-16) — Recablear la aplicación al tipo (solo código, sin drop)

> Ejecutada según lo planeado, con estas decisiones:
> - `TipoArticuloId` quedó **obligatorio** en Create/Update ([Required] + validación de servicio);
>   la categoría es opcional pero si viene debe pertenecer al tipo (`ValidarGrupoAsync`).
> - Tipo inexistente/inactivo/cross-tenant al crear o editar = **error** (antes se asumía "maneja inventario").
> - Grid de artículos: columna "Grupo"→"Tipo" (`TipoArticuloDisplay`) + columna "Categoría"
>   (`GrupoDisplay`) oculta por defecto (Column Chooser).
> - Entidades: `alm_articulo.linea_id`, `alm_grupo.linea_id/linea_codigo` quedaron **sin mapear** en EF
>   (las columnas siguen en BD hasta la Fase 3); `alm_articulo.linea/grupo` (texto SIMAFI) siguen congeladas.
> - Saneo de artículos sin tipo (decisión del usuario): válvula de prueba (con kardex) → tipo 01;
>   artículo de prueba "MANTENIMIENTO…CONTRATADO" eliminado con guardas; los 3 legacy (DISPONIBLE,
>   0032, 0037) se dejan sin tipo a propósito — al editarlos la app exigirá clasificarlos.
>   Script: `Database/2026-07-16_alm_articulo_saneo_sin_tipo.sql` (aplicado en mirror; SRV pendiente).

**Formulario de artículo:**
- `ArticuloForm.razor`: eliminar el DxFormLayoutItem "Grupo" (l.105-113, bind a `Model.LineaId`),
  el parámetro `Lineas` (l.224) y `GruposFiltrados` pasa a filtrar por `TipoArticuloId` (cascada tipo→categoría).
- `ArticuloFormPage.razor`: quitar inject `LineasCatalogoClient` (l.14), campo `lineas` (l.97), carga (l.136) y paso de parámetro (l.61).
- `ArticuloEditDto`: eliminar `LineaId` (l.34).

**Listados y filtros (la cadena huérfana detectada):**
- `ArticulosList.razor`: combo "Todos los grupos" (l.85-89) → lookup de tipos (`TiposArticuloClient.GetLookupAsync`);
  columna "Grupo" sobre `LineaDisplay` (l.146-153) → `TipoArticuloNombre`; `BuildFilter` (l.281).
- `StockAlertas.razor`: combo "Todas las líneas" (l.82-86) → ídem; `BuildFilter` (l.243-249).
- DTOs: `ArticuloFilterDto.Linea` y `AlertaStockFilterDto.Linea` (string, filtran por texto legacy) → `TipoArticuloId` (int?);
  `AlertaStockDto.Linea` → nombre del tipo; `ArticuloListItemDto`: quitar `Linea`/`LineaNombre`/`LineaDisplay`.
- `ArticulosService`: filtros l.30-34 y l.104-108 → `tipo_articulo_id`; proyecciones l.73-77, 151, 178, 307-308;
  Create/Update dejan de escribir `linea_id` (l.419, 585); eliminar `GetLineasAsync` (l.319-327) + firma en interfaz.
- `ArticulosController`: eliminar GET `api/almacen/articulos/lineas` (l.39-44); query param `linea` → `tipoArticuloId`.
- `ArticulosClient`: quitar `GetLineasAsync` (l.78-82) y los params `linea=` (l.27-30, 55-58).

**Categorías (alm_grupo):**
- `GrupoEditDto/GrupoListItemDto/GrupoLookupDto`: `LineaId`/`LineaNombre` → `TipoArticuloId`/`TipoArticuloNombre`.
- `GrupoService`: proyecciones (l.44-45, 61, 72); `ResolverLineaCodigoAsync` (l.147-159) → validar contra
  `alm_tipo_articulos`; dejar de escribir `linea_codigo`.

**Borrado del módulo de líneas (código):**
- `LineasCatalogoController.cs` (6 endpoints), `ILineaService`/`LineaService`, `LineasCatalogoClient`
  + registro DI en `CommonServices.cs` l.93 y `ServiceRegistration.cs` l.120,
  DTOs `LineaLookupDto`/`LineaEditDto`/`LineaListItemDto`.
- La entidad `alm_linea` y su DbSet/bloque EF se quitan aquí también (la tabla sigue en BD hasta Fase 3;
  EF no la necesita mapeada).

**Huecos de la crítica (obligatorios en esta fase):**
- **Validación server-side nueva** en `ArticulosService.Create/Update`: `GrupoId` debe existir y pertenecer
  al `TipoArticuloId` del artículo (hoy no se valida nada; la coherencia vivía solo en la UI).
- **Decidir obligatoriedad de `TipoArticuloId`**: recomendado exigirlo en Create/Update (los tipos ya son los
  grupos reales) y sanear los artículos sin tipo (5 mirror / 3 SRV, artículos sin grupo de origen).
- `ManejaInventarioAsync` (l.203): tratar tipo inexistente/cross-tenant como error, no como "maneja inventario".
- Limpiar código muerto: `ArticuloListItemDto.Grupo/GrupoNombre/GrupoDisplay` (0 consumidores) — eliminar
  o conectar a la columna Categoría del grid si se quiere mostrar.
- `TiposArticuloList.razor` l.22: actualizar subtítulo ("Clasificación de artículos por uso (operativo,...)" ya no aplica).

**Verificación:** build de la solución + flujo completo en la app (crear/editar artículo, filtros de listado
y alertas, CRUD de categorías, export XLSX de los grids que cambiaron de columnas).

## Fase 3 — Drop en BD (destructivo, tarjeta roja de guardia)

Script nuevo (aplicar primero en mirror, luego SRV):
- `ALTER TABLE alm_articulo DROP COLUMN linea_id;` (índice cae con la columna)
- `ALTER TABLE alm_grupo DROP COLUMN linea_id, DROP COLUMN linea_codigo;`
- `DROP TABLE alm_linea;`
- Guardia de replay: `DELETE FROM alm_tipo_articulo WHERE codigo IN ('OPER','MANT','CONS');`
  (re-ejecutar `2026-07-02_alm_catalogos_articulo.sql` en una base nueva resucita los tipos genéricos;
  este script, posterior en orden, los vuelve a quitar).
- Documentar en el encabezado que los scripts del 2026-07-16 dejan de ser re-ejecutables tras el drop
  (secuencia de replay: 07-02 → 07-16 seed → 07-16 backfill → este).

## Fase 4 — Limpieza opcional (cuando haya calma)

- Drop de columnas texto legacy `alm_articulo.linea`/`grupo` y `alm_kardex.linea`/`linea_desc`
  (+ entidad/EF/DTOs), si se decide que el rastro SIMAFI ya no hace falta.
- Nota en `docs/INFORME_RAMA_MODULO_CAJA_EMILIO_2026-07-09.md` de que línea/grupo se unificaron en tipo.

## Riesgos y mitigaciones

| Riesgo | Mitigación |
|---|---|
| Artículos sin tipo tras exigir obligatoriedad | Saneo previo en Fase 2 (5+3 artículos, revisables a mano) |
| Categoría con tipo incoherente (POST directo) | Validación server-side nueva grupo↔tipo (Fase 2) |
| Replay de scripts en base nueva resucita tipos genéricos | Guardia en el script de drop (Fase 3) |
| Contratos JSON cambian (`linea` → `tipoArticuloId`) | Server y cliente se despliegan juntos (mismo publish del portal) |
| Nombre truncado del tipo 02 | Se corrige en Fase 1 al ampliar a varchar(100) |
