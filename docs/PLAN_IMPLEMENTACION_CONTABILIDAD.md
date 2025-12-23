# PLAN DE IMPLEMENTACIÓN TÉCNICO: TABLAS CONTABILIDAD FALTANTES

**Objetivo**: Guía paso a paso para implementar la estructura completa de Contabilidad  
**Arquitectura**: .NET 9 + Blazor WebAssembly + EF Core + Postgres  
**Patrón**: Multiempresa, Auditoría, Validaciones

---

## FASE 1: CREAR MIGRATIONS EF CORE

### Paso 1.1: Nueva Migration para Catálogos Base

```powershell
# Terminal en raíz de solución
dotnet ef migrations add "Contabilidad_01_CatalogosBase" `
  -p SIAD.Data/SIAD.Data.csproj `
  -s apc/apc.csproj `
  -o Migrations/Contabilidad
```

### Paso 1.2: Nueva Migration para Transacciones Core

```powershell
dotnet ef migrations add "Contabilidad_02_Transacciones" `
  -p SIAD.Data/SIAD.Data.csproj `
  -s apc/apc.csproj `
  -o Migrations/Contabilidad
```

### Paso 1.3: Nueva Migration para Terceros

```powershell
dotnet ef migrations add "Contabilidad_03_Terceros" `
  -p SIAD.Data/SIAD.Data.csproj `
  -s apc/apc.csproj `
  -o Migrations/Contabilidad
```

### Paso 1.4: Nueva Migration para Activos Fijos

```powershell
dotnet ef migrations add "Contabilidad_04_ActivosFijos" `
  -p SIAD.Data/SIAD.Data.csproj `
  -s apc/apc.csproj `
  -o Migrations/Contabilidad
```

### Paso 1.5: Nueva Migration para Reportes IVA

```powershell
dotnet ef migrations add "Contabilidad_05_LibroIva" `
  -p SIAD.Data/SIAD.Data.csproj `
  -s apc/apc.csproj `
  -o Migrations/Contabilidad
```

---

## FASE 2: CONFIGURAR MODELOS EN DbContext

### Paso 2.1: Editar `SIAD.Data/SiadDbContext.cs`

Agregar los DbSets:

```csharp
// Contabilidad - Transacciones
public DbSet<con_tipo_transaccion> con_tipo_transacciones { get; set; }
public DbSet<con_poliza> con_polizas { get; set; }
public DbSet<con_poliza_linea> con_poliza_lineas { get; set; }

// Contabilidad - Saldos
public DbSet<con_apertura_saldo> con_apertura_saldos { get; set; }
public DbSet<con_saldo_cuenta> con_saldo_cuentas { get; set; }
public DbSet<con_balance_mensual> con_balance_mensuales { get; set; }

// Contabilidad - Terceros
public DbSet<con_tercero> con_terceros { get; set; }

// Contabilidad - Reportes Fiscales
public DbSet<con_libro_iva> con_libro_ivas { get; set; }

// Contabilidad - Activos Fijos
public DbSet<con_activo_tipo> con_activo_tipos { get; set; }
public DbSet<con_activo_fijo> con_activo_fijos { get; set; }
public DbSet<con_deprecacion> con_depreciaciones { get; set; }
```

### Paso 2.2: Crear Configuraciones en `SIAD.Data/Configurations/`

Archivo: `ContabilidadTipoTransaccionConfiguration.cs`
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIAD.Core.Entities;

namespace SIAD.Data.Configurations;

public class ContabilidadTipoTransaccionConfiguration : IEntityTypeConfiguration<con_tipo_transaccion>
{
    public void Configure(EntityTypeBuilder<con_tipo_transaccion> builder)
    {
        builder.ToTable("con_tipo_transaccion", "public");
        
        builder.HasKey(x => x.type_id);
        
        builder.Property(x => x.type_id)
            .UseIdentityByDefaultColumn();
        
        builder.Property(x => x.code)
            .IsRequired()
            .HasMaxLength(20);
        
        builder.Property(x => x.name)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(x => x.category)
            .IsRequired()
            .HasMaxLength(30);
        
        builder.HasIndex(x => new { x.company_id, x.code })
            .IsUnique();
        
        builder.HasIndex(x => x.company_id);
    }
}
```

