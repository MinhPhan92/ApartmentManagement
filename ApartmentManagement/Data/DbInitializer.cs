using ApartmentManagement.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(
            IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();

            var context =
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

            var roleManager =
                scope.ServiceProvider
                    .GetRequiredService<RoleManager<IdentityRole>>();

            var userManager =
                scope.ServiceProvider
                    .GetRequiredService<UserManager<ApplicationUser>>();

            // Đảm bảo database đã được cập nhật
            await context.Database.MigrateAsync();

            // =========================
            // 1. SEED ROLES
            // =========================

            string[] roles =
            {
                "SystemAdmin",
                "Manager",
                "Resident"
            };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(
                        new IdentityRole(role));
                }
            }

            // =========================
            // 2. SEED SYSTEM ADMIN
            // =========================

            await CreateUserAsync(
                userManager,
                username: "admin",
                email: "admin@apartment.local",
                fullName: "System Administrator",
                password: "Admin@123",
                role: "SystemAdmin");

            // =========================
            // 3. SEED MANAGER
            // =========================

            await CreateUserAsync(
                userManager,
                username: "manager",
                email: "manager@apartment.local",
                fullName: "Building Manager",
                password: "Manager@123",
                role: "Manager");

            // =========================
            // 4. SEED RESIDENT
            // =========================

            var residentUser = await CreateUserAsync(
                userManager,
                username: "resident",
                email: "resident@apartment.local",
                fullName: "Nguyễn Văn A",
                password: "Resident@123",
                role: "Resident");

            // Tạo Resident profile nếu chưa có
            if (residentUser != null)
            {
                var residentExists = await context.Residents
                    .AnyAsync(x => x.UserId == residentUser.Id);

                if (!residentExists)
                {
                    context.Residents.Add(new Resident
                    {
                        UserId = residentUser.Id,
                        CitizenId = "012345678901",
                        DateOfBirth = new DateTime(2000, 1, 1),
                        Gender = "Nam",
                        Address = "Hà Nội",
                        EmergencyContact = "0900000000"
                    });

                    await context.SaveChangesAsync();
                }
            }
        }

        private static async Task<ApplicationUser?> CreateUserAsync(
            UserManager<ApplicationUser> userManager,
            string username,
            string email,
            string fullName,
            string password,
            string role)
        {
            var user = await userManager.FindByNameAsync(username);

            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = username,
                    Email = email,
                    FullName = fullName,
                    IsActive = true,
                    EmailConfirmed = true
                };

                var result =
                    await userManager.CreateAsync(user, password);

                if (!result.Succeeded)
                {
                    var errors = string.Join(
                        ", ",
                        result.Errors.Select(x => x.Description));

                    throw new Exception(
                        $"Không thể tạo user '{username}': {errors}");
                }
            }

            if (!await userManager.IsInRoleAsync(user, role))
            {
                await userManager.AddToRoleAsync(user, role);
            }

            return user;
        }
    }
}