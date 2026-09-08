# Runbook de despliegue a SRV — Módulo Activos Fijos, Fase 1 (registro)

**Base destino:** `siad_v4` @ `172.16.0.9`
**Fecha:** 2026-09-08
**Alcance:** 3 scripts, 3 pasos.
**Scripts:** [2026-09-08_af_activos_fijos_f1_registro.sql](2026-09-08_af_activos_fijos_f1_registro.sql),
[2026-09-08_af_semilla_catalogos_ejemplo.sql](2026-09-08_af_semilla_catalogos_ejemplo.sql)
y [2026-09-08_af_rol_permisos.sql](2026-09-08_af_rol_permisos.sql)

> ✅ **Aplicado y verificado en el mirror `siad_v3_restore` el 2026-09-08.** Respaldo previo en
> `Database/Backups/siad_v3_restore_antes_activos_fijos_20260908_124621.backup`. El estado del SRV
> de abajo sigue siendo presunto: no se verificó contra el servidor.
>
> ⚠️ **Dos defectos corregidos durante la aplicación**, ambos ya arreglados en el archivo:
> 1. El script referenciaba `prv_proveedore`, que es el nombre de la ENTIDAD EF, no de la tabla. La
>    tabla real es **`prv_proveedores`** (plural). Sin la corrección el script aborta.
> 2. `sp_af_activo_guardar` copiaba el nombre del tipo en la columna legacy `af_activo_fijo.tipo`,
>    que es `VARCHAR(15)`. Un tipo llamado "Equipos de transporte" reventaba el alta con
>    *valor demasiado largo*. Las tres columnas de texto libre heredadas de SIMAFI (`tipo`,
>    `ubicacion`, `proveedor`) ya no se escriben: para los registros nuevos el texto sale del JOIN
>    con el catálogo. `responsable` y `cargo_responsable` sí se escriben, truncados al ancho de su
>    columna.
>
> ⚠️ La base **ACTIVA es `siad_v4`**, no `siad_v3`.
>
> ⚠️ **Esta tanda no reemplaza ni cierra las anteriores**, que siguen pendientes arriba:
> `2026-08-22` (CxP unificada), `2026-08-27` (control presupuestario), `2026-08-31` (aprobación por
> niveles), `2026-09-01` (disponible sin truncar), `2026-09-05` (ciudad de la empresa) y `2026-09-07`
> (roles y permisos, que consolida a las anteriores).

---

## 1. Qué cubre este runbook

Levanta el módulo de Activos Fijos sobre la tabla `af_activo_fijo` que ya trae el histórico migrado de
SIMAFI. No crea un maestro paralelo: el histórico y el registro nuevo comparten tabla.

| Pieza | Qué aporta |
|---|---|
| `af_metodo_depreciacion` | Catálogo de sistema, 4 métodos. Solo línea recta queda marcada como implementada |
| `af_estado_activo` | Catálogo de sistema, 7 estados con `permite_depreciar` y `es_final`. Sustituye al `SMALLINT` sin catálogo y a las banderas `descargado`/`vendido` |
| `af_tipo_activo` | Por empresa. Presta al activo su vida útil, método, % residual y 4 cuentas contables |
| `af_ubicacion` | Por empresa, jerárquica por `padre_id`. Aplana en una tabla lo que en Merendon son cuatro |
| `af_activo_asignacion` | Historial de responsable, ubicación y centro de costo con vigencia. Una sola fila abierta por activo |
| `af_activo_componente` | Componentes y accesorios del activo, que antes eran dos campos de texto |
| `af_activo_fijo` | 19 columnas nuevas nulas (referencias normalizadas, marca, placa, código de barras, fechas de depreciación, póliza, garantía y auditoría) |
| 3 columnas ampliadas | `valor_rescate`, `vida_util_anios` y `valor_venta` |
| 12 funciones `fn_af_*` y 8 procedimientos `sp_af_*` | Todo el acceso a datos del módulo |

**Queda fuera, deliberadamente:**

- **Semilla de tipos de activo y ubicaciones.** El script no siembra ninguno: los catálogos arrancan
  vacíos y se capturan desde el portal (`/activos-fijos/tipos` y `/activos-fijos/ubicaciones`), porque
  las cuentas contables de cada tipo dependen del plan de cuentas de cada empresa.
- **Normalización del histórico.** Los activos migrados quedan sin `tipo_activo_id` ni
  `estado_activo_id`; el listado los marca como pendientes y hay un filtro para trabajarlos. No hay
  backfill automático porque el `tipo` de origen es texto libre sin catálogo equivalente.

## 2. Antes de empezar (obligatorio)

