using System.Net;
using ApartmentManagement.Common.Security;
using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Tests.Helpers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ApartmentManagement.Tests;

public sealed class BillingSecurityIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string ResidentOneId = "billing-resident-one";
    private const string ResidentTwoId = "billing-resident-two";
    private readonly CustomWebApplicationFactory _factory;

    public BillingSecurityIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        SeedInvoices();
    }

    [Fact]
    public async Task Anonymous_MyInvoices_RedirectsToLogin()
    {
        using var client = CreateClient();
        using var response = await client.GetAsync("/MyInvoices");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Identity/Account/Login", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task Accountant_CanOpenBillingAdministration()
    {
        using var client = CreateAuthenticatedClient(AppRoles.Accountant, "billing-accountant");
        using var response = await client.GetAsync("/Billing");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Resident_CannotOpenBillingAdministration()
    {
        using var client = CreateAuthenticatedClient(AppRoles.Resident, ResidentOneId);
        using var response = await client.GetAsync("/Billing");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Resident_SeesOnlyOwnIssuedInvoices()
    {
        using var client = CreateAuthenticatedClient(AppRoles.Resident, ResidentOneId);
        using var response = await client.GetAsync("/MyInvoices");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("BILL-OWN", html);
        Assert.DoesNotContain("BILL-OTHER", html);
        Assert.DoesNotContain("BILL-DRAFT", html);
    }

    [Fact]
    public async Task Resident_CannotReadAnotherResidentsInvoiceById()
    {
        using var client = CreateAuthenticatedClient(AppRoles.Resident, ResidentOneId);
        using var response = await client.GetAsync("/MyInvoices/Details/2");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(AppRoles.Accountant)]
    [InlineData(AppRoles.BuildingManager)]
    [InlineData(AppRoles.SuperAdmin)]
    public async Task BillingStaff_CanOpenPaymentAdministration(string role)
    {
        using var client = CreateAuthenticatedClient(role, "billing-staff");
        using var response = await client.GetAsync("/Payments");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(AppRoles.Resident)]
    [InlineData(AppRoles.Technician)]
    public async Task NonBillingStaff_CannotOpenPaymentAdministration(string role)
    {
        using var client = CreateAuthenticatedClient(role, ResidentOneId);
        using var response = await client.GetAsync("/Payments");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ResidentPaymentCreation_RequiresAntiforgeryToken()
    {
        using var client = CreateAuthenticatedClient(AppRoles.Resident, ResidentOneId);
        using var response = await client.PostAsync(
            "/MyInvoices/CreateSimulatedPayment?id=1",
            new FormUrlEncodedContent([]));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task StaffPaymentConfirmation_RequiresAntiforgeryToken()
    {
        using var client = CreateAuthenticatedClient(AppRoles.Accountant, "billing-accountant");
        using var response = await client.PostAsync(
            "/Payments/Confirm",
            new FormUrlEncodedContent([]));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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

    private void SeedInvoices()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();

        var ownerUser = CreateUser(ResidentOneId);
        var otherUser = CreateUser(ResidentTwoId);
        var manager = CreateUser("billing-manager");
        var building = new Building
        {
            BuildingId = 1, BuildingCode = "BILL-A", BuildingName = "Tòa hóa đơn",
            Address = "Test", NumberOfFloors = 10
        };
        var apartmentOne = new Apartment
        {
            ApartmentId = 1, BuildingId = 1, Building = building, ApartmentCode = "A-101",
            Floor = 1, Area = 55, Status = "Đang sử dụng"
        };
        var apartmentTwo = new Apartment
        {
            ApartmentId = 2, BuildingId = 1, Building = building, ApartmentCode = "A-102",
            Floor = 1, Area = 60, Status = "Đang sử dụng"
        };
        var owner = new Resident
        {
            ResidentId = 1, UserId = ownerUser.Id, User = ownerUser, CitizenId = "111111111"
        };
        var other = new Resident
        {
            ResidentId = 2, UserId = otherUser.Id, User = otherUser, CitizenId = "222222222"
        };
        context.AddRange(ownerUser, otherUser, manager, building, apartmentOne, apartmentTwo, owner, other);
        context.ApartmentInvoices.AddRange(
            MakeInvoice(1, "BILL-OWN", apartmentOne, owner, manager, ApartmentInvoiceStatus.Issued),
            MakeInvoice(2, "BILL-OTHER", apartmentTwo, other, manager, ApartmentInvoiceStatus.Issued),
            MakeInvoice(3, "BILL-DRAFT", apartmentOne, owner, manager, ApartmentInvoiceStatus.Draft));
        context.SaveChanges();
    }

    private static ApartmentInvoice MakeInvoice(
        int id,
        string code,
        Apartment apartment,
        Resident resident,
        ApplicationUser creator,
        ApartmentInvoiceStatus status) => new()
    {
        ApartmentInvoiceId = id,
        InvoiceCode = code,
        ApartmentId = apartment.ApartmentId,
        Apartment = apartment,
        ResidentId = resident.ResidentId,
        Resident = resident,
        BillingYear = 2026,
        BillingMonth = 8,
        DueDate = new DateTime(2026, 9, 10),
        ApartmentArea = apartment.Area,
        TotalAmount = 100000,
        Status = status,
        CreatedAtUtc = DateTime.UtcNow,
        CreatedByUserId = creator.Id,
        CreatedByUser = creator,
        IssuedAtUtc = status == ApartmentInvoiceStatus.Issued ? DateTime.UtcNow : null,
        Lines =
        [
            new ApartmentInvoiceLine
            {
                ChargeType = FeeChargeType.Management,
                Description = "Phí quản lý",
                Quantity = apartment.Area,
                UnitRate = 1000,
                Amount = 100000
            }
        ]
    };

    private static ApplicationUser CreateUser(string id) => new()
    {
        Id = id,
        UserName = $"{id}@test.local",
        NormalizedUserName = $"{id}@TEST.LOCAL".ToUpperInvariant(),
        Email = $"{id}@test.local",
        NormalizedEmail = $"{id}@TEST.LOCAL".ToUpperInvariant(),
        FullName = id,
        IsActive = true,
        EmailConfirmed = true
    };
}
