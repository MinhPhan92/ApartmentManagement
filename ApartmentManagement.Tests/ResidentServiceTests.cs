using ApartmentManagement.Common.Security;
using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ApartmentManagement.Tests
{
    public class ResidentServiceTests
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
            var mock = new Mock<UserManager<ApplicationUser>>(
                store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

            mock.Setup(x => x.DeleteAsync(It.IsAny<ApplicationUser>()))
                .ReturnsAsync(IdentityResult.Success);

            mock.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);

            mock.Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);

            mock.Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync((ApplicationUser?)null);

            return mock;
        }

        private async Task<(Building building, Apartment apt, Resident res, ApplicationUser user)>
            SeedResidentAsync(ApplicationDbContext context, string suffix = "1")
        {
            var building = new Building
            {
                BuildingCode = $"BLD-{suffix}",
                BuildingName = $"Tòa Nhà {suffix}",
                NumberOfFloors = 10
            };
            context.Buildings.Add(building);
            await context.SaveChangesAsync();

            var apt = new Apartment
            {
                BuildingId = building.BuildingId,
                ApartmentCode = $"APT-{suffix}",
                Floor = 1,
                Area = 60m,
                Status = ApartmentStatus.Occupied
            };
            context.Apartments.Add(apt);
            await context.SaveChangesAsync();

            var user = new ApplicationUser
            {
                Id = $"user-{suffix}",
                UserName = $"res{suffix}@apartment.local",
                Email = $"res{suffix}@apartment.local",
                FullName = $"Cư Dân {suffix}"
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var res = new Resident
            {
                UserId = user.Id,
                CitizenId = $"01234567890{suffix.Substring(0, 1)}",
                User = user
            };
            context.Residents.Add(res);
            await context.SaveChangesAsync();

            return (building, apt, res, user);
        }

        #region GetById & ID validation

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public async Task GetByIdAsync_WhenIdNonPositive_ReturnsNull(int invalidId)
        {
            using var context = CreateInMemoryDbContext();
            var service = new ResidentService(
                context, CreateMockUserManager().Object, NullLogger<ResidentService>.Instance, Mock.Of<IHttpContextAccessor>());

            var result = await service.GetByIdAsync(invalidId);
            Assert.Null(result);
        }

        #endregion

        #region DeleteAsync Tests

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task DeleteAsync_WhenIdNonPositive_ReturnsFalseWithNotFoundError(int invalidId)
        {
            using var context = CreateInMemoryDbContext();
            var userManagerMock = CreateMockUserManager();
            var service = new ResidentService(
                context, userManagerMock.Object, NullLogger<ResidentService>.Instance, Mock.Of<IHttpContextAccessor>());

            var (success, error) = await service.DeleteAsync(invalidId);
            Assert.False(success);
            Assert.Equal("Không tìm thấy cư dân.", error);
            userManagerMock.Verify(x => x.DeleteAsync(It.IsAny<ApplicationUser>()), Times.Never);
        }

        [Fact]
        public async Task DeleteAsync_WhenResidentDoesNotExist_ReturnsFalseWithNotFoundError()
        {
            using var context = CreateInMemoryDbContext();
            var userManagerMock = CreateMockUserManager();
            var service = new ResidentService(
                context, userManagerMock.Object, NullLogger<ResidentService>.Instance, Mock.Of<IHttpContextAccessor>());

            var (success, error) = await service.DeleteAsync(9999);
            Assert.False(success);
            Assert.Equal("Không tìm thấy cư dân.", error);
            userManagerMock.Verify(x => x.DeleteAsync(It.IsAny<ApplicationUser>()), Times.Never);
        }

        [Fact]
        public async Task DeleteAsync_WhenResidentHasActiveOccupancy_ReturnsFalseAndPreservesRecords()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt, res, _) = await SeedResidentAsync(context, "1");

            context.ApartmentResidents.Add(new ApartmentResident
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = res.ResidentId,
                Relationship = "Chủ hộ",
                MoveInDate = DateTime.Today.AddMonths(-1),
                MoveOutDate = null,
                IsOwner = true
            });
            await context.SaveChangesAsync();

            var userManagerMock = CreateMockUserManager();
            var service = new ResidentService(
                context, userManagerMock.Object, NullLogger<ResidentService>.Instance, Mock.Of<IHttpContextAccessor>());

            var (success, error) = await service.DeleteAsync(res.ResidentId);
            Assert.False(success);
            Assert.Equal("Không thể xóa cư dân đang có lịch sử cư trú. Hãy xử lý hồ sơ cư trú trước.", error);
            userManagerMock.Verify(x => x.DeleteAsync(It.IsAny<ApplicationUser>()), Times.Never);

            // Verify resident still exists
            Assert.NotNull(await context.Residents.FindAsync(res.ResidentId));
        }

        [Fact]
        public async Task DeleteAsync_WhenResidentHasHistoricalOccupancy_ReturnsFalseAndPreservesHistory()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt, res, _) = await SeedResidentAsync(context, "2");

            context.ApartmentResidents.Add(new ApartmentResident
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = res.ResidentId,
                Relationship = "Khách thuê cũ",
                MoveInDate = DateTime.Today.AddYears(-1),
                MoveOutDate = DateTime.Today.AddMonths(-2),
                IsOwner = false
            });
            await context.SaveChangesAsync();

            var userManagerMock = CreateMockUserManager();
            var service = new ResidentService(
                context, userManagerMock.Object, NullLogger<ResidentService>.Instance, Mock.Of<IHttpContextAccessor>());

            var (success, error) = await service.DeleteAsync(res.ResidentId);
            Assert.False(success);
            Assert.Equal("Không thể xóa cư dân đang có lịch sử cư trú. Hãy xử lý hồ sơ cư trú trước.", error);

            // Verify resident and occupancy still exist
            Assert.NotNull(await context.Residents.FindAsync(res.ResidentId));
            Assert.Equal(1, await context.ApartmentResidents.CountAsync(x => x.ResidentId == res.ResidentId));
        }

        [Fact]
        public async Task DeleteAsync_WhenResidentHasContractParties_ReturnsFalseAndPreservesContractParty()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt, res, user) = await SeedResidentAsync(context, "3");

            var contract = new ApartmentContract
            {
                ApartmentId = apt.ApartmentId,
                StartDate = DateTime.Today,
                MonthlyRent = 10000000,
                DepositAmount = 20000000,
                Status = ApartmentContractStatus.Active,
                CreatedByUserId = user.Id
            };
            context.ApartmentContracts.Add(contract);
            await context.SaveChangesAsync();

            context.ContractParties.Add(new ContractParty
            {
                ApartmentContractId = contract.ApartmentContractId,
                ResidentId = res.ResidentId,
                Role = ContractPartyRole.Lessee
            });
            await context.SaveChangesAsync();

            var userManagerMock = CreateMockUserManager();
            var service = new ResidentService(
                context, userManagerMock.Object, NullLogger<ResidentService>.Instance, Mock.Of<IHttpContextAccessor>());

            var (success, error) = await service.DeleteAsync(res.ResidentId);
            Assert.False(success);
            Assert.Contains("Không thể xóa cư dân đã phát sinh dữ liệu liên kết", error);
            userManagerMock.Verify(x => x.DeleteAsync(It.IsAny<ApplicationUser>()), Times.Never);

            // Verify contract party remains intact
            Assert.True(await context.ContractParties.AnyAsync(x => x.ResidentId == res.ResidentId));
            Assert.NotNull(await context.Residents.FindAsync(res.ResidentId));
        }

        [Fact]
        public async Task DeleteAsync_WhenResidentHasApartmentInvoices_ReturnsFalseAndPreservesInvoices()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt, res, user) = await SeedResidentAsync(context, "4");

            context.ApartmentInvoices.Add(new ApartmentInvoice
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = res.ResidentId,
                InvoiceCode = "INV-RES-01",
                BillingYear = 2026,
                BillingMonth = 10,
                DueDate = DateTime.Today.AddDays(15),
                ApartmentArea = 60m,
                TotalAmount = 500000,
                Status = ApartmentInvoiceStatus.Issued,
                CreatedByUserId = user.Id
            });
            await context.SaveChangesAsync();

            var userManagerMock = CreateMockUserManager();
            var service = new ResidentService(
                context, userManagerMock.Object, NullLogger<ResidentService>.Instance, Mock.Of<IHttpContextAccessor>());

            var (success, error) = await service.DeleteAsync(res.ResidentId);
            Assert.False(success);
            Assert.Contains("Không thể xóa cư dân đã phát sinh dữ liệu liên kết", error);

            // Verify invoice remains intact
            Assert.True(await context.ApartmentInvoices.AnyAsync(x => x.ResidentId == res.ResidentId));
            Assert.NotNull(await context.Residents.FindAsync(res.ResidentId));
        }

        [Fact]
        public async Task DeleteAsync_WhenResidentHasVehicles_ReturnsFalseAndPreservesVehicles()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt, res, _) = await SeedResidentAsync(context, "5");

            context.Vehicles.Add(new Vehicle
            {
                ResidentId = res.ResidentId,
                ApartmentId = apt.ApartmentId,
                LicensePlate = "29A-12345",
                VehicleType = "Xe máy",
                Status = "Hoạt động"
            });
            await context.SaveChangesAsync();

            var userManagerMock = CreateMockUserManager();
            var service = new ResidentService(
                context, userManagerMock.Object, NullLogger<ResidentService>.Instance, Mock.Of<IHttpContextAccessor>());

            var (success, error) = await service.DeleteAsync(res.ResidentId);
            Assert.False(success);
            Assert.Contains("Không thể xóa cư dân đã phát sinh dữ liệu liên kết", error);

            // Verify vehicle remains intact
            Assert.True(await context.Vehicles.AnyAsync(x => x.ResidentId == res.ResidentId));
            Assert.NotNull(await context.Residents.FindAsync(res.ResidentId));
        }

        [Fact]
        public async Task DeleteAsync_WhenResidentHasComplaints_ReturnsFalseAndPreservesComplaints()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt, res, _) = await SeedResidentAsync(context, "6");

            context.Complaints.Add(new Complaint
            {
                ResidentId = res.ResidentId,
                ApartmentId = apt.ApartmentId,
                Title = "Phản ánh tiếng ồn",
                Content = "Nội dung phản ánh tiếng ồn ban đêm",
                Category = "Tiếng ồn",
                Status = "Chờ tiếp nhận"
            });
            await context.SaveChangesAsync();

            var userManagerMock = CreateMockUserManager();
            var service = new ResidentService(
                context, userManagerMock.Object, NullLogger<ResidentService>.Instance, Mock.Of<IHttpContextAccessor>());

            var (success, error) = await service.DeleteAsync(res.ResidentId);
            Assert.False(success);
            Assert.Contains("Không thể xóa cư dân đã phát sinh dữ liệu liên kết", error);

            // Verify complaint remains intact
            Assert.True(await context.Complaints.AnyAsync(x => x.ResidentId == res.ResidentId));
            Assert.NotNull(await context.Residents.FindAsync(res.ResidentId));
        }

        [Fact]
        public async Task DeleteAsync_WhenResidentHasAmenityBookings_ReturnsFalseAndPreservesBookings()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, res, _) = await SeedResidentAsync(context, "7");

            var amenity = new Amenity
            {
                Name = "Hồ bơi sapphire",
                PricePerHour = 50000m
            };
            context.Amenities.Add(amenity);
            await context.SaveChangesAsync();

            context.AmenityBookings.Add(new AmenityBooking
            {
                ResidentId = res.ResidentId,
                AmenityId = amenity.AmenityId,
                BookingDate = DateTime.Today.AddDays(1),
                StartTime = new TimeSpan(8, 0, 0),
                EndTime = new TimeSpan(10, 0, 0),
                Status = "Đã xác nhận"
            });
            await context.SaveChangesAsync();

            var userManagerMock = CreateMockUserManager();
            var service = new ResidentService(
                context, userManagerMock.Object, NullLogger<ResidentService>.Instance, Mock.Of<IHttpContextAccessor>());

            var (success, error) = await service.DeleteAsync(res.ResidentId);
            Assert.False(success);
            Assert.Contains("Không thể xóa cư dân đã phát sinh dữ liệu liên kết", error);

            // Verify amenity booking remains intact
            Assert.True(await context.AmenityBookings.AnyAsync(x => x.ResidentId == res.ResidentId));
            Assert.NotNull(await context.Residents.FindAsync(res.ResidentId));
        }

        [Fact]
        public async Task DeleteAsync_WhenResidentHasVisitorPasses_ReturnsFalseAndPreservesVisitorPasses()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt, res, _) = await SeedResidentAsync(context, "8");

            context.VisitorPasses.Add(new VisitorPass
            {
                ResidentId = res.ResidentId,
                ApartmentId = apt.ApartmentId,
                VisitorName = "Khách ghé thăm",
                ExpectedArrivalDate = DateTime.Today.AddDays(1),
                Status = "Đã cấp thẻ"
            });
            await context.SaveChangesAsync();

            var userManagerMock = CreateMockUserManager();
            var service = new ResidentService(
                context, userManagerMock.Object, NullLogger<ResidentService>.Instance, Mock.Of<IHttpContextAccessor>());

            var (success, error) = await service.DeleteAsync(res.ResidentId);
            Assert.False(success);
            Assert.Contains("Không thể xóa cư dân đã phát sinh dữ liệu liên kết", error);

            // Verify visitor pass remains intact
            Assert.True(await context.VisitorPasses.AnyAsync(x => x.ResidentId == res.ResidentId));
            Assert.NotNull(await context.Residents.FindAsync(res.ResidentId));
        }

        [Fact]
        public async Task DeleteAsync_WhenResidentHasNoLinkedData_Succeeds()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, res, user) = await SeedResidentAsync(context, "9");

            var userManagerMock = CreateMockUserManager();
            userManagerMock.Setup(x => x.DeleteAsync(It.Is<ApplicationUser>(u => u.Id == user.Id)))
                .ReturnsAsync(IdentityResult.Success);

            var service = new ResidentService(
                context, userManagerMock.Object, NullLogger<ResidentService>.Instance, Mock.Of<IHttpContextAccessor>());

            var (success, error) = await service.DeleteAsync(res.ResidentId);
            Assert.True(success, error);
            Assert.Null(error);
            userManagerMock.Verify(x => x.DeleteAsync(It.Is<ApplicationUser>(u => u.Id == user.Id)), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_WhenDbUpdateExceptionOccursDuringDeletion_ReturnsFriendlyError()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, res, user) = await SeedResidentAsync(context, "10");

            var userManagerMock = CreateMockUserManager();
            userManagerMock.Setup(x => x.DeleteAsync(It.Is<ApplicationUser>(u => u.Id == user.Id)))
                .ThrowsAsync(new DbUpdateException("Foreign key violation occurred during race condition delete."));

            var service = new ResidentService(
                context, userManagerMock.Object, NullLogger<ResidentService>.Instance, Mock.Of<IHttpContextAccessor>());

            var (success, error) = await service.DeleteAsync(res.ResidentId);
            Assert.False(success);
            Assert.Equal("Không thể xóa cư dân do có dữ liệu liên kết trong hệ thống.", error);
        }

        #endregion

        #region CitizenId Unique Constraint Tests

        [Fact]
        public async Task CreateAsync_WhenCitizenIdAlreadyExistsInDb_ReturnsFriendlyDuplicateError()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, existingRes, _) = await SeedResidentAsync(context, "11");

            var userManagerMock = CreateMockUserManager();
            var service = new ResidentService(
                context, userManagerMock.Object, NullLogger<ResidentService>.Instance, Mock.Of<IHttpContextAccessor>());

            var model = new ResidentCreateViewModel
            {
                Email = "newresident@apartment.local",
                FullName = "Nguyễn Văn Mới",
                TemporaryPassword = "Password123!",
                CitizenId = existingRes.CitizenId
            };

            var (success, error, resident) = await service.CreateAsync(model);
            Assert.False(success);
            Assert.Equal("Số CCCD/CMND đã tồn tại.", error);
            Assert.Null(resident);
            userManagerMock.Verify(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task UpdateAsync_WhenCitizenIdBelongsToAnotherResident_ReturnsFriendlyDuplicateError()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, res1, _) = await SeedResidentAsync(context, "12");
            var (_, _, res2, _) = await SeedResidentAsync(context, "13");

            var userManagerMock = CreateMockUserManager();
            var service = new ResidentService(
                context, userManagerMock.Object, NullLogger<ResidentService>.Instance, Mock.Of<IHttpContextAccessor>());

            var model = new ResidentEditViewModel
            {
                ResidentId = res2.ResidentId,
                FullName = "Tên cập nhật",
                Email = "res13@apartment.local",
                CitizenId = res1.CitizenId // duplicate of res1
            };

            var (success, error) = await service.UpdateAsync(model);
            Assert.False(success);
            Assert.Equal("Số CCCD/CMND đã tồn tại.", error);
        }

        [Fact]
        public async Task UpdateAsync_WhenCitizenIdUnchanged_Succeeds()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, res, _) = await SeedResidentAsync(context, "14");

            var userManagerMock = CreateMockUserManager();
            var service = new ResidentService(
                context, userManagerMock.Object, NullLogger<ResidentService>.Instance, Mock.Of<IHttpContextAccessor>());

            var model = new ResidentEditViewModel
            {
                ResidentId = res.ResidentId,
                FullName = "Tên Mới Sau Cập Nhật",
                Email = "res14@apartment.local",
                CitizenId = res.CitizenId // unchanged
            };

            var (success, error) = await service.UpdateAsync(model);
            Assert.True(success, error);
            Assert.Null(error);
        }

        #endregion
    }
}
