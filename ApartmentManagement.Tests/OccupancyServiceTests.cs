using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ApartmentManagement.Tests
{
    public class OccupancyServiceTests
    {
        private ApplicationDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        private async Task<(Building building, Apartment apt, Resident res)> SeedDataAsync(ApplicationDbContext context)
        {
            var building = new Building
            {
                BuildingCode = "BLD-OCC",
                BuildingName = "Tòa Cư Trú",
                NumberOfFloors = 10
            };
            context.Buildings.Add(building);
            await context.SaveChangesAsync();

            var apt = new Apartment
            {
                BuildingId = building.BuildingId,
                ApartmentCode = "OCC-101",
                Floor = 1,
                Area = 65m,
                Status = "Trống"
            };
            context.Apartments.Add(apt);
            await context.SaveChangesAsync();

            var user = new ApplicationUser
            {
                Id = "user-occ",
                UserName = "res_occ",
                Email = "occ@apartment.local",
                FullName = "Nguyễn Văn Cư Trú"
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var res = new Resident
            {
                UserId = user.Id,
                CitizenId = "034567890123"
            };
            context.Residents.Add(res);
            await context.SaveChangesAsync();

            return (building, apt, res);
        }

        [Fact]
        public async Task MoveIn_Today_Succeeds()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt, res) = await SeedDataAsync(context);
            var service = new OccupancyService(context, NullLogger<OccupancyService>.Instance);

            var model = new MoveInViewModel
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = res.ResidentId,
                Relationship = "Chủ hộ",
                MoveInDate = OccupancyService.GetApplicationToday(),
                IsOwner = true
            };

            var (success, error, occupancy) = await service.MoveInAsync(model);
            Assert.True(success, error);
            Assert.Null(error);
            Assert.NotNull(occupancy);
            Assert.Equal("Đang sử dụng", apt.Status);
        }

        [Fact]
        public async Task MoveIn_ValidPastDate_Succeeds()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt, res) = await SeedDataAsync(context);
            var service = new OccupancyService(context, NullLogger<OccupancyService>.Instance);

            var model = new MoveInViewModel
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = res.ResidentId,
                Relationship = "Thành viên",
                MoveInDate = new DateTime(2025, 6, 1),
                IsOwner = false
            };

            var (success, error, _) = await service.MoveInAsync(model);
            Assert.True(success, error);
            Assert.Null(error);
        }

        [Fact]
        public async Task MoveIn_FutureDate_IsRejected()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt, res) = await SeedDataAsync(context);
            var service = new OccupancyService(context, NullLogger<OccupancyService>.Instance);

            var model = new MoveInViewModel
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = res.ResidentId,
                Relationship = "Chủ hộ",
                MoveInDate = OccupancyService.GetApplicationToday().AddDays(1),
                IsOwner = true
            };

            var (success, error, _) = await service.MoveInAsync(model);
            Assert.False(success);
            Assert.Equal("Ngày chuyển vào không thể ở tương lai.", error);
        }

        [Fact]
        public async Task MoveIn_DateBefore1900_IsRejected()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt, res) = await SeedDataAsync(context);
            var service = new OccupancyService(context, NullLogger<OccupancyService>.Instance);

            var model = new MoveInViewModel
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = res.ResidentId,
                Relationship = "Chủ hộ",
                MoveInDate = new DateTime(1899, 12, 31),
                IsOwner = true
            };

            var (success, error, _) = await service.MoveInAsync(model);
            Assert.False(success);
            Assert.Equal("Ngày chuyển vào phải từ 01/01/1900.", error);
        }

        [Fact]
        public async Task MoveOut_Today_SucceedsAndSetsVacantWhenNoOtherResidents()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt, res) = await SeedDataAsync(context);
            var service = new OccupancyService(context, NullLogger<OccupancyService>.Instance);

            var moveInResult = await service.MoveInAsync(new MoveInViewModel
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = res.ResidentId,
                Relationship = "Chủ hộ",
                MoveInDate = OccupancyService.GetApplicationToday().AddDays(-5),
                IsOwner = true
            });
            Assert.True(moveInResult.Success);

            var (moveOutSuccess, moveOutError) = await service.MoveOutAsync(
                moveInResult.Occupancy!.ApartmentResidentId,
                OccupancyService.GetApplicationToday());

            Assert.True(moveOutSuccess, moveOutError);
            Assert.Null(moveOutError);
            Assert.Equal("Trống", apt.Status);
        }

        [Fact]
        public async Task MoveOut_FutureDate_IsRejected()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt, res) = await SeedDataAsync(context);
            var service = new OccupancyService(context, NullLogger<OccupancyService>.Instance);

            var moveInResult = await service.MoveInAsync(new MoveInViewModel
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = res.ResidentId,
                Relationship = "Chủ hộ",
                MoveInDate = OccupancyService.GetApplicationToday(),
                IsOwner = true
            });
            Assert.True(moveInResult.Success);

            var (moveOutSuccess, moveOutError) = await service.MoveOutAsync(
                moveInResult.Occupancy!.ApartmentResidentId,
                OccupancyService.GetApplicationToday().AddDays(1));

            Assert.False(moveOutSuccess);
            Assert.Equal("Ngày chuyển đi không thể ở tương lai.", moveOutError);
        }

        [Fact]
        public async Task MoveOut_BeforeMoveInDate_IsRejected()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt, res) = await SeedDataAsync(context);
            var service = new OccupancyService(context, NullLogger<OccupancyService>.Instance);

            var moveInResult = await service.MoveInAsync(new MoveInViewModel
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = res.ResidentId,
                Relationship = "Chủ hộ",
                MoveInDate = OccupancyService.GetApplicationToday(),
                IsOwner = true
            });
            Assert.True(moveInResult.Success);

            var (moveOutSuccess, moveOutError) = await service.MoveOutAsync(
                moveInResult.Occupancy!.ApartmentResidentId,
                OccupancyService.GetApplicationToday().AddDays(-1));

            Assert.False(moveOutSuccess);
            Assert.Equal("Ngày chuyển đi không thể trước ngày chuyển vào.", moveOutError);
        }

        [Fact]
        public async Task MoveIn_SecondActiveOwner_IsRejected()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt, res1) = await SeedDataAsync(context);

            var user2 = new ApplicationUser { Id = "user-occ-2", UserName = "res_occ_2", Email = "occ2@apartment.local", FullName = "Cư Dân 2" };
            var res2 = new Resident { UserId = user2.Id, CitizenId = "034567890999" };
            context.Users.Add(user2);
            context.Residents.Add(res2);
            await context.SaveChangesAsync();

            var service = new OccupancyService(context, NullLogger<OccupancyService>.Instance);

            // Move in first owner
            var result1 = await service.MoveInAsync(new MoveInViewModel
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = res1.ResidentId,
                Relationship = "Chủ hộ",
                MoveInDate = OccupancyService.GetApplicationToday(),
                IsOwner = true
            });
            Assert.True(result1.Success);

            // Attempt to move in second owner into same apartment
            var result2 = await service.MoveInAsync(new MoveInViewModel
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = res2.ResidentId,
                Relationship = "Đồng chủ hộ",
                MoveInDate = OccupancyService.GetApplicationToday(),
                IsOwner = true
            });
            Assert.False(result2.Success);
            Assert.Equal("Căn hộ đã có chủ hộ đang hoạt động.", result2.ErrorMessage);
        }

        [Fact]
        public async Task MoveIn_OwnerAfterPreviousOwnerMovedOut_Succeeds()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt, res1) = await SeedDataAsync(context);

            var user2 = new ApplicationUser { Id = "user-occ-3", UserName = "res_occ_3", Email = "occ3@apartment.local", FullName = "Cư Dân 3" };
            var res2 = new Resident { UserId = user2.Id, CitizenId = "034567890888" };
            context.Users.Add(user2);
            context.Residents.Add(res2);
            await context.SaveChangesAsync();

            var service = new OccupancyService(context, NullLogger<OccupancyService>.Instance);

            // First owner moves in and then moves out
            var moveIn1 = await service.MoveInAsync(new MoveInViewModel
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = res1.ResidentId,
                Relationship = "Chủ hộ cũ",
                MoveInDate = OccupancyService.GetApplicationToday().AddDays(-10),
                IsOwner = true
            });
            Assert.True(moveIn1.Success);

            var moveOut1 = await service.MoveOutAsync(moveIn1.Occupancy!.ApartmentResidentId, OccupancyService.GetApplicationToday().AddDays(-1));
            Assert.True(moveOut1.Success);

            // Second owner moves in
            var moveIn2 = await service.MoveInAsync(new MoveInViewModel
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = res2.ResidentId,
                Relationship = "Chủ hộ mới",
                MoveInDate = OccupancyService.GetApplicationToday(),
                IsOwner = true
            });
            Assert.True(moveIn2.Success);
            Assert.Null(moveIn2.ErrorMessage);
        }

        [Fact]
        public async Task MoveIn_ApartmentInMaintenance_IsRejected()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt, res) = await SeedDataAsync(context);
            apt.Status = "Bảo trì";
            await context.SaveChangesAsync();

            var service = new OccupancyService(context, NullLogger<OccupancyService>.Instance);

            var result = await service.MoveInAsync(new MoveInViewModel
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = res.ResidentId,
                Relationship = "Chủ hộ",
                MoveInDate = OccupancyService.GetApplicationToday(),
                IsOwner = true
            });

            Assert.False(result.Success);
            Assert.Equal("Căn hộ đang trong trạng thái bảo trì, không thể làm thủ tục chuyển vào.", result.ErrorMessage);
        }

        [Fact]
        public async Task UpdateOccupancy_ChangeRelationship_Succeeds()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt, res) = await SeedDataAsync(context);
            var service = new OccupancyService(context, NullLogger<OccupancyService>.Instance);

            var moveIn = await service.MoveInAsync(new MoveInViewModel
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = res.ResidentId,
                Relationship = "Thành viên",
                MoveInDate = OccupancyService.GetApplicationToday(),
                IsOwner = false
            });
            Assert.True(moveIn.Success);

            var updateResult = await service.UpdateOccupancyAsync(new OccupancyEditViewModel
            {
                ApartmentResidentId = moveIn.Occupancy!.ApartmentResidentId,
                Relationship = "Người thuê chính",
                IsOwner = false
            });

            Assert.True(updateResult.Success);
            var updated = await service.GetByIdAsync(moveIn.Occupancy.ApartmentResidentId);
            Assert.Equal("Người thuê chính", updated!.Relationship);
        }

        [Fact]
        public async Task UpdateOccupancy_PromoteToOwner_WhenActiveOwnerExists_IsRejected()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt, res1) = await SeedDataAsync(context);

            var user2 = new ApplicationUser { Id = "user-occ-4", UserName = "res_occ_4", Email = "occ4@apartment.local", FullName = "Cư Dân 4" };
            var res2 = new Resident { UserId = user2.Id, CitizenId = "034567890777" };
            context.Users.Add(user2);
            context.Residents.Add(res2);
            await context.SaveChangesAsync();

            var service = new OccupancyService(context, NullLogger<OccupancyService>.Instance);

            // Move in res1 as Owner
            await service.MoveInAsync(new MoveInViewModel
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = res1.ResidentId,
                Relationship = "Chủ hộ",
                MoveInDate = OccupancyService.GetApplicationToday(),
                IsOwner = true
            });

            // Move in res2 as Member
            var memberMoveIn = await service.MoveInAsync(new MoveInViewModel
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = res2.ResidentId,
                Relationship = "Thành viên",
                MoveInDate = OccupancyService.GetApplicationToday(),
                IsOwner = false
            });
            Assert.True(memberMoveIn.Success);

            // Attempt to update res2 to Owner
            var updateResult = await service.UpdateOccupancyAsync(new OccupancyEditViewModel
            {
                ApartmentResidentId = memberMoveIn.Occupancy!.ApartmentResidentId,
                Relationship = "Đồng chủ hộ",
                IsOwner = true
            });

            Assert.False(updateResult.Success);
            Assert.Equal("Căn hộ đã có chủ hộ đang hoạt động.", updateResult.ErrorMessage);
        }

        [Fact]
        public async Task GetActiveHouseholdMembers_ReturnsOnlyActiveMembers()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt, res1) = await SeedDataAsync(context);

            var user2 = new ApplicationUser { Id = "user-occ-5", UserName = "res_occ_5", Email = "occ5@apartment.local", FullName = "Cư Dân 5" };
            var res2 = new Resident { UserId = user2.Id, CitizenId = "034567890666" };
            context.Users.Add(user2);
            context.Residents.Add(res2);
            await context.SaveChangesAsync();

            var service = new OccupancyService(context, NullLogger<OccupancyService>.Instance);

            // res1 is active Owner
            await service.MoveInAsync(new MoveInViewModel
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = res1.ResidentId,
                Relationship = "Chủ hộ",
                MoveInDate = OccupancyService.GetApplicationToday().AddDays(-5),
                IsOwner = true
            });

            // res2 moved in and moved out
            var moveIn2 = await service.MoveInAsync(new MoveInViewModel
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = res2.ResidentId,
                Relationship = "Khách thuê cũ",
                MoveInDate = OccupancyService.GetApplicationToday().AddDays(-10),
                IsOwner = false
            });
            await service.MoveOutAsync(moveIn2.Occupancy!.ApartmentResidentId, OccupancyService.GetApplicationToday().AddDays(-2));

            var members = await service.GetActiveHouseholdMembersAsync(apt.ApartmentId);
            Assert.Single(members);
            Assert.Equal(res1.ResidentId, members[0].ResidentId);
            Assert.True(members[0].IsOwner);
        }
    }
}
