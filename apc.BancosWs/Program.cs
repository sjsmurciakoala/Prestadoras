using apc.BancosWs.Infrastructure;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.BancosWs;

// apc.BancosWs — WS bancario SIMAFI migrado (plan 2026-07-02 F8).
// Contrato CONGELADO: docs/f8-contrato-ws-bancario.md (D8). Host propio,
// separado del portal apc: deploy independiente, canal 24/7 del banco no se
// recicla con cada publish del portal, cutover/rollback limpios.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<SiadDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Tenant del canal: lo resuelve la credencial banco+key de cada request
// (ban_ws_credencial) — no hay Identity ni claims en este host.
builder.Services.AddScoped<BancosWsRequestContext>();
builder.Services.AddScoped<ICurrentCompanyService, BancosWsCurrentCompanyService>();
builder.Services.AddScoped<IBancosWsService, BancosWsService>();

builder.Services.AddControllers();

var app = builder.Build();

app.UseMiddleware<BancosWsAuthMiddleware>();
app.MapControllers();

app.Run();

// Visible para WebApplicationFactory (tests de equivalencia).
public partial class Program { }
