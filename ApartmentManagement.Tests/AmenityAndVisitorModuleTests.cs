using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ApartmentManagement.Tests
{
    public class AmenityAndVisitorModuleTests
    {
        private ApplicationDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        private async Task<(Building building, Apartment apt, Resident resident)> SeedBuildingAndResidentAsync(ApplicationDbContext context)
        {
            var building = new Building
            {
                BuildingCode = "BLD-X",
                BuildingName = "Tòa X",
                NumberOfFloors = 10
            };
            context.Buildings.Add(building);
            await context.SaveChangesAsync();

            var apt = new Apartment
            {
                BuildingId = building.BuildingId,
                ApartmentCode = "X101",
                Floor = 1,
                Area = 60m
            };
            context.Apartments.Add(apt);
            await context.SaveChangesAsync();

            var user = new ApplicationUser
            {
                Id = "user-test",
                UserName = "resident_test",
                Email = "test@res.com",
                FullName = "Lê Văn Test"
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var resident = new Resident
            {
                UserId = user.Id,
                CitizenId = "001234567890"
            };
            context.Residents.Add(resident);
            await context.SaveChangesAsync();

            context.ApartmentResidents.Add(new ApartmentResident
            {
                ApartmentId = apt.ApartmentId,
                ResidentId = resident.ResidentId,
                Relationship = "Chủ hộ",
                MoveInDate = DateTime.Today.AddDays(-10),
                MoveOutDate = null
            });
            await context.SaveChangesAsync();

            return (building, apt, resident);
        }

        [Fact]
        public async Task Amenity_BookAmenity_RejectsOutsideOpenCloseHours()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, resident) = await SeedBuildingAndResidentAsync(context);

            var amenity = new Amenity
            {
                Name = "Hồ bơi",
                Capacity = 10,
                OpenTime = new TimeSpan(8, 0, 0),
                CloseTime = new TimeSpan(20, 0, 0)
            };
            context.Amenities.Add(amenity);
            await context.SaveChangesAsync();

            var service = new AmenityService(context);

            // Đặt lịch lúc 06:00 (trước giờ mở cửa 08:00)
            var (success, error) = await service.BookAmenityAsync(
                resident.UserId,
                amenity.AmenityId,
                DateTime.Today.AddDays(1),
                new TimeSpan(6, 0, 0),
                new TimeSpan(7, 0, 0),
                2,
                "Bơi sáng sớm");

            Assert.False(success);
            Assert.Contains("nằm trong thời gian hoạt động", error);
        }

        [Fact]
        public async Task Amenity_BookAmenity_RejectsWhenExceedingCapacity()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, resident) = await SeedBuildingAndResidentAsync(context);

            var amenity = new Amenity
            {
                Name = "Phòng Gym",
                Capacity = 5,
                OpenTime = new TimeSpan(6, 0, 0),
                CloseTime = new TimeSpan(22, 0, 0)
            };
            context.Amenities.Add(amenity);
            await context.SaveChangesAsync();

            var service = new AmenityService(context);

            // Đặt 6 người trong khi sức chứa là 5
            var (success, error) = await service.BookAmenityAsync(
                resident.UserId,
                amenity.AmenityId,
                DateTime.Today.AddDays(1),
                new TimeSpan(9, 0, 0),
                new TimeSpan(10, 0, 0),
                6,
                null);

            Assert.False(success);
            Assert.Contains("Số người phải từ 1 đến 5", error);
        }

        [Fact]
        public async Task Amenity_BookAmenity_SucceedsAndCanBeCancelled()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, resident) = await SeedBuildingAndResidentAsync(context);

            var amenity = new Amenity
            {
                Name = "Sân Tennis",
                Capacity = 4,
                OpenTime = new TimeSpan(6, 0, 0),
                CloseTime = new TimeSpan(22, 0, 0)
            };
            context.Amenities.Add(amenity);
            await context.SaveChangesAsync();

            var service = new AmenityService(context);

            var (success, _) = await service.BookAmenityAsync(
                resident.UserId,
                amenity.AmenityId,
                DateTime.Today.AddDays(1),
                new TimeSpan(8, 0, 0),
                new TimeSpan(9, 0, 0),
                2,
                "Chơi tennis");

            Assert.True(success);

            var myBookings = await service.GetResidentBookingsAsync(resident.UserId);
            Assert.Single(myBookings);

            var cancelResult = await service.CancelBookingAsync(resident.UserId, myBookings[0].AmenityBookingId);
            Assert.True(cancelResult.Success);

            var updatedBooking = await context.AmenityBookings.FindAsync(myBookings[0].AmenityBookingId);
            Assert.Equal("Đã hủy", updatedBooking!.Status);
        }

        [Fact]
        public async Task Announcement_CreateAndFilterByBuilding_WorksCorrectly()
        {
            using var context = CreateInMemoryDbContext();
            var (building, _, resident) = await SeedBuildingAndResidentAsync(context);

            context.Users.Add(new ApplicationUser
            {
                Id = "user-admin",
                UserName = "admin_test",
                Email = "admin@test.com",
                FullName = "Admin Test"
            });
            await context.SaveChangesAsync();

            var service = new AnnouncementService(context);

            // Thông báo toàn khu
            await service.CreateAnnouncementAsync("user-admin", new Announcement
            {
                Title = "Bảo trì thang máy toàn khu",
                Content = "Nội dung thông báo toàn khu",
                BuildingId = null,
                IsPinned = true
            });

            // Thông báo riêng tòa X
            await service.CreateAnnouncementAsync("user-admin", new Announcement
            {
                Title = "Vệ sinh bồn nước tòa X",
                Content = "Nội dung tòa X",
                BuildingId = building.BuildingId
            });

            // Thông báo tòa Y khác
            await service.CreateAnnouncementAsync("user-admin", new Announcement
            {
                Title = "Thông báo tòa khác",
                Content = "Nội dung tòa khác",
                BuildingId = 999
            });

            var residentAnnouncements = await service.GetAnnouncementsForResidentAsync(resident.UserId);
            // Cư dân tòa X chỉ thấy thông báo toàn khu + thông báo tòa X (không thấy tòa 999)
            Assert.Equal(2, residentAnnouncements.Count);
        }

        [Fact]
        public async Task Visitor_RegisterAndCheckInCheckOut_WorkflowSucceeds()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt, resident) = await SeedBuildingAndResidentAsync(context);

            var service = new VisitorService(context);

            // 1. Đăng ký khách
            var (regSuccess, _) = await service.RegisterVisitorAsync(
                resident.UserId,
                apt.ApartmentId,
                "Khách Vãng Lai A",
                "0987654321",
                "012345678999",
                DateTime.Today,
                "Đến thăm người thân");

            Assert.True(regSuccess);

            var myPasses = await service.GetResidentVisitorPassesAsync(resident.UserId);
            Assert.Single(myPasses);
            var passId = myPasses[0].VisitorPassId;
            Assert.Equal("Đã đăng ký", myPasses[0].Status);

            // 2. Bảo vệ Check-in
            var (inSuccess, _) = await service.CheckInVisitorAsync(passId);
            Assert.True(inSuccess);
            var checkedInPass = await service.GetVisitorPassByIdAsync(passId);
            Assert.Equal("Đã vào", checkedInPass!.Status);
            Assert.NotNull(checkedInPass.ActualCheckInUtc);

            // 3. Bảo vệ Check-out
            var (outSuccess, _) = await service.CheckOutVisitorAsync(passId);
            Assert.True(outSuccess);
            var checkedOutPass = await service.GetVisitorPassByIdAsync(passId);
            Assert.Equal("Đã ra", checkedOutPass!.Status);
            Assert.NotNull(checkedOutPass.ActualCheckOutUtc);
        }

        [Fact]
        public async Task Amenity_Create_DuplicateName_IsRejected()
        {
            using var context = CreateInMemoryDbContext();
            var service = new AmenityService(context);

            var first = new Amenity
            {
                Name = "Bể bơi bốn mùa",
                Capacity = 20,
                OpenTime = new TimeSpan(6, 0, 0),
                CloseTime = new TimeSpan(21, 0, 0)
            };
            var (firstSuccess, _) = await service.CreateAmenityAsync(first);
            Assert.True(firstSuccess);

            // Thử tạo trùng tên (khác chữ hoa-thường và khoảng trắng thừa)
            var duplicate = new Amenity
            {
                Name = "  bể bơi bốn mùa  ",
                Capacity = 15,
                OpenTime = new TimeSpan(7, 0, 0),
                CloseTime = new TimeSpan(20, 0, 0)
            };
            var (dupSuccess, dupError) = await service.CreateAmenityAsync(duplicate);
            Assert.False(dupSuccess);
            Assert.Contains("đã tồn tại trong hệ thống", dupError);
        }

        [Fact]
        public async Task Amenity_Create_DifferentName_Succeeds()
        {
            using var context = CreateInMemoryDbContext();
            var service = new AmenityService(context);

            var first = new Amenity
            {
                Name = "Bể bơi",
                Capacity = 20,
                OpenTime = new TimeSpan(6, 0, 0),
                CloseTime = new TimeSpan(21, 0, 0)
            };
            var (firstSuccess, _) = await service.CreateAmenityAsync(first);
            Assert.True(firstSuccess);

            var second = new Amenity
            {
                Name = "Phòng Gym",
                Capacity = 10,
                OpenTime = new TimeSpan(6, 0, 0),
                CloseTime = new TimeSpan(21, 0, 0)
            };
            var (secondSuccess, secondError) = await service.CreateAmenityAsync(second);
            Assert.True(secondSuccess, secondError);
        }

        [Fact]
        public async Task Amenity_Update_DuplicateName_IsRejected()
        {
            using var context = CreateInMemoryDbContext();
            var service = new AmenityService(context);

            var first = new Amenity
            {
                Name = "Sân Tennis 1",
                Capacity = 4,
                OpenTime = new TimeSpan(6, 0, 0),
                CloseTime = new TimeSpan(21, 0, 0)
            };
            await service.CreateAmenityAsync(first);

            var second = new Amenity
            {
                Name = "Sân Tennis 2",
                Capacity = 4,
                OpenTime = new TimeSpan(6, 0, 0),
                CloseTime = new TimeSpan(21, 0, 0)
            };
            await service.CreateAmenityAsync(second);

            // Đổi tên sân 2 thành sân 1 -> Bị từ chối
            second.Name = "Sân Tennis 1";
            var (updateSuccess, updateError) = await service.UpdateAmenityAsync(second);
            Assert.False(updateSuccess);
            Assert.Contains("đã tồn tại trong hệ thống", updateError);
        }
    }
}
