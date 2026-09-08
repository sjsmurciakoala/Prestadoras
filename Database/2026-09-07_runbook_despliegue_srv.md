# Runbook de despliegue a SRV — Tanda consolidada: Inventario, Proveedores, Compras y Presupuesto

**Base destino:** `siad_v4` @ `172.16.0.9`
**Fecha:** 2026-09-07
**Alcance:** 16 scripts de **cinco tandas abiertas**, más un guion conductor que los aplica en orden.
**Guion:** [2026-09-07_aplicar_pendientes_srv.sql](2026-09-07_aplicar_pendientes_srv.sql)

> ⚠️ **Nada de esto se verificó contra el SRV en vivo.** El estado de abajo es presunto: sale
> de los runbooks de cada tanda, del registro `2026-07-30_pendientes_srv.md` y de la cabecera
> de cada script. El guion trae su **modo revisión**, que informa paso por paso qué falta
> realmente arriba sin escribir nada.
>
> ⚠️ La base **ACTIVA es `siad_v4`**, no `siad_v3`. El guion aborta si la conexión apunta a otra.

---

## 1. Qué cubre este runbook

Consolida las cinco tandas que quedaron abiertas y las ordena por dependencia en un solo
guion. No sustituye a los runbooks de origen: el detalle largo de cada paso sigue ahí.

| Tanda de origen | Qué aporta | Scripts |
|---|---|:--:|
| `2026-07-30_pendientes_srv.md` §3.28 | Elimina la función de existencia negativa en salidas | 1 |
| `2026-09-05` | Ciudad de la empresa para el pagaré y el cheque | 1 |
| `2026-07-30_pendientes_srv.md` §3.29 | Catálogo de formatos fiscales (No. factura SAR y CAI) | 3 |
| `2026-08-22` | Cuentas por pagar unificadas (dos funciones de lectura) | 1 |
| `2026-08-27` + `2026-09-01` | Control presupuestario con compromiso en la orden, y el disponible sin truncar | 6 |
| `2026-08-31` + `2026-09-01` | Aprobación por niveles, con límite de autorización en vez de cascada | 4 |

**Queda fuera, deliberadamente:**

- `2026-09-04_perf_rep_saldo_clientes_categoria_cobranza.sql` — **ya aplicado** arriba el 2026-09-05.
- `2026-09-04_rollback_rep_saldo_clientes_categoria_cobranza.sql` — es el rollback de ese, **no es un paso**.
- `2026-08-15_alm_existencia_negativa.sql` — **NO aplicar**: es lo que el paso 1 revierte. Sirve solo como rollback del paso 1.
- `2026-08-20_alm_articulos_prueba.sql` — datos de prueba, **no va a producción**.

## 2. Antes de empezar (obligatorio)

**Respaldo de `siad_v4`** — no de `siad_v3`:

```bash
pg_dump -h 172.16.0.9 -U postgres -d siad_v4 -Fc -f siad_v4_antes_tanda_2026_09_07.backup
```

**Definir la conexión** (la clave no va en el repo):

```bash
export SRV="postgresql://USUARIO:CLAVE@172.16.0.9:5432/siad_v4"
```

**Confirmar la base antes de escribir nada:**

```bash
psql "$SRV" -c "SELECT current_database(), pg_size_pretty(pg_database_size(current_database()));"
```

Debe decir `siad_v4` y un tamaño del orden de 7 GB. Si dice `siad_v3`, **parar**.

**Correr el guion en modo revisión** — no escribe nada y devuelve dos cuadros: los
prerrequisitos y el estado real de los 16 pasos (`YA` / `FALTA`):

```bash
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-09-07_aplicar_pendientes_srv.sql
```

## 3. Advertencias clave (leer antes de aplicar)

- ⚠️ **El orden no es el alfabético ni el de fechas.** El bloque de aprobación por niveles va
  **después** del de presupuesto: los dos reemplazan `ck_alm_orden_compra_estado`, y el de
  presupuesto lo deja sin el estado `7`. Al revés, la aprobación pierde silenciosamente ese
  estado y el módulo no puede escribirlo. El guion ya los ordena así.
