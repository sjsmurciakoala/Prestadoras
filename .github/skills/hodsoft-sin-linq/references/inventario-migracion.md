# Inventario de LINQ y procedimiento de migración

## Estado al 2026-08-03

Medición: ocurrencias de `.Where(` `.Select(` `.Any(` `.FirstOrDefault` `.SingleOrDefault` `.OrderBy` `.GroupBy(` `.Sum(` `.ToList()` `.ToListAsync` y sintaxis `from … in`.

| Proyecto | Ocurrencias | Archivos | Nota |
|---|---:|---:|---|
| `SIAD.Services` | 2.555 | 78 | El grueso. Mezcla EF LINQ + Dapper ya existente |
| `SIAD.Reports` | 200 | 14 | Datasets y diseño de informes |
| `SIAD.Tests` | 113 | 23 | Aserciones sobre resultados |
| `apc` | 71 | 18 | Controllers y bootstrap |
| `apc.Client` | 38 | 11 | LINQ en memoria sobre DTOs en páginas Blazor |
| `SIAD.Core` | 21 | 8 | DTOs y constantes |
| `SIAD.Data` | 1 | 1 | Casi limpio |
| `apc.BancosWs` | 1 | 1 | Casi limpio |
| `apc.MobileApi` | 0 | 0 | **Limpio** |

Archivos más cargados (candidatos de última fase, no de primera):

```
SIAD.Services/Presupuesto/OrdenesPagoDirectoService.cs       227
SIAD.Services/CaptacionPagos/CaptacionPagosService.cs        141
SIAD.Services/Cobranza/CobranzaService.cs                    139
SIAD.Services/Presupuesto/ConfiguracionPresupuestoService.cs 136
SIAD.Services/Caja/AbonoService.cs                           118
SIAD.Services/Clientes/ClientesServices.cs                   115
SIAD.Services/Bancos/BanTransaccionesService.cs              107
SIAD.Services/Proveedores/ProveedoresService.cs               97
```

Recalcular el inventario:

```bash
for p in SIAD.Core SIAD.Data SIAD.Services SIAD.Reports apc apc.Client apc.BancosWs apc.MobileApi SIAD.Tests; do
  n=$(grep -rn "\.Where(\|\.Select(\|\.Any(\|\.FirstOrDefault\|\.SingleOrDefault\|\.OrderBy\|\.GroupBy(\|\.Sum(\|\.ToListAsync\|from [a-zA-Z_]* in " --include=*.cs "$p" 2>/dev/null | wc -l)
  echo "$p: $n"
done
```

## Regla de avance

1. **Código nuevo:** cero LINQ. Sin excepciones, sin "temporal".
2. **Método que tocas:** lo migras completo antes de entregarlo.
3. **Resto del archivo:** se deja. No se reescribe "de paso".
4. **Migración de un módulo entero:** solo cuando el usuario lo pida explícitamente.

Motivo de la regla 3: cada método migrado mueve la lógica a la BD y cambia el plan de ejecución. Migrar 227 usos de un servicio en un solo cambio no se puede verificar.

## Orden sugerido cuando el usuario pida migrar

De menor a mayor riesgo:

1. **`apc.Client`** (38) — LINQ en memoria sobre DTOs. Cambio mecánico a `foreach`, sin tocar BD, sin riesgo funcional.
2. **`SIAD.Core`** (21) y **`SIAD.Data`** (1) — igual de mecánico.
3. **`apc`** (71) — controllers; sacar lógica al servicio de paso.
4. **`SIAD.Reports`** (200) — los datasets ya son SQL; el paso es moverlos a vistas.
5. **Servicios de solo lectura** — listados y consultas. Se validan comparando el resultado antes/después.
6. **Servicios transaccionales** (`CaptacionPagos`, `Bancos`, `Caja`, `Presupuesto`) — al final. Tocan dinero, correlativos y contabilidad.
7. **`SIAD.Tests`** — se migra junto con el servicio que prueba, no antes.

## Procedimiento por método

1. Lee el método completo y anota qué devuelve exactamente (columnas, orden, filtros, nulos).
2. Escribe la vista o función en un script con fecha en `Database/`. Reproduce **el mismo orden y los mismos nulos** — un `ORDER BY` implícito de EF que se pierde rompe pantallas.
3. Skill `guardia-estructura-bd` si hay DDL destructivo; skill `runbook-despliegue-srv` para registrar el script.
4. El usuario aplica el script al mirror. Tú no te conectas a ninguna BD por iniciativa propia.
5. Reescribe el método con el patrón de [patrones-sp-dapper.md](patrones-sp-dapper.md).
6. `dotnet build HODSOFT_DEVEXPRESS.sln`.
7. Si hay test que lo cubre, córrelo con `SIAD_TEST_DB` apuntando al mirror. Si no lo hay y el método toca dinero o correlativos, escribe uno antes de migrar.
8. Compara el resultado viejo contra el nuevo con datos reales antes de dar por buena la migración.

## Trampas conocidas

| Trampa | Qué pasa |
|---|---|
| `company_id` | EF lo aplicaba solo. En SQL crudo hay que pasarlo y filtrar explícitamente, o se filtra data de otra empresa. |
| Orden implícito | EF suele traer un orden estable por PK; la vista sin `ORDER BY` no lo garantiza. Declara el orden. |
| `Include` / navegación | Un `Include` se convierte en `JOIN` dentro de la vista, o en dos llamadas y un `Dictionary` armado con `foreach`. |
| Nulos | `FirstOrDefault` devuelve `null`; `QuerySingleAsync` lanza. Usa `QueryFirstOrDefaultAsync` para conservar la semántica. |
| Tracking de EF | Si el método leía una entidad para modificarla y guardar, la migración cambia lectura **y** escritura. Van juntas. |
| SP con `COMMIT` interno | No se puede envolver en una transacción externa, y los tests que hacen `BEGIN … ROLLBACK` no lo cubren (ver `SIAD.Tests/README.md`). |
| `decimal` | Redondea en la BD con `MidpointRounding.AwayFromZero` equivalente (`round(x, 2)`), no en C#, o los totales dejan de cuadrar contra los reportes. |
