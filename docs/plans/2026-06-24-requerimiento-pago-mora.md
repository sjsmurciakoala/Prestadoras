# Requerimiento de pago en mora — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Reemplazar la generación de "cartas de cobro" en *Clientes para cobros* por un **Requerimiento de pago en mora** imprimible **idéntico a las imágenes compartidas**, con plazo seleccionable (24/48/72h) que define el número (#1/#2/#3), desglose de saldos fiel desde el ledger (recargos=0), tamaño carta con márgenes 0.5".

**Architecture:** Se reutiliza el flujo existente (`GenerarCartasCobroAsync` → persiste lote `cln_carta_cobro_hdr/dtl` → `ObtenerCartaLoteAsync` → `CobranzaController` render HTML imprimible). Se agrega `PlazoHoras` al request y columna `plazo_horas` al header; se extiende el DTO del cliente con `Medidor/Libreta/Secuencia/Identidad` (consulta a `cliente_maestro` + `maestro_medidor`); el render reproduce el formato de las capturas y calcula el desglose Mes Actual/Anteriores desde el `Detalle` (`Periodo`/`TipoServicio`/`SaldoDetalle`).

**Tech Stack:** .NET 9, EF Core + Dapper + Npgsql (PostgreSQL), Blazor WASM + DevExpress 25.1.7, xUnit (`BEGIN…ROLLBACK`).

**Diseño aprobado:** `docs/plans/2026-06-24-requerimiento-pago-mora-design.md`.

**Conexión mirror (tests/DDL, localhost):**
`Host=localhost;Port=5432;Database=siad_v3_restore;Username=postgres;Password=Koala@2021;Timeout=10;SslMode=Prefer`
psql: `C:\Program Files\PostgreSQL\15\bin\psql.exe`.

**Fuentes de datos del encabezado (verificadas en el mirror):**
- `cliente_maestro`: `maestro_cliente_clave`, `maestro_cliente_nombre`, `maestro_cliente_identidad`,
  `maestro_cliente_secuencia`, `maestro_cliente_indicativo_ruta` (= Libreta), `ciclos_id` (= Ciclo).
- Dirección: `cliente_detalle.detalle_cliente_direccion` (ya la trae el `Detalle`).
- Medidor: `maestro_medidor.maestro_medidor_numero` vía `cliente_detalle.maestro_medidor_id`.

---

## Task 1: Columna `plazo_horas` en `cln_carta_cobro_hdr`

**Files:**
- Create: `Prestadoras/Database/2026-06-24_add_plazo_horas_carta_cobro.sql`
- Modify: `Prestadoras/SIAD.Core/Entities/cln_carta_cobro_hdr.cs`

**Step 1: Script DDL**

```sql
-- Plazo (en horas) del requerimiento de pago en mora; conserva el número (#1/#2/#3) en reimpresiones.
ALTER TABLE cln_carta_cobro_hdr ADD COLUMN IF NOT EXISTS plazo_horas int;
```

**Step 2: Aplicar en el mirror**

Run: `$env:PGPASSWORD='Koala@2021'; & 'C:\Program Files\PostgreSQL\15\bin\psql.exe' -h localhost -U postgres -d siad_v3_restore -v ON_ERROR_STOP=1 -f Prestadoras/Database/2026-06-24_add_plazo_horas_carta_cobro.sql`
Expected: `ALTER TABLE`.

**Step 3: Propiedad en la entidad** — en `cln_carta_cobro_hdr.cs`, tras `fechacreacion`:

```csharp
    public int? plazo_horas { get; set; }
```

**Step 4:** `dotnet build Prestadoras/SIAD.Core/SIAD.Core.csproj` → 0 errores.

---

## Task 2: DTOs

**Files:**
- Modify: `Prestadoras/SIAD.Core/DTOs/Cobranza/ClientesCobroDtos.cs`

**Step 1:** Reemplazar las tres líneas de records por:

```csharp
public record GenerarCartasCobroRequest(IReadOnlyList<string> Claves, int PlazoHoras = 24);

public record CartaCobroHdrDto(int Id, string Correlativo, DateOnly FechaGeneracion, int TotalClientes, int? PlazoHoras = null);
```

