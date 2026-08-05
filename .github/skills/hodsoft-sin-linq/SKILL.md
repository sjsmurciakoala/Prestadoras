---
name: hodsoft-sin-linq
description: Use when writing or modifying any C# in this repo that reads or writes data, or that filters, sorts, groups, projects or aggregates a collection — services, controllers, Blazor pages, reports, tests. Triggers on LINQ operators (.Where, .Select, .Any, .FirstOrDefault, .OrderBy, .Sum, .GroupBy, .ToList) and on query syntax (from ... in ... select). LINQ is banned in this project; all data access goes through Postgres stored procedures, functions and views. Responde en español.
---

# Sin LINQ — acceso a datos por SP, funciones y vistas

## Regla

**En este proyecto no se usa LINQ. En ninguna capa, en ningún proyecto de la solución.**

Todo acceso a datos pasa por **stored procedures, funciones y vistas de Postgres**.
El único SQL que puede aparecer en C# es la línea que **invoca** un SP, una función o una vista.

```
Lógica de consulta (SELECT, JOIN, WHERE, GROUP BY, ORDER BY, agregados)  →  vive en la BD
C#  →  solo invoca y recibe filas
```

Esto vale para `SIAD.Services`, `SIAD.Data`, `SIAD.Core`, `SIAD.Reports`, `apc`, `apc.Client`, `apc.BancosWs`, `apc.MobileApi` y `SIAD.Tests`.

**Violar la letra de la regla es violar su espíritu.** No hay "LINQ pequeño".

## Qué queda prohibido

| Prohibido | Por qué |
|---|---|
| `_context.tabla.Where(...)` / `.FirstOrDefaultAsync()` / cualquier `IQueryable` de EF | La consulta queda enterrada en C# en vez de versionada en `Database/` |
| `.Where` `.Select` `.Any` `.All` `.First*` `.Single*` `.Last*` `.OrderBy*` `.GroupBy` `.Sum` `.Count` `.Max` `.Min` `.Distinct` `.Take` `.Skip` `.SelectMany` `.Join` `.Aggregate` `.ToList()` `.ToArray()` `.ToDictionary()` sobre colecciones en memoria | Es LINQ igual, aunque no toque la BD |
| Sintaxis de consulta `from x in ... select` | Es LINQ |
| `using System.Linq` | Si hace falta, es que quedó LINQ |
| SQL con lógica de negocio embebido en C#: `FromSqlRaw`/`FromSqlInterpolated`/`ExecuteSqlInterpolated` con un `SELECT ... JOIN ... WHERE` completo | El SQL debe estar en la BD como SP/función/vista, no en un string de C# |
| `Add`/`Update`/`Remove` + `SaveChangesAsync` para escribir datos de negocio | La escritura va por SP |

## Qué se usa en su lugar

Patrón canónico del repo — [SIAD.Services/BancosWs/BancosWsService.cs:95](../../../SIAD.Services/BancosWs/BancosWsService.cs) y [SIAD.Services/Contabilidad/SaldosService.cs:63](../../../SIAD.Services/Contabilidad/SaldosService.cs):

```csharp
// Lectura por función: Dapper mapea las columnas al DTO sin LINQ
const string sql = @"
    SELECT clave AS Clave, nombre AS Nombre, direccion AS Direccion
    FROM public.fn_ban_ws_cliente(@CompanyId, @Clave);";

var conn = _context.Database.GetDbConnection();
var filas = await conn.QueryAsync<ClienteDto>(
    new CommandDefinition(sql, new { CompanyId = companyId, Clave = clave },
                          cancellationToken: ct));

// Materializar sin .ToList()
var resultado = new List<ClienteDto>(filas);
```

```csharp
// Escritura por SP
await conn.ExecuteAsync(new CommandDefinition(
    "CALL public.sp_alm_registrar_movimiento(@CompanyId, @ArticuloId, @Cantidad, @Usuario);",
    new { CompanyId = companyId, ArticuloId = id, Cantidad = cant, Usuario = user },
    transaction: tx, cancellationToken: ct));
```

Equivalencias directas:

| En vez de… | Usa… |
|---|---|
| `.Where(...)` / `.OrderBy(...)` | `WHERE` / `ORDER BY` dentro de la vista o función |
| `.FirstOrDefaultAsync()` | `QueryFirstOrDefaultAsync<T>` sobre la función |
| `.AnyAsync(...)` | `QuerySingleAsync<bool>("SELECT EXISTS(...)")` o una función que devuelva `boolean` |
| `.CountAsync()` / `.Sum(...)` / `.GroupBy(...)` | `count()` / `sum()` / `GROUP BY` dentro de la vista o función |
| `.Select(x => new Dto { ... })` sobre filas de BD | Alias de columna + `QueryAsync<Dto>`: Dapper mapea solo |
| `.Select(...)` sobre una lista ya en memoria | `foreach` + `lista.Add(...)` |
| `.ToList()` sobre un `IEnumerable` ya materializado | `new List<T>(origen)` |
| `.Any(x => x.Id == id)` en una página Blazor | `foreach` con bandera `bool` |
| SP que devuelve `refcursor` | `NpgsqlCommand` con `CommandType.StoredProcedure` — ver [BanTransaccionesService.cs:229](../../../SIAD.Services/Bancos/BanTransaccionesService.cs) |

