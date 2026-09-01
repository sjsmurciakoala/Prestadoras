# Runbook de despliegue a SRV — El disponible presupuestario deja de ocultar los sobregiros

**Base destino:** `siad_v4` @ `172.16.0.9`
**Fecha:** 2026-09-01
**Alcance:** 1 script. Corrección de 5 objetos del control presupuestario (hallazgo H4 de la ronda de QA de Compras).
**Origen:** ronda de pruebas funcionales del módulo de Compras del 2026-09-01.

> ⚠️ **DEPENDE de la tanda del 2026-08-27**, que sigue **PENDIENTE** en SRV.
> Este script hace `CREATE OR REPLACE` sobre objetos que crea
> `2026-08-27_pst_compromiso_02_funciones.sql`, `_03_procedimientos.sql` y `_04_vistas.sql`.
> **Si aquella tanda no se ha aplicado, este script falla** (o peor: si se aplica y luego se
> corre la del 27, esta corrección queda pisada). **Va DESPUÉS de la tanda del 2026-08-27.**
>
> ⚠️ **Tandas anteriores pendientes:** `2026-08-22` (CxP unificada), `2026-08-27` (control
> presupuestario, 5 scripts) y `2026-08-31` (aprobación por niveles, 3 scripts). Esta tanda
> **no las reemplaza ni las cierra**.
>
> ⚠️ La base **ACTIVA es `siad_v4`**, no `siad_v3`.

---

## 1. Qué cubre este runbook

El disponible presupuestario se calculaba con `GREATEST(proyección − comprometido − real, 0)`.
Cuando una partida está sobregirada, ese truncado la reporta en `0.00` y deja de distinguirse
una partida justo agotada de una excedida en miles.

| Síntoma observado en QA | Con la corrección |
|---|---|
| Reporte de ejecución: cuenta `11401010101` con proyección 10,000.00 y comprometido 15,805.92 mostraba disponible `0.00` | Muestra `−5,805.92` |
| Mensaje al aprobar: «Disponible: 0.00 … Faltan: 13,125.00» | Informa el faltante real |
| Sumar la columna «disponible» de un reporte daba un total inflado (los sobregiros no restaban) | La suma refleja los sobregiros |

El `COMMENT` de la propia vista ya declaraba la fórmula sin truncar
(«Disponible = proyeccion - comprometido - real»): era el código el que no la cumplía.

| Objeto | Qué alimenta |
|---|---|
| `fn_pst_disponible` | Panel de presupuesto previo de la orden de compra |
| `vw_pst_ejecucion_presupuestaria` | Reporte de ejecución (pantalla, PDF y Excel) |
| `sp_pst_comprometer_documento` | Texto «Disponible / Faltan» al aprobar una orden |
| `sp_pst_ajustar_compromiso` | El mismo mensaje, al aumentar una orden ya comprometida |
| `sp_pst_devengar_documento` | El mismo mensaje, al recibir una factura |

## 2. Antes de empezar (obligatorio)

**Backup de `siad_v4`:**

```bash
pg_dump -h 172.16.0.9 -U postgres -d siad_v4 -Fc -f siad_v4_antes_disponible_sin_truncar.backup
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

**Comprobar el prerrequisito duro** — los 5 objetos deben existir ya (tanda del 2026-08-27):

```sql
SELECT to_regproc('public.fn_pst_disponible')                 AS fn_disponible,
       to_regclass('public.vw_pst_ejecucion_presupuestaria')  AS vista,
       to_regproc('public.sp_pst_comprometer_documento')      AS comprometer,
       to_regproc('public.sp_pst_ajustar_compromiso')         AS ajustar,
       to_regproc('public.sp_pst_devengar_documento')         AS devengar;