**Step 2:** En el record `CartaCobroClienteDto`, agregar 4 campos opcionales al final:

```csharp
public record CartaCobroClienteDto(
    string Clave, string? Nombre, string? Direccion, int? CicloId, string? Ruta,
    decimal SaldoTotal, int? DiasMora, IReadOnlyList<CobranzaSaldoDetalleDto> Detalle,
    string? Medidor = null, string? Libreta = null, string? Secuencia = null, string? Identidad = null);
```

**Step 3:** `dotnet build Prestadoras/SIAD.Core/SIAD.Core.csproj` → 0 errores.

---

## Task 3: Servicio — persistir plazo + datos del encabezado (TDD)

**Files:**
- Modify: `Prestadoras/SIAD.Services/Cobranza/CobranzaService.cs`
- Modify: `Prestadoras/SIAD.Tests/ClientesCobroTests.cs`

**Step 1: Test que falla** (agregar en `ClientesCobroTests`, antes de `ObtenerClavesConDeudaAsync`):

```csharp
    [SkippableFact]
    public async Task GenerarRequerimiento_PersistePlazoHoras()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var claves = await ObtenerClavesConDeudaAsync(1);
        Skip.If(claves.Count == 0, "No hay clientes con deuda en la BD de prueba");

        var hdr = await _service!.GenerarCartasCobroAsync(
            new GenerarCartasCobroRequest(claves, PlazoHoras: 48), "tester");

        Assert.Equal(48, hdr.PlazoHoras);

        var lote = await _service.ObtenerCartaLoteAsync(hdr.Id);
        Assert.NotNull(lote);
        Assert.Equal(48, lote!.Encabezado.PlazoHoras);
    }
```

**Step 2: Verificar que falla**

Run: `$env:SIAD_TEST_DB='Host=localhost;Port=5432;Database=siad_v3_restore;Username=postgres;Password=Koala@2021;Timeout=10;SslMode=Prefer'; dotnet test Prestadoras/SIAD.Tests/SIAD.Tests.csproj --filter FullyQualifiedName~GenerarRequerimiento_PersistePlazoHoras --artifacts-path "$env:TEMP\req_b1"`
Expected: FAIL.

**Step 3: Persistir el plazo** — en `GenerarCartasCobroAsync`, dentro del `new cln_carta_cobro_hdr { … }` agregar `plazo_horas = request.PlazoHoras,` y cambiar el `return` por:

```csharp
        return new CartaCobroHdrDto(hdr.id, hdr.correlativo, hdr.fecha_generacion, hdr.total_clientes, hdr.plazo_horas);
```

En `ObtenerCartaLoteAsync`, cambiar el `Select` del header a:

```csharp
            .Select(h => new CartaCobroHdrDto(h.id, h.correlativo, h.fecha_generacion, h.total_clientes, h.plazo_horas))
```

**Step 4: Datos del encabezado** — agregar un helper Dapper y su row (junto a los demás helpers privados de la clase):

```csharp
    private sealed class DatosReqRow
    {
        public string? Identidad { get; init; }
        public string? Secuencia { get; init; }
        public string? Libreta { get; init; }
        public string? Medidor { get; init; }
    }

    private async Task<DatosReqRow?> ObtenerDatosRequerimientoAsync(string clave, long companyId, CancellationToken ct)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(ct);

        return await connection.QuerySingleOrDefaultAsync<DatosReqRow>(new CommandDefinition("""
            SELECT cm.maestro_cliente_identidad        AS Identidad,
                   cm.maestro_cliente_secuencia        AS Secuencia,
                   cm.maestro_cliente_indicativo_ruta  AS Libreta,
                   mm.maestro_medidor_numero           AS Medidor
            FROM cliente_maestro cm
            LEFT JOIN LATERAL (
                SELECT cd.maestro_medidor_id
                FROM cliente_detalle cd
                WHERE cd.maestro_cliente_id = cm.maestro_cliente_id
                  AND cd.maestro_medidor_id IS NOT NULL
                ORDER BY cd.detalle_cliente_id DESC
                LIMIT 1
            ) cd ON TRUE
            LEFT JOIN maestro_medidor mm
                ON mm.maestro_medidor_id = cd.maestro_medidor_id
               AND mm.company_id = cm.company_id
            WHERE cm.company_id = @CompanyId
              AND cm.maestro_cliente_clave = @Clave
            LIMIT 1
            """, new { CompanyId = companyId, Clave = clave }, cancellationToken: ct));
    }
```