**Similar para las otras entidades...**

---

## FASE 3: SCAFFOLD EF ENTITIES

Una vez aplicadas las migrations en DB:

```powershell
dotnet ef dbcontext scaffold "Host=localhost;Database=siad_db;Username=postgres;Password=password" `
  Npgsql.EntityFrameworkCore.PostgreSQL `
  -p SIAD.Data/SIAD.Data.csproj `
  -s apc/apc.csproj `
  -c SiadDbContext `
  --namespace SIAD.Core.Entities `
  --output-dir Entities `
  --force
```

---

## FASE 4: CREAR DTOs (SIAD.Core/DTOs/Contabilidad/)

### Archivo: `TipoTransaccionDto.cs`

```csharp
namespace SIAD.Core.DTOs.Contabilidad;

public sealed record TipoTransaccionDto(
    long TipoTransaccionId,
    long CompanyId,
    string Code,
    string Name,
    string? Description,
    string Category,
    bool IsAutomatic,
    bool AllowsCostCenter,
    bool AllowsThirdParty,
    string Status,
    DateTime CreatedAt,
    string CreatedBy
);

public sealed record TipoTransaccionUpsertDto(
    string Code,
    string Name,
    string? Description,
    string Category,
    bool IsAutomatic = false,
    bool AllowsCostCenter = false,
    bool AllowsThirdParty = false
);
```

### Archivo: `PolizaDto.cs`

```csharp
namespace SIAD.Core.DTOs.Contabilidad;

public sealed record PolizaDto(
    long VoucherId,
    long CompanyId,
    long PeriodId,
    long JournalId,
    long TypeId,
    string VoucherNumber,
    DateTime VoucherDate,
    string Description,
    string? DocumentRef,
    decimal TotalDebit,
    decimal TotalCredit,
    string Status,
    bool IsBalanced,
    string? Notes,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? PostedAt,
    string? PostedBy
);

public sealed record PolizaCrearDto(
    long JournalId,
    long TypeId,
    DateTime VoucherDate,
    string Description,
    string? DocumentRef,
    string? Notes,
    List<PolizaLineaCrearDto> Lineas
);

public sealed record PolizaLineaDto(
    long LineId,
    long VoucherId,
    int LineNumber,
    long AccountId,
    long? CostCenterId,
    long? ThirdPartyId,
    decimal DebitAmount,
    decimal CreditAmount,
    string? CurrencyCode,
    decimal ExchangeRate,
    string? Description,
    string? Reference
);

public sealed record PolizaLineaCrearDto(
    int LineNumber,
    long AccountId,
    long? CostCenterId,
    long? ThirdPartyId,
    decimal DebitAmount,
    decimal CreditAmount,
    string? CurrencyCode = "HNL",
    decimal ExchangeRate = 1.0m,
    string? Description = null,
    string? Reference = null
);
```

### Archivo: `AperturaSaldoDto.cs`

```csharp
namespace SIAD.Core.DTOs.Contabilidad;

public sealed record AperturaSaldoDto(
    long OpeningId,
    long CompanyId,
    long PeriodId,
    long AccountId,
    long? CostCenterId,
    decimal DebitAmount,
    decimal CreditAmount,
    string? CurrencyCode,
    decimal ExchangeRate,
    string? Notes,
    DateTime CreatedAt
);

public sealed record AperturaSaldoUpsertDto(
    long AccountId,
    long? CostCenterId,
    decimal DebitAmount = 0,
    decimal CreditAmount = 0,
    string? CurrencyCode = "HNL",
    decimal ExchangeRate = 1.0m,
    string? Notes = null
);
```

### Archivo: `SaldoCuentaDto.cs`

```csharp
namespace SIAD.Core.DTOs.Contabilidad;

