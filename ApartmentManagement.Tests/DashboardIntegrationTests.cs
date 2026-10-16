using System.Net;
using System.Security.Claims;
using ApartmentManagement.Common.Security;
using ApartmentManagement.Controllers;
using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ApartmentManagement.Tests;

public sealed class DashboardIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string AdminUserId = "dashboard-admin";
    private const string ManagerUserId = "dashboard-manager";
    private const string ResidentOneUserId = "dashboard-resident-1";
    private const string ResidentTwoUserId = "dashboard-resident-2";

    private readonly CustomWebApplicationFactory _factory;

    public DashboardIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        SeedTestData();
    }

    [Fact]
    public async Task SuperAdmin_CanAccessManagementDashboard()
    {
        using var client = CreateAuthenticatedClient(AppRoles.SuperAdmin, AdminUserId);
        using var response = await client.GetAsync("/Dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("System Administrator", content);
        Assert.Contains("BLD-A", content);
    }

    [Fact]
    public async Task BuildingManager_CanAccessManagementDashboard()
    {
        using var client = CreateAuthenticatedClient(AppRoles.BuildingManager, ManagerUserId);
        using var response = await client.GetAsync("/Dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Building Manager", content);
    }

    [Fact]
    public async Task Resident_CanAccessResidentDashboard()
    {
        using var client = CreateAuthenticatedClient(AppRoles.Resident, ResidentOneUserId);
        using var response = await client.GetAsync("/Dashboard/Resident");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("100000000001", content);
        Assert.Contains("A-101", content);
        Assert.Contains("resident1@test.local", content);

        // Resident data isolation: Resident 2's info must not appear on Resident 1's dashboard
        Assert.DoesNotContain("100000000002", content);
        Assert.DoesNotContain("B-201", content);
    }

    [Fact]
    public async Task Anonymous_Access_RedirectsToLogin()
    {
        using var client = CreateClient();

        using var managementResponse = await client.GetAsync("/Dashboard");
        Assert.Equal(HttpStatusCode.Redirect, managementResponse.StatusCode);
        Assert.Equal("/Identity/Account/Login", managementResponse.Headers.Location?.AbsolutePath);
        Assert.Contains("ReturnUrl=%2FDashboard", managementResponse.Headers.Location?.Query);

        using var residentResponse = await client.GetAsync("/Dashboard/Resident");
        Assert.Equal(HttpStatusCode.Redirect, residentResponse.StatusCode);
        Assert.Equal("/Identity/Account/Login", residentResponse.Headers.Location?.AbsolutePath);
        Assert.Contains("ReturnUrl=%2FDashboard%2FResident", residentResponse.Headers.Location?.Query);
    }

    [Fact]
    public async Task Resident_CannotAccessManagementDashboard()
    {
        using var client = CreateAuthenticatedClient(AppRoles.Resident, ResidentOneUserId);
        using var response = await client.GetAsync("/Dashboard");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task BuildingManager_CannotAccessResidentDashboard()
    {
        using var client = CreateAuthenticatedClient(AppRoles.BuildingManager, ManagerUserId);
        using var response = await client.GetAsync("/Dashboard/Resident");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Dashboard_KpiCalculations_AreCorrect_ForDeterministicData()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var controller = new DashboardController(context, userManager)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = CreatePrincipal(AppRoles.SuperAdmin, AdminUserId)
                }
            }
        };

        var actionResult = await controller.Index();
        var viewResult = Assert.IsType<ViewResult>(actionResult);

        // Assert exact KPI counts stored in ViewData/ViewBag
        Assert.Equal(2, viewResult.ViewData["TotalBuildings"]);
        Assert.Equal(3, viewResult.ViewData["TotalApartments"]);
        Assert.Equal(2, viewResult.ViewData["TotalResidents"]);
        Assert.Equal(1, viewResult.ViewData["OccupiedApartments"]);
        Assert.Equal(2, viewResult.ViewData["VacantApartments"]);
        Assert.Equal(2, viewResult.ViewData["PendingTickets"]);
        Assert.Equal(2, viewResult.ViewData["PendingComplaints"]);
        Assert.Equal(1, viewResult.ViewData["PendingVehicles"]);
        Assert.Equal(1, viewResult.ViewData["ActiveAmenities"]);
        Assert.Equal(1, viewResult.ViewData["UnpaidInvoices"]);
        Assert.Equal("System Administrator", viewResult.ViewData["UserRole"]);

        // Assert model contains recent buildings
        var model = Assert.IsAssignableFrom<IEnumerable<Building>>(viewResult.Model);
        Assert.Equal(2, model.Count());

        // Also verify via HTTP rendering that the page renders 200 OK
        using var client = CreateAuthenticatedClient(AppRoles.SuperAdmin, AdminUserId);
        using var response = await client.GetAsync("/Dashboard");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task EmptyBusinessData_RendersSuccessfully_WithZeroValues()
    {
        // Use a dedicated in-memory factory to guarantee zero business data
        await using var emptyFactory = new CustomWebApplicationFactory();
        using (var scope = emptyFactory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

            var adminUser = CreateUser(AdminUserId, "empty-admin@test.local", "Empty Admin");
            context.Users.Add(adminUser);
            await context.SaveChangesAsync();

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            await roleManager.CreateAsync(new IdentityRole(AppRoles.SuperAdmin));
            await userManager.AddToRoleAsync(adminUser, AppRoles.SuperAdmin);
        }

        using var scopeForController = emptyFactory.Services.CreateScope();
        var emptyContext = scopeForController.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emptyUserManager = scopeForController.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var controller = new DashboardController(emptyContext, emptyUserManager)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = CreatePrincipal(AppRoles.SuperAdmin, AdminUserId)
                }
            }
        };

        var actionResult = await controller.Index();
        var viewResult = Assert.IsType<ViewResult>(actionResult);

        Assert.Equal(0, viewResult.ViewData["TotalBuildings"]);
        Assert.Equal(0, viewResult.ViewData["TotalApartments"]);
        Assert.Equal(0, viewResult.ViewData["TotalResidents"]);
        Assert.Equal(0, viewResult.ViewData["OccupiedApartments"]);
        Assert.Equal(0, viewResult.ViewData["VacantApartments"]);
        Assert.Equal(0, viewResult.ViewData["PendingTickets"]);
        Assert.Equal(0, viewResult.ViewData["PendingComplaints"]);
        Assert.Equal(0, viewResult.ViewData["PendingVehicles"]);
        Assert.Equal(0, viewResult.ViewData["ActiveAmenities"]);
        Assert.Equal(0, viewResult.ViewData["UnpaidInvoices"]);

        var model = Assert.IsAssignableFrom<IEnumerable<Building>>(viewResult.Model);
        Assert.Empty(model);

        // Also test through HTTP pipeline with empty business data
        var client = emptyFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add("X-Test-Role", AppRoles.SuperAdmin);
        client.DefaultRequestHeaders.Add("X-Test-UserId", AdminUserId);

        using var response = await client.GetAsync("/Dashboard");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost")
    });

    private HttpClient CreateAuthenticatedClient(string role, string userId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId);
        return client;
    }

    private static ClaimsPrincipal CreatePrincipal(string role, string userId)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, $"user_{userId}@test.local"),
            new Claim(ClaimTypes.Role, role)
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, TestAuthHandler.SchemeName));
    }

    private void SeedTestData()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();

        // 1. Roles
        foreach (var role in AppRoles.AllRoles)
        {
            context.Roles.Add(new IdentityRole(role)
            {
                Id = $"role-{role.ToLowerInvariant()}",
                NormalizedName = role.ToUpperInvariant()
            });
        }

        // 2. Users
        var adminUser = CreateUser(AdminUserId, "admin@test.local", "Admin User");
        var managerUser = CreateUser(ManagerUserId, "manager@test.local", "Manager User");
        var residentOneUser = CreateUser(ResidentOneUserId, "resident1@test.local", "Resident One");
        var residentTwoUser = CreateUser(ResidentTwoUserId, "resident2@test.local", "Resident Two");
        context.Users.AddRange(adminUser, managerUser, residentOneUser, residentTwoUser);

        // 3. Buildings (2 total)
        var buildingA = new Building
        {
            BuildingId = 1,
            BuildingCode = "BLD-A",
            BuildingName = "Tòa Sapphire A",
            NumberOfFloors = 10,
            Address = "123 Đường A",
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        };
        var buildingB = new Building
        {
            BuildingId = 2,
            BuildingCode = "BLD-B",
            BuildingName = "Tòa Ruby B",
            NumberOfFloors = 8,
            Address = "456 Đường B",
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        };
        context.Buildings.AddRange(buildingA, buildingB);

        // 4. Apartments (3 total: 2 in A, 1 in B)
        var aptA101 = new Apartment
        {
            ApartmentId = 1,
            BuildingId = 1,
            ApartmentCode = "A-101",
            Floor = 1,
            Area = 65,
            Status = "Đang sử dụng"
        };
        var aptA102 = new Apartment
        {
            ApartmentId = 2,
            BuildingId = 1,
            ApartmentCode = "A-102",
            Floor = 1,
            Area = 70,
            Status = "Trống"
        };
        var aptB201 = new Apartment
        {
            ApartmentId = 3,
            BuildingId = 2,
            ApartmentCode = "B-201",
            Floor = 2,
            Area = 80,
            Status = "Trống"
        };
        context.Apartments.AddRange(aptA101, aptA102, aptB201);

        // 5. Residents (2 total)
        var residentOne = new Resident
        {
            ResidentId = 1,
            UserId = ResidentOneUserId,
            CitizenId = "100000000001",
            Address = "Hà Nội"
        };
        var residentTwo = new Resident
        {
            ResidentId = 2,
            UserId = ResidentTwoUserId,
            CitizenId = "100000000002",
            Address = "TP HCM"
        };
        context.Residents.AddRange(residentOne, residentTwo);

        // 6. Occupancy: 1 active in aptA101, 1 past moved-out in aptA102
        context.ApartmentResidents.AddRange(
            new ApartmentResident
            {
                ApartmentResidentId = 1,
                ApartmentId = 1,
                ResidentId = 1,
                Relationship = "Chủ hộ",
                MoveInDate = DateTime.Today.AddMonths(-6),
                MoveOutDate = null, // ACTIVE
                IsOwner = true
            },
            new ApartmentResident
            {
                ApartmentResidentId = 2,
                ApartmentId = 2,
                ResidentId = 2,
                Relationship = "Thuê",
                MoveInDate = DateTime.Today.AddMonths(-12),
                MoveOutDate = DateTime.Today.AddMonths(-1), // MOVED OUT (INACTIVE)
                IsOwner = false
            }
        );

        // 7. Incident Tickets: 1 Submitted, 1 InProgress, 1 Completed -> Pending = 2
        context.IncidentTickets.AddRange(
            new IncidentTicket
            {
                IncidentTicketId = 1,
                TicketCode = "INC-001",
                ApartmentId = 1,
                ResidentId = 1,
                Category = IncidentCategory.Water,
                Title = "Rò nước",
                Description = "Ống nước rò rỉ",
                Status = IncidentTicketStatus.Submitted,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-2)
            },
            new IncidentTicket
            {
                IncidentTicketId = 2,
                TicketCode = "INC-002",
                ApartmentId = 1,
                ResidentId = 1,
                Category = IncidentCategory.Electricity,
                Title = "Cháy bóng đèn",
                Description = "Đèn phòng khách hỏng",
                Status = IncidentTicketStatus.InProgress,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-1)
            },
            new IncidentTicket
            {
                IncidentTicketId = 3,
                TicketCode = "INC-003",
                ApartmentId = 1,
                ResidentId = 1,
                Category = IncidentCategory.Other,
                Title = "Khóa cửa kẹt",
                Description = "Đã sửa xong",
                Status = IncidentTicketStatus.Completed,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-5),
                CompletedAtUtc = DateTime.UtcNow.AddDays(-3)
            }
        );

        // 8. Complaints: 1 "Chờ tiếp nhận", 1 "Đang xử lý", 1 "Đã giải quyết" -> Pending = 2
        context.Complaints.AddRange(
            new Complaint
            {
                ComplaintId = 1,
                ResidentId = 1,
                ApartmentId = 1,
                Title = "Tiếng ồn",
                Content = "Tiếng ồn đêm khuya",
                Category = "Tiếng ồn",
                Status = "Chờ tiếp nhận",
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new Complaint
            {
                ComplaintId = 2,
                ResidentId = 1,
                ApartmentId = 1,
                Title = "Vệ sinh hành lang",
                Content = "Hành lang chưa quét",
                Category = "Vệ sinh",
                Status = "Đang xử lý",
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            },
            new Complaint
            {
                ComplaintId = 3,
                ResidentId = 1,
                ApartmentId = 1,
                Title = "Đỗ xe sai quy định",
                Content = "Đã nhắc nhở",
                Category = "Khác",
                Status = "Đã giải quyết",
                CreatedAt = DateTime.UtcNow.AddDays(-4),
                ResolvedAt = DateTime.UtcNow.AddDays(-3)
            }
        );

        // 9. Vehicles: 1 "Chờ duyệt", 1 "Đã duyệt", 1 "Từ chối" -> Pending = 1
        context.Vehicles.AddRange(
            new Vehicle
            {
                VehicleId = 1,
                ResidentId = 1,
                VehicleType = "Xe máy",
                LicensePlate = "29A1-12345",
                Status = "Chờ duyệt",
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new Vehicle
            {
                VehicleId = 2,
                ResidentId = 1,
                VehicleType = "Ô tô",
                LicensePlate = "30E-67890",
                Status = "Đã duyệt",
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                ReviewedAt = DateTime.UtcNow.AddDays(-9)
            },
            new Vehicle
            {
                VehicleId = 3,
                ResidentId = 2,
                VehicleType = "Xe đạp điện",
                LicensePlate = "29MD-99999",
                Status = "Từ chối",
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                ReviewedAt = DateTime.UtcNow.AddDays(-4)
            }
        );

        // 10. Amenities: 1 Active, 1 Inactive -> Active = 1
        context.Amenities.AddRange(
            new Amenity
            {
                AmenityId = 1,
                Name = "Hồ bơi bốn mùa",
                Capacity = 30,
                OpenTime = new TimeSpan(6, 0, 0),
                CloseTime = new TimeSpan(22, 0, 0),
                IsActive = true,
                PricePerHour = 50000
            },
            new Amenity
            {
                AmenityId = 2,
                Name = "Sân tennis phụ",
                Capacity = 4,
                OpenTime = new TimeSpan(7, 0, 0),
                CloseTime = new TimeSpan(21, 0, 0),
                IsActive = false,
                PricePerHour = 100000
            }
        );

        // 11. Invoices: 1 Issued with no confirmed payment, 1 Issued with confirmed payment -> Unpaid = 1
        var unpaidInvoice = new ApartmentInvoice
        {
            ApartmentInvoiceId = 1,
            InvoiceCode = "INV-202610-001",
            ApartmentId = 1,
            ResidentId = 1,
            BillingYear = 2026,
            BillingMonth = 10,
            DueDate = DateTime.Today.AddDays(10),
            ApartmentArea = 65,
            TotalAmount = 500000,
            Status = ApartmentInvoiceStatus.Issued,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-3),
            IssuedAtUtc = DateTime.UtcNow.AddDays(-3)
        };
        var paidInvoice = new ApartmentInvoice
        {
            ApartmentInvoiceId = 2,
            InvoiceCode = "INV-202610-002",
            ApartmentId = 2,
            ResidentId = 2,
            BillingYear = 2026,
            BillingMonth = 10,
            DueDate = DateTime.Today.AddDays(10),
            ApartmentArea = 70,
            TotalAmount = 800000,
            Status = ApartmentInvoiceStatus.Issued,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-5),
            IssuedAtUtc = DateTime.UtcNow.AddDays(-5)
        };
        context.ApartmentInvoices.AddRange(unpaidInvoice, paidInvoice);

        // Confirmed transaction for paidInvoice
        context.PaymentTransactions.Add(new PaymentTransaction
        {
            PaymentTransactionId = 1,
            ApartmentInvoiceId = 2,
            Reference = "REF-PAID-001",
            Amount = 800000,
            Status = PaymentTransactionStatus.Confirmed,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-2),
            ConfirmedAtUtc = DateTime.UtcNow.AddDays(-1),
            ConfirmedByUserId = AdminUserId
        });

        context.SaveChanges();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True(userManager.AddToRoleAsync(adminUser, AppRoles.SuperAdmin).GetAwaiter().GetResult().Succeeded);
        Assert.True(userManager.AddToRoleAsync(managerUser, AppRoles.BuildingManager).GetAwaiter().GetResult().Succeeded);
        Assert.True(userManager.AddToRoleAsync(residentOneUser, AppRoles.Resident).GetAwaiter().GetResult().Succeeded);
        Assert.True(userManager.AddToRoleAsync(residentTwoUser, AppRoles.Resident).GetAwaiter().GetResult().Succeeded);
    }

    private static ApplicationUser CreateUser(string id, string email, string fullName) => new()
    {
        Id = id,
        UserName = email,
        NormalizedUserName = email.ToUpperInvariant(),
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
        FullName = fullName,
        EmailConfirmed = true,
        IsActive = true
    };
}