**Respaldo de `siad_v4`** — no de `siad_v3`:

```bash
pg_dump -h 172.16.0.9 -U postgres -d siad_v4 -Fc -f siad_v4_antes_activos_fijos.backup
```

**Definir la conexión** (la clave no va en el repo):

```bash
export SRV="postgresql://USUARIO:CLAVE@172.16.0.9:5432/siad_v4"
```

**Confirmar la base antes de escribir nada:**

```bash
psql "$SRV" -c "SELECT current_database(), pg_size_pretty(pg_database_size(current_database()));"
```

Debe decir `siad_v4`. Si dice `siad_v3`, **parar**.

**Confirmar el prerrequisito.** El script referencia `th_empleado`, `prv_proveedores` y
`con_centro_costo`, y `af_activo_fijo` debe existir:

```bash
psql "$SRV" -c "SELECT to_regclass('public.af_activo_fijo')  AS activo_fijo,
                       to_regclass('public.th_empleado')     AS empleado,
                       to_regclass('public.prv_proveedores')  AS proveedor,
                       to_regclass('public.con_centro_costo') AS centro_costo;"
```

Las cuatro deben devolver un nombre. Si `af_activo_fijo` sale en `NULL`, falta aplicar arriba
`2026-07-01_alm_almacen_af_activo_fijo.sql`, y este script no corre.

## 3. Advertencias clave (leer antes de aplicar)

- ⚠️ **Tres columnas cambian de tipo.** Es la única parte no aditiva del script. Las tres son
  ampliaciones y el rango nuevo contiene al viejo, así que ninguna fila se pierde ni se redondea:

  | Columna | Antes | Después | Por qué |
  |---|---|---|---|
  | `af_activo_fijo.valor_rescate` | `NUMERIC(7,2)` | `NUMERIC(14,2)` | El tope heredado de SIMAFI era L. 99,999.99, y el residual de un vehículo no cabe |
  | `af_activo_fijo.vida_util_anios` | `NUMERIC(3,0)` | `NUMERIC(4,1)` | Solo admitía enteros; no se podía registrar 2.5 años |
  | `af_activo_fijo.valor_venta` | `NUMERIC(11,2)` | `NUMERIC(14,2)` | Se alinea con `valor_compra`, que es `NUMERIC(12,2)` |

  Postgres resuelve estas ampliaciones sin reescribir la tabla, pero toma un `ACCESS EXCLUSIVE`
  breve sobre `af_activo_fijo`. Aplicar fuera de hora pico.

- ⚠️ **Los `DROP FUNCTION IF EXISTS` del bloque 5 son sobre objetos de este mismo script.** Están para
  poder cambiar la firma sin dejar sobrecargas colgando. En una base donde el script nunca corrió son
  un no-op. **No tocan ninguna función existente del sistema**: todas llevan prefijo `fn_af_`.

- ⚠️ **El módulo nace invisible.** Las opciones del menú y los endpoints exigen los permisos
  `module.activosfijos.*`, que ningún rol tiene todavía. Aplicar el script **no cambia lo que ve
  ningún usuario**: hay que conceder los permisos por rol después (§6).

- **Todo va en una sola transacción.** Si algo falla, no queda nada a medias.

- **Idempotente.** `CREATE TABLE IF NOT EXISTS`, `ADD COLUMN IF NOT EXISTS`, `INSERT … ON CONFLICT DO
  NOTHING` y `CREATE OR REPLACE`. Se puede correr más de una vez sin efecto adicional. Los `ALTER
  COLUMN … TYPE` también son idempotentes: aplicar el mismo tipo dos veces no hace nada.

## 4. Orden de aplicación

| # | Script | Naturaleza | ¿Re-ejecutable? | Depende de |
|:--:|---|---|:--:|---|
| 1 | `2026-09-08_af_activos_fijos_f1_registro.sql` | Aditivo + 3 ampliaciones de tipo + objetos | Sí | `af_activo_fijo`, `th_empleado`, `prv_proveedores`, `con_centro_costo` |
| 2 | `2026-09-08_af_semilla_catalogos_ejemplo.sql` | Datos idempotente (solo `INSERT`) | Sí | El paso 1, y el plan de cuentas ERSAPS cargado |
| 3 | `2026-09-08_af_rol_permisos.sql` | Datos idempotente (solo `INSERT` en `identity`) | Sí | El portal publicado con los permisos `module.activosfijos.*` |

El orden entre 1 y 2 es obligatorio: el paso 2 llena tablas que crea el paso 1. El paso 3 es
independiente de los otros dos y toca otro esquema, pero sin él el módulo queda invisible para todos
salvo el Super Administrador. Sin dependencias con las tandas abiertas: ninguna de ellas toca las
tablas `af_*` ni estos roles.