public sealed record SaldoCuentaDto(
    long BalanceId,
    long CompanyId,
    long PeriodId,
    long AccountId,
    long? CostCenterId,
    decimal BeginningDebit,
    decimal BeginningCredit,
    decimal PeriodDebit,
    decimal PeriodCredit,
    decimal EndingDebit,
    decimal EndingCredit,
    DateTime LastUpdated
);
```

---

## FASE 5: CREAR SERVICIOS (SIAD.Services/Contabilidad/)

### Archivo: `IPolizaService.cs`

```csharp
using SIAD.Core.DTOs.Contabilidad;

namespace SIAD.Services.Contabilidad;

public interface IPolizaService
{
    Task<PolizaDto> ObtenerAsync(long voucherId, CancellationToken ct = default);
    Task<List<PolizaDto>> ListarPorPeriodoAsync(long companyId, long periodId, CancellationToken ct = default);
    Task<long> CrearAsync(long companyId, PolizaCrearDto dto, string userId, CancellationToken ct = default);
    Task ActualizarAsync(long voucherId, PolizaCrearDto dto, string userId, CancellationToken ct = default);
    Task EliminarAsync(long voucherId, CancellationToken ct = default);
    Task RegistrarAsync(long voucherId, string userId, CancellationToken ct = default);
    Task ReversarAsync(long voucherId, string userId, CancellationToken ct = default);
    Task<bool> ValidarBalanceAsync(long voucherId, CancellationToken ct = default);
}
```

### Archivo: `PolizaService.cs`

```csharp
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Data;
using SIAD.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace SIAD.Services.Contabilidad;

public sealed class PolizaService : IPolizaService
{
    private readonly SiadDbContext _context;

    public PolizaService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<PolizaDto> ObtenerAsync(long voucherId, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(voucherId);

        var poliza = await _context.con_polizas
            .Where(x => x.voucher_id == voucherId)
            .Select(MapearDto)
            .FirstOrDefaultAsync(ct);

        if (poliza is null)
            throw new InvalidOperationException($"Póliza {voucherId} no encontrada.");

        return poliza;
    }

    public async Task<List<PolizaDto>> ListarPorPeriodoAsync(long companyId, long periodId, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(periodId);

        return await _context.con_polizas
            .Where(x => x.company_id == companyId && x.period_id == periodId)
            .OrderByDescending(x => x.voucher_date)
            .ThenByDescending(x => x.voucher_id)
            .Select(MapearDto)
            .ToListAsync(ct);
    }

    public async Task<long> CrearAsync(long companyId, PolizaCrearDto dto, string userId, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(userId);

        // Validar existencia de período y diario
        var periodo = await _context.con_periodo_contables
            .Where(x => x.period_id == dto.JournalId) // TODO: Verificar que sea JournalId
            .FirstOrDefaultAsync(ct);

        if (periodo is null)
            throw new InvalidOperationException("Período contable no encontrado.");

        if (periodo.status != "OPEN")
            throw new InvalidOperationException("El período contable no está abierto.");

        // Generar número de póliza
        var lastVoucher = await _context.con_polizas
            .Where(x => x.company_id == companyId && x.journal_id == dto.JournalId)
            .OrderByDescending(x => x.voucher_id)
            .FirstOrDefaultAsync(ct);

        var voucherNumber = (long.Parse(lastVoucher?.voucher_number ?? "0") + 1).ToString();

        // Crear póliza
        var poliza = new con_poliza
        {
            company_id = companyId,
            period_id = periodo.period_id,
            journal_id = dto.JournalId,
            type_id = dto.TypeId,
            voucher_number = voucherNumber,
            voucher_date = dto.VoucherDate,
            description = dto.Description,
            document_ref = dto.DocumentRef,
            total_debit = dto.Lineas.Sum(x => x.DebitAmount),
            total_credit = dto.Lineas.Sum(x => x.CreditAmount),
            status = "DRAFT",
            notes = dto.Notes,
            created_at = DateTime.UtcNow,
            created_by = userId,
            is_balanced = Math.Abs(dto.Lineas.Sum(x => x.DebitAmount) - dto.Lineas.Sum(x => x.CreditAmount)) < 0.01m
        };

        // Agregar líneas
        foreach (var lineaDto in dto.Lineas)
        {
            var linea = new con_poliza_linea
            {
                line_number = lineaDto.LineNumber,
                account_id = lineaDto.AccountId,
                cost_center_id = lineaDto.CostCenterId,
                third_party_id = lineaDto.ThirdPartyId,
                debit_amount = lineaDto.DebitAmount,
                credit_amount = lineaDto.CreditAmount,
                currency_code = lineaDto.CurrencyCode ?? "HNL",
                exchange_rate = lineaDto.ExchangeRate,
                description = lineaDto.Description,
                reference = lineaDto.Reference,
                created_at = DateTime.UtcNow,
                created_by = userId
            };

            poliza.con_poliza_lineas.Add(linea);
        }

        _context.con_polizas.Add(poliza);
        await _context.SaveChangesAsync(ct);

        return poliza.voucher_id;
    }

