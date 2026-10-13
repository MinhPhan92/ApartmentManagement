using ApartmentManagement.Common.Security;
using ApartmentManagement.Controllers;
using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ApartmentManagement.Tests;

public sealed class VehicleParkingApiTests
{
    [Fact]
    public async Task Resident_RegistersNormalizedVehicle_ForOwnApartment()
    {
        await using var db = CreateDb();
        Seed(db);
        var controller = new VehiclesController(db, CurrentUser(AppRoles.Resident, "resident-user"));

        var result = await controller.Register(new RegisterVehicleRequest
        {
            ApartmentId = 1, VehicleType = "Xe máy", LicensePlate = " 29a1-123.45 ", BrandModel = " Honda "
        }, default);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(201, created.StatusCode);
        Assert.Equal("29A1-123.45", db.Vehicles.Single().LicensePlate);
        Assert.Equal(1, db.Vehicles.Single().ApartmentId);
    }

    [Fact]
    public async Task Vehicle_InvalidOrDuplicateData_IsRejected()
    {
        await using var db = CreateDb();
        Seed(db);
        db.Vehicles.Add(NewVehicle("29A1-123.45"));
        await db.SaveChangesAsync();
        var controller = new VehiclesController(db, CurrentUser(AppRoles.Resident, "resident-user"));

        var invalid = await controller.Register(new RegisterVehicleRequest
            { ApartmentId = 1, VehicleType = "Xe máy", LicensePlate = "@@" }, default);
        Assert.IsType<ObjectResult>(invalid.Result);
        var duplicate = await controller.Register(new RegisterVehicleRequest
            { ApartmentId = 1, VehicleType = "Xe máy", LicensePlate = "29a1-123.45" }, default);
        Assert.IsType<ConflictObjectResult>(duplicate.Result);
    }

    [Fact]
    public async Task Resident_CannotRegister_ForAnotherApartment()
    {
        await using var db = CreateDb();
        Seed(db);
        db.Apartments.Add(new Apartment { ApartmentId = 2, BuildingId = 1, ApartmentCode = "A-102", Floor = 1, Area = 50 });
        await db.SaveChangesAsync();
        var controller = new VehiclesController(db, CurrentUser(AppRoles.Resident, "resident-user"));

        var result = await controller.Register(new RegisterVehicleRequest
            { ApartmentId = 2, VehicleType = "Xe máy", LicensePlate = "30A-12345" }, default);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task Manager_IssuesNormalizedCard_AndOneVehicleGetsOnlyOneCard()
    {
        await using var db = CreateDb();
        Seed(db);
        var vehicle = NewVehicle("29A1-123.45");
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync();
        var controller = new ParkingCardsController(db, CurrentUser(AppRoles.BuildingManager, "manager"));

        var first = await controller.Issue(new IssueParkingCardRequest
            { VehicleId = vehicle.VehicleId, CardCode = " card-001 " }, default);
        Assert.IsType<CreatedAtActionResult>(first.Result);
        Assert.Equal("CARD-001", db.ParkingCards.Single().CardCode);

        var second = await controller.Issue(new IssueParkingCardRequest
            { VehicleId = vehicle.VehicleId, CardCode = "CARD-002" }, default);
        Assert.IsType<ConflictObjectResult>(second.Result);
    }

    [Fact]
    public void WriteEndpoints_HaveExpectedAuthorizationPolicies()
    {
        var vehicle = typeof(VehiclesController).GetMethod(nameof(VehiclesController.Register))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single();
        var card = typeof(ParkingCardsController).GetMethod(nameof(ParkingCardsController.Issue))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single();
        Assert.Equal(AppPolicies.RequireResident, vehicle.Policy);
        Assert.Equal(AppPolicies.RequireManagement, card.Policy);
    }

    private static ApplicationDbContext CreateDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ICurrentUserService CurrentUser(string role, string userId)
    {
        var mock = new Mock<ICurrentUserService>();
        mock.SetupGet(x => x.UserId).Returns(userId);
        mock.Setup(x => x.IsInRole(It.IsAny<string>())).Returns((string value) => value == role);
        return mock.Object;
    }

    private static void Seed(ApplicationDbContext db)
    {
        var user = new ApplicationUser { Id = "resident-user", UserName = "resident@test", FullName = "Cư dân" };
        var resident = new Resident { ResidentId = 1, UserId = user.Id, User = user, CitizenId = "000000000001" };
        var building = new Building { BuildingId = 1, BuildingCode = "A", BuildingName = "Tòa A", Address = "Test", NumberOfFloors = 10 };
        var apartment = new Apartment { ApartmentId = 1, BuildingId = 1, Building = building, ApartmentCode = "A-101", Floor = 1, Area = 60 };
        db.AddRange(user, resident, building, apartment,
            new ApartmentResident { ApartmentResidentId = 1, ApartmentId = 1, Apartment = apartment, ResidentId = 1, Resident = resident, Relationship = "Chủ hộ", MoveInDate = DateTime.UtcNow.Date });
        db.SaveChanges();
    }

    private static Vehicle NewVehicle(string plate) => new()
    {
        ApartmentId = 1, ResidentId = 1, VehicleType = "Xe máy", LicensePlate = plate, Status = "Đã duyệt"
    };
}
