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
    public class ComplaintModuleTests
    {
        private ApplicationDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        private async Task<(Resident resA, Resident resB, Apartment apt101, Apartment apt102, Complaint cA, Complaint cB)> SeedDataAsync(ApplicationDbContext context)
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

            var cA = new Complaint
            {
                ResidentId = resA.ResidentId,
                ApartmentId = apt101.ApartmentId,
                Title = "Hành lang tầng 1 có rác",
                Content = "Rác chưa được thu gom sạch sẽ lúc 8h sáng.",
                Category = "Vệ sinh",
                Status = "Chờ tiếp nhận",
                CreatedAt = DateTime.UtcNow
            };

            var cB = new Complaint
            {
                ResidentId = resB.ResidentId,
                ApartmentId = apt102.ApartmentId,
                Title = "Tiếng ồn ban đêm",
                Content = "Căn hộ bên cạnh bật nhạc lớn sau 23h.",
                Category = "Tiếng ồn",
                Status = "Chờ tiếp nhận",
                CreatedAt = DateTime.UtcNow
            };

            context.Complaints.AddRange(cA, cB);
            await context.SaveChangesAsync();

            return (resA, resB, apt101, apt102, cA, cB);
        }

        // 1. Resident A xem complaint của A -> PASS
        [Fact]
        public async Task Test1_ResidentA_CanViewOwnComplaint()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, _, cA, _) = await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("user-a");
            currentUserServiceMock.Setup(s => s.IsInRole("Resident")).Returns(true);
            currentUserServiceMock.Setup(s => s.IsInRole("SystemAdmin")).Returns(false);
            currentUserServiceMock.Setup(s => s.IsInRole("Manager")).Returns(false);

            var complaintService = new ComplaintService(context, currentUserServiceMock.Object);

            var complaint = await complaintService.GetComplaintByIdAsync(cA.ComplaintId);
            Assert.NotNull(complaint);
            Assert.Equal("Hành lang tầng 1 có rác", complaint.Title);

            var list = await complaintService.GetComplaintsAsync();
            Assert.Single(list);
            Assert.Equal("Hành lang tầng 1 có rác", list[0].Title);
        }

        // 2. Resident A không xem complaint của B -> 404 / null
        [Fact]
        public async Task Test2_ResidentA_CannotViewResidentB_Complaint()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, _, _, cB) = await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("user-a");
            currentUserServiceMock.Setup(s => s.IsInRole("Resident")).Returns(true);
            currentUserServiceMock.Setup(s => s.IsInRole("SystemAdmin")).Returns(false);
            currentUserServiceMock.Setup(s => s.IsInRole("Manager")).Returns(false);

            var complaintService = new ComplaintService(context, currentUserServiceMock.Object);

            // Service returns null
            var result = await complaintService.GetComplaintByIdAsync(cB.ComplaintId);
            Assert.Null(result);

            // Controller returns 404 NotFound
            var residentServiceMock = new Mock<IResidentService>();
            var controller = new ComplaintController(complaintService, residentServiceMock.Object);
            var actionResult = await controller.Details(cB.ComplaintId);
            Assert.IsType<NotFoundResult>(actionResult);
        }

        // 3. Resident B không xem complaint của A -> 404 / null
        [Fact]
        public async Task Test3_ResidentB_CannotViewResidentA_Complaint()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, _, cA, _) = await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("user-b");
            currentUserServiceMock.Setup(s => s.IsInRole("Resident")).Returns(true);
            currentUserServiceMock.Setup(s => s.IsInRole("SystemAdmin")).Returns(false);
            currentUserServiceMock.Setup(s => s.IsInRole("Manager")).Returns(false);

            var complaintService = new ComplaintService(context, currentUserServiceMock.Object);

            var result = await complaintService.GetComplaintByIdAsync(cA.ComplaintId);
            Assert.Null(result);

            var residentServiceMock = new Mock<IResidentService>();
            var controller = new ComplaintController(complaintService, residentServiceMock.Object);
            var actionResult = await controller.Details(cA.ComplaintId);
            Assert.IsType<NotFoundResult>(actionResult);
        }

        // 4. Manager xem complaint của cả A và B -> PASS
        [Fact]
        public async Task Test4_Manager_CanViewAnyComplaint()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, _, cA, cB) = await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("manager-id");
            currentUserServiceMock.Setup(s => s.IsInRole("Manager")).Returns(true);
            currentUserServiceMock.Setup(s => s.IsInRole("SystemAdmin")).Returns(false);

            var complaintService = new ComplaintService(context, currentUserServiceMock.Object);

            var complaintA = await complaintService.GetComplaintByIdAsync(cA.ComplaintId);
            var complaintB = await complaintService.GetComplaintByIdAsync(cB.ComplaintId);

            Assert.NotNull(complaintA);
            Assert.NotNull(complaintB);
            Assert.Equal("Hành lang tầng 1 có rác", complaintA.Title);
            Assert.Equal("Tiếng ồn ban đêm", complaintB.Title);

            var allComplaints = await complaintService.GetComplaintsAsync();
            Assert.Equal(2, allComplaints.Count);
        }

        // 5. Resident A tạo complaint -> PASS (Tự suy ResidentId, không nhận ResidentId từ form)
        [Fact]
        public async Task Test5_ResidentA_CreatesComplaint_AutomaticallyBoundToResidentA()
        {
            using var context = CreateInMemoryDbContext();
            var (resA, _, apt101, _, _, _) = await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("user-a");

            var complaintService = new ComplaintService(context, currentUserServiceMock.Object);

            var model = new ComplaintCreateViewModel
            {
                Title = "Đèn cầu thang tầng 1 nhấp nháy",
                Content = "Bóng đèn chập chờn gây khó khăn khi đi lại vào ban đêm.",
                Category = "Tiện ích / Thiết bị",
                ApartmentId = apt101.ApartmentId
            };

            var (success, error) = await complaintService.CreateComplaintAsync(model);
            Assert.True(success);
            Assert.Null(error);

            var created = await context.Complaints.FirstOrDefaultAsync(c => c.Title == "Đèn cầu thang tầng 1 nhấp nháy");
            Assert.NotNull(created);
            Assert.Equal(resA.ResidentId, created.ResidentId);
            Assert.Equal("Chờ tiếp nhận", created.Status);
        }

        // 6. Resident chỉ có thể hủy complaint khi đang ở trạng thái 'Chờ tiếp nhận'
        [Fact]
        public async Task Test6_Resident_CanOnlyCancelPendingComplaint()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, _, cA, _) = await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("user-a");

            var complaintService = new ComplaintService(context, currentUserServiceMock.Object);

            // 1. Hủy khi 'Chờ tiếp nhận' -> Thành công
            var (cancelSuccess, cancelErr) = await complaintService.CancelComplaintAsync(cA.ComplaintId);
            Assert.True(cancelSuccess);
            Assert.Null(cancelErr);

            var updated = await context.Complaints.FindAsync(cA.ComplaintId);
            Assert.NotNull(updated);
            Assert.Equal("Đã hủy", updated.Status);

            // 2. Chuyển sang 'Đang xử lý', cố tình hủy -> Thất bại
            updated.Status = "Đang xử lý";
            await context.SaveChangesAsync();

            var (reCancelSuccess, reCancelErr) = await complaintService.CancelComplaintAsync(cA.ComplaintId);
            Assert.False(reCancelSuccess);
            Assert.NotNull(reCancelErr);
            Assert.Contains("Không thể hủy phản ánh đang ở trạng thái 'Đang xử lý'", reCancelErr);
        }

        // 7. Manager chuyển trạng thái sang 'Đang xử lý' -> PASS
        [Fact]
        public async Task Test7_Manager_CanStartProcessingComplaint()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, _, cA, _) = await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("manager-id");
            currentUserServiceMock.Setup(s => s.IsInRole("Manager")).Returns(true);

            var complaintService = new ComplaintService(context, currentUserServiceMock.Object);

            var model = new ComplaintProcessViewModel
            {
                ComplaintId = cA.ComplaintId,
                TargetStatus = "Đang xử lý",
                ResponseNotes = "Đã tiếp nhận và cử nhân viên lao công dọn dẹp trong 30 phút tới."
            };

            var (success, error) = await complaintService.ProcessComplaintAsync(model);
            Assert.True(success);
            Assert.Null(error);

            var updated = await context.Complaints.FindAsync(cA.ComplaintId);
            Assert.NotNull(updated);
            Assert.Equal("Đang xử lý", updated.Status);
        }

        // 8. Manager bắt buộc phải nhập ResponseNotes khi 'Đã xử lý' hoặc 'Từ chối'
        [Fact]
        public async Task Test8_Manager_MustProvideResponseNotes_WhenResolvingOrRejecting()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, _, cA, cB) = await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("manager-id");
            currentUserServiceMock.Setup(s => s.IsInRole("Manager")).Returns(true);

            var complaintService = new ComplaintService(context, currentUserServiceMock.Object);

            // 1. Chuyển sang 'Đã xử lý' không có ResponseNotes -> Thất bại
            var modelNoNotes = new ComplaintProcessViewModel
            {
                ComplaintId = cA.ComplaintId,
                TargetStatus = "Đã xử lý",
                ResponseNotes = "   " // Rỗng
            };

            var (failResolve, failResolveErr) = await complaintService.ProcessComplaintAsync(modelNoNotes);
            Assert.False(failResolve);
            Assert.Contains("Vui lòng nhập nội dung phản hồi", failResolveErr);

            // 2. Chuyển sang 'Đã xử lý' có ResponseNotes -> Thành công và set ResolvedAt
            var modelWithNotes = new ComplaintProcessViewModel
            {
                ComplaintId = cA.ComplaintId,
                TargetStatus = "Đã xử lý",
                ResponseNotes = "Khu vực tầng 1 đã được vệ sinh sạch sẽ."
            };

            var (successResolve, resolveErr) = await complaintService.ProcessComplaintAsync(modelWithNotes);
            Assert.True(successResolve);
            Assert.Null(resolveErr);

            var resolvedComplaint = await context.Complaints.FindAsync(cA.ComplaintId);
            Assert.NotNull(resolvedComplaint);
            Assert.Equal("Đã xử lý", resolvedComplaint.Status);
            Assert.NotNull(resolvedComplaint.ResolvedAt);

            // 3. Chuyển sang 'Từ chối' có ResponseNotes -> Thành công
            var modelRejectWithNotes = new ComplaintProcessViewModel
            {
                ComplaintId = cB.ComplaintId,
                TargetStatus = "Từ chối",
                ResponseNotes = "Nội dung phản ánh không thuộc phạm vi xử lý của ban quản lý."
            };

            var (successReject, rejectErr) = await complaintService.ProcessComplaintAsync(modelRejectWithNotes);
            Assert.True(successReject);
            Assert.Null(rejectErr);

            var rejectedComplaint = await context.Complaints.FindAsync(cB.ComplaintId);
            Assert.NotNull(rejectedComplaint);
            Assert.Equal("Từ chối", rejectedComplaint.Status);
        }

        // 9. Apartment Spoofing: Resident A không thể tạo complaint gắn căn hộ của Resident B
        [Fact]
        public async Task Test9_ResidentA_CannotCreateComplaintForResidentB_Apartment()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, apt102, _, _) = await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("user-a"); // Resident A (ở A101)

            var complaintService = new ComplaintService(context, currentUserServiceMock.Object);

            // Cố tình gắn căn hộ A102 của Resident B
            var spoofedModel = new ComplaintCreateViewModel
            {
                Title = "Phản ánh giả mạo căn hộ",
                Content = "Nội dung phản ánh",
                Category = "Tiếng ồn",
                ApartmentId = apt102.ApartmentId
            };

            var (success, error) = await complaintService.CreateComplaintAsync(spoofedModel);
            Assert.False(success);
            Assert.Equal("Bạn chỉ có thể gắn phản ánh vào căn hộ mà bạn hiện đang cư trú.", error);
        }

        // 10. Resident không được tự ý hoàn tất hoặc từ chối complaint
        [Fact]
        public async Task Test10_Resident_CannotCompleteOrRejectComplaint()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, _, cA, _) = await SeedDataAsync(context);

            // 1. Service-level check: Resident gọi ProcessComplaintAsync bị chặn
            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("user-a");
            currentUserServiceMock.Setup(s => s.IsInRole("Resident")).Returns(true);
            currentUserServiceMock.Setup(s => s.IsInRole("Manager")).Returns(false);
            currentUserServiceMock.Setup(s => s.IsInRole("SystemAdmin")).Returns(false);

            var complaintService = new ComplaintService(context, currentUserServiceMock.Object);

            var unauthorizedModel = new ComplaintProcessViewModel
            {
                ComplaintId = cA.ComplaintId,
                TargetStatus = "Đã xử lý",
                ResponseNotes = "Tự duyệt"
            };

            var (success, error) = await complaintService.ProcessComplaintAsync(unauthorizedModel);
            Assert.False(success);
            Assert.Equal("Bạn không có quyền xử lý phản ánh của cư dân.", error);

            // 2. Controller-level check: Process action có [Authorize(Roles = "SystemAdmin,Manager")]
            var processMethod = typeof(ComplaintController).GetMethod(nameof(ComplaintController.Process), new[] { typeof(ComplaintProcessViewModel) });
            Assert.NotNull(processMethod);

            var authAttr = processMethod.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(authAttr);
            Assert.Contains("Manager", authAttr.Roles);
            Assert.Contains("SystemAdmin", authAttr.Roles);
            Assert.DoesNotContain("Resident", authAttr.Roles);
        }
    }
}
