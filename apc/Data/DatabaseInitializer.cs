using Microsoft.AspNetCore.Identity;
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
            // Ensure roles exist
            var roles = new[] { "Admin", "User" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
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
                var result = await userManager.CreateAsync(user, "Admin123$");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, "Admin");
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
        }
    }
}
