using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApartmentManagement.Common.Security;
using ApartmentManagement.Controllers;
using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ApartmentManagement.Tests;

public sealed class ResidentPortalApiIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ResidentPortalApiIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateHttpsClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true,
        BaseAddress = new Uri("https://localhost")
    });

    [Fact]
    public async Task ResidentPortalEndpoints_Unauthenticated_Returns401()
    {
        using var client = CreateHttpsClient();

        using var dashboardRes = await client.GetAsync("/api/resident/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, dashboardRes.StatusCode);

        using var amenitiesRes = await client.GetAsync("/api/resident/amenities");
        Assert.Equal(HttpStatusCode.Unauthorized, amenitiesRes.StatusCode);

        using var invoicesRes = await client.GetAsync("/api/resident/invoices");
        Assert.Equal(HttpStatusCode.Unauthorized, invoicesRes.StatusCode);
    }

    [Fact]
    public async Task ResidentPortalEndpoints_ManagementUser_Returns403Forbidden()
    {
        var email = "mgr.forbidden.resident@apartment.local";
        await EnsureUserAsync(email, "MgrPass@123!", AppRoles.BuildingManager, isActive: true);

        using var client = CreateHttpsClient();
        await LoginAsync(client, email, "MgrPass@123!");

        using var dashboardRes = await client.GetAsync("/api/resident/dashboard");
        Assert.Equal(HttpStatusCode.Forbidden, dashboardRes.StatusCode);
    }

    [Fact]
    public async Task ResidentPortal_FullWorkflow_Dashboard_Amenities_Vehicles_Visitors_Repairs_Complaints_Profile_WorksEndToEnd()
    {
        var email = "vinhomes.resident.e2e@apartment.local";
        var (_, apartmentId, amenityId) = await SeedResidentWithApartmentAndAmenityAsync(email, "Resident@123!");

        using var client = CreateHttpsClient();
        await LoginAsync(client, email, "Resident@123!");

        // 1. Dashboard
        using var dashboardRes = await client.GetAsync("/api/resident/dashboard");
        var dashboardRaw = await dashboardRes.Content.ReadAsStringAsync();
        Assert.True(dashboardRes.StatusCode == HttpStatusCode.OK, $"Dashboard failed: {dashboardRaw}");
        using var dashboardJson = JsonDocument.Parse(dashboardRaw);
        Assert.True(dashboardJson.RootElement.GetProperty("residentId").GetInt32() > 0);
        Assert.Equal("S1.0808", dashboardJson.RootElement.GetProperty("activeApartment").GetProperty("apartmentCode").GetString());

        // 2. Apartment & Household
        using var aptRes = await client.GetAsync("/api/resident/apartment");
        var aptRaw = await aptRes.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, aptRes.StatusCode);
        using var aptJson = JsonDocument.Parse(aptRaw);
        Assert.True(aptJson.RootElement.GetProperty("hasResidentProfile").GetBoolean());

        // 3. Amenities & Booking
        using var amenitiesRes = await client.GetAsync("/api/resident/amenities");
        Assert.Equal(HttpStatusCode.OK, amenitiesRes.StatusCode);

        var csrf = await FetchCsrfAsync(client);
        var tomorrow = DateTime.Today.AddDays(2).ToString("yyyy-MM-dd");

        using var bookReq = new HttpRequestMessage(HttpMethod.Post, "/api/resident/amenities/bookings")
        {
            Content = JsonContent.Create(new
            {
                amenityId,
                bookingDate = tomorrow,
                startTime = "09:00",
                endTime = "10:00",
                numberOfGuests = 4,
                notes = "Đặt sân cầu lông gia đình"
            })
        };
        bookReq.Headers.Add(csrf.HeaderName, csrf.RequestToken);
        using var bookRes = await client.SendAsync(bookReq);
        var bookRaw = await bookRes.Content.ReadAsStringAsync();
        Assert.True(bookRes.StatusCode == HttpStatusCode.OK, $"Booking failed: {bookRaw}");

        // 4. Vehicles & RFID Parking Card
        using var vehReq = new HttpRequestMessage(HttpMethod.Post, "/api/resident/vehicles")
        {
            Content = JsonContent.Create(new
            {
                apartmentId,
                vehicleType = "Ô tô",
                licensePlate = "30K-888.99",
                brandModel = "VinFast VF 8",
                color = "Trắng"
            })
        };
        vehReq.Headers.Add(csrf.HeaderName, csrf.RequestToken);
        using var vehRes = await client.SendAsync(vehReq);
        var vehRaw = await vehRes.Content.ReadAsStringAsync();
        Assert.True(vehRes.StatusCode == HttpStatusCode.OK, $"Vehicle registration failed: {vehRaw}");

        using var vehiclesListRes = await client.GetAsync("/api/resident/vehicles");
        var vehiclesListRaw = await vehiclesListRes.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, vehiclesListRes.StatusCode);
        Assert.Contains("30K-888.99", vehiclesListRaw);

        // 5. Visitors pre-registration
        using var visReq = new HttpRequestMessage(HttpMethod.Post, "/api/resident/visitors")
        {
            Content = JsonContent.Create(new
            {
                apartmentId,
                visitorName = "Nguyễn Văn Khách",
                phoneNumber = "0912345678",
                idCardNumber = "001099001122",
                expectedArrivalDate = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd"),
                purpose = "Thăm gia đình"
            })
        };
        visReq.Headers.Add(csrf.HeaderName, csrf.RequestToken);
        using var visRes = await client.SendAsync(visReq);
        var visRaw = await visRes.Content.ReadAsStringAsync();
        Assert.True(visRes.StatusCode == HttpStatusCode.OK, $"Visitor registration failed: {visRaw}");

        // 6. Repair Request
        using var repReq = new HttpRequestMessage(HttpMethod.Post, "/api/resident/repairs")
        {
            Content = JsonContent.Create(new
            {
                apartmentId,
                title = "Kiểm tra điều hòa phòng khách",
                description = "Điều hòa kêu to và làm mát chậm, nhờ kỹ thuật kiểm tra giúp.",
                priority = RepairPriority.Normal
            })
        };
        repReq.Headers.Add(csrf.HeaderName, csrf.RequestToken);
        using var repRes = await client.SendAsync(repReq);
        var repRaw = await repRes.Content.ReadAsStringAsync();
        Assert.True(repRes.StatusCode == HttpStatusCode.OK, $"Repair creation failed: {repRaw}");

        // 7. Complaint submission
        using var compReq = new HttpRequestMessage(HttpMethod.Post, "/api/resident/complaints")
        {
            Content = JsonContent.Create(new
            {
                apartmentId,
                title = "Góp ý vệ sinh hành lang tầng 8",
                content = "Nhờ ban quản lý tăng cường lau dọn khu vực sảnh thang máy tầng 8.",
                category = "Vệ sinh",
                priority = "Bình thường"
            })
        };
        compReq.Headers.Add(csrf.HeaderName, csrf.RequestToken);
        using var compRes = await client.SendAsync(compReq);
        var compRaw = await compRes.Content.ReadAsStringAsync();
        Assert.True(compRes.StatusCode == HttpStatusCode.OK, $"Complaint creation failed: {compRaw}");

        // 8. Profile fetch & update
        using var profileRes = await client.GetAsync("/api/resident/profile");
        Assert.Equal(HttpStatusCode.OK, profileRes.StatusCode);

        using var updateProfileReq = new HttpRequestMessage(HttpMethod.Put, "/api/resident/profile")
        {
            Content = JsonContent.Create(new
            {
                dateOfBirth = "1990-05-15",
                gender = "Nam",
                address = "Căn S1.0808, Vinhomes",
                emergencyContact = "0988777666"
            })
        };
        updateProfileReq.Headers.Add(csrf.HeaderName, csrf.RequestToken);
        using var updateProfileRes = await client.SendAsync(updateProfileReq);
        var updateProfileRaw = await updateProfileRes.Content.ReadAsStringAsync();
        Assert.True(updateProfileRes.StatusCode == HttpStatusCode.OK, $"Profile update failed: {updateProfileRaw}");
    }

    private async Task LoginAsync(HttpClient client, string email, string password)
    {
        var csrf = await FetchCsrfAsync(client);
        using var loginReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest(email, password, false))
        };
        loginReq.Headers.Add(csrf.HeaderName, csrf.RequestToken);
        using var loginRes = await client.SendAsync(loginReq);
        loginRes.EnsureSuccessStatusCode();
    }

    private static async Task<CsrfTokenResponse> FetchCsrfAsync(HttpClient client)
    {
        using var res = await client.GetAsync("/api/auth/csrf");
        res.EnsureSuccessStatusCode();
        var csrf = await res.Content.ReadFromJsonAsync<CsrfTokenResponse>();
        return csrf!;
    }

    private async Task<ApplicationUser> EnsureUserAsync(string email, string password, string role, bool isActive)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }

        var existing = await userManager.FindByEmailAsync(email);
        if (existing != null)
        {
            existing.IsActive = isActive;
            await userManager.UpdateAsync(existing);
            return existing;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = $"Test {role}",
            PhoneNumber = "0909111222",
            IsActive = isActive
        };

        var createResult = await userManager.CreateAsync(user, password);
        Assert.True(createResult.Succeeded);

        var roleResult = await userManager.AddToRoleAsync(user, role);
        Assert.True(roleResult.Succeeded);
        return user;
    }

    private async Task<(string UserId, int ApartmentId, int AmenityId)> SeedResidentWithApartmentAndAmenityAsync(
        string email,
        string password)
    {
        var user = await EnsureUserAsync(email, password, AppRoles.Resident, isActive: true);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var building = await db.Buildings.FirstOrDefaultAsync(b => b.BuildingCode == "VH-S1");
        if (building == null)
        {
            building = new Building
            {
                BuildingCode = "VH-S1",
                BuildingName = "Vinhomes S1",
                Address = "Khu đô thị Vinhomes",
                NumberOfFloors = 35
            };
            db.Buildings.Add(building);
            await db.SaveChangesAsync();
        }

        var apartment = await db.Apartments.FirstOrDefaultAsync(
            a => a.BuildingId == building.BuildingId && a.ApartmentCode == "S1.0808");
        if (apartment == null)
        {
            apartment = new Apartment
            {
                BuildingId = building.BuildingId,
                ApartmentCode = "S1.0808",
                Floor = 8,
                Area = 82.5m,
                Status = ApartmentStatus.Occupied
            };
            db.Apartments.Add(apartment);
            await db.SaveChangesAsync();
        }

        var resident = await db.Residents.FirstOrDefaultAsync(r => r.UserId == user.Id);
        if (resident == null)
        {
            resident = new Resident
            {
                UserId = user.Id,
                CitizenId = "001090123456",
                DateOfBirth = new DateTime(1990, 5, 15),
                Gender = "Nam",
                Address = "S1.0808 Vinhomes",
                EmergencyContact = "0909888777"
            };
            db.Residents.Add(resident);
            await db.SaveChangesAsync();
        }

        var link = await db.ApartmentResidents.FirstOrDefaultAsync(
            ar => ar.ApartmentId == apartment.ApartmentId && ar.ResidentId == resident.ResidentId && ar.MoveOutDate == null);
        if (link == null)
        {
            link = new ApartmentResident
            {
                ApartmentId = apartment.ApartmentId,
                ResidentId = resident.ResidentId,
                Relationship = "Chủ hộ",
                IsOwner = true,
                MoveInDate = DateTime.Today.AddMonths(-6)
            };
            db.ApartmentResidents.Add(link);
            await db.SaveChangesAsync();
        }

        var amenity = await db.Amenities.FirstOrDefaultAsync(a => a.Name == "Sân Cầu Lông S1");
        if (amenity == null)
        {
            amenity = new Amenity
            {
                Name = "Sân Cầu Lông S1",
                Description = "Sân cầu lông nội khu tiêu chuẩn",
                Location = "Tầng 1 Tòa S1",
                Capacity = 8,
                OpenTime = new TimeSpan(6, 0, 0),
                CloseTime = new TimeSpan(22, 0, 0),
                IsActive = true,
                PricePerHour = 0m
            };
            db.Amenities.Add(amenity);
            await db.SaveChangesAsync();
        }

        return (user.Id, apartment.ApartmentId, amenity.AmenityId);
    }
}