En `ObtenerCartaLoteAsync`, dentro del `foreach (var d in dtls)`, antes del `clientes.Add(...)`, agregar:

```csharp
            var datos = await ObtenerDatosRequerimientoAsync(d.cliente_clave, companyId, ct);
```
y cambiar el `clientes.Add(new CartaCobroClienteDto(...))` para pasar los 4 campos:

```csharp
            clientes.Add(new CartaCobroClienteDto(
                d.cliente_clave,
                d.nombre_cliente ?? primero?.ClienteNombre,
                primero?.Direccion,
                primero?.CicloId,
                Ruta: null,
                SaldoTotal: d.saldo ?? detalle.Sum(x => x.SaldoDetalle),
                DiasMora: d.dias_mora,
                Detalle: detalle,
                Medidor: datos?.Medidor,
                Libreta: datos?.Libreta,
                Secuencia: datos?.Secuencia,
                Identidad: datos?.Identidad));
```

(`companyId` ya está declarado arriba en `ObtenerCartaLoteAsync`. `using Dapper;` y `using System.Data;` ya están en el archivo.)

**Step 5: Verificar que pasa**

Run: mismo comando del Step 2. Expected: PASS. Limpiar `$env:TEMP\req_b1`.

---

## Task 4: Render del requerimiento (formato exacto)

**Files:**
- Modify: `Prestadoras/apc/Controllers/CobranzaController.cs` (`RenderCartasHtml`)

Reemplazar **todo el método** `RenderCartasHtml` por:

