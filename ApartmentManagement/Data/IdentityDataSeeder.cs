using ApartmentManagement.Common.Security;
using ApartmentManagement.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.Data;

public static class IdentityDataSeeder
{
    public static async Task InitializeAsync(
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        using var scope = serviceProvider.CreateScope();
        var services = scope.ServiceProvider;

        var context = services.GetRequiredService<ApplicationDbContext>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        if (context.Database.IsRelational())
        {
            await context.Database.MigrateAsync();
        }
        else
        {
            await context.Database.EnsureCreatedAsync();
        }

        foreach (var roleName in AppRoles.AllRoles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(roleName));
                EnsureSucceeded(result, $"Không thể tạo role '{roleName}'");
            }
        }

        await MigrateLegacyRoleAsync(userManager, roleManager, "SystemAdmin", AppRoles.SuperAdmin);
        await MigrateLegacyRoleAsync(userManager, roleManager, "Admin", AppRoles.SuperAdmin);
        await MigrateLegacyRoleAsync(userManager, roleManager, "Manager", AppRoles.BuildingManager);

        var email = configuration["Seed:SuperAdmin:Email"];
        var password = configuration["Seed:SuperAdmin:Password"];
        var fullName = configuration["Seed:SuperAdmin:FullName"] ?? "System Administrator";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var admin = await userManager.FindByEmailAsync(email);
        if (admin == null)
        {
            admin = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                IsActive = true,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(admin, password);
            EnsureSucceeded(createResult, "Không thể tạo tài khoản SuperAdmin khởi tạo");
        }

        if (!await userManager.IsInRoleAsync(admin, AppRoles.SuperAdmin))
        {
            var addRoleResult = await userManager.AddToRoleAsync(admin, AppRoles.SuperAdmin);
            EnsureSucceeded(addRoleResult, "Không thể gán role SuperAdmin");
        }
    }

    private static async Task MigrateLegacyRoleAsync(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        string legacyRole,
        string currentRole)
    {
        if (!await roleManager.RoleExistsAsync(legacyRole))
        {
            return;
        }

        foreach (var user in await userManager.GetUsersInRoleAsync(legacyRole))
        {
            if (!await userManager.IsInRoleAsync(user, currentRole))
            {
                var result = await userManager.AddToRoleAsync(user, currentRole);
                EnsureSucceeded(result, $"Không thể migrate role cho user '{user.UserName}'");
            }
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string message)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join(", ", result.Errors.Select(error => error.Description));
        throw new InvalidOperationException($"{message}: {errors}");
    }
}
