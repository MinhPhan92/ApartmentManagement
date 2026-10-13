using System.Reflection;
using ApartmentManagement.Controllers;
using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using ApartmentManagement.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ApartmentManagement.Tests
{
    public class VehicleModuleTests
    {
        private ApplicationDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        private async Task<(Resident resA, Resident resB, Vehicle vA, Vehicle vB)> SeedDataAsync(ApplicationDbContext context)
        {
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

            var building = new Building { BuildingCode = "A", BuildingName = "Tòa A", Address = "Test", NumberOfFloors = 10 };
            var apartment = new Apartment { Building = building, ApartmentCode = "A-101", Floor = 1, Area = 60 };
            context.AddRange(building, apartment);
            context.ApartmentResidents.AddRange(
                new ApartmentResident { Apartment = apartment, ResidentId = resA.ResidentId, Relationship = "Chủ hộ", MoveInDate = DateTime.UtcNow.Date },
                new ApartmentResident { Apartment = apartment, ResidentId = resB.ResidentId, Relationship = "Thành viên", MoveInDate = DateTime.UtcNow.Date });
            await context.SaveChangesAsync();

            var vA = new Vehicle
            {
                ResidentId = resA.ResidentId,
                ApartmentId = apartment.ApartmentId,
                VehicleType = "Xe máy",
                LicensePlate = "29A-11111",
                BrandModel = "Honda Vision",
                Color = "Trắng",
                Status = "Chờ duyệt",
                CreatedAt = DateTime.UtcNow
            };

            var vB = new Vehicle
            {
                ResidentId = resB.ResidentId,
                ApartmentId = apartment.ApartmentId,
                VehicleType = "Ô tô",
                LicensePlate = "30E-22222",
                BrandModel = "Mazda 3",
                Color = "Đỏ",
                Status = "Chờ duyệt",
                CreatedAt = DateTime.UtcNow
            };

            context.Vehicles.AddRange(vA, vB);
            await context.SaveChangesAsync();

            return (resA, resB, vA, vB);
        }

        // 1. Resident A xem vehicle của A -> PASS
        [Fact]
        public async Task Test1_ResidentA_CanViewOwnVehicle()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, vA, _) = await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("user-a");
            currentUserServiceMock.Setup(s => s.IsInRole("Resident")).Returns(true);
            currentUserServiceMock.Setup(s => s.IsInRole("SystemAdmin")).Returns(false);
            currentUserServiceMock.Setup(s => s.IsInRole("Manager")).Returns(false);

            var vehicleService = new VehicleService(context, currentUserServiceMock.Object);

            var vehicle = await vehicleService.GetVehicleByIdAsync(vA.VehicleId);
            Assert.NotNull(vehicle);
            Assert.Equal("29A-11111", vehicle.LicensePlate);

            var list = await vehicleService.GetVehiclesAsync();
            Assert.Single(list);
            Assert.Equal("29A-11111", list[0].LicensePlate);
        }

        // 2. Resident A không xem vehicle của B -> 404/null
        [Fact]
        public async Task Test2_ResidentA_CannotViewResidentB_Vehicle()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, vB) = await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("user-a");
            currentUserServiceMock.Setup(s => s.IsInRole("Resident")).Returns(true);
            currentUserServiceMock.Setup(s => s.IsInRole("SystemAdmin")).Returns(false);
            currentUserServiceMock.Setup(s => s.IsInRole("Manager")).Returns(false);

            var vehicleService = new VehicleService(context, currentUserServiceMock.Object);

            // Service level returns null
            var result = await vehicleService.GetVehicleByIdAsync(vB.VehicleId);
            Assert.Null(result);

            // Controller level returns 404 NotFound
            var controller = new VehicleController(vehicleService);
            var actionResult = await controller.Details(vB.VehicleId);
            Assert.IsType<NotFoundResult>(actionResult);
        }

        // 3. Resident B không xem vehicle của A -> 404/null
        [Fact]
        public async Task Test3_ResidentB_CannotViewResidentA_Vehicle()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, vA, _) = await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("user-b");
            currentUserServiceMock.Setup(s => s.IsInRole("Resident")).Returns(true);
            currentUserServiceMock.Setup(s => s.IsInRole("SystemAdmin")).Returns(false);
            currentUserServiceMock.Setup(s => s.IsInRole("Manager")).Returns(false);

            var vehicleService = new VehicleService(context, currentUserServiceMock.Object);

            var result = await vehicleService.GetVehicleByIdAsync(vA.VehicleId);
            Assert.Null(result);

            var controller = new VehicleController(vehicleService);
            var actionResult = await controller.Details(vA.VehicleId);
            Assert.IsType<NotFoundResult>(actionResult);
        }

        // 4. Manager xem vehicle của A và B -> PASS
        [Fact]
        public async Task Test4_Manager_CanViewAnyVehicle()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, vA, vB) = await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("manager-id");
            currentUserServiceMock.Setup(s => s.IsInRole("BuildingManager")).Returns(true);
            currentUserServiceMock.Setup(s => s.IsInRole("SuperAdmin")).Returns(false);

            var vehicleService = new VehicleService(context, currentUserServiceMock.Object);

            var vehicleA = await vehicleService.GetVehicleByIdAsync(vA.VehicleId);
            var vehicleB = await vehicleService.GetVehicleByIdAsync(vB.VehicleId);

            Assert.NotNull(vehicleA);
            Assert.NotNull(vehicleB);
            Assert.Equal("29A-11111", vehicleA.LicensePlate);
            Assert.Equal("30E-22222", vehicleB.LicensePlate);

            var allVehicles = await vehicleService.GetVehiclesAsync();
            Assert.Equal(2, allVehicles.Count);
        }

        // 5. Resident A tạo vehicle cho chính mình -> PASS (Tự suy ra ResidentId từ UserId, không nhận ResidentId từ client)
        [Fact]
        public async Task Test5_ResidentA_RegistersVehicle_AutomaticallyBoundToResidentA()
        {
            using var context = CreateInMemoryDbContext();
            var (resA, _, _, _) = await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("user-a");

            var vehicleService = new VehicleService(context, currentUserServiceMock.Object);

            var model = new VehicleRegisterViewModel
            {
                VehicleType = "Xe máy",
                LicensePlate = "29C-99999",
                BrandModel = "Yamaha NVX",
                Color = "Đen"
            };

            var (success, error) = await vehicleService.RegisterVehicleAsync(model);
            Assert.True(success);
            Assert.Null(error);

            var created = await context.Vehicles.FirstOrDefaultAsync(v => v.LicensePlate == "29C-99999");
            Assert.NotNull(created);
            Assert.Equal(resA.ResidentId, created.ResidentId);
            Assert.Equal("Chờ duyệt", created.Status);
        }

        // 6. Resident không thể tự duyệt phương tiện (RBAC: Controller action Approve/Reject chỉ cho phép Manager/SystemAdmin)
        [Fact]
        public void Test6_Resident_CannotApproveVehicle_ThroughController()
        {
            // Verify Controller methods have [Authorize(Roles = "SystemAdmin,Manager")]
            var approveMethod = typeof(VehicleController).GetMethod(nameof(VehicleController.Approve));
            Assert.NotNull(approveMethod);

            var authAttr = approveMethod.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(authAttr);
            Assert.Contains("BuildingManager", authAttr.Roles);
            Assert.Contains("SuperAdmin", authAttr.Roles);
            Assert.DoesNotContain("Resident", authAttr.Roles);

            var rejectMethod = typeof(VehicleController).GetMethod(nameof(VehicleController.Reject));
            Assert.NotNull(rejectMethod);

            var rejectAuthAttr = rejectMethod.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(rejectAuthAttr);
            Assert.Contains("BuildingManager", rejectAuthAttr.Roles);
            Assert.DoesNotContain("Resident", rejectAuthAttr.Roles);
        }

        // 7. Manager có thể duyệt / từ chối phương tiện
        [Fact]
        public async Task Test7_Manager_CanApproveOrRejectVehicle()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, vA, vB) = await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("manager-id");
            currentUserServiceMock.Setup(s => s.IsInRole("BuildingManager")).Returns(true);

            var vehicleService = new VehicleService(context, currentUserServiceMock.Object);

            // 1. Phê duyệt xe A
            var (approveSuccess, approveErr) = await vehicleService.ApproveVehicleAsync(vA.VehicleId, "Đã cấp thẻ từ #01");
            Assert.True(approveSuccess);
            Assert.Null(approveErr);

            var updatedA = await context.Vehicles.FindAsync(vA.VehicleId);
            Assert.NotNull(updatedA);
            Assert.Equal("Đã duyệt", updatedA.Status);
            Assert.Equal("Đã cấp thẻ từ #01", updatedA.ReviewNotes);
            Assert.NotNull(updatedA.ReviewedAt);

            // 2. Từ chối xe B kèm lý do
            var (rejectSuccess, rejectErr) = await vehicleService.RejectVehicleAsync(vB.VehicleId, "Biển số xe không trùng giấy tờ");
            Assert.True(rejectSuccess);
            Assert.Null(rejectErr);

            var updatedB = await context.Vehicles.FindAsync(vB.VehicleId);
            Assert.NotNull(updatedB);
            Assert.Equal("Từ chối", updatedB.Status);
            Assert.Equal("Biển số xe không trùng giấy tờ", updatedB.ReviewNotes);
        }

        // 8. Resident chỉ có thể hủy đăng ký khi xe đang 'Chờ duyệt'
        [Fact]
        public async Task Test8_Resident_CanOnlyCancelPendingVehicle()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, vA, _) = await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("user-a");

            var vehicleService = new VehicleService(context, currentUserServiceMock.Object);

            // 1. Hủy khi đang 'Chờ duyệt' -> Thành công
            var (cancelSuccess, cancelErr) = await vehicleService.CancelRegistrationAsync(vA.VehicleId);
            Assert.True(cancelSuccess);
            Assert.Null(cancelErr);

            var updated = await context.Vehicles.FindAsync(vA.VehicleId);
            Assert.NotNull(updated);
            Assert.Equal("Đã hủy", updated.Status);

            // 2. Thử hủy lại khi đã ở trạng thái 'Đã hủy' -> Thất bại
            var (reCancelSuccess, reCancelErr) = await vehicleService.CancelRegistrationAsync(vA.VehicleId);
            Assert.False(reCancelSuccess);
            Assert.NotNull(reCancelErr);

            // 3. Nếu xe đã được duyệt 'Đã duyệt' -> Thất bại
            updated.Status = "Đã duyệt";
            await context.SaveChangesAsync();

            var (cancelApprovedSuccess, cancelApprovedErr) = await vehicleService.CancelRegistrationAsync(vA.VehicleId);
            Assert.False(cancelApprovedSuccess);
            Assert.NotNull(cancelApprovedErr);
        }

        // Bonus Test: Chống đăng ký trùng biển số đang còn hiệu lực (Chờ duyệt / Đã duyệt)
        [Fact]
        public async Task DuplicateActiveLicensePlate_IsRejected()
        {
            using var context = CreateInMemoryDbContext();
            await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("user-a");

            var vehicleService = new VehicleService(context, currentUserServiceMock.Object);

            // Cố tình đăng ký lại biển số đang 'Chờ duyệt' của xe A
            var model = new VehicleRegisterViewModel
            {
                VehicleType = "Xe máy",
                LicensePlate = "29A-11111" // Already pending
            };

            var (success, error) = await vehicleService.RegisterVehicleAsync(model);
            Assert.False(success);
            Assert.Contains("đang có đăng ký đang chờ duyệt hoặc đã được duyệt", error);
        }

        [Fact]
        public async Task DuplicateApprovedLicensePlate_IsRejected()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, vA, _) = await SeedDataAsync(context);
            vA.Status = "Đã duyệt";
            await context.SaveChangesAsync();

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("user-a");

            var vehicleService = new VehicleService(context, currentUserServiceMock.Object);

            var model = new VehicleRegisterViewModel
            {
                VehicleType = "Xe máy",
                LicensePlate = vA.LicensePlate
            };

            var (success, error) = await vehicleService.RegisterVehicleAsync(model);
            Assert.False(success);
            Assert.Contains("đang có đăng ký đang chờ duyệt hoặc đã được duyệt", error);
        }

        [Fact]
        public async Task RejectedLicensePlate_CannotBeReRegistered()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, vA, _) = await SeedDataAsync(context);
            vA.Status = "Từ chối";
            await context.SaveChangesAsync();

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("user-a");

            var vehicleService = new VehicleService(context, currentUserServiceMock.Object);

            var model = new VehicleRegisterViewModel
            {
                VehicleType = "Xe máy",
                LicensePlate = vA.LicensePlate,
                BrandModel = "Honda Vision",
                Color = "Trắng"
            };

            var (success, error) = await vehicleService.RegisterVehicleAsync(model);
            Assert.False(success);
            Assert.NotNull(error);

            var vehicles = await context.Vehicles.Where(v => v.LicensePlate == vA.LicensePlate).ToListAsync();
            Assert.Single(vehicles);
        }

        [Fact]
        public async Task CancelledLicensePlate_CannotBeReRegistered()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, vA, _) = await SeedDataAsync(context);
            vA.Status = "Đã hủy";
            await context.SaveChangesAsync();

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("user-a");

            var vehicleService = new VehicleService(context, currentUserServiceMock.Object);

            var model = new VehicleRegisterViewModel
            {
                VehicleType = "Xe máy",
                LicensePlate = vA.LicensePlate,
                BrandModel = "Honda Lead",
                Color = "Vàng"
            };

            var (success, error) = await vehicleService.RegisterVehicleAsync(model);
            Assert.False(success);
            Assert.NotNull(error);

            var vehicles = await context.Vehicles.Where(v => v.LicensePlate == vA.LicensePlate).ToListAsync();
            Assert.Single(vehicles);
        }
    }
}
