# Patrones de invocación sin LINQ

Referencia de cómo llamar a stored procedures, funciones y vistas de Postgres desde C# en este repo. Todos los ejemplos salen de código que ya existe en la solución.

## Elegir el objeto de BD

| Necesitas | Objeto | Invocación |
|---|---|---|
| Lectura fija, sin parámetros más allá de `company_id` | **Vista** (`vw_*`) | `SELECT ... FROM public.vw_x WHERE company_id = @CompanyId` |
| Lectura parametrizada (filtros, rangos, búsqueda) | **Función** (`fn_*`) que devuelve `TABLE(...)` o `SETOF` | `SELECT ... FROM public.fn_x(@CompanyId, @Otro)` |
| Escritura, o lectura con efectos (posteo, correlativos, locks) | **Procedure** (`sp_*`) | `CALL public.sp_x(...)` o `SELECT * FROM public.sp_x(...)` si está declarado como función |
| Un solo valor escalar | **Función** que devuelve el tipo | `SELECT public.fn_x(@CompanyId, ...)` |

Convención de nombres del repo: `vw_` vistas, `fn_` funciones, `sp_` procedures/funciones de proceso. Todo en el esquema `public`, calificado explícitamente en el SQL de invocación.

## 1. Lectura de varias filas → DTO

`SIAD.Services/Contabilidad/SaldosService.cs`

```csharp
using Dapper;

var conn = _context.Database.GetDbConnection();   // reusa conexión y transacción del contexto

const string sql = @"
    SELECT period_id     AS PeriodId,
           codigo_cuenta AS CodigoCuenta,
           debitos       AS Debitos,
           creditos      AS Creditos
    FROM public.fn_con_verificar_saldo_cuenta(@CompanyId, @PeriodId, NULL)";

var filas = await conn.QueryAsync<SaldoDivergenciaDto>(
    new CommandDefinition(sql, new { CompanyId = companyId, PeriodId = periodId },
                          cancellationToken: ct));

var resultado = new List<SaldoDivergenciaDto>(filas);   // no .ToList()
```

**El alias de columna hace el mapeo.** Nombra las columnas de la función igual que las propiedades del DTO (o pon alias) y Dapper llena el objeto solo — sin `.Select(...)`.

Si de verdad hay que transformar algo que la función no puede resolver:

```csharp
var resultado = new List<SaldoDivergenciaDto>();
foreach (var f in filas)
{
    resultado.Add(new SaldoDivergenciaDto
    {
        PeriodId = f.PeriodId,
        Etiqueta = ArmarEtiqueta(f)     // lógica de C# que no es SQL
    });
}
```

Pero antes pregúntate si esa transformación no pertenece a la función.

## 2. Una fila o ninguna

```csharp
var dto = await conn.QueryFirstOrDefaultAsync<ArticuloDto>(
    new CommandDefinition(
        "SELECT * FROM public.fn_alm_articulo(@CompanyId, @ArticuloId)",
        new { CompanyId = companyId, ArticuloId = id },
        cancellationToken: ct));

if (dto is null) { /* no encontrado */ }
```

Obligatoria (lanza si no hay fila): `QuerySingleAsync<T>`.

## 3. Escalares y existencia

```csharp
// reemplaza .AnyAsync(...)
var existe = await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
    "SELECT public.fn_alm_articulo_existe(@CompanyId, @Codigo)",
    new { CompanyId = companyId, Codigo = codigo }, cancellationToken: ct));

// reemplaza .CountAsync()
var total = await conn.ExecuteScalarAsync<long>(new CommandDefinition(
    "SELECT count(*) FROM public.vw_alm_articulos WHERE company_id = @CompanyId",
    new { CompanyId = companyId }, cancellationToken: ct));
```

## 4. Escritura

```csharp
await conn.ExecuteAsync(new CommandDefinition(
    @"CALL public.sp_alm_registrar_movimiento(
          @CompanyId, @ArticuloId, @BodegaId, @Cantidad, @Costo, @Usuario);",
    new { CompanyId = companyId, ArticuloId = id, BodegaId = bodega,
          Cantidad = cant, Costo = costo, Usuario = user },
    cancellationToken: ct));
```

Si el SP devuelve resultado (patrón de `sp_ban_ws_pagar`, declarado como función):