- ⚠️ **El paso 1 es el único destructivo:** `DROP COLUMN alm_bodega.permite_existencia_negativa`
  y `DROP TABLE cfg_inventario_negativo`. En el SRV es casi seguro **un no-op**, porque la
  función que crea esas piezas (`2026-08-15`) nunca se aplicó allá; los `DROP … IF EXISTS` no
  fallan si no existen. Impacto medido en el mirror: 1 fila en su default y 3 bodegas en `NULL`,
  es decir cero configuración de negocio perdida.
- ⚠️ **El paso 4 asume `company_id = 2`** y es la única pieza **opcional** de la tanda. Si arriba
  se prefiere capturar los formatos desde `/mantenimientos/formatos-fiscales`, correr el guion
  con `-v SIN_SEMILLA=si`.
- ⚠️ **El paso 11 reemplaza una función que producción ya usa:** `fn_pst_afectar_saldo_real_credito`,
  la que llama bancos. Misma firma y mismos códigos de retorno; el detalle está en el runbook
  del 2026-08-27.
- ⚠️ **El paso 16 cambia la regla de negocio de la aprobación:** renombra
  `cfg_aprobacion_nivel.monto_desde` a `monto_hasta` y la vuelve anulable. Sin él el código nuevo
  no funciona. Impacto de datos nulo mientras esas dos tablas estén vacías, que es lo esperado
  en el SRV.
- **Todo lo nuevo nace apagado.** `cfg_presupuesto_control.modo` y `cfg_aprobacion_control.modo`
  entran en `0` para todas las empresas: aplicar la tanda **no cambia el comportamiento de
  ninguna pantalla**. Encender es una decisión posterior (§7).
- **Cada script trae su propio `BEGIN … COMMIT`.** No hay transacción envolvente: si un paso
  falla, `ON_ERROR_STOP` corta ahí y los pasos anteriores quedan aplicados. Se retoma corriendo
  el guion otra vez, porque los 16 son re-ejecutables.
- ⚠️ **Cache de 30 minutos** en la bitácora de maestros. El alta del paso 5 hecha por SQL tarda
  hasta media hora en surtir efecto, o hay que reiniciar el host `apc`.
- **El SQL sin el binario es inocuo; el binario sin el SQL rompe la pantalla correspondiente.**
  Los pasos 3 a 16 van en la misma ventana que el despliegue del portal.

## 4. Orden de aplicación (resumen)

| Paso | Script | Naturaleza | ¿Re-ejecutable? | Depende de |
|---:|---|---|:--:|---|
| 1 | `2026-08-20_alm_quitar_existencia_negativa.sql` | **Destructivo** (1 columna, 1 tabla), impacto nulo | Sí | — |
| 2 | `2026-09-05_ciudad_empresa_configuracion.sql` | Datos idempotente (1 `UPDATE`) | Sí | — |
| 3 | `2026-08-22_cfg_formato_fiscal.sql` | Aditivo (1 tabla, 1 índice) | Sí | — |
| 4 | `2026-08-22_cfg_formato_fiscal_seed.sql` | Datos idempotente (2 filas). **Opcional** | Sí | Paso 3 |
| 5 | `2026-08-22_bitacora_config_formato_fiscal.sql` | Datos idempotente (2 filas por empresa) | Sí | Paso 3 y la bitácora de maestros |
| 6 | `2026-08-22_prv_cxp_unificada.sql` | Objetos (2 funciones de lectura) | Sí | `alm_compra_cxp`, `prv_compromiso_hdr`, `fn_prv_estado_cuenta_documentos` |
| 7 | `2026-08-27_pst_compromiso_01_estructura.sql` | Aditivo (4 tablas, 5 columnas, 9 índices, 1 trigger) + 1 CHECK ampliado | Sí | Presupuesto multitenant |
| 8 | `2026-08-27_pst_compromiso_02_funciones.sql` | Objetos (5 funciones) | Sí | Paso 7 |
| 9 | `2026-08-27_pst_compromiso_03_procedimientos.sql` | Objetos (7 procedimientos) | Sí | Pasos 7 y 8 |
| 10 | `2026-08-27_pst_compromiso_04_vistas.sql` | Objetos (4 vistas) | Sí | Pasos 7 a 9 |
| 11 | `2026-08-27_pst_compromiso_05_proveedores_bancos.sql` | Objetos + **reemplazo de una función viva** | Sí | Pasos 7 a 9 |
| 12 | `2026-09-01_pst_disponible_sin_truncar.sql` | Objetos (2 funciones, 1 vista, 2 procedimientos) | Sí | Pasos 8, 9 y 10 |
| 13 | `2026-08-31_apr_niveles_01_estructura.sql` | Aditivo (5 tablas, 8 índices, 1 semilla) + 1 CHECK ampliado | Sí | **Paso 7** (ver §3) |
| 14 | `2026-08-31_apr_niveles_02_funciones.sql` | Objetos (3 funciones) | Sí | Paso 13 |
| 15 | `2026-08-31_apr_niveles_03_requisicion.sql` | Aditivo (1 tabla, 2 índices) | Sí | Paso 13 y `alm_requisicion_hdr` |
| 16 | `2026-09-01_apr_niveles_04_limite_por_aprobador.sql` | **Cambio de modelo** (1 rename, 5 columnas, 5 funciones) | Sí | Pasos 13 a 15 |

