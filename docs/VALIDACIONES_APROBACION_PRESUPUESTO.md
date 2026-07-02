# Validaciones para aprobar un presupuesto

## 1. Control de permisos (seguridad)

La aprobación de presupuesto requiere la política `PresupuestoAprobacion`, que permite solo roles **Admin** o **Contabilidad**.

- Definición de política: `apc/Program.cs`.
- Constante de política: `SIAD.Core/Constants/AuthorizationPolicies.cs`.
- Endpoint protegido: `apc/Controllers/Presupuesto/ConfiguracionPresupuestoController.cs`.

Además, en UI se valida también el permiso y se oculta o bloquea la acción **Aprobar** si el usuario no tiene autorización.

## 2. Validaciones de negocio al aprobar

Método principal: `ApprovePresupuestoAsync` en `SIAD.Services/Presupuesto/ConfiguracionPresupuestoService.cs`.

Se validan estas reglas, en orden:

1. El `idPresupuesto` no puede ser vacío o nulo.
2. El presupuesto debe existir.
3. El presupuesto no puede estar ya aprobado.
4. Debe existir al menos un detalle (`pst_config_presupuesto_dtl`) con `valor_proyeccion > 0`.
5. Si todo es válido, se marca `estado_aprobado = true` y se persiste en base de datos.

## 3. Reglas relacionadas después de aprobar

Una vez aprobado:

- No se puede cambiar el estado de aprobación desde edición general (solo por acción de aprobar).
- No se puede agregar detalle.
- No se puede editar detalle.
- No se puede eliminar detalle.

Estas restricciones se implementan en `ConfiguracionPresupuestoService` con `InvalidOperationException` cuando `estado_aprobado = true`.

## 4. Vigencia del presupuesto (rango de fechas)

La validación de vigencia se usa en operaciones de gestión de solicitudes y detalle por medio de:

- `EnsurePresupuestoAprobadoAsync`
- `EnsurePresupuestoVigente`

Regla aplicada:

- La fecha actual debe estar entre `fecha_inicia` y `fecha_finaliza`.

Si está fuera de rango, se rechaza la operación con mensaje de negocio.

> Nota: actualmente la aprobación en sí valida estado y proyección, pero la vigencia se aplica sobre todo en la gestión operativa posterior.

## 5. Respuestas HTTP cuando falla

Desde el controlador (`ConfiguracionPresupuestoController`) las excepciones se traducen así:

- `KeyNotFoundException` -> **404 Not Found**
- `ArgumentException` -> **400 Bad Request**
- `InvalidOperationException` -> **409 Conflict**

## 6. Flujo resumido

1. Usuario autorizado pulsa **Aprobar**.
2. API ejecuta `ApprovePresupuestoAsync`.
3. Se validan existencia, estado y proyección.
4. Se actualiza `estado_aprobado` a `true`.
5. El presupuesto queda bloqueado para cambios de detalle y habilitado para gestión según reglas de vigencia.
