# Runbook de despliegue a SRV — Datos de empresa para el cheque y la firma de cobranza

**Base destino:** `siad_v4` @ `172.16.0.9`
**Fecha:** 2026-09-05
**Alcance:** 2 scripts. Un `UPDATE` idempotente de una fila de configuración y un `ADD COLUMN` aditivo sobre la misma tabla.
**Origen:** al probar el pagaré del convenio en el portal se vio que `con_empresa_configuracion.ciudad` está vacía. El pagaré terminó con el municipio fijo en la plantilla, así que el campo le queda al **cheque**, que imprime esa ciudad como lugar de emisión.

> ✅ **Esta tanda NO depende de las tandas pendientes.** La tabla
> `con_empresa_configuracion` y su columna `ciudad` existen desde el esquema base; el script
> solo escribe un dato. Puede aplicarse antes, después o entre las tandas pendientes.
>
> ⚠️ **Tandas anteriores aún pendientes** y que esta **no reemplaza ni cierra**: `2026-08-22`
> (CxP unificada), `2026-08-27` (control presupuestario, 5 scripts), `2026-08-31` (aprobación
> por niveles, 3 scripts) y `2026-09-01` (disponible sin truncar). La tanda `2026-09-04`
> (rendimiento del informe de saldos por categoría) ya quedó aplicada.
>
> ⚠️ La base **ACTIVA es `siad_v4`**, no `siad_v3`.

---

## 1. Qué cubre este runbook

El cheque imprime su lugar de emisión desde `con_empresa_configuracion.ciudad`. Ese campo
nunca se llenó, así que el cheque sale sin ciudad.

| Antes | Después |
|---|---|
| `ciudad` vacía; el cheque imprime solo la fecha | `ciudad` = `Puerto Cortés`; el cheque imprime "Puerto Cortés, 13 JULIO 2026" |

⚠️ **No escribir aquí el departamento.** Guardar "Puerto Cortés, Cortés" haría que el cheque
imprimiera "Puerto Cortés, Cortés, 13 JULIO 2026". El pagaré ya no depende de este campo: su
municipio y departamento son fijos del formato y viven en la plantilla.

## 2. Antes de empezar (obligatorio)

**Backup de `siad_v4`:**

```bash
pg_dump -h 172.16.0.9 -U postgres -d siad_v4 -Fc -f siad_v4_antes_ciudad_empresa.backup
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

**Ver qué hay hoy** (y con qué `company_id` quedará la empresa arriba):

```sql
SELECT c.company_id, c.commercial_name, coalesce(nullif(btrim(e.ciudad), ''), '(vacia)') AS ciudad
  FROM public.cfg_company c
  LEFT JOIN public.con_empresa_configuracion e ON e.company_id = c.company_id
 ORDER BY c.company_id;
```

## 3. Advertencias clave (leer antes de aplicar)

- El script es **re-ejecutable**: escribe solo donde la ciudad está vacía. Correrlo dos veces
  no cambia nada la segunda.
- **No pisa un valor existente.** Si en `siad_v4` alguien ya cargó la ciudad, el `UPDATE`
  no afecta filas y el paso queda en `UPDATE 0`, que es resultado correcto.
- **No usa `company_id` fijo.** Ubica la empresa por `commercial_name ILIKE 'Aguas de Puerto
  Cort%'`, así que no depende de que el id sea el mismo que en el mirror (ahí es `2`).
- ⚠️ **Toca un dato que ven dos documentos**: el pagaré y el cheque. Si el cheque físico ya se
  imprime con la ciudad puesta a mano en otro lado, revisar que no quede duplicada.
- El valor se escribe **con tilde**: `Puerto Cortés`. El script fija `client_encoding` a UTF8
  para que no se corrompa al aplicarlo desde una consola Windows.

## 4. Orden de aplicación (resumen)

| Paso | Script | Naturaleza | ¿Re-ejecutable? | Depende de |
|---:|---|---|:--:|---|
| 1 | `2026-09-05_ciudad_empresa_configuracion.sql` | Datos idempotente (1 fila de configuración) | Sí | Nada. Solo que exista la fila de `con_empresa_configuracion` de la empresa |
| 2 | `2026-09-08_firmante_cobranza.sql` | Aditivo (1 columna) | Sí | Nada. Independiente del paso 1 |