    public async Task ActualizarAsync(long voucherId, PolizaCrearDto dto, string userId, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(voucherId);
        ArgumentNullException.ThrowIfNull(dto);

        var poliza = await _context.con_polizas
            .Include(x => x.con_poliza_lineas)
            .FirstOrDefaultAsync(x => x.voucher_id == voucherId, ct);

        if (poliza is null)
            throw new InvalidOperationException($"Póliza {voucherId} no encontrada.");

        if (poliza.status == "POSTED")
            throw new InvalidOperationException("No se puede editar una póliza registrada.");

        poliza.description = dto.Description;
        poliza.document_ref = dto.DocumentRef;
        poliza.notes = dto.Notes;
        poliza.updated_at = DateTime.UtcNow;
        poliza.updated_by = userId;

        // Eliminar líneas existentes y agregar nuevas
        _context.con_poliza_lineas.RemoveRange(poliza.con_poliza_lineas);

        foreach (var lineaDto in dto.Lineas)
        {
            poliza.con_poliza_lineas.Add(new con_poliza_linea
            {
                line_number = lineaDto.LineNumber,
                account_id = lineaDto.AccountId,
                cost_center_id = lineaDto.CostCenterId,
                third_party_id = lineaDto.ThirdPartyId,
                debit_amount = lineaDto.DebitAmount,
                credit_amount = lineaDto.CreditAmount,
                currency_code = lineaDto.CurrencyCode ?? "HNL",
                exchange_rate = lineaDto.ExchangeRate,
                description = lineaDto.Description,
                reference = lineaDto.Reference,
                created_at = DateTime.UtcNow,
                created_by = userId
            });
        }

        poliza.total_debit = dto.Lineas.Sum(x => x.DebitAmount);
        poliza.total_credit = dto.Lineas.Sum(x => x.CreditAmount);
        poliza.is_balanced = Math.Abs(poliza.total_debit - poliza.total_credit) < 0.01m;

        await _context.SaveChangesAsync(ct);
    }

    public async Task EliminarAsync(long voucherId, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(voucherId);

        var poliza = await _context.con_polizas.FirstOrDefaultAsync(x => x.voucher_id == voucherId, ct);

        if (poliza is null)
            throw new InvalidOperationException($"Póliza {voucherId} no encontrada.");

        if (poliza.status == "POSTED")
            throw new InvalidOperationException("No se puede eliminar una póliza registrada.");

        _context.con_polizas.Remove(poliza);
        await _context.SaveChangesAsync(ct);
    }

    public async Task RegistrarAsync(long voucherId, string userId, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(voucherId);
        ArgumentNullException.ThrowIfNull(userId);

        var poliza = await _context.con_polizas.FirstOrDefaultAsync(x => x.voucher_id == voucherId, ct);

        if (poliza is null)
            throw new InvalidOperationException($"Póliza {voucherId} no encontrada.");

        if (!poliza.is_balanced)
            throw new InvalidOperationException("La póliza debe estar balanceada para registrarse.");

        if (poliza.status == "POSTED")
            throw new InvalidOperationException("La póliza ya fue registrada.");

        poliza.status = "POSTED";
        poliza.posted_at = DateTime.UtcNow;
        poliza.posted_by = userId;

        await _context.SaveChangesAsync(ct);
    }

