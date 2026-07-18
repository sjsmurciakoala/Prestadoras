# Diseño: campo Apellidos en solicitudes de nuevo cliente

**Fecha:** 2026-07-15
**Aprobado por:** Emilio (enfoque A, campo obligatorio)

## Problema

El formulario de solicitudes de servicio captura el nombre del cliente en un solo campo
"Nombre Completo" (`solicitud_servicio.cliente_nombre`). Al crear el cliente desde la
solicitud (`ClienteCreatePage`), todo el texto cae en `Nombre` y `Apellidos` queda vacío,
y no hay forma de capturar los apellidos por separado.

## Decisión

Agregar una columna nueva `cliente_apellidos` a `solicitud_servicio` y llevar el dato por
todo el slice (entidad → DTOs → AutoMapper → formularios → prefill de cliente). Se descartó
la alternativa de unir/separar sobre `cliente_nombre` con heurística porque es imprecisa
con nombres y apellidos compuestos.

## Alcance

### Base de datos
- Script aditivo `Database/2026-07-15_solicitud_servicio_cliente_apellidos.sql`:
  `ALTER TABLE solicitud_servicio ADD COLUMN cliente_apellidos varchar NULL`.
- La columna es **NULL en BD** (las solicitudes históricas no tienen apellidos separados).
  La obligatoriedad se exige en la UI para solicitudes nuevas/editadas.
- Se aplica solo al mirror local `siad_v3_restore`; **producción la aplica Emilio**.

### Backend
- `SIAD.Core/Entities/solicitud_servicio.cs`: propiedad `cliente_apellidos` (string?).
- DTOs (`SIAD.Core/DTOs/Solicitudes/`): `ApellidosCliente` en `SolicitudCreateDto`,
  `SolicitudUpdateDto` y `SolicitudDetailDto`.
- `SolicitudListDto.NombreCliente` pasa a mapearse como nombre + apellidos concatenados
  (la grilla y los buscadores siguen funcionando sin cambios de UI).
- `SIAD.Services/Solicitudes/SolicitudMappings.cs`: mapeos nuevos en los 4 CreateMap.
- Controller y HTTP client no cambian de firma (viajan por los mismos DTOs).

### UI (apc.Client)
- `SolicitudForm.razor` (nueva): caption "Nombre Completo *" → "Nombres *", campo nuevo
  "Apellidos *", ambos requeridos en `EsValido`.
- `SolicitudFormEdicion.razor` (edición): mismo cambio.
- `SolicitudesIndex.razor`: el panel de detalle muestra nombres + apellidos; el flujo de
  edición traslada `ApellidosCliente` entre detalle y update.
- `ClienteCreatePage.razor`: prefill `Nombre ← NombreCliente`, `Apellidos ← ApellidosCliente`.
  Para solicitudes viejas sin apellidos separados se conserva el comportamiento actual
  (todo el texto en `Nombre`).

## Fuera de alcance

- No se cambia el modelo de `cliente_maestro` (sigue guardando nombre completo en una
  columna, con unión/separación heurística existente).
- No se migran las solicitudes históricas (quedan con `cliente_apellidos = NULL`).