## 5. Cómo se aplica

**Un solo comando**, después del respaldo y de haber leído el informe del modo revisión:

```bash
psql "$SRV" -v ON_ERROR_STOP=1 -v CONFIRMO=si -f Database/2026-09-07_aplicar_pendientes_srv.sql
```

Sin `-v CONFIRMO=si` el guion **solo informa**. Con `-v SIN_SEMILLA=si` además salta el paso 4.

El guion trae tres guardas antes de escribir: nombre de la base, prerrequisitos estructurales
y la confirmación explícita. Si prefiere aplicar paso por paso, cada script se corre suelto con
`psql "$SRV" -v ON_ERROR_STOP=1 -f Database/<script>.sql`, respetando el orden de §4.

**Prerrequisitos que el guion exige** y que deben existir arriba: `pst_config_presupuesto_hdr`,
`pst_config_presupuesto_dtl` (con `company_id`), `alm_orden_compra`, `alm_orden_compra_detalle`,
`alm_compra_hdr`, `alm_compra_cxp`, `alm_requisicion_hdr`, `con_centro_costo`, `con_plan_cuentas`,
`con_empresa_configuracion`, `cfg_compra_isv`, `bitacora_maestro_catalogo` y
`fn_prv_estado_cuenta_documentos`. Si falta `pst_config_presupuesto_dtl.company_id`, primero van
`2026-07-24_presupuesto_multitenant_company_id.sql` y `2026-07-28_presupuesto_completar_ddl_valor_real.sql`.

## 6. Detalle por paso

El detalle largo, con su verificación propia, está en el runbook de origen. Aquí queda la señal
de «¿ya aplicado?» que usa el informe del guion.

| Paso | Señal de que ya está aplicado | Detalle en |
|---:|---|---|
| 1 | `to_regclass('cfg_inventario_negativo')` es `NULL` y `alm_bodega` no tiene `permite_existencia_negativa` | `2026-07-30_pendientes_srv.md` §3.28 |
| 2 | Alguna fila de `con_empresa_configuracion` tiene `ciudad` no vacía | `2026-09-05_runbook_despliegue_srv.md` |
| 3 | Existe la tabla `cfg_formato_fiscal` | `2026-07-30_pendientes_srv.md` §3.29 |
| 4 | `cfg_formato_fiscal` tiene los códigos `NUMERO_SAR` y `CAI` | §3.29 |
| 5 | `bitacora_maestro_catalogo` tiene la fila `cfg_formato_fiscal` | §3.29 |
| 6 | Existen `fn_prv_cxp_documentos` y `fn_prv_cxp_resumen` | `2026-08-22_runbook_despliegue_srv.md` |
| 7 | Existen `pst_compromiso`, `pst_movimiento` y `cfg_presupuesto_control` | `2026-08-27_runbook_despliegue_srv.md` |
| 8 | Existe `fn_pst_disponible` | `2026-08-27` |
| 9 | Existe `sp_pst_comprometer_documento` | `2026-08-27` |
| 10 | Existe `vw_pst_ejecucion_presupuestaria` | `2026-08-27` |
| 11 | Existe `sp_pst_afectar_valor_real` | `2026-08-27` |
| 12 | La definición de `vw_pst_ejecucion_presupuestaria` ya no usa `GREATEST` | `2026-09-01_runbook_despliegue_srv.md` |
| 13 | Existen `cfg_aprobacion_control` y `cfg_aprobacion_nivel`, y `ck_alm_orden_compra_estado` admite el `7` | `2026-08-31_runbook_despliegue_srv.md` |
| 14 | Existe alguna función `fn_apr_*` | `2026-08-31` |
| 15 | Existe `alm_requisicion_aprobacion` | `2026-08-31` |
| 16 | `cfg_aprobacion_nivel` tiene la columna `monto_hasta` | `2026-08-31` (paso 4) |