Detalle completo de invocación (cursores, transacciones, tipos, mapeo) en [references/patrones-sp-dapper.md](references/patrones-sp-dapper.md).

## Tenancy — el punto que más se rompe

Dapper y `NpgsqlCommand` **no pasan por el filtro global de `SiadDbContext`**. Al salir de EF pierdes el `company_id` automático.

- Toda vista, función y SP recibe `company_id` como **primer parámetro explícito** y filtra por él.
- El valor sale de `ICurrentCompanyService`, **nunca** del body de la petición.
- Reusa la conexión del contexto (`_context.Database.GetDbConnection()`) para quedar dentro de la transacción y la conexión ya abiertas.

Una consulta sin `company_id` es una fuga entre empresas, no un descuido de estilo.

## ¿No existe el SP / la función / la vista?

**Se crea.** No se resuelve con LINQ "mientras tanto".

1. Escribe la vista/función/SP en un script con fecha en `Database/` (ej. `2026-08-03_fn_alm_articulos_listado.sql`).
2. Antes de cualquier DDL usa la skill `guardia-estructura-bd`.
3. Regístralo en el runbook con la skill `runbook-despliegue-srv`.
4. El usuario decide cuándo aplicarlo al mirror y al SRV — tú no te conectas a ninguna BD por iniciativa propia.
5. Recién entonces escribe la invocación en C#.

Si eso bloquea la tarea, **dilo y para** — no entregues LINQ como puente.

## Código existente

Hay ~3.000 usos de LINQ repartidos en la solución. La migración es por etapas, no de golpe:

- **Código nuevo:** cero LINQ, sin excepción.
- **Archivo que tocas:** migras el método que tocas, no el archivo entero.
- **Migración dirigida:** solo cuando el usuario la pida explícitamente por módulo.

Inventario por proyecto y procedimiento de migración en [references/inventario-migracion.md](references/inventario-migracion.md).

No "aprovechar el viaje" para reescribir un servicio completo: cada método migrado cambia el plan de ejecución en la BD y necesita verificación aparte.

## Banderas rojas — para y corrige

- Escribiste `using System.Linq`
- Escribiste `.Where(`, `.Select(`, `.Any(`, `.FirstOrDefault`, `.ToList()`
- Escribiste un `SELECT` con `JOIN` o `WHERE` dentro de un string de C#
- Estás por usar `_context.<tabla>.` seguido de cualquier cosa que no sea una invocación a SP
- Pensaste "esto es en memoria, no cuenta"
- Pensaste "creo el SP después"
- Llamaste a una función de BD sin pasarle `company_id`

**Todas significan lo mismo: bórralo y hazlo por SP, función o vista.**

## Racionalizaciones y respuesta

| Excusa | Realidad |
|---|---|
| "Es un `.Any()` en memoria, no toca la BD" | Sigue siendo LINQ. `foreach` + bandera. |
| "EF traduce el `Where` a SQL, es lo mismo" | No. El objetivo es que el SQL viva en la BD, versionado en `Database/` y desplegable al SRV. Un `Where` en C# no se puede revisar ni desplegar. |
| "Es una consulta trivial de una tabla" | Entonces es una vista trivial. Cuesta 3 líneas. |
| "El archivo ya está lleno de LINQ" | Razón para no agregar más. Migras lo que tocas. |
| "Dapper igual necesita `.Select` para mapear" | No: `QueryAsync<TDto>` mapea por alias de columna. Si hay transformación, `foreach`. |
| "Es solo un test" | `SIAD.Tests` también. Un test con LINQ valida distinto a como corre producción. |
| "Ya es `FromSqlInterpolated`, o sea SQL crudo" | SQL de negocio embebido en C# tampoco. Muévelo a SP/función/vista. |
| "Lo migro completo después" | Después no llega. Este método, ahora. |
| "No hay tiempo de crear el SP" | Crear la función toma menos que discutirlo. Y si de verdad bloquea, se reporta el bloqueo. |

## Verificación antes de decir "listo"

```bash
git diff --unified=0 -- '*.cs' | grep -n "^+" | grep -E "using System\.Linq|\.Where\(|\.Select\(|\.Any\(|\.All\(|\.FirstOrDefault|\.SingleOrDefault|\.OrderBy|\.GroupBy\(|\.Sum\(|\.ToList\(\)|from [a-zA-Z_]+ in "
```

Sin salida = limpio. Con salida = todavía no terminaste. No declares completado sin correrlo.
