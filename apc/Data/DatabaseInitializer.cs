using Microsoft.AspNetCore.Identity;
using SIAD.Core.Constants;
using SIAD.Data;

namespace apc.Data
{
    public static class DatabaseInitializer
    {
        public static async Task SeedAsync(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            SiadDbContext siadDbContext)
        {
            // Ensure Super Admin role exists
            var superAdminRoleName = RoleNames.SuperAdministrador;
            if (!await roleManager.RoleExistsAsync(superAdminRoleName))
            {
                await roleManager.CreateAsync(new IdentityRole(superAdminRoleName));
            }

            var superAdminRole = await roleManager.FindByNameAsync(superAdminRoleName);
            if (superAdminRole is not null)
            {
                var existingClaims = await roleManager.GetClaimsAsync(superAdminRole);
                var actuales = existingClaims
                    .Where(c => c.Type == PermissionClaimTypes.Permission)
                    .Select(c => c.Value)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var permiso in PermissionNames.All)
                {
                    if (!actuales.Contains(permiso))
                    {
                        await roleManager.AddClaimAsync(superAdminRole,
                            new System.Security.Claims.Claim(PermissionClaimTypes.Permission, permiso));
                    }
                }
            }

            // Seed admin user
            var adminEmail = "admin@siad-demo.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                var user = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(user, "Admin123@");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, superAdminRoleName);
                }
            }
            else
            {
                if (!await userManager.IsInRoleAsync(adminUser, superAdminRoleName))
                {
                    await userManager.AddToRoleAsync(adminUser, superAdminRoleName);
                }
            }

            // Seed demo company
            var demoCompanyCode = "APC";
            var demoCompany = siadDbContext.cfg_companies.FirstOrDefault(c => c.code == demoCompanyCode);
            if (demoCompany == null)
            {
                demoCompany = new SIAD.Core.Entities.cfg_company
                {
                    code = demoCompanyCode,
                    commercial_name = "Aguas de Puerto Cortes",
                    legal_name = "Aguas de Puerto Cortes, S.A.",
                    tax_id = "0000000000000",
                    country_code = "HN",
                    currency_code = "HNL",
                    timezone = "America/Tegucigalpa",
                    status = "A",
                    created_at = DateTime.UtcNow,
                    created_by = adminEmail
                };
                siadDbContext.cfg_companies.Add(demoCompany);
                await siadDbContext.SaveChangesAsync();
            }

            // Asignar claim de empresa al usuario admin (si existe)
            var admin = adminUser ?? await userManager.FindByEmailAsync(adminEmail);
            if (admin is not null && demoCompany is not null)
            {
                var claims = await userManager.GetClaimsAsync(admin);
                var companyClaim = claims.FirstOrDefault(c => c.Type == TenantClaimTypes.CompanyId);
                if (companyClaim is null)
                {
                    await userManager.AddClaimAsync(admin,
                        new System.Security.Claims.Claim(TenantClaimTypes.CompanyId, demoCompany.company_id.ToString()));
                }
            }
        }
    }
}