**Verificación posterior:** el propio guion la imprime al final. Comprueba que los dos
interruptores nuevos quedaron con filas y ninguna encendida, la definición del CHECK de la
orden de compra, el catálogo de formatos fiscales y que las dos piezas de existencia negativa
desaparecieron.

## 7. Después del SQL

1. **Desplegar el binario del portal.** Sin él, las pantallas de aprobaciones, control
   presupuestario, formatos fiscales y cuentas por pagar unificadas no existen.
2. **Reiniciar el host `apc`** si no se quiere esperar los 30 minutos del cache de la bitácora.
3. **Encender por empresa lo que corresponda**, que es una decisión de negocio, no del despliegue:
   - Aprobaciones, en `/configuracion/aprobaciones`: primero cargar los tramos con su límite y
     sus aprobadores, y solo después encender el documento. Encender sin tramos deja la orden
     de compra sin quién la firme.
   - Control presupuestario, en `/presupuesto/control`: conviene dejarlo un ciclo en
     **Advertencia** antes de pasar a **Bloqueo**.
4. **Prueba logueada** de las cuatro pantallas antes de dar la tanda por cerrada.

## 8. Estado presunto

| Tanda | Mirror `siad_v3_restore` | SRV `siad_v4` |
|---|---|---|
| §3.28 existencia negativa (paso 1) | ⏳ pendiente | ⏳ pendiente (allá es no-op) |
| 2026-09-05 ciudad (paso 2) | ✅ aplicado el 2026-09-05 | ⏳ pendiente |
| §3.29 formatos fiscales (pasos 3 a 5) | ✅ aplicado el 2026-08-22 | ⏳ pendiente |
| 2026-08-22 CxP unificada (paso 6) | ✅ aplicado | ⏳ pendiente |
| 2026-08-27 control presupuestario (pasos 7 a 11) | ✅ aplicado | ⏳ pendiente |
| 2026-09-01 disponible sin truncar (paso 12) | ✅ aplicado | ⏳ pendiente |
| 2026-08-31 aprobación por niveles (pasos 13 a 16) | ✅ aplicado | ⏳ pendiente |

Nunca se verifica conectándose a la BD desde aquí: el modo revisión del guion lo resuelve.

## 9. Rollback

No hay un rollback consolidado, y no conviene inventarlo: el respaldo de §2 es la salida.
Piezas sueltas por si hace falta revertir algo puntual:

- Paso 1 → re-aplicar `2026-08-15_alm_existencia_negativa.sql` recrea la tabla y la columna,
  ambas en su default de bloquear.
- Pasos 13 a 16 → las cinco tablas de aprobación no las referencia nada: `DROP TABLE … CASCADE`
  las quita. El estado `7` en el CHECK puede quedarse, es solo un valor admitido más.
- Pasos 7 a 12 → apagar `cfg_presupuesto_control` (`modo = 0`) neutraliza el comportamiento sin
  tocar estructura. Es la vía preferible: `pst_movimiento` es inmutable por trigger.

## 10. Anexo: roles y permisos (independiente de los 16 pasos)

Cuatro scripts que **no forman parte del guion conductor** y no tienen dependencia con él:
tocan el esquema `identity`, no `public`, y se pueden aplicar antes o después, en cualquier
ventana. Se registran acá para que no se pierdan.

| # | Script | Qué crea | ¿Re-ejecutable? | Depende de |
|:-:|---|---|:--:|---|
| A1 | `2026-09-07_rol_almacen_permisos.sql` | Roles `Almacen` (10 claims) y `Almacen Jefatura` (6 claims) | Sí | — |
| A2 | `2026-09-07_rol_compras_permisos.sql` | Roles `Compras` (8 claims) y `Compras Jefatura` (3 claims) | Sí | — |
| A3 | `2026-09-07_rol_proveedores_permisos.sql` | Rol `Proveedores` (4 claims) | Sí | — |
| A4 | `2026-09-07_rol_presupuesto_permisos.sql` | Permisos del rol `Contabilidad` (4 claims) | Sí | — |