## 5. Detalle del paso 1

**Qué hace:** crea los seis objetos nuevos del módulo, extiende `af_activo_fijo` y publica las
funciones y procedimientos del registro.

**¿Ya aplicado?**

```bash
psql "$SRV" -c "SELECT to_regclass('public.af_tipo_activo')       AS tipo_activo,
                       to_regclass('public.af_ubicacion')         AS ubicacion,
                       to_regclass('public.af_activo_asignacion') AS asignacion,
                       to_regproc('public.sp_af_activo_guardar')  AS sp_guardar;"
```

Si las cuatro devuelven un nombre, ya está aplicado. Si las cuatro salen en `NULL`, falta entero.

**Aplicar:**

```bash
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-09-08_af_activos_fijos_f1_registro.sql
```

**Verificación posterior.** Resultado real en el mirror: `metodos = 4`, `estados = 7`,
`columnas_nuevas = 19`, las tres ampliaciones en su tipo nuevo y las 829 filas del histórico con sus
totales intactos (`valor_compra = 20,254,221.89`).

Los catálogos de sistema deben quedar con sus filas y las columnas nuevas en su tipo:

```bash
psql "$SRV" -c "SELECT (SELECT count(*) FROM af_metodo_depreciacion) AS metodos,
                       (SELECT count(*) FROM af_estado_activo)       AS estados,
                       (SELECT count(*) FROM information_schema.columns
                         WHERE table_name = 'af_activo_fijo'
                           AND column_name IN ('tipo_activo_id','estado_activo_id','ubicacion_id',
                                               'metodo_depreciacion_id','empleado_id','cod_proveedor',
                                               'centro_costo_id','marca','placa','codigo_barra',
                                               'fecha_inicio_depreciacion','fecha_fin_depreciacion',
                                               'poliza_seguro','poliza_vence','garantia_vence',
                                               'usuariocreacion','fechacreacion','usuariomodificacion',
                                               'fechamodificacion')) AS columnas_nuevas;"
```

Debe devolver `metodos = 4`, `estados = 7` y `columnas_nuevas = 19`.

Y las tres ampliaciones:

```bash
psql "$SRV" -c "SELECT column_name, numeric_precision, numeric_scale
                  FROM information_schema.columns
                 WHERE table_name = 'af_activo_fijo'
                   AND column_name IN ('valor_rescate','vida_util_anios','valor_venta')
                 ORDER BY column_name;"
```

Debe devolver `valor_rescate 14,2`, `valor_venta 14,2` y `vida_util_anios 4,1`.

**Que ninguna fila del histórico se haya alterado** — el conteo y los totales deben ser los mismos
que antes de aplicar:

```bash
psql "$SRV" -c "SELECT count(*)                        AS activos,
                       sum(valor_compra)               AS valor_compra,
                       sum(depreciacion_acumulada)     AS depreciacion,
                       count(*) FILTER (WHERE tipo_activo_id IS NULL) AS sin_tipo
                  FROM af_activo_fijo;"
```

`sin_tipo` debe salir igual a `activos`: el script no clasifica el histórico, eso se hace desde el
portal.

## 5 bis. Detalle del paso 2, la semilla de catálogos

**Qué hace:** llena `af_tipo_activo` con siete tipos alineados a las clases de Propiedad, Planta y
Equipo del plan ERSAPS (grupos 12302 a 12308), y `af_ubicacion` con once ubicaciones jerárquicas.
Las cuentas son códigos reales del plan, verificados como cuentas de detalle que admiten movimiento.
Asume `company_id = 2`, la única empresa de esta base.

⚠️ **Las vidas útiles y los porcentajes residuales son un punto de partida, no una definición
contable.** Deben validarse con el contador antes de la fase de depreciación. La rama de cuentas
elegida es "Comunes / Adquirido con Recursos Propios"; un activo donado, transferido o específico de
Agua Potable o Alcantarillado va a otra hoja del mismo grupo.

**¿Ya aplicado?**

```bash
psql "$SRV" -c "SELECT count(*) FROM af_tipo_activo WHERE company_id = 2;"
```

Devuelve `7` si ya corrió, `0` si falta.

**Aplicar:**

```bash
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-09-08_af_semilla_catalogos_ejemplo.sql
```

**Verificación posterior.** Resultado real en el mirror: 7 tipos y 11 ubicaciones, con la ruta
jerárquica armada (por ejemplo `Sistema de agua potable / Planta potabilizadora`).

