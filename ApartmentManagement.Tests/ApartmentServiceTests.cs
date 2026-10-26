using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ApartmentManagement.Tests
{
    public class ApartmentServiceTests
    {
        private ApplicationDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        private async Task<(Building buildingA, Building buildingB)> SeedBuildingsAsync(ApplicationDbContext context)
        {
            var buildingA = new Building
            {
                BuildingCode = "BLD-A",
                BuildingName = "Tòa A",
                NumberOfFloors = 15
            };
            var buildingB = new Building
            {
                BuildingCode = "BLD-B",
                BuildingName = "Tòa B",
                NumberOfFloors = 20
            };
            context.Buildings.AddRange(buildingA, buildingB);
            await context.SaveChangesAsync();

            return (buildingA, buildingB);
        }

        [Fact]
        public void ApartmentStatus_HasStandardValues_AndNormalizesLegacyLabels()
        {
            Assert.Equal("Trống", ApartmentStatus.Vacant);
            Assert.Equal("Đang sử dụng", ApartmentStatus.Occupied);
            Assert.Equal("Bảo trì", ApartmentStatus.Maintenance);
            Assert.Equal("Không hoạt động", ApartmentStatus.Inactive);

            Assert.True(ApartmentStatus.IsValid("Trống"));
            Assert.True(ApartmentStatus.IsValid("Đang sử dụng"));
            Assert.True(ApartmentStatus.IsValid("Bảo trì"));
            Assert.True(ApartmentStatus.IsValid("Không hoạt động"));
            Assert.True(ApartmentStatus.IsValid("Đang trống"));
            Assert.True(ApartmentStatus.IsValid("Đang bảo trì"));
            Assert.False(ApartmentStatus.IsValid("Trạng thái tào lao"));

            Assert.Equal(ApartmentStatus.Vacant, ApartmentStatus.Normalize("Đang trống"));
            Assert.Equal(ApartmentStatus.Maintenance, ApartmentStatus.Normalize("Đang bảo trì"));
            Assert.Equal(ApartmentStatus.Occupied, ApartmentStatus.Normalize("Đang sử dụng"));
            Assert.Equal(ApartmentStatus.Inactive, ApartmentStatus.Normalize("Không hoạt động"));
        }

        [Fact]
        public async Task CreateApartment_Success_WithDefaultVacantStatus()
        {
            using var context = CreateInMemoryDbContext();
            var (buildingA, _) = await SeedBuildingsAsync(context);
            var service = new ApartmentService(context, NullLogger<ApartmentService>.Instance);

            var apartment = new Apartment
            {
                BuildingId = buildingA.BuildingId,
                ApartmentCode = "A-101",
                Floor = 1,
                Area = 70.5m
            };

            var (success, error) = await service.CreateApartmentAsync(apartment);

            Assert.True(success, error);
            Assert.Null(error);

            var saved = await context.Apartments.FirstOrDefaultAsync(x => x.ApartmentCode == "A-101");
            Assert.NotNull(saved);
            Assert.Equal(ApartmentStatus.Vacant, saved.Status);
        }

        [Fact]
        public async Task CreateApartment_Fails_WhenFloorExceedsBuildingFloors()
        {
            using var context = CreateInMemoryDbContext();
            var (buildingA, _) = await SeedBuildingsAsync(context);
            var service = new ApartmentService(context, NullLogger<ApartmentService>.Instance);

            var apartment = new Apartment
            {
                BuildingId = buildingA.BuildingId,
                ApartmentCode = "A-999",
                Floor = 25, // Tòa A chỉ có 15 tầng
                Area = 70.5m
            };

            var (success, error) = await service.CreateApartmentAsync(apartment);

            Assert.False(success);
            Assert.Contains("không được vượt quá 15", error);
        }

        [Fact]
        public async Task CreateApartment_Fails_WhenCodeExistsInSameBuilding()
        {
            using var context = CreateInMemoryDbContext();
            var (buildingA, _) = await SeedBuildingsAsync(context);
            var service = new ApartmentService(context, NullLogger<ApartmentService>.Instance);

            await service.CreateApartmentAsync(new Apartment
            {
                BuildingId = buildingA.BuildingId,
                ApartmentCode = "A-101",
                Floor = 1,
                Area = 60m
            });

            var (success, error) = await service.CreateApartmentAsync(new Apartment
            {
                BuildingId = buildingA.BuildingId,
                ApartmentCode = "A-101",
                Floor = 2,
                Area = 65m
            });

            Assert.False(success);
            Assert.Contains("đã tồn tại trong tòa nhà", error);
        }

        [Fact]
        public async Task CreateApartment_Success_WhenSameCodeInDifferentBuilding()
        {
            using var context = CreateInMemoryDbContext();
            var (buildingA, buildingB) = await SeedBuildingsAsync(context);
            var service = new ApartmentService(context, NullLogger<ApartmentService>.Instance);

            var (successA, _) = await service.CreateApartmentAsync(new Apartment
            {
                BuildingId = buildingA.BuildingId,
                ApartmentCode = "101",
                Floor = 1,
                Area = 60m
            });

            var (successB, _) = await service.CreateApartmentAsync(new Apartment
            {
                BuildingId = buildingB.BuildingId,
                ApartmentCode = "101",
                Floor = 1,
                Area = 60m
            });

            Assert.True(successA);
            Assert.True(successB);
            Assert.Equal(2, await context.Apartments.CountAsync(x => x.ApartmentCode == "101"));
        }

        [Fact]
        public async Task UpdateApartment_Fails_WhenChangingStatusToVacant_WhileActiveResidentsExist()
        {
            using var context = CreateInMemoryDbContext();
            var (buildingA, _) = await SeedBuildingsAsync(context);
            var service = new ApartmentService(context, NullLogger<ApartmentService>.Instance);

            var apt = new Apartment
            {
                BuildingId = buildingA.BuildingId,
                ApartmentCode = "A-102",
                Floor = 1,
                Area = 65m,
                Status = ApartmentStatus.Occupied
            };
            context.Apartments.Add(apt);
            await context.SaveChangesAsync();

            var resident = new Resident { CitizenId = "012345678901", UserId = "user-1" };
            context.Residents.Add(resident);
            await context.SaveChangesAsync();

            context.ApartmentResidents.Add(new ApartmentResident
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = resident.ResidentId,
                Relationship = "Chủ hộ",
                MoveInDate = DateTime.Today,
                MoveOutDate = null
            });
            await context.SaveChangesAsync();

            // Try to set status to Vacant
            apt.Status = ApartmentStatus.Vacant;
            var (success, error) = await service.UpdateApartmentAsync(apt);

            Assert.False(success);
            Assert.Contains("vẫn còn cư dân đang lưu trú", error);
        }

        [Fact]
        public async Task UpdateApartment_Fails_WhenChangingStatusToInactive_WhileActiveResidentsExist()
        {
            using var context = CreateInMemoryDbContext();
            var (buildingA, _) = await SeedBuildingsAsync(context);
            var service = new ApartmentService(context, NullLogger<ApartmentService>.Instance);

            var apt = new Apartment
            {
                BuildingId = buildingA.BuildingId,
                ApartmentCode = "A-103",
                Floor = 1,
                Area = 65m,
                Status = ApartmentStatus.Occupied
            };
            context.Apartments.Add(apt);
            await context.SaveChangesAsync();

            var resident = new Resident { CitizenId = "012345678902", UserId = "user-2" };
            context.Residents.Add(resident);
            await context.SaveChangesAsync();

            context.ApartmentResidents.Add(new ApartmentResident
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = resident.ResidentId,
                Relationship = "Thành viên",
                MoveInDate = DateTime.Today,
                MoveOutDate = null
            });
            await context.SaveChangesAsync();

            apt.Status = ApartmentStatus.Inactive;
            var (success, error) = await service.UpdateApartmentAsync(apt);

            Assert.False(success);
            Assert.Contains("vẫn còn cư dân đang lưu trú", error);
        }

        [Fact]
        public async Task DeleteApartment_Fails_WhenActiveResidentsExist()
        {
            using var context = CreateInMemoryDbContext();
            var (buildingA, _) = await SeedBuildingsAsync(context);
            var service = new ApartmentService(context, NullLogger<ApartmentService>.Instance);

            var apt = new Apartment
            {
                BuildingId = buildingA.BuildingId,
                ApartmentCode = "A-104",
                Floor = 1,
                Area = 60m,
                Status = ApartmentStatus.Occupied
            };
            context.Apartments.Add(apt);
            await context.SaveChangesAsync();

            var res = new Resident { CitizenId = "012345678903", UserId = "user-3" };
            context.Residents.Add(res);
            await context.SaveChangesAsync();

            context.ApartmentResidents.Add(new ApartmentResident
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = res.ResidentId,
                Relationship = "Chủ hộ",
                MoveInDate = DateTime.Today,
                MoveOutDate = null
            });
            await context.SaveChangesAsync();

            var (success, error) = await service.DeleteApartmentAsync(apt.ApartmentId);

            Assert.False(success);
            Assert.Contains("đang có cư dân lưu trú", error);
        }

        [Fact]
        public async Task DeleteApartment_Fails_WhenHistoricalOccupancyRecordsExist()
        {
            using var context = CreateInMemoryDbContext();
            var (buildingA, _) = await SeedBuildingsAsync(context);
            var service = new ApartmentService(context, NullLogger<ApartmentService>.Instance);

            var apt = new Apartment
            {
                BuildingId = buildingA.BuildingId,
                ApartmentCode = "A-105",
                Floor = 1,
                Area = 60m,
                Status = ApartmentStatus.Vacant
            };
            context.Apartments.Add(apt);
            await context.SaveChangesAsync();

            var res = new Resident { CitizenId = "012345678904", UserId = "user-4" };
            context.Residents.Add(res);
            await context.SaveChangesAsync();

            // Resident moved out in the past
            context.ApartmentResidents.Add(new ApartmentResident
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = res.ResidentId,
                Relationship = "Chủ hộ",
                MoveInDate = new DateTime(2023, 1, 1),
                MoveOutDate = new DateTime(2023, 12, 31)
            });
            await context.SaveChangesAsync();

            var (success, error) = await service.DeleteApartmentAsync(apt.ApartmentId);

            Assert.False(success);
            Assert.Contains("Không thể xóa căn hộ đã phát sinh dữ liệu lịch sử", error);
            Assert.Contains("Không hoạt động", error);
        }

        [Fact]
        public async Task DeleteApartment_Fails_WhenHistoricalInvoicesExist()
        {
            using var context = CreateInMemoryDbContext();
            var (buildingA, _) = await SeedBuildingsAsync(context);
            var service = new ApartmentService(context, NullLogger<ApartmentService>.Instance);

            var apt = new Apartment
            {
                BuildingId = buildingA.BuildingId,
                ApartmentCode = "A-106",
                Floor = 1,
                Area = 60m,
                Status = ApartmentStatus.Vacant
            };
            context.Apartments.Add(apt);
            await context.SaveChangesAsync();

            var user = new ApplicationUser { Id = "creator-user", UserName = "admin", FullName = "Admin" };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var res = new Resident { CitizenId = "012345678905", UserId = "user-5" };
            context.Residents.Add(res);
            await context.SaveChangesAsync();

            context.ApartmentInvoices.Add(new ApartmentInvoice
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = res.ResidentId,
                InvoiceCode = "INV-2024-001",
                BillingYear = 2024,
                BillingMonth = 5,
                DueDate = DateTime.Today,
                ApartmentArea = 60m,
                TotalAmount = 500000m,
                Status = ApartmentInvoiceStatus.Issued,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = user.Id
            });
            await context.SaveChangesAsync();

            var (success, error) = await service.DeleteApartmentAsync(apt.ApartmentId);

            Assert.False(success);
            Assert.Contains("Không thể xóa căn hộ đã phát sinh dữ liệu lịch sử", error);
        }

        [Fact]
        public async Task DeleteApartment_Success_WhenNoResidentsOrHistoricalRecords()
        {
            using var context = CreateInMemoryDbContext();
            var (buildingA, _) = await SeedBuildingsAsync(context);
            var service = new ApartmentService(context, NullLogger<ApartmentService>.Instance);

            var apt = new Apartment
            {
                BuildingId = buildingA.BuildingId,
                ApartmentCode = "A-CLEAN",
                Floor = 2,
                Area = 55m,
                Status = ApartmentStatus.Vacant
            };
            context.Apartments.Add(apt);
            await context.SaveChangesAsync();

            var (success, error) = await service.DeleteApartmentAsync(apt.ApartmentId);

            Assert.True(success, error);
            Assert.Null(await context.Apartments.FindAsync(apt.ApartmentId));
        }

        [Fact]
        public async Task DeactivateApartment_Success_WhenNoActiveResidents()
        {
            using var context = CreateInMemoryDbContext();
            var (buildingA, _) = await SeedBuildingsAsync(context);
            var service = new ApartmentService(context, NullLogger<ApartmentService>.Instance);

            var apt = new Apartment
            {
                BuildingId = buildingA.BuildingId,
                ApartmentCode = "A-DEACT",
                Floor = 3,
                Area = 60m,
                Status = ApartmentStatus.Vacant
            };
            context.Apartments.Add(apt);
            await context.SaveChangesAsync();

            var (success, error) = await service.DeactivateApartmentAsync(apt.ApartmentId);

            Assert.True(success, error);
            var refreshed = await context.Apartments.FindAsync(apt.ApartmentId);
            Assert.NotNull(refreshed);
            Assert.Equal(ApartmentStatus.Inactive, refreshed.Status);
        }

        [Fact]
        public async Task DeactivateApartment_Fails_WhenActiveResidentsExist()
        {
            using var context = CreateInMemoryDbContext();
            var (buildingA, _) = await SeedBuildingsAsync(context);
            var service = new ApartmentService(context, NullLogger<ApartmentService>.Instance);

            var apt = new Apartment
            {
                BuildingId = buildingA.BuildingId,
                ApartmentCode = "A-OCCUPIED",
                Floor = 3,
                Area = 60m,
                Status = ApartmentStatus.Occupied
            };
            context.Apartments.Add(apt);
            await context.SaveChangesAsync();

            var res = new Resident { CitizenId = "012345678906", UserId = "user-6" };
            context.Residents.Add(res);
            await context.SaveChangesAsync();

            context.ApartmentResidents.Add(new ApartmentResident
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = res.ResidentId,
                Relationship = "Chủ hộ",
                MoveInDate = DateTime.Today,
                MoveOutDate = null
            });
            await context.SaveChangesAsync();

            var (success, error) = await service.DeactivateApartmentAsync(apt.ApartmentId);

            Assert.False(success);
            Assert.Contains("đang có cư dân lưu trú", error);
        }

        [Fact]
        public async Task MoveIn_Rejected_WhenApartmentIsMaintenanceOrInactive()
        {
            using var context = CreateInMemoryDbContext();
            var (buildingA, _) = await SeedBuildingsAsync(context);
            var occupancyService = new OccupancyService(context, NullLogger<OccupancyService>.Instance);

            var aptMaint = new Apartment
            {
                BuildingId = buildingA.BuildingId,
                ApartmentCode = "A-MAINT",
                Floor = 1,
                Area = 60m,
                Status = ApartmentStatus.Maintenance
            };
            var aptInactive = new Apartment
            {
                BuildingId = buildingA.BuildingId,
                ApartmentCode = "A-INACT",
                Floor = 1,
                Area = 60m,
                Status = ApartmentStatus.Inactive
            };
            context.Apartments.AddRange(aptMaint, aptInactive);

            var user = new ApplicationUser { Id = "res-user-movein", UserName = "movein_res", FullName = "Cư Dân Test" };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var res = new Resident { CitizenId = "012345678999", UserId = user.Id };
            context.Residents.Add(res);
            await context.SaveChangesAsync();

            var resultMaint = await occupancyService.MoveInAsync(new MoveInViewModel
            {
                ApartmentId = aptMaint.ApartmentId,
                ResidentId = res.ResidentId,
                Relationship = "Chủ hộ",
                MoveInDate = DateTime.Today,
                IsOwner = true
            });

            Assert.False(resultMaint.Success);
            Assert.Contains("trạng thái bảo trì", resultMaint.ErrorMessage);

            var resultInactive = await occupancyService.MoveInAsync(new MoveInViewModel
            {
                ApartmentId = aptInactive.ApartmentId,
                ResidentId = res.ResidentId,
                Relationship = "Chủ hộ",
                MoveInDate = DateTime.Today,
                IsOwner = true
            });

            Assert.False(resultInactive.Success);
            Assert.Contains("ngừng sử dụng", resultInactive.ErrorMessage);
        }

        [Fact]
        public async Task MoveOut_PreservesInactiveStatus_DoesNotRevertToVacant()
        {
            using var context = CreateInMemoryDbContext();
            var (buildingA, _) = await SeedBuildingsAsync(context);
            var occupancyService = new OccupancyService(context, NullLogger<OccupancyService>.Instance);

            var apt = new Apartment
            {
                BuildingId = buildingA.BuildingId,
                ApartmentCode = "A-PRESERVE",
                Floor = 2,
                Area = 60m,
                Status = ApartmentStatus.Inactive // Already set to Inactive
            };
            context.Apartments.Add(apt);

            var user = new ApplicationUser { Id = "res-user-moveout", UserName = "moveout_res", FullName = "Cư Dân MoveOut" };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var res = new Resident { CitizenId = "012345678888", UserId = user.Id };
            context.Residents.Add(res);
            await context.SaveChangesAsync();

            var occupancy = new ApartmentResident
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = res.ResidentId,
                Relationship = "Thành viên",
                MoveInDate = new DateTime(2024, 1, 1),
                MoveOutDate = null
            };
            context.ApartmentResidents.Add(occupancy);
            await context.SaveChangesAsync();

            var (success, _) = await occupancyService.MoveOutAsync(occupancy.ApartmentResidentId, DateTime.Today);

            Assert.True(success);
            var refreshed = await context.Apartments.FindAsync(apt.ApartmentId);
            Assert.NotNull(refreshed);
            Assert.Equal(ApartmentStatus.Inactive, refreshed.Status); // Preserved Inactive, not changed to Vacant
        }

        [Fact]
        public async Task GetAllApartments_FiltersByBuildingId()
        {
            using var context = CreateInMemoryDbContext();
            var (buildingA, buildingB) = await SeedBuildingsAsync(context);
            var service = new ApartmentService(context, NullLogger<ApartmentService>.Instance);

            context.Apartments.AddRange(
                new Apartment { BuildingId = buildingA.BuildingId, ApartmentCode = "A-1", Floor = 1, Area = 50m },
                new Apartment { BuildingId = buildingA.BuildingId, ApartmentCode = "A-2", Floor = 2, Area = 55m },
                new Apartment { BuildingId = buildingB.BuildingId, ApartmentCode = "B-1", Floor = 1, Area = 60m }
            );
            await context.SaveChangesAsync();

            var listA = await service.GetAllApartmentsAsync(buildingA.BuildingId);
            var listB = await service.GetAllApartmentsAsync(buildingB.BuildingId);
            var listAll = await service.GetAllApartmentsAsync();

            Assert.Equal(2, listA.Count);
            Assert.All(listA, a => Assert.Equal(buildingA.BuildingId, a.BuildingId));

            Assert.Single(listB);
            Assert.Equal(buildingB.BuildingId, listB[0].BuildingId);

            Assert.Equal(3, listAll.Count);
        }

        [Fact]
        public async Task CreateApartment_Fails_WhenBuildingDoesNotExist()
        {
            using var context = CreateInMemoryDbContext();
            var service = new ApartmentService(context, NullLogger<ApartmentService>.Instance);

            var apartment = new Apartment
            {
                BuildingId = 9999,
                ApartmentCode = "X-101",
                Floor = 1,
                Area = 60m
            };

            var (success, error) = await service.CreateApartmentAsync(apartment);

            Assert.False(success);
            Assert.Equal("Tòa nhà không tồn tại.", error);
        }

        [Fact]
        public async Task CreateApartment_Fails_WhenFloorIsZeroOrNegative()
        {
            using var context = CreateInMemoryDbContext();
            var (buildingA, _) = await SeedBuildingsAsync(context);
            var service = new ApartmentService(context, NullLogger<ApartmentService>.Instance);

            var aptZero = new Apartment
            {
                BuildingId = buildingA.BuildingId,
                ApartmentCode = "A-ZERO",
                Floor = 0,
                Area = 60m
            };

            var (success, error) = await service.CreateApartmentAsync(aptZero);

            Assert.False(success);
            Assert.Equal("Tầng phải lớn hơn 0.", error);
        }

        [Fact]
        public async Task CreateApartment_Fails_WhenFloorExceeds200()
        {
            using var context = CreateInMemoryDbContext();
            var service = new ApartmentService(context, NullLogger<ApartmentService>.Instance);

            var aptHigh = new Apartment
            {
                BuildingId = 1,
                ApartmentCode = "A-HIGH",
                Floor = 201,
                Area = 60m
            };

            var (success, error) = await service.CreateApartmentAsync(aptHigh);

            Assert.False(success);
            Assert.Equal("Tầng phải từ 1 đến 200.", error);
        }

        [Fact]
        public async Task UpdateApartment_Success_ValidFloorAndBuilding()
        {
            using var context = CreateInMemoryDbContext();
            var (buildingA, _) = await SeedBuildingsAsync(context);
            var service = new ApartmentService(context, NullLogger<ApartmentService>.Instance);

            var apt = new Apartment
            {
                BuildingId = buildingA.BuildingId,
                ApartmentCode = "A-UPD",
                Floor = 1,
                Area = 60m,
                Status = ApartmentStatus.Vacant
            };
            context.Apartments.Add(apt);
            await context.SaveChangesAsync();

            var updateModel = new Apartment
            {
                ApartmentId = apt.ApartmentId,
                BuildingId = buildingA.BuildingId,
                ApartmentCode = "A-UPD-NEW",
                Floor = 5,
                Area = 75m,
                Status = ApartmentStatus.Vacant
            };

            var (success, error) = await service.UpdateApartmentAsync(updateModel);

            Assert.True(success, error);
            var refreshed = await context.Apartments.FindAsync(apt.ApartmentId);
            Assert.NotNull(refreshed);
            Assert.Equal("A-UPD-NEW", refreshed.ApartmentCode);
            Assert.Equal(5, refreshed.Floor);
            Assert.Equal(75m, refreshed.Area);
        }

        [Fact]
        public async Task UpdateApartment_Fails_WhenBuildingDoesNotExist()
        {
            using var context = CreateInMemoryDbContext();
            var (buildingA, _) = await SeedBuildingsAsync(context);
            var service = new ApartmentService(context, NullLogger<ApartmentService>.Instance);

            var apt = new Apartment
            {
                BuildingId = buildingA.BuildingId,
                ApartmentCode = "A-NOBLD",
                Floor = 1,
                Area = 60m,
                Status = ApartmentStatus.Vacant
            };
            context.Apartments.Add(apt);
            await context.SaveChangesAsync();

            var updateModel = new Apartment
            {
                ApartmentId = apt.ApartmentId,
                BuildingId = 9999,
                ApartmentCode = "A-NOBLD",
                Floor = 1,
                Area = 60m,
                Status = ApartmentStatus.Vacant
            };

            var (success, error) = await service.UpdateApartmentAsync(updateModel);

            Assert.False(success);
            Assert.Equal("Tòa nhà không tồn tại.", error);
        }

        [Fact]
        public async Task UpdateApartment_Fails_WhenNewBuildingFloorsExceeded()
        {
            using var context = CreateInMemoryDbContext();
            var (buildingA, buildingB) = await SeedBuildingsAsync(context);
            // buildingA has 15 floors, buildingB has 20 floors
            // Let's create buildingC with 3 floors
            var buildingC = new Building { BuildingCode = "BLD-C", BuildingName = "Tòa C", NumberOfFloors = 3 };
            context.Buildings.Add(buildingC);
            await context.SaveChangesAsync();

            var service = new ApartmentService(context, NullLogger<ApartmentService>.Instance);

            var apt = new Apartment
            {
                BuildingId = buildingA.BuildingId,
                ApartmentCode = "A-EXCEED",
                Floor = 10,
                Area = 60m,
                Status = ApartmentStatus.Vacant
            };
            context.Apartments.Add(apt);
            await context.SaveChangesAsync();

            // Try to move to buildingC where Floor 10 exceeds buildingC's 3 floors
            var updateModel = new Apartment
            {
                ApartmentId = apt.ApartmentId,
                BuildingId = buildingC.BuildingId,
                ApartmentCode = "A-EXCEED",
                Floor = 10,
                Area = 60m,
                Status = ApartmentStatus.Vacant
            };

            var (success, error) = await service.UpdateApartmentAsync(updateModel);

            Assert.False(success);
            Assert.Contains("không được vượt quá 3", error);
        }

        [Fact]
        public async Task UpdateApartment_Fails_WhenChangingBuilding_WhileActiveResidentsExist()
        {
            using var context = CreateInMemoryDbContext();
            var (buildingA, buildingB) = await SeedBuildingsAsync(context);
            var service = new ApartmentService(context, NullLogger<ApartmentService>.Instance);

            var apt = new Apartment
            {
                BuildingId = buildingA.BuildingId,
                ApartmentCode = "A-MOVEBLD",
                Floor = 1,
                Area = 60m,
                Status = ApartmentStatus.Occupied
            };
            context.Apartments.Add(apt);
            await context.SaveChangesAsync();

            var resident = new Resident { CitizenId = "012345678990", UserId = "user-movebld" };
            context.Residents.Add(resident);
            await context.SaveChangesAsync();

            context.ApartmentResidents.Add(new ApartmentResident
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = resident.ResidentId,
                Relationship = "Chủ hộ",
                MoveInDate = DateTime.Today,
                MoveOutDate = null
            });
            await context.SaveChangesAsync();

            var updateModel = new Apartment
            {
                ApartmentId = apt.ApartmentId,
                BuildingId = buildingB.BuildingId, // Change to building B
                ApartmentCode = "A-MOVEBLD",
                Floor = 1,
                Area = 60m,
                Status = ApartmentStatus.Occupied
            };

            var (success, error) = await service.UpdateApartmentAsync(updateModel);

            Assert.False(success);
            Assert.Contains("Không thể đổi tòa nhà cho căn hộ đang có cư dân lưu trú", error);
        }

        [Fact]
        public async Task UpdateApartment_Fails_WhenDuplicateCodeInSameBuilding()
        {
            using var context = CreateInMemoryDbContext();
            var (buildingA, _) = await SeedBuildingsAsync(context);
            var service = new ApartmentService(context, NullLogger<ApartmentService>.Instance);

            var apt1 = new Apartment { BuildingId = buildingA.BuildingId, ApartmentCode = "A-101", Floor = 1, Area = 50m, Status = ApartmentStatus.Vacant };
            var apt2 = new Apartment { BuildingId = buildingA.BuildingId, ApartmentCode = "A-102", Floor = 1, Area = 50m, Status = ApartmentStatus.Vacant };
            context.Apartments.AddRange(apt1, apt2);
            await context.SaveChangesAsync();

            var updateModel = new Apartment
            {
                ApartmentId = apt2.ApartmentId,
                BuildingId = buildingA.BuildingId,
                ApartmentCode = "A-101", // duplicate
                Floor = 1,
                Area = 50m,
                Status = ApartmentStatus.Vacant
            };

            var (success, error) = await service.UpdateApartmentAsync(updateModel);

            Assert.False(success);
            Assert.Contains("Mã căn hộ đã tồn tại trong tòa nhà", error);
        }

        [Fact]
        public async Task UpdateApartment_Success_WhenSameCodeInDifferentBuilding()
        {
            using var context = CreateInMemoryDbContext();
            var (buildingA, buildingB) = await SeedBuildingsAsync(context);
            var service = new ApartmentService(context, NullLogger<ApartmentService>.Instance);

            var aptA = new Apartment { BuildingId = buildingA.BuildingId, ApartmentCode = "DUP-CODE", Floor = 1, Area = 50m, Status = ApartmentStatus.Vacant };
            var aptB = new Apartment { BuildingId = buildingB.BuildingId, ApartmentCode = "OTHER-CODE", Floor = 1, Area = 50m, Status = ApartmentStatus.Vacant };
            context.Apartments.AddRange(aptA, aptB);
            await context.SaveChangesAsync();

            var updateModel = new Apartment
            {
                ApartmentId = aptB.ApartmentId,
                BuildingId = buildingB.BuildingId,
                ApartmentCode = "DUP-CODE", // same code as building A, but in building B
                Floor = 1,
                Area = 50m,
                Status = ApartmentStatus.Vacant
            };

            var (success, error) = await service.UpdateApartmentAsync(updateModel);

            Assert.True(success, error);
            var refreshed = await context.Apartments.FindAsync(aptB.ApartmentId);
            Assert.NotNull(refreshed);
            Assert.Equal("DUP-CODE", refreshed.ApartmentCode);
        }

        [Fact]
        public async Task UpdateApartment_Fails_WhenChangingStatusToMaintenance_WhileActiveResidentsExist()
        {
            using var context = CreateInMemoryDbContext();
            var (buildingA, _) = await SeedBuildingsAsync(context);
            var service = new ApartmentService(context, NullLogger<ApartmentService>.Instance);

            var apt = new Apartment
            {
                BuildingId = buildingA.BuildingId,
                ApartmentCode = "A-MAINT-TEST",
                Floor = 1,
                Area = 60m,
                Status = ApartmentStatus.Occupied
            };
            context.Apartments.Add(apt);
            await context.SaveChangesAsync();

            var res = new Resident { CitizenId = "012345678991", UserId = "user-maint-test" };
            context.Residents.Add(res);
            await context.SaveChangesAsync();

            context.ApartmentResidents.Add(new ApartmentResident
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = res.ResidentId,
                Relationship = "Chủ hộ",
                MoveInDate = DateTime.Today,
                MoveOutDate = null
            });
            await context.SaveChangesAsync();

            var updateModel = new Apartment
            {
                ApartmentId = apt.ApartmentId,
                BuildingId = buildingA.BuildingId,
                ApartmentCode = "A-MAINT-TEST",
                Floor = 1,
                Area = 60m,
                Status = ApartmentStatus.Maintenance
            };

            var (success, error) = await service.UpdateApartmentAsync(updateModel);

            Assert.False(success);
            Assert.Contains("vẫn còn cư dân đang lưu trú", error);
        }

        [Fact]
        public async Task DeleteApartment_Fails_WhenVehiclesExist()
        {
            using var context = CreateInMemoryDbContext();
            var (buildingA, _) = await SeedBuildingsAsync(context);
            var service = new ApartmentService(context, NullLogger<ApartmentService>.Instance);

            var apt = new Apartment
            {
                BuildingId = buildingA.BuildingId,
                ApartmentCode = "A-VEHICLE",
                Floor = 1,
                Area = 60m,
                Status = ApartmentStatus.Vacant
            };
            context.Apartments.Add(apt);
            await context.SaveChangesAsync();

            var res = new Resident { CitizenId = "012345678992", UserId = "user-veh" };
            context.Residents.Add(res);
            await context.SaveChangesAsync();

            context.Vehicles.Add(new Vehicle
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = res.ResidentId,
                VehicleType = "Xe máy",
                LicensePlate = "29A1-12345"
            });
            await context.SaveChangesAsync();

            var (success, error) = await service.DeleteApartmentAsync(apt.ApartmentId);

            Assert.False(success);
            Assert.Contains("Không thể xóa căn hộ đã phát sinh dữ liệu lịch sử", error);
        }

        [Fact]
        public async Task DeleteApartment_Fails_WhenComplaintsExist()
        {
            using var context = CreateInMemoryDbContext();
            var (buildingA, _) = await SeedBuildingsAsync(context);
            var service = new ApartmentService(context, NullLogger<ApartmentService>.Instance);

            var apt = new Apartment
            {
                BuildingId = buildingA.BuildingId,
                ApartmentCode = "A-COMPLAINT",
                Floor = 1,
                Area = 60m,
                Status = ApartmentStatus.Vacant
            };
            context.Apartments.Add(apt);
            await context.SaveChangesAsync();

            var res = new Resident { CitizenId = "012345678993", UserId = "user-comp" };
            context.Residents.Add(res);
            await context.SaveChangesAsync();

            context.Complaints.Add(new Complaint
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = res.ResidentId,
                Title = "Phản ánh tiếng ồn",
                Content = "Tiếng ồn ban đêm",
                Status = "Chờ tiếp nhận"
            });
            await context.SaveChangesAsync();

            var (success, error) = await service.DeleteApartmentAsync(apt.ApartmentId);

            Assert.False(success);
            Assert.Contains("Không thể xóa căn hộ đã phát sinh dữ liệu lịch sử", error);
        }

        [Fact]
        public async Task DeleteApartment_Fails_WhenContractsExist()
        {
            using var context = CreateInMemoryDbContext();
            var (buildingA, _) = await SeedBuildingsAsync(context);
            var service = new ApartmentService(context, NullLogger<ApartmentService>.Instance);

            var apt = new Apartment
            {
                BuildingId = buildingA.BuildingId,
                ApartmentCode = "A-CONTRACT",
                Floor = 1,
                Area = 60m,
                Status = ApartmentStatus.Vacant
            };
            context.Apartments.Add(apt);

            var user = new ApplicationUser { Id = "creator-contract", UserName = "admin_c", FullName = "Admin" };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var contract = new ApartmentContract
            {
                ApartmentId = apt.ApartmentId,
                StartDate = DateTime.Today,
                MonthlyRent = 10000000m,
                DepositAmount = 20000000m,
                Status = ApartmentContractStatus.Active,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = user.Id
            };
            context.ApartmentContracts.Add(contract);
            await context.SaveChangesAsync();

            var (success, error) = await service.DeleteApartmentAsync(apt.ApartmentId);

            Assert.False(success);
            Assert.Contains("Không thể xóa căn hộ đã phát sinh dữ liệu lịch sử", error);
        }

        [Fact]
        public async Task DeleteApartment_ReturnsNotFound_WhenApartmentDoesNotExist()
        {
            using var context = CreateInMemoryDbContext();
            var service = new ApartmentService(context, NullLogger<ApartmentService>.Instance);

            var (success, error) = await service.DeleteApartmentAsync(9999);

            Assert.False(success);
            Assert.Equal("Không tìm thấy căn hộ.", error);
        }
    }
}
