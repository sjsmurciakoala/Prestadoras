using apc.Client;
using apc.Client.Pages;
using apc.Components;
using apc.Components.Account;
using apc.Data;
using apc.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using System.Linq;
using System.Net.Http;
using SIAD.Core.Constants;
using SIAD.Data;
using SIAD.Services;
using System.Net.Http.Headers;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient("ServerAPI")
    .ConfigurePrimaryHttpMessageHandler(() => 
    {
        var handler = new HttpClientHandler
        {
            UseCookies = false
        };
        
        // Allow self-signed certificates in development
        if (builder.Environment.IsDevelopment())
        {
            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
        }
        
        return handler;
    });
builder.Services.AddScoped(sp =>
{
    var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
    var httpContext = httpContextAccessor.HttpContext;
    var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient("ServerAPI");

    if (httpContext is not null)
    {
        client.BaseAddress = new Uri($"{httpContext.Request.Scheme}://{httpContext.Request.Host}/");

        client.DefaultRequestHeaders.Remove("Cookie");
        if (httpContext.Request.Headers.TryGetValue("Cookie", out var cookies) && !StringValues.IsNullOrEmpty(cookies))
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", cookies.AsEnumerable());
        }
    }
    else if (client.BaseAddress is null)
    {
        // Fallback para cuando no hay HttpContext (WASM prerendering o background tasks)
        // Usar configuración explícita o valor por defecto
        var baseUrl = builder.Configuration["BaseUrl"] ?? "http://localhost:5000/";
        client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
        client.DefaultRequestHeaders.Remove("Cookie");
    }

    return client;
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        options.DetailedErrors = true; // mostrar detalles de excepciones en circuitos Blazor
    });

CommonServices.Configure(builder.Services, builder.Configuration);
builder.Services.AddMvc();

builder.Services.AddDevExpressServerSideBlazorPdfViewer();
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

builder.Services.AddAuthorization(options =>
{
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

builder.Services.AddDbContext<SiadDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

// Wire SIAD layered services
builder.Services.AddSiadServices();

var app = builder.Build();

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

// Habilitar WebSockets para Blazor Server (crítico para IIS)
app.UseWebSockets();

app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(Counter).Assembly)
    .AllowAnonymous();

app.MapAdditionalIdentityEndpoints();
app.MapControllers();

app.Run();

static void ForwardHeader(IHeaderDictionary source, HttpRequestHeaders target, string headerName)
{
    if (!source.TryGetValue(headerName, out var values))
    {
        return;
    }

    target.Remove(headerName);
    foreach (var value in values)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            target.TryAddWithoutValidation(headerName, value);
        }
    }
}

static void RequirePermissionOrSuperAdmin(AuthorizationPolicyBuilder policy, params string[] permissions)
{
    policy.RequireAssertion(context =>
        context.User.IsInRole(RoleNames.SuperAdministrador) ||
        permissions.Any(permission => context.User.HasClaim(PermissionClaimTypes.Permission, permission)));
}