```

Los cinco deben devolver nombre. Si alguno sale `NULL`, **la tanda del 2026-08-27 no está
aplicada: aplicarla primero** y recién después este script.

## 3. Advertencias clave (leer antes de aplicar)

- El script es **re-ejecutable**: solo `CREATE OR REPLACE` dentro de un `BEGIN … COMMIT`.
  No hay `DROP`, `DELETE`, `TRUNCATE` ni `UPDATE`. **No modifica ni una fila.**
- **No cambia ninguna firma de función**, así que nada que las invoque necesita recompilarse.
- **La regla de bloqueo es idéntica.** Se valida `requerido > disponible`: cuando no hay
  sobregiro el `GREATEST` era transparente (el valor ya era positivo), y cuando lo hay, tanto
  `0` como el negativo hacen fallar la comparación. **Nada que hoy pase empieza a rechazarse**;
  solo mejora la cifra que se informa.
- ⚠️ **Cambia lo que ven los reportes ya existentes.** Una partida sobregirada pasa de mostrar
  `0.00` a un número negativo. Si alguien exporta el reporte de ejecución a Excel y suma la
  columna, el total bajará respecto de la exportación anterior — porque ahora sí resta los
  sobregiros. Conviene avisar a quien use ese reporte.
- **Queda fuera a propósito** `fn_pst_aplicar_movimiento` y la columna cacheada
  `pst_config_presupuesto_dtl.valor_disponible` (y por tanto `pst_movimiento.disponible_*`).
  Esa columna la consume además la pantalla de configuración presupuestaria, que usa **otra
  fórmula** (`valor_global − real`, sin comprometido, ver `2026-07-24_presupuesto_multitenant_company_id.sql`)
  y no entró en la ronda de pruebas. Cambiarla exige probar ese módulo aparte.
  **Consecuencia conocida:** el histórico de `pst_movimiento` seguirá guardando el disponible
  truncado a 0 en los movimientos sobre partidas sobregiradas.
- Los `GREATEST` sobre `valor_proyeccion` / `comprometido` / `real` / `pagado` **NO se tocan**:
  esos evitan acumuladores negativos y son una salvaguarda distinta.

## 4. Orden de aplicación (resumen)

| Paso | Script | Naturaleza | ¿Re-ejecutable? | Depende de |
|---:|---|---|:--:|---|
| 1 | `2026-09-01_pst_disponible_sin_truncar.sql` | Objetos (2 funciones, 1 vista, 2 procedimientos) | Sí | **Toda la tanda `2026-08-27`** (pasos 2, 3 y 4) |

Trae su propio `BEGIN … COMMIT`.

## 5. Detalle por paso

### Paso 1 — Disponible sin truncar (`2026-09-01_pst_disponible_sin_truncar.sql`)

Reemplaza las 5 rutinas quitando el `GREATEST(…, 0)` que truncaba el disponible, y actualiza
los `COMMENT` de la función y de la vista para que declaren el comportamiento nuevo.

```bash
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-09-01_pst_disponible_sin_truncar.sql
```

**¿Ya aplicado?**

```sql
SELECT count(*) AS objetos_con_truncado_residual
  FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
 WHERE n.nspname = 'public'
   AND p.proname IN ('fn_pst_disponible', 'sp_pst_comprometer_documento',
                     'sp_pst_ajustar_compromiso', 'sp_pst_devengar_documento')
   AND pg_get_functiondef(p.oid) LIKE '%GREATEST(v_proy - v_comp - v_real, 0)%';
```

Esperado tras aplicar: `0`. Si devuelve `1` o más, **falta aplicar** (o quedó a medias).

**Verificación:**

```sql
-- a) Las partidas sobregiradas ya reportan el negativo real, no 0
SELECT con_cuenta_code, presupuesto, comprometido, ejecutado, disponible, pct_utilizado
  FROM public.vw_pst_ejecucion_presupuestaria
 WHERE company_id = 2 AND disponible < 0
 ORDER BY disponible;

-- b) La función coincide con la vista para la misma cuenta y fecha
SELECT public.fn_pst_disponible(2, '11401010101', CURRENT_DATE) AS por_funcion,
       (SELECT disponible FROM public.vw_pst_ejecucion_presupuestaria
         WHERE company_id = 2 AND con_cuenta_code = '11401010101'
           AND CURRENT_DATE BETWEEN fecha_inicia AND fecha_finaliza) AS por_vista;

-- c) Las partidas SIN sobregiro no cambiaron de valor
SELECT count(*) FILTER (WHERE disponible >= 0) AS sanas,
       count(*) FILTER (WHERE disponible <  0) AS sobregiradas
  FROM public.vw_pst_ejecucion_presupuestaria WHERE company_id = 2;
```

**No-regresión:** aprobar en el portal una orden que **cabe** en su presupuesto debe seguir
funcionando igual que antes (el disponible positivo no cambia de valor). Aprobar una que **no
cabe** debe seguir siendo rechazada, con la única diferencia de que el mensaje informa el
faltante real en vez de uno subestimado.

## 6. Estado presunto

| Base | Estado |
|---|---|
| `siad_v3_restore` (mirror, localhost) | ✅ **APLICADO** el 2026-09-01, exit 0. Verificado: la cuenta `11401010101` pasó de reportar `0.00` a `−5,805.92`; `fn_pst_disponible` devuelve el mismo valor que la vista; 0 objetos con truncado residual |
| `siad_v4` @ 172.16.0.9 (SRV) | ⏳ **Pendiente** — lo aplica el usuario, y **solo después** de la tanda del 2026-08-27 |

Nunca se verifica conectándose a la BD desde aquí: el paso trae su consulta «¿ya aplicado?».

## 7. Versionado (git)

Archivos nuevos, **untracked** al 2026-09-01:

- `Database/2026-09-01_pst_disponible_sin_truncar.sql`
- `Database/2026-09-01_runbook_despliegue_srv.md`

Cambios de código de la misma sesión, **no incluidos en esta tanda de BD** (van con el
despliegue del portal, no con el SQL):

- `SIAD.Services/Almacen/OrdenCompraService.cs`, `RecepcionCompraService.cs` — el ISV se
  calcula sobre la base neta de descuento (hallazgo H1)
- `SIAD.Services/Almacen/ClasificacionNormalizer.cs` + 6 servicios del módulo — el buscador
  encuentra por el número con relleno de ceros (hallazgo H5)
- `apc.Client/Pages/Almacen/RecepcionCompraFormPage.razor` — la pantalla relee el documento
  guardado (hallazgo H2)
- `apc.Client/Pages/Almacen/OrdenCompraFormPage.razor` — espejo del cálculo del ISV