```csharp
    private static string RenderCartasHtml(CartaCobroLoteDto lote)
    {
        var es  = System.Globalization.CultureInfo.GetCultureInfo("es-HN");
        Func<string?, string> enc = s => System.Net.WebUtility.HtmlEncode(s ?? string.Empty);
        Func<decimal, string> money = v => v.ToString("N2", es);
        var hoy = DateTime.Today;
        var emp = lote.Empresa;
        var empNombre = !string.IsNullOrWhiteSpace(emp.RazonSocial) ? emp.RazonSocial! : emp.NombreComercial;
        var empCorto  = emp.NombreComercial;
        var plazo = lote.Encabezado.PlazoHoras ?? 24;
        var numReq = plazo switch { 48 => 2, 72 => 3, _ => 1 };

        static int PeriodKey(string? p)
        {
            if (string.IsNullOrWhiteSpace(p)) return 0;
            var parts = p.Split('/');
            return (parts.Length == 2 && int.TryParse(parts[0], out var y) && int.TryParse(parts[1], out var m))
                ? y * 100 + m : 0;
        }

        var sb = new System.Text.StringBuilder();
        sb.Append("<!DOCTYPE html><html lang=\"es\"><head><meta charset=\"utf-8\"/>")
          .Append("<title>Requerimiento de pago en mora ").Append(enc(lote.Encabezado.Correlativo)).Append("</title>")
          .Append("<style>")
          .Append("@page{size:Letter;margin:0.5in}")
          .Append("body{font-family:'Times New Roman',serif;font-size:11pt;color:#000;margin:0}")
          .Append(".req{page-break-after:always}")
          .Append(".empresa{text-align:center;font-weight:bold;font-size:15pt}")
          .Append(".subt{text-align:center;font-weight:bold;font-size:11pt;margin-top:2px}")
          .Append(".rule{border:0;border-top:3px solid #000;margin:8px 0}")
          .Append(".datos{width:100%;font-size:10pt}.datos td{vertical-align:top;padding:1px 0}")
          .Append(".linea{font-size:10pt;margin-top:2px}")
          .Append("table.saldos{width:100%;border-collapse:collapse;font-size:9.5pt;margin:10px 0;text-align:center}")
          .Append("table.saldos th,table.saldos td{padding:3px 6px}")
          .Append(".num{text-align:right}.totmora{text-align:right;font-weight:bold;font-style:italic;margin:6px 0}")
          .Append(".legal{font-size:10pt;margin:5px 0}.slogan{text-align:center;margin:14px 0}")
          .Append("table.firmas{margin-left:auto;font-size:10pt;margin-top:8px}")
          .Append("table.firmas td{padding:3px 6px}.fl{text-align:right;font-style:italic;font-weight:bold;white-space:nowrap}")
          .Append(".fln{border-bottom:1px solid #000;width:230px}")
          .Append(".unidad{font-style:italic;font-weight:bold;font-size:10pt;border-top:1px solid #000;width:200px;padding-top:2px;margin-top:14px}")
          .Append(".obs{font-size:10pt;margin-top:10px}.obs .l{display:inline-block;border-bottom:1px solid #000;width:80%}")
          .Append(".no-print{padding:10px}@media print{.no-print{display:none}}")
          .Append("</style></head><body>");

        sb.Append("<div class=\"no-print\"><button onclick=\"window.print()\">Imprimir / Guardar PDF</button></div>");

        foreach (var c in lote.Clientes)
        {
            var movs = c.Detalle.Where(d => d.SaldoDetalle != 0).ToList();
            var maxPeriod = movs.Count > 0 ? movs.Max(d => PeriodKey(d.Periodo)) : 0;
            var grupos = movs
                .GroupBy(d => string.IsNullOrWhiteSpace(d.TipoServicio)
                    ? (string.IsNullOrWhiteSpace(d.Descripcion) ? "Servicio" : d.Descripcion)
                    : d.TipoServicio!)
                .Select(g => new
                {
                    Servicio = g.Key,
                    Actual   = g.Where(d => PeriodKey(d.Periodo) == maxPeriod).Sum(d => d.SaldoDetalle),
                    Anterior = g.Where(d => PeriodKey(d.Periodo) != maxPeriod).Sum(d => d.SaldoDetalle)
                })
                .OrderBy(x => x.Servicio)
                .ToList();

            sb.Append("<div class=\"req\">");

            sb.Append("<div class=\"empresa\">").Append(enc(empNombre)).Append("</div>");
            sb.Append("<div class=\"subt\">REQUERIMIENTO DE PAGO EN MORA&nbsp;&nbsp;#&nbsp;").Append(numReq).Append("</div>");
            sb.Append("<hr class=\"rule\"/>");

            sb.Append("<table class=\"datos\"><tr><td>")
              .Append("<div><b>Clave :</b> ").Append(enc(c.Clave)).Append("</div>")
              .Append("<div><b>Propietario :</b> ").Append(enc(c.Nombre)).Append("</div>")
              .Append("<div><b>Direccion :</b> ").Append(enc(c.Direccion)).Append("</div>")
              .Append("</td><td style=\"text-align:right\">")
              .Append("<div><b>Fecha Emisión</b> ").Append(hoy.ToString("dd/MM/yy", es)).Append("</div>")
              .Append("<div><b>No.Identidad :</b> ").Append(enc(c.Identidad)).Append("</div>")
              .Append("</td></tr></table>");
            sb.Append("<div class=\"linea\"><b>Medidor :</b> ").Append(enc(c.Medidor))
              .Append(" &nbsp;&nbsp; <b>Ciclo :</b> ").Append(enc(c.CicloId?.ToString()))
              .Append(" &nbsp;&nbsp; <b>Libreta :</b> ").Append(enc(c.Libreta))
              .Append(" &nbsp;&nbsp; <b>Secuencia :</b> ").Append(enc(c.Secuencia)).Append("</div>");

            sb.Append("<p>Estimado Usuario :</p>");
            sb.Append("<p>Hacemos de su conocimiento que a la fecha tiene una mora Pendiente de LPS.")
              .Append(money(c.SaldoTotal)).Append("</p>");

            sb.Append("<table class=\"saldos\"><thead>")
              .Append("<tr><th rowspan=\"2\" style=\"text-align:left\">DESCRIPCION</th>")
              .Append("<th colspan=\"2\">SALDOS ANTERIORES</th><th colspan=\"2\">SALDOS MES ACTUAL</th>")
              .Append("<th rowspan=\"2\">TOTAL</th></tr>")
              .Append("<tr><th>SALDOS</th><th>RECARGOS</th><th>SALDOS</th><th>RECARGOS</th></tr></thead><tbody>");
            foreach (var g in grupos)
                sb.Append("<tr><td style=\"text-align:left\">").Append(enc(g.Servicio)).Append("</td>")
                  .Append("<td class=\"num\">").Append(money(g.Anterior)).Append("</td><td class=\"num\">0.00</td>")
                  .Append("<td class=\"num\">").Append(money(g.Actual)).Append("</td><td class=\"num\">0.00</td>")
                  .Append("<td class=\"num\">").Append(money(g.Anterior + g.Actual)).Append("</td></tr>");
            sb.Append("</tbody></table>");

            sb.Append("<div class=\"totmora\">TOTAL mora: &nbsp;&nbsp; ").Append(money(c.SaldoTotal)).Append("</div>");

            sb.Append("<p class=\"legal\">Por antes expuesto se le brinda un plazo de <b>").Append(plazo)
              .Append(" HORAS</b> para realizar un plan de pago, lo cual debera presentarse a las oficinas de Atencion al Cliente; DEPARTAMENTO DE COBRANZAS.</p>");
            sb.Append("<p class=\"legal\">En caso contrario ").Append(enc(empCorto))
              .Append(", da por terminado el plazo que se le concedio y procede a la recuperacion total de su obligacion a traves de la via JUDICIAL, por medio de nuestro apoderado legal.</p>");
            sb.Append("<p class=\"legal\">Asi mismo hacemos de su conocimiento que al ser trasladado a esta instancia incurre en un cargo del 25% por concepto de honorarios de abogado por el valor en mora. Esperando una pronta respuesta a este llamado.</p>");

            sb.Append("<div class=\"slogan\">NO SE APRECIA EL VALOR DEL AGUA HASTA QUE SE SECA EL POZO</div>");

            sb.Append("<table class=\"firmas\">")
              .Append("<tr><td class=\"fl\">Recibida por:</td><td class=\"fln\"></td></tr>")
              .Append("<tr><td class=\"fl\">Identidad:</td><td class=\"fln\"></td></tr>")
              .Append("<tr><td class=\"fl\">Telefono:</td><td class=\"fln\"></td></tr>")
              .Append("<tr><td class=\"fl\">Fecha Recibido:</td><td class=\"fln\"></td></tr></table>");

            sb.Append("<div class=\"unidad\">Unidad de Cobranzas A.P.C.</div>");
            sb.Append("<div class=\"obs\"><b>Observacion:</b> <span class=\"l\"></span></div>");

            sb.Append("</div>");
        }

        sb.Append("</body></html>");
        return sb.ToString();
    }
```