    public async Task ReversarAsync(long voucherId, string userId, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(voucherId);
        ArgumentNullException.ThrowIfNull(userId);

        var poliza = await _context.con_polizas.FirstOrDefaultAsync(x => x.voucher_id == voucherId, ct);

        if (poliza is null)
            throw new InvalidOperationException($"Póliza {voucherId} no encontrada.");

        if (poliza.status != "POSTED")
            throw new InvalidOperationException("Solo se pueden reversar pólizas registradas.");

        poliza.status = "REVERSED";
        poliza.updated_at = DateTime.UtcNow;
        poliza.updated_by = userId;

        await _context.SaveChangesAsync(ct);
    }

    public async Task<bool> ValidarBalanceAsync(long voucherId, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(voucherId);

        var poliza = await _context.con_polizas
            .FirstOrDefaultAsync(x => x.voucher_id == voucherId, ct);

        if (poliza is null)
            return false;

        return poliza.is_balanced;
    }

    private static PolizaDto MapearDto(con_poliza p) => new(
        p.voucher_id,
        p.company_id,
        p.period_id,
        p.journal_id,
        p.type_id,
        p.voucher_number,
        p.voucher_date,
        p.description,
        p.document_ref,
        p.total_debit,
        p.total_credit,
        p.status,
        p.is_balanced,
        p.notes,
        p.created_at,
        p.created_by,
        p.posted_at,
        p.posted_by
    );
}
```

---

## FASE 6: CREAR CONTROLADORES API

### Archivo: `apc/Controllers/Contabilidad/PolizasController.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Services.Contabilidad;

namespace apc.Controllers.Contabilidad;

[ApiController]
[Route("api/contabilidad/polizas")]
[Authorize(Policy = "Contabilidad")]
public sealed class PolizasController : ControllerBase
{
    private readonly IPolizaService _polizaService;

