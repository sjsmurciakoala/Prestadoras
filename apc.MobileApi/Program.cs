using apc.MobileApi.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.MobileApi;

// apc.MobileApi — backend REST/JSON de la app Flutter de lectores (plan
// app_lectores L3, decisión A4). Host propio, separado del portal y del WS WCF
// viejo (que queda para la app Java): reusa los MISMOS SPs V3 con un contrato
// JSON limpio. Auth propia por credencial/sesión de lector (A6: el tenant sale
// de la sesión, nunca de un parámetro del cliente).
var builder = WebApplication.CreateBuilder(args);

// Misma convención que el portal: la conexión real vive en el servidor
// (appsettings.Local.json, gitignored) — el publish viaja sin credenciales.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Log a archivo: el host corre bajo IIS con stdoutLogEnabled=false, así que sin esto
// todo LogError se descarta y un error de campo no deja rastro en ningún lado.
// Se configura en MobileApi:Log (Directorio, NivelMinimo, RetencionDias).
var logOptions = builder.Configuration.GetSection("MobileApi:Log").Get<FileLoggerOptions>()
                 ?? new FileLoggerOptions();
builder.Logging.AddProvider(new FileLoggerProvider(logOptions, builder.Environment.ContentRootPath));

builder.Services.AddDbContext<SiadDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Tenant del request: lo resuelve el token bearer de cada request
// (adm_lector_sesion) — no hay Identity ni claims en este host.
builder.Services.AddScoped<MobileApiRequestContext>();
builder.Services.AddScoped<ICurrentCompanyService, MobileApiCurrentCompanyService>();
builder.Services.AddScoped<ILectoresMobileService, LectoresMobileService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "APC MobileApi — Lectores",
        Version = "v1",
        Description = "API REST/JSON de la app de lectores (paridad funcional V3 con el WS viejo, mismos SPs).",
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "opaque",
        In = ParameterLocation.Header,
        Description = "Token emitido por /api/lectores/login.",
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
            },
            Array.Empty<string>()
        },
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseMiddleware<MobileApiAuthMiddleware>();
app.MapControllers();

app.Run();

// Visible para WebApplicationFactory (tests de integración).
public partial class Program { }