**Step 2:** `dotnet build Prestadoras/apc/apc.csproj --artifacts-path "$env:TEMP\req_apc"` → 0 errores; limpiar la carpeta.

> Verificación de datos: en el mirror, confirmar contra un cliente real que `TipoServicio`
> trae el nombre del servicio (si viene vacío, el fallback usa `Descripcion`).

---

## Task 5: UI — popup de plazo y rename del botón

**Files:**
- Modify: `Prestadoras/apc.Client/Pages/Facturacion/Cobranza/ClientesParaCobros.razor`

**Step 1:** Botón "Generar cartas" → abre popup:

```razor
            <DxButton Text="Generar requerimiento"
                      IconCssClass="bi bi-envelope-paper"
                      RenderStyle="ButtonRenderStyle.Secondary"
                      Click="AbrirRequerimiento"
                      Enabled="@(SeleccionadosClaves.Count > 0 && !isGenerandoCartas)" />
```

**Step 2:** Popup (después del popup de acción en lote):

```razor
<DxPopup @bind-Visible="mostrarRequerimiento"
         HeaderText="Generar requerimiento de pago en mora"
         Width="min(420px, 95vw)" ShowFooter="true" CloseOnOutsideClick="false">
    <BodyTemplate>
        <p class="text-muted small mb-3">
            Se generará el requerimiento para <strong>@SeleccionadosClaves.Count</strong> cliente(s).
        </p>
        <DxFormLayout CaptionPosition="CaptionPosition.Vertical">
            <DxFormLayoutItem Caption="Plazo" ColSpanXs="12" Context="p1">
                <DxComboBox Data="plazosRequerimiento" @bind-Value="plazoHoras"
                            ValueFieldName="Horas" TextFieldName="Texto" CssClass="w-100" />
            </DxFormLayoutItem>
        </DxFormLayout>
    </BodyTemplate>
    <FooterTemplate>
        <div class="d-flex gap-2 justify-content-end">
            <DxButton Text="Cancelar" RenderStyle="ButtonRenderStyle.Secondary" Click="() => mostrarRequerimiento = false" />
            <DxButton Text="Generar" RenderStyle="ButtonRenderStyle.Primary" Click="GenerarCartasAsync" Enabled="@(!isGenerandoCartas)" />
        </div>
    </FooterTemplate>
</DxPopup>
```