```bash
psql "$SRV" -c "SELECT codigo, nombre, vida_util_anios, cuenta_activo
                  FROM public.fn_af_tipo_activo_listar(2, true, NULL) ORDER BY codigo;"
psql "$SRV" -c "SELECT codigo, ruta FROM public.fn_af_ubicacion_listar(2, true, NULL);"
```

## 5 ter. Detalle del paso 3, los roles del módulo

**Qué hace:** crea los roles "Activos Fijos" y "Activos Fijos Jefatura" y les siembra sus permisos.
El operativo registra y reasigna activos; la jefatura además mantiene los tipos y las ubicaciones.

⚠️ **Ninguno de los dos lleva `module.activosfijos.edit`.** Ese permiso abre por cascada todos los
recursos finos, catálogos incluidos, y anularía la separación: el tipo de activo lleva las cuentas
contables y la vida útil que heredan todos sus activos.

**Incluye la asignación real hecha en desarrollo.** El script asigna
`contabilidad@aguasdepuestocortes.com` al rol "Activos Fijos Jefatura", que es el usuario con el que
se probó el módulo el 2026-09-08. Todo lo que se configura en desarrollo queda en el script para que
el servidor termine igual.

⚠️ **Ese correo dice `aguasdepuestocortes.com`, no `aguasdepuertocortes.com`** como el resto de la
empresa. Está así en la base de desarrollo y el script lo respeta tal cual. Si en el servidor
estuviera bien escrito, el INSERT no encontraría al usuario y no haría nada **en silencio**: por eso
hay que mirar el cuadro de usuarios asignados que el script imprime al final.

⚠️ **Queda comentado** el bloque que da lectura al rol Contabilidad, porque toca un rol que ya
existe, y la plantilla de asignación del rol operativo, a la espera de saber quién lleva el control
físico del inventario.

⚠️ **Quien tenga sesión abierta debe volver a entrar:** los claims del rol se copian al usuario al
iniciar sesión.

**¿Ya aplicado?**

```bash
psql "$SRV" -c "SELECT count(*) FROM identity.\"AspNetRoleClaims\"
                 WHERE \"ClaimValue\" LIKE 'module.activosfijos%';"
```

Devuelve `10` si ya corrió, 4 del rol operativo más 6 de la jefatura. Devuelve `0` si falta.

**Aplicar:**

```bash
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-09-08_af_rol_permisos.sql
```

El script imprime al final los permisos por rol y los usuarios asignados.

**Verificación posterior.** Resultado real en el mirror: 10 claims, 4 en "Activos Fijos" y 6 en
"Activos Fijos Jefatura", más el usuario de contabilidad asignado a la jefatura. Se corrió tres veces
para comprobar la idempotencia: a partir de la segunda inserta cero filas.

## 6. Después de aplicar

1. **Asignar al resto de personas** a los roles que crea el paso 3, desde `/parametros/roles` o
   descomentando la plantilla del bloque 5 de ese script. El usuario de contabilidad ya va incluido.
2. **Revisar los tipos sembrados** en `/activos-fijos/tipos`: vidas útiles, porcentajes residuales y
   la rama de cuentas, con el contador.
3. **Ajustar las ubicaciones** en `/activos-fijos/ubicaciones` a las instalaciones reales.
4. **Clasificar el histórico** con el filtro "Solo pendientes" del listado.

## 7. Estado presunto

| Base | Estado |
|---|---|
| Mirror `siad_v3_restore` @ localhost | ✅ **Los tres pasos aplicados y verificados** el 2026-09-08. El paso 3 se corrió dos veces para comprobar la idempotencia: la segunda no insertó nada |
| Desarrollo @ `3.208.232.209` | **Pendiente** — no aplicado |
| SRV `siad_v4` @ `172.16.0.9` | **Pendiente** — no aplicado |

## 8. Versionado (git)

| Archivo | Estado en git |
|---|---|
| `Database/2026-09-08_af_activos_fijos_f1_registro.sql` | Sin seguimiento, sin commit |
| `Database/2026-09-08_af_semilla_catalogos_ejemplo.sql` | Sin seguimiento, sin commit |
| `Database/2026-09-08_af_rol_permisos.sql` | Sin seguimiento, sin commit |
| `Database/2026-09-08_af_runbook_despliegue_srv.md` | Sin seguimiento, sin commit |

El código C# y Blazor del módulo (DTOs, servicios, controladores, clientes y páginas) también está sin
commit. El plan del módulo está en
[`docs/plans/2026-09-08-activos-fijos-modulo-plan.md`](../docs/plans/2026-09-08-activos-fijos-modulo-plan.md).