## 5. Detalle por paso

### Paso 1 — Cargar la ciudad (`2026-09-05_ciudad_empresa_configuracion.sql`)

Escribe `ciudad = 'Puerto Cortés'` en la fila de configuración de la empresa, solo si está
vacía, y sella `updated_at` / `updated_by`.

```bash
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-09-05_ciudad_empresa_configuracion.sql
```

**¿Ya aplicado?**

```sql
SELECT c.company_id,
       c.commercial_name,
       e.ciudad,
       e.updated_by
  FROM public.cfg_company c
  JOIN public.con_empresa_configuracion e ON e.company_id = c.company_id
 WHERE c.commercial_name ILIKE 'Aguas de Puerto Cort%';
```

Esperado tras aplicar: `ciudad` = `Puerto Cortés`. Si ya trae una ciudad distinta, el script
la respeta y no hay nada que hacer.

**Verificación funcional en el portal:** emitir un cheque desde Bancos y comprobar que el
lugar de emisión sale como "Puerto Cortés, <fecha>".

### Paso 2 — Quién firma por cobranza (`2026-09-08_firmante_cobranza.sql`)

Agrega `con_empresa_configuracion.firmante_cobranza` (varchar 120, acepta nulos). El pagaré y
el compromiso de pago imprimen ese nombre al pie; mientras la columna esté vacía siguen saliendo
con el rótulo genérico, así que **el script se puede aplicar antes que el despliegue del portal
sin cambiar ningún documento**.

```bash
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-09-08_firmante_cobranza.sql
```

**¿Ya aplicado?**

```sql
SELECT column_name, data_type, character_maximum_length
  FROM information_schema.columns
 WHERE table_name = 'con_empresa_configuracion'
   AND column_name = 'firmante_cobranza';
```

Esperado tras aplicar: una fila, `character varying`, 120.

**Verificación funcional en el portal:** Contabilidad → Empresas → editar la empresa → campo
**Firma por cobranza**. Al llenarlo, el nombre sale sobre la línea de firma de la empresa en los
dos documentos del convenio; al dejarlo vacío, vuelve el rótulo genérico.

## 6. Estado presunto

| Base | Estado |
|---|---|
| `siad_v3_restore` (mirror, localhost) | ✅ **LOS DOS APLICADOS**, autorizados por el usuario. Paso 1 el 2026-09-05 (`UPDATE 1`, ciudad `Puerto Cortés`); paso 2 el 2026-09-08 (columna `firmante_cobranza` varchar 120 creada y verificada) |
| `siad_v4` @ 172.16.0.9 (SRV) | ⏳ **Los dos pendientes** |

Nunca se verifica conectándose a la BD desde aquí: el paso trae su consulta «¿ya aplicado?».

## 7. Cambio de código que acompaña a esta tanda

**Va con el despliegue del portal, no con el SQL.** El SQL sirve por sí solo al cheque; el
pagaré es nuevo y llega con el código.

- `SIAD.Core/DTOs/Cobranza/PagareImpresionDto.cs` — datos del pagaré.
- `SIAD.Services/Cobranza/CobranzaService.cs` — `ObtenerPagareImpresionAsync`.
- `SIAD.Reports/Templates/Rpt_Dev_Pagare.cs` — plantilla que replica el formulario preimpreso.
- `apc/Controllers/CobranzaController.cs` — `GET api/cobranza/planes/{id}/pagare`.
- `apc.Client/Services/Facturacion/CobranzaClient.cs` y
  `apc.Client/Pages/Facturacion/Cobranza/Cobranza.razor` — botón **Pagaré** en la grilla.
- `SIAD.Tests/Cobros/PagareRenderTests.cs` — pruebas de render.

## 8. Versionado (git)

Archivos nuevos, **untracked** al 2026-09-05:

- `Database/2026-09-05_ciudad_empresa_configuracion.sql`
- `Database/2026-09-05_runbook_despliegue_srv.md`
- `Database/2026-09-08_firmante_cobranza.sql`

El campo nuevo se edita desde `apc.Client/Pages/Contabilidad/EmpresaForm.razor`, con su DTO en
`CompanyCreationDto` y el mapeo en `CompanyManagementService`.
