using apc.Client;
using apc.Client.Pages;
using apc.Components;
using apc.Components.Account;
using apc.Data;
using apc.Options;
using apc.Security;
using DevExpress.AspNetCore.Reporting;
using DevExpress.Blazor.Reporting;
using DevExpress.DataAccess.Sql;
using DevExpress.DataAccess.Web;
using DevExpress.DataAccess.Wizard.Services;
using DevExpress.Utils;
using DevExpress.XtraReports.Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Linq;
using System.Net.Http;
using SIAD.Core.Constants;
using SIAD.Data;
using SIAD.Reports;
using SIAD.Services;
using SIAD.Services.Auditoria;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;

// Cultura por defecto (HN). Preservar — Identity, formatos numéricos y fechas dependen.
var hondurasCulture = new CultureInfo("es-HN");
CultureInfo.DefaultThreadCurrentCulture = hondurasCulture;
CultureInfo.DefaultThreadCurrentUICulture = hondurasCulture;

// Npgsql: comportamiento legacy de timestamps (timestamp without time zone como Local en lugar de Unspecified).
// Necesario para que las columnas de fecha existentes sigan funcionando sin convertir todas las entidades.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Sobre-config local (gitignored) — cada dev pone aquí las credenciales reales
// (Azure PG, Azure Maps, etc.) sin commitearlas. Si el archivo no existe, se
// usan los valores de appsettings.json / appsettings.{Environment}.json.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

if (builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
    builder.Logging.AddDebug();

    var dataProtectionKeysPath = Path.Combine(builder.Environment.ContentRootPath, "DataProtectionKeys");
    builder.Services.AddDataProtection()
        .SetApplicationName("HODSOFT.Prestadoras.Development")
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
}

// Add services to the container.

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<ServerApiCookieHeaderHandler>();
builder.Services.AddHttpClient("ServerAPI", (sp, client) =>
    {
        var httpContext = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
        var baseUrl = httpContext is not null
            ? $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/"
            : builder.Configuration["BaseUrl"] ?? "http://localhost:5000/";

        client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
    })
    .AddHttpMessageHandler<ServerApiCookieHeaderHandler>()
    .ConfigurePrimaryHttpMessageHandler(() =>
    {
        var handler = new HttpClientHandler
        {
            UseCookies = false,
            AllowAutoRedirect = false
        };

        // Allow self-signed certificates in development
        if (builder.Environment.IsDevelopment())
        {
            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
        }

        return handler;
    });
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("ServerAPI"));

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        options.DetailedErrors = true; // mostrar detalles de excepciones en circuitos Blazor
    });

CommonServices.Configure(builder.Services, builder.Configuration);
builder.Services.Configure<MapsOptions>(builder.Configuration.GetSection(MapsOptions.SectionName));
builder.Services.AddMvc();

builder.Services.AddDevExpressBlazorReporting();
builder.Services.AddDevExpressServerSideBlazorPdfViewer();
builder.Services.AddTransient<ICustomQueryValidator, ReportingCustomQueryValidator>();
builder.Services.ConfigureReportingServices(configurator =>
{
    if (builder.Environment.IsDevelopment())
    {
        configurator.UseDevelopmentMode();
    }

    configurator.ConfigureReportDesigner(designerConfigurator =>
    {
        designerConfigurator.EnableCustomSql();
        designerConfigurator.RegisterDataSourceWizardConnectionStringsProvider<ReportingDataSourceWizardConnectionStringsProvider>();
        designerConfigurator.RegisterSqlDataSourceWizardCustomizationService<ReportingSqlDataSourceWizardCustomizationService>();
    });
    configurator.ConfigureWebDocumentViewer(viewerConfigurator =>
    {
        viewerConfigurator.RegisterConnectionProviderFactory<ReportingConnectionProviderFactory>();
    });
});
builder.Services.AddSiadReports();
builder.Services.AddScoped<IConnectionProviderService, ReportingConnectionProviderService>();
builder.Services.AddScoped<IConnectionProviderFactory, ReportingConnectionProviderFactory>();
builder.Services.AddScoped<ReportStorageWebExtension, CompanyReportStorageWebExtension>();
builder.Services.AddAntiforgery(options => 
{
    options.SuppressXFrameOptionsHeader = false;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
    });
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, PersistingServerAuthenticationStateProvider>();
builder.Services.AddScoped<IClaimsTransformation, TenantCompanyClaimTransformation>();
builder.Services.AddScoped<ICompanyAccessValidator, CompanyAccessValidator>();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.Contabilidad,
        policy => policy.RequireRole(RoleNames.Admin, RoleNames.Contabilidad));
    options.AddPolicy(AuthorizationPolicies.PresupuestoAprobacion,
        policy => policy.RequireRole(RoleNames.Admin, RoleNames.Contabilidad));
    options.AddPolicy(AuthorizationPolicies.Compras,
        policy => policy.RequireRole(RoleNames.Admin, RoleNames.Compras));
    options.AddPolicy(AuthorizationPolicies.Ventas,
        policy => policy.RequireRole(RoleNames.Admin, RoleNames.Ventas));
    options.AddPolicy(AuthorizationPolicies.Facturacion,
        policy => policy.RequireRole(RoleNames.Admin, RoleNames.Ventas, RoleNames.Facturacion));
    options.AddPolicy(AuthorizationPolicies.Bancos,
        policy => policy.RequireRole(RoleNames.Admin, RoleNames.Bancos));
    options.AddPolicy(AuthorizationPolicies.Configuracion,
        policy => policy.RequireRole(RoleNames.Admin, RoleNames.Configuracion));
    foreach (var policyDefinition in PermissionNames.Policies)
    {
        options.AddPolicy(policyDefinition.Policy,
            policy => RequirePermissionOrSuperAdmin(policy, policyDefinition.Permissions));
    }
    options.AddPolicy(AuthorizationPolicies.SuperAdmin,
        policy => policy.RequireRole(RoleNames.SuperAdministrador));
});
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
})
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql =>
        npgsql.MigrationsHistoryTable("__IdentityMigrationsHistory", "identity")));

builder.Services.AddScoped<BitacoraMaestrosInterceptor>();
builder.Services.AddDbContext<SiadDbContext>((sp, options) =>
    options.UseNpgsql(connectionString)
           .AddInterceptors(sp.GetRequiredService<BitacoraMaestrosInterceptor>()));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

// Wire SIAD layered services
builder.Services.AddSiadServices();

// Auditoría: en el portal usamos la impl real sobre IHttpContextAccessor.
// Replace sobrescribe el fallback SystemUserAudit registrado por AddSiadServices.
builder.Services.Replace(ServiceDescriptor.Scoped<ICurrentUserAudit, apc.Security.CurrentUserAudit>());

var app = builder.Build();
ReportingRuntimeBootstrap.Initialize(app.Services);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

var supportedCultures = new[] { hondurasCulture };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(hondurasCulture),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

// Habilitar WebSockets para Blazor Server (crítico para IIS)
app.UseWebSockets();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();
app.UseDevExpressBlazorReporting();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(Counter).Assembly)
    .AllowAnonymous();

app.MapAdditionalIdentityEndpoints();
app.MapControllers();

app.Run();

static void RequirePermissionOrSuperAdmin(AuthorizationPolicyBuilder policy, params string[] permissions)
{
    policy.RequireAssertion(context =>
        context.User.IsInRole(RoleNames.SuperAdministrador) ||
        permissions.Any(permission => context.User.HasClaim(PermissionClaimTypes.Permission, permission)));
}
