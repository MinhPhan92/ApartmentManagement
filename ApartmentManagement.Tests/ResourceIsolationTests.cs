using System.Security.Claims;
using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ApartmentManagement.Tests
{
    public class ResourceIsolationTests
    {
        private ApplicationDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        private Mock<UserManager<ApplicationUser>> CreateMockUserManager()
        {
            var store = new Mock<IUserStore<ApplicationUser>>();
            return new Mock<UserManager<ApplicationUser>>(
                store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        }

        private async Task<(Building building, Apartment apt101, Apartment apt102, Resident resA, Resident resB)>
            SeedResidentAAndResidentBAsync(ApplicationDbContext context)
        {
            var building = new Building
            {
                BuildingCode = "BLD-A",
                BuildingName = "Tòa A - Sapphire",
                NumberOfFloors = 20,
                Address = "123 Hoàng Mai, Hà Nội"
            };
            context.Buildings.Add(building);
            await context.SaveChangesAsync();

            var apt101 = new Apartment
            {
                BuildingId = building.BuildingId,
                ApartmentCode = "A101",
                Floor = 1,
                Area = 75.5m,
                Status = "Đang sử dụng"
            };

            var apt102 = new Apartment
            {
                BuildingId = building.BuildingId,
                ApartmentCode = "A102",
                Floor = 1,
                Area = 82.0m,
                Status = "Đang sử dụng"
            };

            context.Apartments.AddRange(apt101, apt102);
            await context.SaveChangesAsync();

            var userA = new ApplicationUser
            {
                Id = "user-a",
                UserName = "resident_a",
                Email = "resident_a@apartment.local",
                FullName = "Nguyễn Văn A"
            };

            var userB = new ApplicationUser
            {
                Id = "user-b",
                UserName = "resident_b",
                Email = "resident_b@apartment.local",
                FullName = "Trần Thị B"
            };

            context.Users.AddRange(userA, userB);
            await context.SaveChangesAsync();

            var resA = new Resident
            {
                UserId = userA.Id,
                CitizenId = "012345678901",
                DateOfBirth = new DateTime(1995, 5, 10),
                Gender = "Nam"
            };

            var resB = new Resident
            {
                UserId = userB.Id,
                CitizenId = "012345678902",
                DateOfBirth = new DateTime(1998, 8, 20),
                Gender = "Nữ"
            };

            context.Residents.AddRange(resA, resB);
            await context.SaveChangesAsync();

            context.ApartmentResidents.AddRange(
                new ApartmentResident
                {
                    ApartmentId = apt101.ApartmentId,
                    ResidentId = resA.ResidentId,
                    Relationship = "Chủ hộ",
                    MoveInDate = new DateTime(2024, 1, 1),
                    MoveOutDate = null,
                    IsOwner = true
                },
                new ApartmentResident
                {
                    ApartmentId = apt102.ApartmentId,
                    ResidentId = resB.ResidentId,
                    Relationship = "Chủ hộ",
                    MoveInDate = new DateTime(2024, 1, 1),
                    MoveOutDate = null,
                    IsOwner = true
                }
            );
            await context.SaveChangesAsync();

            return (building, apt101, apt102, resA, resB);
        }

        [Fact]
        public async Task ResidentA_CanOnlyAccessOwnResidentData_AndCannotAccessResidentBData()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, resA, resB) = await SeedResidentAAndResidentBAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("user-a");
            currentUserServiceMock.Setup(s => s.IsInRole("Resident")).Returns(true);
            currentUserServiceMock.Setup(s => s.IsInRole("SystemAdmin")).Returns(false);
            currentUserServiceMock.Setup(s => s.IsInRole("Manager")).Returns(false);

            var authService = new ResourceAuthorizationService(currentUserServiceMock.Object, context);

            // 1. Can access own resident data
            var canAccessSelf = await authService.CanAccessResidentDataAsync(resA.ResidentId);
            Assert.True(canAccessSelf);

            // 2. Cannot access other resident data (IDOR protection)
            var canAccessOther = await authService.CanAccessResidentDataAsync(resB.ResidentId);
            Assert.False(canAccessOther);
        }

        [Fact]
        public async Task ResidentA_HasAccessOnlyToOwnApartment_AndCannotAccessOtherApartment()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt101, apt102, _, _) = await SeedResidentAAndResidentBAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("user-a");
            currentUserServiceMock.Setup(s => s.IsInRole("Resident")).Returns(true);
            currentUserServiceMock.Setup(s => s.IsInRole("SystemAdmin")).Returns(false);
            currentUserServiceMock.Setup(s => s.IsInRole("Manager")).Returns(false);

            var authService = new ResourceAuthorizationService(currentUserServiceMock.Object, context);

            // Own apartment A101 -> true
            Assert.True(await authService.HasAccessToApartmentAsync(apt101.ApartmentId));

            // Other apartment A102 -> false
            Assert.False(await authService.HasAccessToApartmentAsync(apt102.ApartmentId));
        }

        [Fact]
        public async Task Admin_CanAccessAnyResidentData_AndAnyApartment()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt101, apt102, resA, resB) = await SeedResidentAAndResidentBAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("admin-id");
            currentUserServiceMock.Setup(s => s.IsInRole("SystemAdmin")).Returns(true);
            currentUserServiceMock.Setup(s => s.IsInRole("Manager")).Returns(false);

            var authService = new ResourceAuthorizationService(currentUserServiceMock.Object, context);

            Assert.True(await authService.CanAccessResidentDataAsync(resA.ResidentId));
            Assert.True(await authService.CanAccessResidentDataAsync(resB.ResidentId));
            Assert.True(await authService.HasAccessToApartmentAsync(apt101.ApartmentId));
            Assert.True(await authService.HasAccessToApartmentAsync(apt102.ApartmentId));
        }

        [Fact]
        public async Task ResidentService_GetApartmentsForCurrentResidentAsync_ReturnsOnlyResidingApartments()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt101, _, _, _) = await SeedResidentAAndResidentBAsync(context);

            var httpContext = new DefaultHttpContext();
            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "user-a") };
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

            var httpAccessorMock = new Mock<IHttpContextAccessor>();
            httpAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);

            var loggerMock = new Mock<ILogger<ResidentService>>();
            var residentService = new ResidentService(
                context,
                CreateMockUserManager().Object,
                loggerMock.Object,
                httpAccessorMock.Object);

            var myApartments = await residentService.GetApartmentsForCurrentResidentAsync();
            Assert.Single(myApartments);
            Assert.Equal(apt101.ApartmentId, myApartments[0].ApartmentId);
        }
    }
}
