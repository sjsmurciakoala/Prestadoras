using apc.Client;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using System;
using System.Linq;
using System.Net.Http;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

CommonServices.Configure(builder.Services, builder.Configuration);

builder.Services.AddDevExpressWebAssemblyBlazorPdfViewer();

DevExpress.XtraPrinting.PrintingOptions.Pdf.RenderingEngine = DevExpress.XtraPrinting.XRPdfRenderingEngine.Skia;

builder.Services.AddAuthorizationCore(options =>
{
    foreach (var policyDefinition in SIAD.Core.Constants.PermissionNames.Policies)
    {
        options.AddPolicy(policyDefinition.Policy,
            policy => RequirePermissionOrSuperAdmin(policy, policyDefinition.Permissions));
    }
    options.AddPolicy(SIAD.Core.Constants.AuthorizationPolicies.SuperAdmin,
        policy => policy.RequireRole(SIAD.Core.Constants.RoleNames.SuperAdministrador));
});
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddSingleton<AuthenticationStateProvider, PersistentAuthenticationStateProvider>();
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

await builder.Build().RunAsync();

static void RequirePermissionOrSuperAdmin(AuthorizationPolicyBuilder policy, params string[] permissions)
{
    policy.RequireAssertion(context =>
        context.User.IsInRole(SIAD.Core.Constants.RoleNames.SuperAdministrador) ||
        permissions.Any(permission => context.User.HasClaim(SIAD.Core.Constants.PermissionClaimTypes.Permission, permission)));
}