**Step 3:** Estado en `@code`:

```csharp
    private bool mostrarRequerimiento;
    private int plazoHoras = 24;
    private readonly List<PlazoOption> plazosRequerimiento =
    [
        new(24, "24 horas (1.er requerimiento)"),
        new(48, "48 horas (2.º requerimiento)"),
        new(72, "72 horas (3.er requerimiento)")
    ];
    private record PlazoOption(int Horas, string Texto);

    private void AbrirRequerimiento()
    {
        plazoHoras = 24;
        mostrarRequerimiento = true;
    }
```

**Step 4:** En `GenerarCartasAsync`, cerrar el popup al inicio y pasar el plazo:

```csharp
        mostrarRequerimiento = false;
        ...
            var hdr = await ClientesCobroClient.GenerarCartasAsync(
                new GenerarCartasCobroRequest(claves, plazoHoras));
```
(El resto —abrir el HTML en pestaña nueva— igual.)

**Step 5:** `dotnet build Prestadoras/apc.Client/apc.Client.csproj --artifacts-path "$env:TEMP\req_cli"` → 0 errores; limpiar.

---

## Task 6: Build de solución + verificación

**Step 1:** `dotnet build Prestadoras/HODSOFT_DEVEXPRESS.sln` → 0 errores (app detenida o `--artifacts-path` aislado).

**Step 2:** Tests:
`$env:SIAD_TEST_DB='Host=localhost;Port=5432;Database=siad_v3_restore;Username=postgres;Password=Koala@2021;Timeout=10;SslMode=Prefer'; dotnet test Prestadoras/SIAD.Tests/SIAD.Tests.csproj --filter FullyQualifiedName~ClientesCobroTests`
Expected: verde. Aplicar `verification-before-completion`.

**Step 3:** Verificación manual (skill `verify`): en *Clientes para cobros*, seleccionar 1+ clientes → "Generar requerimiento" → 48h → Generar → confirmar contra las imágenes: empresa centrada, "# 2", datos (Clave/Propietario/Dirección/Medidor/Ciclo/Libreta/Secuencia/Identidad), tabla SALDOS ANTERIORES/MES ACTUAL con Recargos 0.00, TOTAL mora, 3 párrafos legales con "48 HORAS", slogan, firmas, "Unidad de Cobranzas A.P.C." y "Observacion:". Imprimir → márgenes 0.5" carta. Verificar Libreta/Ciclo contra un cliente real.

---

## Resumen de archivos

| Acción | Archivo |
|--------|---------|
| Create | `Database/2026-06-24_add_plazo_horas_carta_cobro.sql` |
| Modify | `SIAD.Core/Entities/cln_carta_cobro_hdr.cs` |
| Modify | `SIAD.Core/DTOs/Cobranza/ClientesCobroDtos.cs` |
| Modify | `SIAD.Services/Cobranza/CobranzaService.cs` |
| Modify | `SIAD.Tests/ClientesCobroTests.cs` |
| Modify | `apc/Controllers/CobranzaController.cs` |
| Modify | `apc.Client/Pages/Facturacion/Cobranza/ClientesParaCobros.razor` |

**Pendiente despliegue:** replicar el DDL en SRV `siad_v3` (regla mirror).
**Asunciones a verificar en UI:** `Libreta ← indicativo_ruta`, `Ciclo ← ciclos_id`.
