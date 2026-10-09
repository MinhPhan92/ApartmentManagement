using System.Net;
using ApartmentManagement.Common.Security;
using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ApartmentManagement.Tests;

public sealed class IncidentTicketIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string ResidentUserId = "ticket-resident";
    private const string OtherResidentUserId = "ticket-other-resident";
    private const string TechnicianUserId = "ticket-technician";
    private readonly CustomWebApplicationFactory _factory;

    public IncidentTicketIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        SeedTestData();
    }

    [Fact]
    public async Task Anonymous_MyIncidents_RedirectsToLogin()
    {
        using var client = CreateClient();
        using var response = await client.GetAsync("/MyIncidents");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Identity/Account/Login", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task NonResident_CannotOpenMyIncidents()
    {
        using var client = CreateAuthenticatedClient(AppRoles.Technician, TechnicianUserId);
        using var response = await client.GetAsync("/MyIncidents");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Resident_CannotReadAnotherResidentsTicket()
    {
        using var client = CreateAuthenticatedClient(AppRoles.Resident, ResidentUserId);
        using var response = await client.GetAsync("/MyIncidents/Details/2");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Resident_CannotForgeOwnerThroughQueryString()
    {
        using var client = CreateAuthenticatedClient(AppRoles.Resident, ResidentUserId);
        using var response = await client.GetAsync("/MyIncidents/Details/2?userId=" + OtherResidentUserId);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Resident_CreateRequiresAntiforgeryToken()
    {
        using var client = CreateAuthenticatedClient(AppRoles.Resident, ResidentUserId);
        using var content = CreateTicketForm(token: null);
        using var response = await client.PostAsync("/MyIncidents/Create", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Resident_CannotCreateTicketForAnotherApartment()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ApartmentManagement.Services.IIncidentTicketService>();
        var result = await service.CreateAsync(ResidentUserId, new ApartmentManagement.Services.IncidentTicketInput(
            2, IncidentCategory.Water, "Phòng bên cạnh", "Sự cố căn hộ khác"));
        Assert.False(result.Success);
        Assert.Contains("đang cư trú", result.ErrorMessage!);
        Assert.Equal(3, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .IncidentTickets.CountAsync());
    }

    [Fact]
    public async Task Resident_CanCreateTicketForActiveApartment()
    {
        using var client = CreateAuthenticatedClient(AppRoles.Resident, ResidentUserId);
        var (_, token) = await AntiforgeryHelper.ExtractAsync(client, "/MyIncidents/Create");
        using var content = CreateTicketForm(token);
        using var response = await client.PostAsync("/MyIncidents/Create", content);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var ticket = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .IncidentTickets.SingleAsync(x => x.Title == "Rò rỉ nước");
        Assert.Equal(1, ticket.ResidentId);
        Assert.Equal(1, ticket.ApartmentId);
    }

    [Fact]
    public async Task StaffQueue_OnlyAllowsConfiguredTicketStaff()
    {
        using var client = CreateAuthenticatedClient(AppRoles.Accountant, "ticket-accountant");
        using var response = await client.GetAsync("/IncidentQueue");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Technician_CanClaimAndCompleteTicket()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ApartmentManagement.Services.IIncidentTicketService>();
        var initialVersion = await ReadRowVersionAsync(1);
        var claim = await service.ClaimAsync(1, TechnicianUserId, initialVersion);
        Assert.True(claim.Success, claim.ErrorMessage);
        var complete = await service.CompleteAsync(1, TechnicianUserId, initialVersion);
        Assert.True(complete.Success, complete.ErrorMessage);

        var ticket = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .IncidentTickets.Include(x => x.StatusHistory).SingleAsync(x => x.IncidentTicketId == 1);
        Assert.Equal(IncidentTicketStatus.Completed, ticket.Status);
        Assert.Equal(TechnicianUserId, ticket.AssignedToUserId);
        Assert.Equal(3, ticket.StatusHistory.Count);
    }

    [Fact]
    public async Task Resident_CanRateCompletedTicketOnlyOnce()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ApartmentManagement.Services.IIncidentTicketService>();
        var rowVersion = await ReadRowVersionAsync(3);
        var first = await service.RateAsync(3, ResidentUserId, 5, "Xử lý nhanh", rowVersion);
        Assert.True(first.Success, first.ErrorMessage);
        var second = await service.RateAsync(3, ResidentUserId, 4, null, rowVersion);
        Assert.False(second.Success);

        var ticket = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .IncidentTickets.SingleAsync(x => x.IncidentTicketId == 3);
        Assert.Equal((byte)5, ticket.Rating);
        Assert.Equal("Xử lý nhanh", ticket.ResidentFeedback);
    }

    [Fact]
    public async Task TicketWorkflow_DoesNotChangeOccupancy()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var occupancy = await context.ApartmentResidents.SingleAsync(x => x.ResidentId == 1);
        Assert.Null(occupancy.MoveOutDate);
        Assert.Equal("Đang sử dụng", (await context.Apartments.FindAsync(1))!.Status);
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

    private static FormUrlEncodedContent CreateTicketForm(string? token, int apartmentId = 1)
    {
        var values = new Dictionary<string, string>
        {
            ["ApartmentId"] = apartmentId.ToString(),
            ["Category"] = nameof(IncidentCategory.Water),
            ["Title"] = "Rò rỉ nước",
            ["Description"] = "Nước rò rỉ dưới bồn rửa",
            ["ResidentId"] = "2"
        };
        if (token != null) values["__RequestVerificationToken"] = token;
        return new FormUrlEncodedContent(values);
    }

    private async Task<byte[]> ReadRowVersionAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .IncidentTickets.AsNoTracking().SingleAsync(x => x.IncidentTicketId == id)).RowVersion;
    }

    private void SeedTestData()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();

        foreach (var role in AppRoles.AllRoles)
        {
            context.Roles.Add(new IdentityRole(role)
            {
                Id = $"role-{role.ToLowerInvariant()}",
                NormalizedName = role.ToUpperInvariant()
            });
        }
        var building = new Building
        {
            BuildingId = 1, BuildingCode = "INC-A", BuildingName = "Tòa A",
            Address = "Địa chỉ thử", NumberOfFloors = 10
        };
        var apartmentOne = new Apartment
        {
            ApartmentId = 1, BuildingId = 1, Building = building, ApartmentCode = "A-101",
            Floor = 1, Area = 60, Status = "Đang sử dụng"
        };
        var apartmentTwo = new Apartment
        {
            ApartmentId = 2, BuildingId = 1, Building = building, ApartmentCode = "A-102",
            Floor = 1, Area = 65, Status = "Đang sử dụng"
        };
        var residentUser = CreateUser(ResidentUserId, "ticket-resident@test.local");
        var otherResidentUser = CreateUser(OtherResidentUserId, "ticket-other@test.local");
        var technicianUser = CreateUser(TechnicianUserId, "ticket-tech@test.local");
        var resident = new Resident { ResidentId = 1, UserId = ResidentUserId, User = residentUser, CitizenId = "100000000001" };
        var otherResident = new Resident { ResidentId = 2, UserId = OtherResidentUserId, User = otherResidentUser, CitizenId = "100000000002" };
        context.AddRange(building, apartmentOne, apartmentTwo, residentUser, otherResidentUser, technicianUser, resident, otherResident);
        context.ApartmentResidents.Add(new ApartmentResident
        {
            ApartmentId = 1, ResidentId = 1, Apartment = apartmentOne, Resident = resident,
            Relationship = "Chủ hộ", MoveInDate = DateTime.Today, IsOwner = true
        });
        context.IncidentTickets.AddRange(
            MakeTicket(1, "INC-TEST-0001", apartmentOne, resident, IncidentTicketStatus.Submitted),
            MakeTicket(2, "INC-TEST-0002", apartmentOne, otherResident, IncidentTicketStatus.Submitted),
            MakeTicket(3, "INC-TEST-0003", apartmentOne, resident, IncidentTicketStatus.Completed));
        context.IncidentTickets.Local.Single(x => x.IncidentTicketId == 3).RowVersion = [2];
        context.IncidentTickets.Local.Single(x => x.IncidentTicketId == 1).RowVersion = [1];
        context.SaveChanges();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True(userManager.AddToRoleAsync(residentUser, AppRoles.Resident).GetAwaiter().GetResult().Succeeded);
        Assert.True(userManager.AddToRoleAsync(otherResidentUser, AppRoles.Resident).GetAwaiter().GetResult().Succeeded);
        Assert.True(userManager.AddToRoleAsync(technicianUser, AppRoles.Technician).GetAwaiter().GetResult().Succeeded);
    }

    private static IncidentTicket MakeTicket(
        int id, string code, Apartment apartment, Resident resident, IncidentTicketStatus status) => new()
    {
        IncidentTicketId = id,
        TicketCode = code,
        ApartmentId = apartment.ApartmentId,
        Apartment = apartment,
        ResidentId = resident.ResidentId,
        Resident = resident,
        Category = IncidentCategory.Water,
        Title = $"Sự cố {id}",
        Description = "Mô tả sự cố kiểm thử",
        Status = status,
        CreatedAtUtc = DateTime.UtcNow,
        CompletedAtUtc = status == IncidentTicketStatus.Completed ? DateTime.UtcNow : null,
        RowVersion = [1],
        StatusHistory =
        [
            new IncidentTicketStatusHistory
            {
                PreviousStatus = null,
                NewStatus = IncidentTicketStatus.Submitted,
                ChangedAtUtc = DateTime.UtcNow,
                ChangedByUserId = resident.UserId,
                ChangedByUser = resident.User
            }
        ]
    };

    private static ApplicationUser CreateUser(string id, string email) => new()
    {
        Id = id, UserName = email, NormalizedUserName = email.ToUpperInvariant(),
        Email = email, NormalizedEmail = email.ToUpperInvariant(),
        FullName = email, EmailConfirmed = true, IsActive = true
    };
}