Los cuatro son **aditivos**: solo `INSERT` con guarda `NOT EXISTS` sobre `AspNetRoles`,
`AspNetRoleClaims` y `AspNetUserRoles`. Ninguno borra, renombra ni revoca nada.

**Estado: ✅ APLICADOS en el mirror `siad_v3_restore` el 2026-09-07, autorizado por el usuario.
⏳ Pendientes en `siad_v4`.** Lo que se midió al aplicar:

| Rol | Claims | Estado del rol |
|---|:--:|---|
| `Almacen` | 10 | creado |
| `Almacen Jefatura` | 6 | creado |
| `Compras` | 8 | **ya existía**, se le sembraron los claims |
| `Compras Jefatura` | 3 | creado |
| `Proveedores` | 4 | creado |
| `Contabilidad` | 4 | **ya existía**, se le sembraron los claims |

El total de claims del mirror pasó de 147 a 182. Se repitió el script de almacén para
comprobar la idempotencia: `INSERT 0 0` en los cuatro enunciados.

> ⚠️ **Dos de los seis roles ya existían y ya tenían gente.** En el mirror, `Compras` tenía 1
> miembro y `Contabilidad` 3, así que esos usuarios **ganaron los permisos nuevos en el acto**,
> sin que nadie descomentara la asignación. Arriba la lista de miembros será distinta: correr
> antes la consulta de miembros de cada script y confirmar que a esa gente le corresponde el
> permiso nuevo.

Comando, uno por uno:

```bash
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-09-07_rol_almacen_permisos.sql
```

**Advertencias:**

- ⚠️ **La asignación de usuarios va comentada en los cuatro.** Hay que descomentarla y poner
  los correos reales en mayúsculas. Sin eso los roles quedan creados y vacíos de gente, que es
  el estado seguro.
- ⚠️ **El permiso de módulo manda sobre el fino.** `module.<modulo>.edit` habilita todos los
  recursos del módulo por cascada, así que los roles operativos no lo llevan a propósito.
  Agregárselo a mano anula la separación de funciones que estos scripts construyen.
- ⚠️ **Presupuesto no se delega por permisos.** Sus pantallas piden el rol Admin o Contabilidad.
  El script A4 documenta el cambio de código que haría falta para un rol propio.
- ⚠️ **El menú no se filtra por permisos** (hallazgo 1 de
  [PENDIENTES_ROLES_Y_MENU_2026-08-05.md](../docs/PENDIENTES_ROLES_Y_MENU_2026-08-05.md)): quien
  reciba estos roles va a ver opciones que no puede abrir y recibirá un 403. Conviene avisarlo
  antes de repartirlos.
- Cada claim nuevo viaja en la cookie de sesión. Con 208 claims ya se vio un HTTP 400 por
  tamaño de cabeceras (hallazgo 4), y por eso los scripts siembran lo mínimo que cubre el caso.

**¿Ya aplicado?** Cada script termina con su verificación. Para verlo todo junto:

```sql
SELECT r."Name" AS rol, count(rc."Id") AS claims
  FROM identity."AspNetRoles" r
  LEFT JOIN identity."AspNetRoleClaims" rc ON rc."RoleId" = r."Id"
 GROUP BY r."Name" ORDER BY r."Name";
```

## 11. Versionado (git)

**Untracked** al 2026-09-07, en la rama `feat/almacen-integracion-contable`:

- `Database/2026-09-07_aplicar_pendientes_srv.sql` (este guion)
- `Database/2026-09-07_runbook_despliegue_srv.md` (este runbook)
- `Database/2026-09-07_rol_almacen_permisos.sql`, `..._rol_compras_permisos.sql`,
  `..._rol_proveedores_permisos.sql` y `..._rol_presupuesto_permisos.sql` (anexo §10)
- `Database/2026-09-05_ciudad_empresa_configuracion.sql` (paso 2) y su runbook
- `Database/2026-09-04_perf_rep_saldo_clientes_categoria_cobranza.sql`, su rollback y su runbook
  (fuera de esta tanda: ya está aplicado arriba)

Los otros 15 scripts incluidos ya están versionados. Conviene commitear el guion junto con el
binario del portal que acompaña la tanda.