```csharp
var r = await conn.QueryFirstAsync<PagoResultDto>(new CommandDefinition(
    @"SELECT status AS Status, pago_id AS PagoId, poliza_id AS PolizaId
      FROM public.sp_ban_ws_pagar(@CompanyId, @Referencia, @Monto, @Usuario);",
    new { ... }, cancellationToken: ct));
```

Convención del repo: el SP devuelve un `status` textual y el servicio lo traduce a un resultado tipado con `switch`.

## 5. SP con `refcursor`

Cuando el procedure devuelve uno o más cursores hay que abrir transacción propia — el cursor solo vive dentro de ella. Patrón de `SIAD.Services/Bancos/BanTransaccionesService.cs:229`:

```csharp
await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync(ct);
await using var transaction = await connection.BeginTransactionAsync(ct);

var cursorName = "sp_ban_kardex_detalle_cursor";

await using (var command = connection.CreateCommand())
{
    command.Transaction = transaction;
    command.CommandType  = CommandType.StoredProcedure;
    command.CommandText  = "public.sp_ban_kardex_detalle";

    command.Parameters.AddWithValue("p_company_id",   NpgsqlDbType.Bigint, companyId);
    command.Parameters.AddWithValue("p_ban_kardex_id", NpgsqlDbType.Bigint, banKardexId);

    var cursorParam = new NpgsqlParameter("p_result", NpgsqlDbType.Refcursor)
    {
        Direction = ParameterDirection.InputOutput,
        Value = cursorName
    };
    command.Parameters.Add(cursorParam);

    await command.ExecuteNonQueryAsync(ct);

    if (cursorParam.Value is string value && !string.IsNullOrWhiteSpace(value))
        cursorName = value;
}

var lineas = new List<LineaDto>();
await using (var fetch = connection.CreateCommand())
{
    fetch.Transaction = transaction;
    fetch.CommandText = $"FETCH ALL IN \"{cursorName}\";";

    await using var reader = await fetch.ExecuteReaderAsync(ct);
    while (await reader.ReadAsync(ct))
    {
        lineas.Add(new LineaDto
        {
            Id     = reader.GetInt64(reader.GetOrdinal("id")),
            Valor  = reader.GetDecimal(reader.GetOrdinal("valor"))
        });
    }
}

await transaction.CommitAsync(ct);
```

`while (reader.ReadAsync())` + `List.Add` es el reemplazo natural de `.Select(...).ToList()`.

## 6. Transacciones

- **Dentro del flujo de EF:** usa `_context.Database.GetDbConnection()` y pásale `_context.Database.CurrentTransaction?.GetDbTransaction()` al `CommandDefinition` para no salirte de la transacción abierta.
- **Flujo propio:** `NpgsqlConnection` + `BeginTransactionAsync`, commit explícito, `await using` en todo.
- Un SP que hace `COMMIT` interno **no** se puede llamar dentro de una transacción externa. Verifica el cuerpo del SP en `Database/ddl_v3/` antes de envolverlo.

## 7. Tenancy

Al salir de EF pierdes el filtro global de `SiadDbContext.Tenancy.cs`.

```csharp
var companyId = _currentCompanyService.GetCompanyId();   // nunca del DTO de request
```

- `company_id` es el **primer parámetro** de toda vista, función y SP.
- La vista filtra por `company_id`; la función lo recibe y filtra adentro.
- Ningún objeto de BD nuevo se acepta sin `company_id`, salvo catálogos globales — y eso se documenta en el script.

## 8. Parámetros y tipos

- Siempre parámetros nombrados (`@Nombre`), nunca concatenación de strings.
- `date` / `time`: castea en el SQL de invocación — `@Fecha::date`, `@Hora::time`.
- `null`: pasa `null` en el objeto anónimo; Dapper lo manda como `DBNULL`.
- Arrays: Npgsql mapea `long[]` / `string[]` a `bigint[]` / `text[]` directo.
- `decimal` monetario: redondea en la BD, no en C#, para que reportes y pantallas coincidan.

## 9. Registrar el objeto de BD

Toda vista/función/SP nueva:

1. Script con fecha en `Database/` (ej. `2026-08-03_fn_alm_articulos_listado.sql`), idempotente (`CREATE OR REPLACE`).
2. Skill `guardia-estructura-bd` antes de cualquier DDL destructivo.
3. Skill `runbook-despliegue-srv` para registrarlo en `Database/*_runbook_despliegue_srv.md`.
4. No te conectas a ninguna BD por iniciativa propia — el usuario aplica el script.