    public PolizasController(IPolizaService polizaService)
    {
        _polizaService = polizaService;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PolizaDto>> Obtener(long id, CancellationToken ct = default)
    {
        try
        {
            var poliza = await _polizaService.ObtenerAsync(id, ct);
            return Ok(poliza);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("periodo/{periodId}")]
    public async Task<ActionResult<List<PolizaDto>>> ListarPorPeriodo(long periodId, CancellationToken ct = default)
    {
        var companyId = 1; // TODO: Obtener del usuario actual

        var polizas = await _polizaService.ListarPorPeriodoAsync(companyId, periodId, ct);
        return Ok(polizas);
    }

    [HttpPost]
    public async Task<ActionResult<long>> Crear(PolizaCrearDto dto, CancellationToken ct = default)
    {
        try
        {
            var companyId = 1; // TODO: Obtener del usuario actual
            var userId = User.Identity?.Name ?? "sistema";

            var voucherId = await _polizaService.CrearAsync(companyId, dto, userId, ct);
            return CreatedAtAction(nameof(Obtener), new { id = voucherId }, voucherId);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Actualizar(long id, PolizaCrearDto dto, CancellationToken ct = default)
    {
        try
        {
            var userId = User.Identity?.Name ?? "sistema";
            await _polizaService.ActualizarAsync(id, dto, userId, ct);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Eliminar(long id, CancellationToken ct = default)
    {
        try
        {
            await _polizaService.EliminarAsync(id, ct);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id}/registrar")]
    public async Task<IActionResult> Registrar(long id, CancellationToken ct = default)
    {
        try
        {
            var userId = User.Identity?.Name ?? "sistema";
            await _polizaService.RegistrarAsync(id, userId, ct);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
```

---

## FASE 7: CREAR CLIENTS HTTP (apc.Client)

### Archivo: `apc.Client/Services/Contabilidad/PolizasClient.cs`

```csharp
using SIAD.Core.DTOs.Contabilidad;

namespace apc.Client.Services.Contabilidad;

public sealed class PolizasClient
{
    private readonly HttpClient _http;

    public PolizasClient(HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
    }

    public async Task<PolizaDto> ObtenerAsync(long id, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        var response = await _http.GetAsync($"api/contabilidad/polizas/{id}", cancellationToken: ct);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Error al obtener póliza: {response.StatusCode}");

        return await response.Content.ReadAsAsync<PolizaDto>(cancellationToken: ct);
    }

    public async Task<List<PolizaDto>> ListarPorPeriodoAsync(long periodId, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(periodId);

        var response = await _http.GetAsync($"api/contabilidad/polizas/periodo/{periodId}", cancellationToken: ct);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Error al listar pólizas: {response.StatusCode}");

        return await response.Content.ReadAsAsync<List<PolizaDto>>(cancellationToken: ct) ?? new();
    }

    public async Task<long> CrearAsync(PolizaCrearDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var response = await _http.PostAsJsonAsync("api/contabilidad/polizas", dto, cancellationToken: ct);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Error al crear póliza: {response.StatusCode}");

        return await response.Content.ReadAsAsync<long>(cancellationToken: ct);
    }

    public async Task ActualizarAsync(long id, PolizaCrearDto dto, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        ArgumentNullException.ThrowIfNull(dto);

        var response = await _http.PutAsJsonAsync($"api/contabilidad/polizas/{id}", dto, cancellationToken: ct);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Error al actualizar póliza: {response.StatusCode}");
    }

    public async Task EliminarAsync(long id, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        var response = await _http.DeleteAsync($"api/contabilidad/polizas/{id}", cancellationToken: ct);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Error al eliminar póliza: {response.StatusCode}");
    }

    public async Task RegistrarAsync(long id, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        var response = await _http.PostAsJsonAsync($"api/contabilidad/polizas/{id}/registrar", null, cancellationToken: ct);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Error al registrar póliza: {response.StatusCode}");
    }
}
```

---

## FASE 8: REGISTRAR EN DEPENDENCY INJECTION

### Archivo: `SIAD.Services/ServiceRegistration.cs`

Agregar en el método `AddSiadServices`:

```csharp
services.AddScoped<IPolizaService, PolizaService>();
services.AddScoped<ITipoTransaccionService, TipoTransaccionService>();
services.AddScoped<IAperturaService, AperturaService>();
services.AddScoped<ISaldosService, SaldosService>();
// ... más servicios
```

### Archivo: `apc.Client/CommonServices.cs`

Agregar en el método `AddCommonServices`:

```csharp
services.AddHttpClient<PolizasClient>(client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));
services.AddHttpClient<TiposTransaccionClient>(client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));
// ... más clients
```

---

## FASE 9: CREAR COMPONENTES BLAZOR

### Archivo: `apc.Client/Pages/Contabilidad/PolizasIndex.razor`

```razor
@page "/contabilidad/polizas"
@using apc.Client.Services.Contabilidad
@using SIAD.Core.DTOs.Contabilidad
@inject PolizasClient polizasClient
@inject PeriodosContablesClient periodosClient

<div class="card">
    <div class="card-header">
        <h4>Pólizas Contables</h4>
        <button class="btn btn-primary" @onclick="() => modalCrear = true">Nueva Póliza</button>
    </div>
    <div class="card-body">
        <div class="row mb-3">
            <div class="col-md-4">
                <label>Período:</label>
                <select class="form-select" @onchange="CambiarPeriodo">
                    <option value="">-- Seleccione --</option>
                    @foreach (var p in periodos)
                    {
                        <option value="@p.PeriodoId">@p.Code - @p.Name</option>
                    }
                </select>
            </div>
        </div>

        @if (cargando)
        {
            <div>Cargando...</div>
        }
        else if (polizas.Any())
        {
            <table class="table table-sm">
                <thead>
                    <tr>
                        <th>Número</th>
                        <th>Fecha</th>
                        <th>Descripción</th>
                        <th>Débito</th>
                        <th>Crédito</th>
                        <th>Estado</th>
                        <th>Acciones</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach (var p in polizas)
                    {
                        <tr>
                            <td>@p.VoucherNumber</td>
                            <td>@p.VoucherDate.ToShortDateString()</td>
                            <td>@p.Description</td>
                            <td>@p.TotalDebit</td>
                            <td>@p.TotalCredit</td>
                            <td>
                                <span class="badge bg-@GetStatusColor(p.Status)">@p.Status</span>
                            </td>
                            <td>
                                <button class="btn btn-sm btn-info" @onclick="() => EditarPoliza(p.VoucherId)">Editar</button>
                                <button class="btn btn-sm btn-danger" @onclick="() => EliminarPoliza(p.VoucherId)">Eliminar</button>
                            </td>
                        </tr>
                    }
                </tbody>
            </table>
        }
        else
        {
            <div class="alert alert-info">No hay pólizas registradas.</div>
        }
    </div>
</div>

@code {
    private List<PeriodoContableDto> periodos = new();
    private List<PolizaDto> polizas = new();
    private bool cargando = false;
    private bool modalCrear = false;
    private long periodSeleccionado = 0;

    protected override async Task OnInitializedAsync()
    {
        periodos = await periodosClient.ListarAsync();
    }

    private async Task CambiarPeriodo(ChangeEventArgs e)
    {
        if (long.TryParse(e.Value?.ToString(), out var periodId))
        {
            periodSeleccionado = periodId;
            await CargarPolizas();
        }
    }

    private async Task CargarPolizas()
    {
        if (periodSeleccionado <= 0)
        {
            polizas.Clear();
            return;
        }

        cargando = true;
        try
        {
            polizas = await polizasClient.ListarPorPeriodoAsync(periodSeleccionado);
        }
        finally
        {
            cargando = false;
        }
    }

    private string GetStatusColor(string status) => status switch
    {
        "DRAFT" => "secondary",
        "POSTED" => "success",
        "REVERSED" => "danger",
        _ => "light"
    };

    private async Task EditarPoliza(long id)
    {
        // Navegar a formulario de edición
    }

    private async Task EliminarPoliza(long id)
    {
        if (await JSRuntime.InvokeAsync<bool>("confirm", "¿Desea eliminar esta póliza?"))
        {
            await polizasClient.EliminarAsync(id);
            await CargarPolizas();
        }
    }
}
```

---

## FASE 10: SEED DATA

### Archivo: `Database/Seeds_v2/05_seed_contabilidad_tipos_transaccion.sql`

```sql
-- Insertar tipos de transacción
INSERT INTO public.con_tipo_transaccion (company_id, code, name, category, is_automatic, allows_cost_center, allows_third_party, status)
SELECT 
    c.company_id,
    vals.code,
    vals.name,
    vals.category,
    vals.is_automatic,
    vals.allows_cost_center,
    vals.allows_third_party,
    'ACTIVE'
FROM public.cfg_company c
CROSS JOIN (
    VALUES
        ('DIARIO', 'Diario General', 'DIARIO', false, true, true),
        ('AJUSTE', 'Ajuste', 'AJUSTE', false, true, false),
        ('CIERRE', 'Cierre de Período', 'CIERRE', true, false, false),
        ('APERTURA', 'Apertura de Período', 'APERTURA', true, false, false)
) AS vals(code, name, category, is_automatic, allows_cost_center, allows_third_party)
ON CONFLICT (company_id, code) DO NOTHING;
```

---

## 📝 CHECKLIST DE VALIDACIÓN ANTES DE GO-LIVE

- [ ] Todas las migrations aplicadas correctamente
- [ ] Todas las entidades creadas en SIAD.Core/Entities
- [ ] Todos los DTOs creados y mapeados
- [ ] Todos los servicios implementados y testeados
- [ ] Todos los controladores con validaciones
- [ ] Todos los clientes HTTP creados
- [ ] DI registrado correctamente
- [ ] Componentes Blazor funcionales
- [ ] Seed data cargado
- [ ] Permisos/Policies configurados
- [ ] Tests unitarios pasando
- [ ] Testing en Postgres real completado

---

**Estado**: LISTO PARA IMPLEMENTACIÓN  
**Complejidad**: MEDIA-ALTA  
**Tiempo estimado**: 2-3 semanas de desarrollo  
**Riesgo**: BAJO (siguiendo patrones existentes)
