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

        // 11. SuperAdmin và BuildingManager (AppRoles) có thể xem và xử lý phản ánh
        [Fact]
        public async Task Test11_SuperAdmin_And_BuildingManager_CanViewAndProcessComplaints()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, _, cA, _) = await SeedDataAsync(context);

            // Test SuperAdmin
            var superAdminMock = new Mock<ICurrentUserService>();
            superAdminMock.Setup(s => s.UserId).Returns("superadmin-id");
            superAdminMock.Setup(s => s.IsInRole(ApartmentManagement.Common.Security.AppRoles.SuperAdmin)).Returns(true);

            var superAdminService = new ComplaintService(context, superAdminMock.Object);
            var superAdminList = await superAdminService.GetComplaintsAsync();
            Assert.Equal(2, superAdminList.Count);

            var superAdminComplaint = await superAdminService.GetComplaintByIdAsync(cA.ComplaintId);
            Assert.NotNull(superAdminComplaint);

            // Test BuildingManager
            var bldMgrMock = new Mock<ICurrentUserService>();
            bldMgrMock.Setup(s => s.UserId).Returns("bldmgr-id");
            bldMgrMock.Setup(s => s.IsInRole(ApartmentManagement.Common.Security.AppRoles.BuildingManager)).Returns(true);

            var bldMgrService = new ComplaintService(context, bldMgrMock.Object);
            var bldMgrList = await bldMgrService.GetComplaintsAsync();
            Assert.Equal(2, bldMgrList.Count);

            var processResult = await bldMgrService.ProcessComplaintAsync(new ComplaintProcessViewModel
            {
                ComplaintId = cA.ComplaintId,
                TargetStatus = "Đang xử lý"
            });
            Assert.True(processResult.Success);
        }

        // 12. GetComplaintByIdAsync với ID <= 0 trả về null
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task Test12_GetComplaintByIdAsync_ZeroOrNegativeId_ReturnsNull(int id)
        {
            using var context = CreateInMemoryDbContext();
            await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("user-a");

            var service = new ComplaintService(context, currentUserServiceMock.Object);
            var result = await service.GetComplaintByIdAsync(id);
            Assert.Null(result);
        }

        // 13. CancelComplaintAsync với ID <= 0 trả về lỗi không tìm thấy
        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public async Task Test13_CancelComplaintAsync_ZeroOrNegativeId_ReturnsNotFound(int id)
        {
            using var context = CreateInMemoryDbContext();
            await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("user-a");

            var service = new ComplaintService(context, currentUserServiceMock.Object);
            var (success, error) = await service.CancelComplaintAsync(id);
            Assert.False(success);
            Assert.Equal("Không tìm thấy phản ánh.", error);
        }

        // 14. Controller Details với ID <= 0 trả về NotFound
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task Test14_Controller_Details_ZeroOrNegativeId_ReturnsNotFound(int id)
        {
            var complaintServiceMock = new Mock<IComplaintService>();
            var residentServiceMock = new Mock<IResidentService>();

            var controller = new ComplaintController(complaintServiceMock.Object, residentServiceMock.Object);
            var result = await controller.Details(id);

            Assert.IsType<NotFoundResult>(result);
            complaintServiceMock.Verify(s => s.GetComplaintByIdAsync(It.IsAny<int>()), Times.Never);
        }

        // 15. Controller Process (GET) với ID <= 0 trả về NotFound
        [Theory]
        [InlineData(0)]
        [InlineData(-2)]
        public async Task Test15_Controller_Process_Get_ZeroOrNegativeId_ReturnsNotFound(int id)
        {
            var complaintServiceMock = new Mock<IComplaintService>();
            var residentServiceMock = new Mock<IResidentService>();

            var controller = new ComplaintController(complaintServiceMock.Object, residentServiceMock.Object);
            var result = await controller.Process(id);

            Assert.IsType<NotFoundResult>(result);
            complaintServiceMock.Verify(s => s.GetComplaintByIdAsync(It.IsAny<int>()), Times.Never);
        }

        // 16. Controller Cancel với ID <= 0 hoặc không tồn tại trả về NotFound
        [Fact]
        public async Task Test16_Controller_Cancel_ZeroOrNonExistent_ReturnsNotFound()
        {
            var complaintServiceMock = new Mock<IComplaintService>();
            var residentServiceMock = new Mock<IResidentService>();

            complaintServiceMock.Setup(s => s.CancelComplaintAsync(999))
                .ReturnsAsync((false, "Không tìm thấy phản ánh."));

            var controller = new ComplaintController(complaintServiceMock.Object, residentServiceMock.Object);

            // Zero ID
            var zeroResult = await controller.Cancel(0);
            Assert.IsType<NotFoundResult>(zeroResult);

            // Non-existent ID
            var notFoundResult = await controller.Cancel(999);
            Assert.IsType<NotFoundResult>(notFoundResult);
        }

        // 17. Controller Create (POST) với null model trả về BadRequest
        [Fact]
        public async Task Test17_Controller_Create_NullModel_ReturnsBadRequest()
        {
            var complaintServiceMock = new Mock<IComplaintService>();
            var residentServiceMock = new Mock<IResidentService>();

            var controller = new ComplaintController(complaintServiceMock.Object, residentServiceMock.Object);
            var result = await controller.Create((ComplaintCreateViewModel)null!);

            Assert.IsType<BadRequestResult>(result);
        }

        // 18. Controller Create (POST) khi ModelState không hợp lệ nạp lại ViewBag.MyApartments
        [Fact]
        public async Task Test18_Controller_Create_InvalidModelState_RepopulatesViewBagAndReturnsView()
        {
            var complaintServiceMock = new Mock<IComplaintService>();
            var residentServiceMock = new Mock<IResidentService>();

            var dummyApartments = new List<Apartment> { new Apartment { ApartmentId = 1, ApartmentCode = "A101" } };
            residentServiceMock.Setup(r => r.GetApartmentsForCurrentResidentAsync())
                .ReturnsAsync(dummyApartments);

            var controller = new ComplaintController(complaintServiceMock.Object, residentServiceMock.Object);
            controller.ModelState.AddModelError("Title", "Tiêu đề bắt buộc");

            var model = new ComplaintCreateViewModel();
            var result = await controller.Create(model);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Same(model, viewResult.Model);
            Assert.Equal(dummyApartments, controller.ViewBag.MyApartments);
        }

        // 19. Controller Process (POST) với model null hoặc ComplaintId <= 0 trả về NotFound
        [Fact]
        public async Task Test19_Controller_Process_Post_NullOrNegativeId_ReturnsNotFound()
        {
            var complaintServiceMock = new Mock<IComplaintService>();
            var residentServiceMock = new Mock<IResidentService>();

            var controller = new ComplaintController(complaintServiceMock.Object, residentServiceMock.Object);

            var nullResult = await controller.Process((ComplaintProcessViewModel)null!);
            Assert.IsType<NotFoundResult>(nullResult);

            var zeroResult = await controller.Process(new ComplaintProcessViewModel { ComplaintId = 0 });
            Assert.IsType<NotFoundResult>(zeroResult);
        }

        // 20. Controller Process (POST) khi phản ánh không tồn tại trả về NotFound
        [Fact]
        public async Task Test20_Controller_Process_Post_NonExistentComplaint_ReturnsNotFound()
        {
            var complaintServiceMock = new Mock<IComplaintService>();
            var residentServiceMock = new Mock<IResidentService>();

            complaintServiceMock.Setup(s => s.ProcessComplaintAsync(It.IsAny<ComplaintProcessViewModel>()))
                .ReturnsAsync((false, "Không tìm thấy phản ánh."));

            var controller = new ComplaintController(complaintServiceMock.Object, residentServiceMock.Object);
            var model = new ComplaintProcessViewModel { ComplaintId = 999 };

            var result = await controller.Process(model);
            Assert.IsType<NotFoundResult>(result);
        }

        // 21. Controller Process (POST) khi ModelState không hợp lệ nạp lại model từ database
        [Fact]
        public async Task Test21_Controller_Process_Post_InvalidModelState_RepopulatesModelAndReturnsView()
        {
            var complaintServiceMock = new Mock<IComplaintService>();
            var residentServiceMock = new Mock<IResidentService>();

            var existingComplaint = new Complaint
            {
                ComplaintId = 10,
                Title = "Tiêu đề gốc",
                Content = "Nội dung gốc",
                Category = "Vệ sinh",
                Status = "Chờ tiếp nhận"
            };

            complaintServiceMock.Setup(s => s.GetComplaintByIdAsync(10))
                .ReturnsAsync(existingComplaint);

            var controller = new ComplaintController(complaintServiceMock.Object, residentServiceMock.Object);
            controller.ModelState.AddModelError("TargetStatus", "Trạng thái bắt buộc");

            var postModel = new ComplaintProcessViewModel { ComplaintId = 10 };
            var result = await controller.Process(postModel);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Same(postModel, viewResult.Model);
            Assert.Equal("Tiêu đề gốc", postModel.Title);
            Assert.Equal("Nội dung gốc", postModel.Content);
        }

        // 22. CreateComplaintAsync kiểm tra dữ liệu đầu vào và các trường hợp biên
        [Fact]
        public async Task Test22_CreateComplaintAsync_EdgeCases()
        {
            using var context = CreateInMemoryDbContext();
            var (resA, _, apt101, _, _, _) = await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("user-a");

            var service = new ComplaintService(context, currentUserServiceMock.Object);

            // Null model
            var (nullSuccess, nullErr) = await service.CreateComplaintAsync(null!);
            Assert.False(nullSuccess);
            Assert.Equal("Dữ liệu không hợp lệ.", nullErr);

            // Empty title
            var (noTitleSuccess, noTitleErr) = await service.CreateComplaintAsync(new ComplaintCreateViewModel
            {
                Title = "   ",
                Content = "Nội dung hợp lệ",
                Category = "Vệ sinh"
            });
            Assert.False(noTitleSuccess);
            Assert.Equal("Vui lòng nhập tiêu đề phản ánh.", noTitleErr);

            // Title > 200 chars
            var (longTitleSuccess, longTitleErr) = await service.CreateComplaintAsync(new ComplaintCreateViewModel
            {
                Title = new string('A', 201),
                Content = "Nội dung hợp lệ",
                Category = "Vệ sinh"
            });
            Assert.False(longTitleSuccess);
            Assert.Equal("Tiêu đề không quá 200 ký tự.", longTitleErr);

            // Empty content
            var (noContentSuccess, noContentErr) = await service.CreateComplaintAsync(new ComplaintCreateViewModel
            {
                Title = "Tiêu đề hợp lệ",
                Content = "  ",
                Category = "Vệ sinh"
            });
            Assert.False(noContentSuccess);
            Assert.Equal("Vui lòng nhập nội dung chi tiết phản ánh.", noContentErr);

            // Invalid category
            var (badCatSuccess, badCatErr) = await service.CreateComplaintAsync(new ComplaintCreateViewModel
            {
                Title = "Tiêu đề",
                Content = "Nội dung",
                Category = "Hacker Category"
            });
            Assert.False(badCatSuccess);
            Assert.Equal("Danh mục phản ánh không hợp lệ.", badCatErr);

            // ApartmentId <= 0
            var (negAptSuccess, negAptErr) = await service.CreateComplaintAsync(new ComplaintCreateViewModel
            {
                Title = "Tiêu đề",
                Content = "Nội dung",
                Category = "Vệ sinh",
                ApartmentId = 0
            });
            Assert.False(negAptSuccess);
            Assert.Equal("Căn hộ không hợp lệ.", negAptErr);
        }

        // 23. ProcessComplaintAsync vòng đời trạng thái và ResolvedAt
        [Fact]
        public async Task Test23_ProcessComplaintAsync_StatusLifecycle_ResolvedAtReset()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, _, cA, _) = await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("manager-id");
            currentUserServiceMock.Setup(s => s.IsInRole(ApartmentManagement.Common.Security.AppRoles.BuildingManager)).Returns(true);

            var service = new ComplaintService(context, currentUserServiceMock.Object);

            // 1. Chuyển sang 'Đã xử lý' -> ResolvedAt có giá trị
            var (resSuccess, _) = await service.ProcessComplaintAsync(new ComplaintProcessViewModel
            {
                ComplaintId = cA.ComplaintId,
                TargetStatus = "Đã xử lý",
                ResponseNotes = "Đã giải quyết xong rác thải."
            });
            Assert.True(resSuccess);

            var resolvedItem = await context.Complaints.FindAsync(cA.ComplaintId);
            Assert.NotNull(resolvedItem);
            Assert.NotNull(resolvedItem.ResolvedAt);

            // 2. Chuyển lại về 'Đang xử lý' -> ResolvedAt được reset về null
            var (reopenSuccess, _) = await service.ProcessComplaintAsync(new ComplaintProcessViewModel
            {
                ComplaintId = cA.ComplaintId,
                TargetStatus = "Đang xử lý",
                ResponseNotes = "Phát sinh thêm việc, tiếp tục xử lý."
            });
            Assert.True(reopenSuccess);

            var reopenedItem = await context.Complaints.FindAsync(cA.ComplaintId);
            Assert.NotNull(reopenedItem);
            Assert.Null(reopenedItem.ResolvedAt);

            // 3. ResponseNotes > 1000 ký tự -> Thất bại
            var (longNotesSuccess, longNotesErr) = await service.ProcessComplaintAsync(new ComplaintProcessViewModel
            {
                ComplaintId = cA.ComplaintId,
                TargetStatus = "Đang xử lý",
                ResponseNotes = new string('X', 1001)
            });
            Assert.False(longNotesSuccess);
            Assert.Equal("Nội dung phản hồi không quá 1000 ký tự.", longNotesErr);
        }

        // 24. Tất cả POST action trong ComplaintController phải có ValidateAntiForgeryTokenAttribute
        [Fact]
        public void Test24_AllPostActions_HaveValidateAntiForgeryTokenAttribute()
        {
            var postMethods = typeof(ComplaintController)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetCustomAttribute<HttpPostAttribute>() != null)
                .ToList();

            Assert.NotEmpty(postMethods);
            foreach (var method in postMethods)
            {
                var antiForgery = method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>();
                Assert.True(antiForgery != null, $"Action {method.Name} thiếu [ValidateAntiForgeryToken]");
            }
        }

        // 25. RateComplaintAsync thành công khi đơn 'Đã xử lý' và do chính chủ sở hữu đánh giá
        [Fact]
        public async Task Test25_RateComplaint_WhenStatusIsResolved_ByOwner_Succeeds()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, _, cA, _) = await SeedDataAsync(context);

            cA.Status = "Đã xử lý";
            cA.ResolvedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("user-a");

            var service = new ComplaintService(context, currentUserServiceMock.Object);

            var (success, error) = await service.RateComplaintAsync(cA.ComplaintId, 5, "Dịch vụ giải quyết rất nhanh và thỏa đáng.");

            Assert.True(success);
            Assert.Null(error);

            var updated = await context.Complaints.FindAsync(cA.ComplaintId);
            Assert.NotNull(updated);
            Assert.Equal(5, updated.Rating);
            Assert.Equal("Dịch vụ giải quyết rất nhanh và thỏa đáng.", updated.ResidentFeedback);
            Assert.NotNull(updated.RatedAt);
        }

        // 26. RateComplaintAsync thất bại khi đơn chưa ở trạng thái 'Đã xử lý'
        [Theory]
        [InlineData("Chờ tiếp nhận")]
        [InlineData("Đang xử lý")]
        [InlineData("Từ chối")]
        [InlineData("Đã hủy")]
        public async Task Test26_RateComplaint_WhenStatusIsNotResolved_ReturnsError(string status)
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, _, cA, _) = await SeedDataAsync(context);

            cA.Status = status;
            await context.SaveChangesAsync();

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("user-a");

            var service = new ComplaintService(context, currentUserServiceMock.Object);

            var (success, error) = await service.RateComplaintAsync(cA.ComplaintId, 4, "Hài lòng.");

            Assert.False(success);
            Assert.Equal("Chỉ có thể đánh giá phản ánh đã được xử lý hoàn tất.", error);
        }

        // 27. RateComplaintAsync thất bại khi người đánh giá không phải chủ sở hữu phản ánh (chống IDOR)
        [Fact]
        public async Task Test27_RateComplaint_WhenNotOwner_ReturnsError()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, _, cA, _) = await SeedDataAsync(context);

            cA.Status = "Đã xử lý";
            cA.ResolvedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            // user-b không phải là chủ của cA
            currentUserServiceMock.Setup(s => s.UserId).Returns("user-b");

            var service = new ComplaintService(context, currentUserServiceMock.Object);

            var (success, error) = await service.RateComplaintAsync(cA.ComplaintId, 5, "Hack rating.");

            Assert.False(success);
            Assert.Equal("Bạn không có quyền đánh giá phản ánh này.", error);
        }

        // 28. RateComplaintAsync thất bại khi phản ánh đã được đánh giá trước đó (idempotency)
        [Fact]
        public async Task Test28_RateComplaint_WhenAlreadyRated_ReturnsError()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, _, cA, _) = await SeedDataAsync(context);

            cA.Status = "Đã xử lý";
            cA.ResolvedAt = DateTime.UtcNow;
            cA.Rating = 4;
            cA.ResidentFeedback = "Đã đánh giá rồi";
            cA.RatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("user-a");

            var service = new ComplaintService(context, currentUserServiceMock.Object);

            var (success, error) = await service.RateComplaintAsync(cA.ComplaintId, 5, "Đánh giá lại.");

            Assert.False(success);
            Assert.Equal("Phản ánh này đã được đánh giá trước đó.", error);
        }

        // 29. RateComplaintAsync thất bại khi điểm đánh giá nằm ngoài khoảng 1-5 sao
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(6)]
        [InlineData(10)]
        public async Task Test29_RateComplaint_InvalidRating_ReturnsError(int rating)
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, _, cA, _) = await SeedDataAsync(context);

            cA.Status = "Đã xử lý";
            cA.ResolvedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("user-a");

            var service = new ComplaintService(context, currentUserServiceMock.Object);

            var (success, error) = await service.RateComplaintAsync(cA.ComplaintId, rating, "Feedback");

            Assert.False(success);
            Assert.Equal("Đánh giá mức độ hài lòng phải từ 1 đến 5 sao.", error);
        }

        // 30. CreateComplaintAsync lưu đúng mức độ ưu tiên Priority và xử lý validate giá trị hợp lệ
        [Fact]
        public async Task Test30_CreateComplaint_WithPriority_SavesCorrectly()
        {
            using var context = CreateInMemoryDbContext();
            var (resA, _, apt101, _, _, _) = await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("user-a");

            var service = new ComplaintService(context, currentUserServiceMock.Object);

            // 1. Tạo với mức độ ưu tiên 'Khẩn cấp'
            var model1 = new ComplaintCreateViewModel
            {
                Title = "Chập điện tại tầng 1",
                Content = "Tủ điện bốc khói đen cần ngắt ngay lập tức.",
                Category = "An ninh",
                Priority = "Khẩn cấp",
                ApartmentId = apt101.ApartmentId
            };
            var (success1, error1) = await service.CreateComplaintAsync(model1);
            Assert.True(success1);
            Assert.Null(error1);

            var saved1 = await context.Complaints.FirstOrDefaultAsync(c => c.Title == "Chập điện tại tầng 1");
            Assert.NotNull(saved1);
            Assert.Equal("Khẩn cấp", saved1.Priority);

            // 2. Tạo với mức độ không hợp lệ -> Báo lỗi
            var model2 = new ComplaintCreateViewModel
            {
                Title = "Vấn đề ưu tiên sai",
                Content = "Nội dung phản ánh kiểm tra priority không hợp lệ.",
                Category = "Vệ sinh",
                Priority = "Siêu cấp",
                ApartmentId = apt101.ApartmentId
            };
            var (success2, error2) = await service.CreateComplaintAsync(model2);
            Assert.False(success2);
            Assert.Equal("Mức độ ưu tiên không hợp lệ.", error2);

            // 3. Tạo với Priority rỗng -> Mặc định 'Bình thường'
            var model3 = new ComplaintCreateViewModel
            {
                Title = "Vấn đề ưu tiên mặc định",
                Content = "Nội dung phản ánh kiểm tra priority mặc định.",
                Category = "Tiếng ồn",
                Priority = "   ",
                ApartmentId = apt101.ApartmentId
            };
            var (success3, error3) = await service.CreateComplaintAsync(model3);
            Assert.True(success3);
            Assert.Null(error3);

            var saved3 = await context.Complaints.FirstOrDefaultAsync(c => c.Title == "Vấn đề ưu tiên mặc định");
            Assert.NotNull(saved3);
            Assert.Equal("Bình thường", saved3.Priority);
        }

        // 31. ConvertToRepairRequestAsync thành công: tạo đúng RepairRequest, map ApartmentId, ResidentId, Title, Description, Priority và cập nhật Complaint
        [Fact]
        public async Task Test31_ConvertToRepairRequest_WhenValid_CreatesRepairRequestAndUpdatesComplaint()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, _, cA, _) = await SeedDataAsync(context);

            cA.Priority = "Khẩn cấp";
            cA.Status = "Chờ tiếp nhận";
            cA.ResponseNotes = null;
            await context.SaveChangesAsync();

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("manager-1");
            currentUserServiceMock.Setup(s => s.IsInRole(ApartmentManagement.Common.Security.AppRoles.BuildingManager)).Returns(true);

            var service = new ComplaintService(context, currentUserServiceMock.Object);

            var (success, repairRequestId, error) = await service.ConvertToRepairRequestAsync(cA.ComplaintId);

            Assert.True(success);
            Assert.NotNull(repairRequestId);
            Assert.Null(error);

            // Kiểm tra RepairRequest được tạo trong Db
            var repairRequest = await context.RepairRequests
                .Include(r => r.StatusHistory)
                .FirstOrDefaultAsync(r => r.RepairRequestId == repairRequestId.Value);

            Assert.NotNull(repairRequest);
            Assert.Equal(cA.ResidentId, repairRequest.ResidentId);
            Assert.Equal(cA.ApartmentId, repairRequest.ApartmentId);
            Assert.Equal(cA.Title, repairRequest.Title);
            Assert.Equal(cA.Content, repairRequest.Description);
            Assert.Equal(RepairPriority.Urgent, repairRequest.Priority);
            Assert.Equal(RepairRequestStatus.Pending, repairRequest.Status);
            Assert.NotEmpty(repairRequest.StatusHistory);

            // Kiểm tra trạng thái và ResponseNotes của Complaint
            var updatedComplaint = await context.Complaints.FindAsync(cA.ComplaintId);
            Assert.NotNull(updatedComplaint);
            Assert.Equal("Đang xử lý", updatedComplaint.Status);
            Assert.Contains($"Đã chuyển thành Yêu cầu sửa chữa mã #RR-{repairRequestId.Value}", updatedComplaint.ResponseNotes);
        }

        // 32. ConvertToRepairRequestAsync chống tạo lặp khi phản ánh đã được chuyển đổi trước đó
        [Fact]
        public async Task Test32_ConvertToRepairRequest_WhenAlreadyConverted_ReturnsError()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, _, cA, _) = await SeedDataAsync(context);

            cA.Status = "Đang xử lý";
            cA.ResponseNotes = "Đã chuyển thành Yêu cầu sửa chữa mã #RR-99 vào lúc 01/01/2026 10:00";
            await context.SaveChangesAsync();

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("manager-1");
            currentUserServiceMock.Setup(s => s.IsInRole(ApartmentManagement.Common.Security.AppRoles.BuildingManager)).Returns(true);

            var service = new ComplaintService(context, currentUserServiceMock.Object);

            var (success, repairRequestId, error) = await service.ConvertToRepairRequestAsync(cA.ComplaintId);

            Assert.False(success);
            Assert.Null(repairRequestId);
            Assert.Equal("Phản ánh này đã được chuyển tiếp sang Yêu cầu sửa chữa trước đó.", error);
        }

        // 33. ConvertToRepairRequestAsync chặn chuyển đổi khi phản ánh đã ở trạng thái Đã hủy, Từ chối, hoặc Đã xử lý
        [Theory]
        [InlineData("Đã hủy")]
        [InlineData("Từ chối")]
        [InlineData("Đã xử lý")]
        public async Task Test33_ConvertToRepairRequest_WhenStatusNotOpen_ReturnsError(string status)
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, _, cA, _) = await SeedDataAsync(context);

            cA.Status = status;
            await context.SaveChangesAsync();

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("manager-1");
            currentUserServiceMock.Setup(s => s.IsInRole(ApartmentManagement.Common.Security.AppRoles.BuildingManager)).Returns(true);

            var service = new ComplaintService(context, currentUserServiceMock.Object);

            var (success, repairRequestId, error) = await service.ConvertToRepairRequestAsync(cA.ComplaintId);

            Assert.False(success);
            Assert.Null(repairRequestId);
            Assert.Equal($"Không thể chuyển tiếp phản ánh đang ở trạng thái '{status}'.", error);
        }

        // 34. ConvertToRepairRequestAsync chặn khi người thực hiện là Cư dân và Verify Authorize Attribute trên Controller Action
        [Fact]
        public async Task Test34_ConvertToRepairRequest_WhenResidentCalls_ReturnsForbidden()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, _, cA, _) = await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("user-a");
            currentUserServiceMock.Setup(s => s.IsInRole("Resident")).Returns(true);

            var service = new ComplaintService(context, currentUserServiceMock.Object);

            var (success, repairRequestId, error) = await service.ConvertToRepairRequestAsync(cA.ComplaintId);

            Assert.False(success);
            Assert.Null(repairRequestId);
            Assert.Equal("Bạn không có quyền chuyển tiếp phản ánh sang yêu cầu sửa chữa.", error);

            // Kiểm tra AuthorizeAttribute của Action ConvertToRepairRequest trên Controller
            var actionMethod = typeof(ComplaintController).GetMethod(nameof(ComplaintController.ConvertToRepairRequest));
            Assert.NotNull(actionMethod);

            var authAttr = actionMethod.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(authAttr);
            Assert.Contains("BuildingManager", authAttr.Roles);
            Assert.Contains("Manager", authAttr.Roles);
            Assert.Contains("SuperAdmin", authAttr.Roles);
            Assert.Contains("SystemAdmin", authAttr.Roles);
            Assert.DoesNotContain("Resident", authAttr.Roles);
        }

        // 35. ConvertToRepairRequestAsync tự động gán căn hộ của cư dân nếu Complaint ban đầu không có ApartmentId
        [Fact]
        public async Task Test35_ConvertToRepairRequest_WhenNoApartmentId_UsesResidentActiveApartment()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, apt101, _, cA, _) = await SeedDataAsync(context);

            cA.ApartmentId = null; // Phản ánh khu vực chung
            cA.Status = "Chờ tiếp nhận";
            cA.ResponseNotes = null;
            await context.SaveChangesAsync();

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("manager-1");
            currentUserServiceMock.Setup(s => s.IsInRole(ApartmentManagement.Common.Security.AppRoles.BuildingManager)).Returns(true);

            var service = new ComplaintService(context, currentUserServiceMock.Object);

            var (success, repairRequestId, error) = await service.ConvertToRepairRequestAsync(cA.ComplaintId);

            Assert.True(success);
            Assert.NotNull(repairRequestId);
            Assert.Null(error);

            var repairRequest = await context.RepairRequests.FindAsync(repairRequestId.Value);
            Assert.NotNull(repairRequest);
            Assert.Equal(apt101.ApartmentId, repairRequest.ApartmentId);
        }

        // ==========================================
        // UC31 — CẬP NHẬT PHẢN ÁNH (RESIDENT UPDATE)
        // ==========================================

        // 36. Chủ sở hữu cập nhật phản ánh thành công khi ở trạng thái 'Chờ tiếp nhận'
        [Fact]
        public async Task Test36_Owner_CanUpdatePendingComplaint_Success()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, apt101, _, cA, _) = await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("user-a");
            currentUserServiceMock.Setup(s => s.IsInRole("Resident")).Returns(true);

            var service = new ComplaintService(context, currentUserServiceMock.Object);

            var model = new ComplaintEditViewModel
            {
                ComplaintId = cA.ComplaintId,
                Title = "Hành lang tầng 1 có rác (Đã bổ sung hình chụp)",
                Content = "Rác chưa dọn và có mùi hôi ở góc cầu thang bộ.",
                Category = "Vệ sinh",
                Priority = "Cao",
                ApartmentId = apt101.ApartmentId
            };

            var (success, error) = await service.UpdateComplaintAsync(model);

            Assert.True(success);
            Assert.Null(error);

            var updated = await context.Complaints.FindAsync(cA.ComplaintId);
            Assert.NotNull(updated);
            Assert.Equal("Hành lang tầng 1 có rác (Đã bổ sung hình chụp)", updated.Title);
            Assert.Equal("Rác chưa dọn và có mùi hôi ở góc cầu thang bộ.", updated.Content);
            Assert.Equal("Cao", updated.Priority);
            Assert.Equal("Chờ tiếp nhận", updated.Status);
        }

        // 37. Cư dân khác không thể chỉnh sửa phản ánh (chống IDOR)
        [Fact]
        public async Task Test37_OtherResident_CannotUpdateComplaint_IDOR()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, _, cA, _) = await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("user-b"); // Resident B
            currentUserServiceMock.Setup(s => s.IsInRole("Resident")).Returns(true);

            var service = new ComplaintService(context, currentUserServiceMock.Object);

            var model = new ComplaintEditViewModel
            {
                ComplaintId = cA.ComplaintId,
                Title = "Cố ý sửa tiêu đề của cư dân A",
                Content = "Nội dung phá hoại",
                Category = "Vệ sinh",
                Priority = "Thấp"
            };

            var (success, error) = await service.UpdateComplaintAsync(model);

            Assert.False(success);
            Assert.Equal("Bạn không có quyền thao tác trên phản ánh này.", error);

            var unchanged = await context.Complaints.FindAsync(cA.ComplaintId);
            Assert.NotNull(unchanged);
            Assert.Equal("Hành lang tầng 1 có rác", unchanged.Title);
        }

        // 38. Không thể chỉnh sửa phản ánh khi trạng thái không phải 'Chờ tiếp nhận'
        [Theory]
        [InlineData("Đang xử lý")]
        [InlineData("Đã xử lý")]
        [InlineData("Từ chối")]
        [InlineData("Đã hủy")]
        public async Task Test38_CannotUpdateComplaint_WhenNotPending(string status)
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, _, cA, _) = await SeedDataAsync(context);

            cA.Status = status;
            await context.SaveChangesAsync();

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("user-a");
            currentUserServiceMock.Setup(s => s.IsInRole("Resident")).Returns(true);

            var service = new ComplaintService(context, currentUserServiceMock.Object);

            var model = new ComplaintEditViewModel
            {
                ComplaintId = cA.ComplaintId,
                Title = "Chỉnh sửa phản ánh không ở trạng thái chờ",
                Content = "Nội dung cập nhật",
                Category = "An ninh",
                Priority = "Bình thường"
            };

            var (success, error) = await service.UpdateComplaintAsync(model);

            Assert.False(success);
            Assert.Equal($"Không thể chỉnh sửa phản ánh đang ở trạng thái '{status}'.", error);
        }

        // 39. Chỉnh sửa bảo toàn toàn bộ trường hệ thống (ResidentId, Status, CreatedAt, ResolvedAt, Rating, ResponseNotes)
        [Fact]
        public async Task Test39_CannotSpoofResidentId_OrSystemFields()
        {
            using var context = CreateInMemoryDbContext();
            var (resA, _, _, _, cA, _) = await SeedDataAsync(context);

            var originalResidentId = cA.ResidentId;
            var originalCreatedAt = cA.CreatedAt;
            var originalStatus = cA.Status;

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("user-a");
            currentUserServiceMock.Setup(s => s.IsInRole("Resident")).Returns(true);

            var service = new ComplaintService(context, currentUserServiceMock.Object);

            var model = new ComplaintEditViewModel
            {
                ComplaintId = cA.ComplaintId,
                Title = "Tiêu đề mới hợp lệ",
                Content = "Nội dung mới hợp lệ",
                Category = "Tiếng ồn",
                Priority = "Khẩn cấp"
            };

            var (success, error) = await service.UpdateComplaintAsync(model);

            Assert.True(success);
            Assert.Null(error);

            var updated = await context.Complaints.FindAsync(cA.ComplaintId);
            Assert.NotNull(updated);
            Assert.Equal(originalResidentId, updated.ResidentId);
            Assert.Equal(originalStatus, updated.Status);
            Assert.Equal(originalCreatedAt, updated.CreatedAt);
            Assert.Null(updated.ResolvedAt);
            Assert.Null(updated.Rating);
            Assert.Null(updated.ResidentFeedback);
            Assert.Null(updated.RatedAt);
            Assert.Null(updated.ResponseNotes);
        }

        // 40. Chống Apartment Spoofing khi cập nhật căn hộ
        [Fact]
        public async Task Test40_CannotUpdateWithInvalidOrSpoofedApartment()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, apt102, cA, _) = await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("user-a");
            currentUserServiceMock.Setup(s => s.IsInRole("Resident")).Returns(true);

            var service = new ComplaintService(context, currentUserServiceMock.Object);

            // 1. Gắn căn hộ A102 của Resident B
            var spoofModel = new ComplaintEditViewModel
            {
                ComplaintId = cA.ComplaintId,
                Title = "Thử gắn căn hộ người khác",
                Content = "Nội dung",
                Category = "Vệ sinh",
                Priority = "Bình thường",
                ApartmentId = apt102.ApartmentId
            };

            var (success1, error1) = await service.UpdateComplaintAsync(spoofModel);
            Assert.False(success1);
            Assert.Equal("Bạn chỉ có thể gắn phản ánh vào căn hộ mà bạn hiện đang cư trú.", error1);

            // 2. Gắn căn hộ âm / không hợp lệ
            var invalidAptModel = new ComplaintEditViewModel
            {
                ComplaintId = cA.ComplaintId,
                Title = "Căn hộ âm",
                Content = "Nội dung",
                Category = "Vệ sinh",
                Priority = "Bình thường",
                ApartmentId = -1
            };

            var (success2, error2) = await service.UpdateComplaintAsync(invalidAptModel);
            Assert.False(success2);
            Assert.Equal("Căn hộ không hợp lệ.", error2);
        }

        // 41. UpdateComplaintAsync kiểm tra dữ liệu đầu vào và các trường hợp biên
        [Fact]
        public async Task Test41_UpdateComplaint_ValidationEdgeCases()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, _, cA, _) = await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("user-a");
            currentUserServiceMock.Setup(s => s.IsInRole("Resident")).Returns(true);

            var service = new ComplaintService(context, currentUserServiceMock.Object);

            // 1. Model null
            var (s1, e1) = await service.UpdateComplaintAsync(null!);
            Assert.False(s1);
            Assert.Equal("Dữ liệu không hợp lệ.", e1);

            // 2. ComplaintId <= 0
            var (s2, e2) = await service.UpdateComplaintAsync(new ComplaintEditViewModel { ComplaintId = 0 });
            Assert.False(s2);
            Assert.Equal("Dữ liệu không hợp lệ.", e2);

            // 3. Phản ánh không tồn tại
            var (s3, e3) = await service.UpdateComplaintAsync(new ComplaintEditViewModel
            {
                ComplaintId = 99999,
                Title = "Valid",
                Content = "Valid",
                Category = "Vệ sinh"
            });
            Assert.False(s3);
            Assert.Equal("Không tìm thấy phản ánh.", e3);

            // 4. Danh mục không hợp lệ
            var (s4, e4) = await service.UpdateComplaintAsync(new ComplaintEditViewModel
            {
                ComplaintId = cA.ComplaintId,
                Title = "Valid",
                Content = "Valid",
                Category = "InvalidCategory"
            });
            Assert.False(s4);
            Assert.Equal("Danh mục phản ánh không hợp lệ.", e4);

            // 5. Mức độ ưu tiên không hợp lệ
            var (s5, e5) = await service.UpdateComplaintAsync(new ComplaintEditViewModel
            {
                ComplaintId = cA.ComplaintId,
                Title = "Valid",
                Content = "Valid",
                Category = "Vệ sinh",
                Priority = "SuperUrgent"
            });
            Assert.False(s5);
            Assert.Equal("Mức độ ưu tiên không hợp lệ.", e5);

            // 6. Tiêu đề rỗng hoặc quá 200 ký tự
            var (s6a, e6a) = await service.UpdateComplaintAsync(new ComplaintEditViewModel
            {
                ComplaintId = cA.ComplaintId,
                Title = "   ",
                Content = "Valid",
                Category = "Vệ sinh"
            });
            Assert.False(s6a);
            Assert.Equal("Vui lòng nhập tiêu đề phản ánh.", e6a);

            var (s6b, e6b) = await service.UpdateComplaintAsync(new ComplaintEditViewModel
            {
                ComplaintId = cA.ComplaintId,
                Title = new string('A', 201),
                Content = "Valid",
                Category = "Vệ sinh"
            });
            Assert.False(s6b);
            Assert.Equal("Tiêu đề không quá 200 ký tự.", e6b);

            // 7. Nội dung rỗng hoặc quá 2000 ký tự
            var (s7a, e7a) = await service.UpdateComplaintAsync(new ComplaintEditViewModel
            {
                ComplaintId = cA.ComplaintId,
                Title = "Valid",
                Content = "   ",
                Category = "Vệ sinh"
            });
            Assert.False(s7a);
            Assert.Equal("Vui lòng nhập nội dung chi tiết phản ánh.", e7a);

            var (s7b, e7b) = await service.UpdateComplaintAsync(new ComplaintEditViewModel
            {
                ComplaintId = cA.ComplaintId,
                Title = "Valid",
                Content = new string('B', 2001),
                Category = "Vệ sinh"
            });
            Assert.False(s7b);
            Assert.Equal("Nội dung không quá 2000 ký tự.", e7b);
        }

        // 42. Controller GET Edit xử lý các trường hợp: ID <= 0, không tồn tại, trạng thái không hợp lệ, hợp lệ
        [Fact]
        public async Task Test42_Controller_Edit_Get_Scenarios()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, _, cA, cB) = await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("user-a");
            currentUserServiceMock.Setup(s => s.IsInRole("Resident")).Returns(true);

            var complaintService = new ComplaintService(context, currentUserServiceMock.Object);
            var residentServiceMock = new Mock<IResidentService>();
            residentServiceMock.Setup(r => r.GetApartmentsForCurrentResidentAsync())
                .ReturnsAsync(new List<Apartment>());

            var controller = new ComplaintController(complaintService, residentServiceMock.Object);
            var tempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                new Microsoft.AspNetCore.Http.DefaultHttpContext(),
                Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
            controller.TempData = tempData;

            // 1. ID <= 0 -> NotFound
            var resNegative = await controller.Edit(0);
            Assert.IsType<NotFoundResult>(resNegative);

            // 2. ID không tồn tại hoặc của Resident B (IDOR) -> NotFound
            var resNotFound = await controller.Edit(cB.ComplaintId);
            Assert.IsType<NotFoundResult>(resNotFound);

            // 3. Status != 'Chờ tiếp nhận' -> RedirectToAction Details kèm TempData error
            cA.Status = "Đang xử lý";
            await context.SaveChangesAsync();
            var resRedirect = await controller.Edit(cA.ComplaintId);
            var redirectResult = Assert.IsType<RedirectToActionResult>(resRedirect);
            Assert.Equal("Details", redirectResult.ActionName);
            Assert.True(controller.TempData.ContainsKey("ErrorMessage"));

            // 4. Hợp lệ -> ViewResult có model ComplaintEditViewModel
            cA.Status = "Chờ tiếp nhận";
            await context.SaveChangesAsync();
            var resSuccess = await controller.Edit(cA.ComplaintId);
            var viewResult = Assert.IsType<ViewResult>(resSuccess);
            var model = Assert.IsType<ComplaintEditViewModel>(viewResult.Model);
            Assert.Equal(cA.ComplaintId, model.ComplaintId);
            Assert.Equal(cA.Title, model.Title);
        }

        // 43. Controller POST Edit xử lý các trường hợp: Mismatched ID, ModelState invalid, Service failure, Success
        [Fact]
        public async Task Test43_Controller_Edit_Post_Scenarios()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, _, cA, _) = await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("user-a");
            currentUserServiceMock.Setup(s => s.IsInRole("Resident")).Returns(true);

            var complaintService = new ComplaintService(context, currentUserServiceMock.Object);
            var residentServiceMock = new Mock<IResidentService>();
            residentServiceMock.Setup(r => r.GetApartmentsForCurrentResidentAsync())
                .ReturnsAsync(new List<Apartment>());

            var controller = new ComplaintController(complaintService, residentServiceMock.Object);
            var tempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                new Microsoft.AspNetCore.Http.DefaultHttpContext(),
                Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
            controller.TempData = tempData;

            // 1. id != model.ComplaintId -> NotFound
            var resMismatch = await controller.Edit(cA.ComplaintId + 1, new ComplaintEditViewModel { ComplaintId = cA.ComplaintId });
            Assert.IsType<NotFoundResult>(resMismatch);

            // 2. ModelState không hợp lệ -> nạp lại ViewBag và trả về View
            controller.ModelState.AddModelError("Title", "Tiêu đề bắt buộc");
            var invalidModel = new ComplaintEditViewModel { ComplaintId = cA.ComplaintId };
            var resInvalidModel = await controller.Edit(cA.ComplaintId, invalidModel);
            var viewInvalid = Assert.IsType<ViewResult>(resInvalidModel);
            Assert.NotNull(controller.ViewBag.MyApartments);

            // 3. Service trả về lỗi -> thêm ModelError và trả về View
            controller.ModelState.Clear();
            var failingModel = new ComplaintEditViewModel
            {
                ComplaintId = cA.ComplaintId,
                Title = "Valid",
                Content = "Valid",
                Category = "InvalidCategory"
            };
            var resFail = await controller.Edit(cA.ComplaintId, failingModel);
            var viewFail = Assert.IsType<ViewResult>(resFail);
            Assert.False(controller.ModelState.IsValid);

            // 4. Thành công -> TempData success và redirect về Details
            controller.ModelState.Clear();
            var successModel = new ComplaintEditViewModel
            {
                ComplaintId = cA.ComplaintId,
                Title = "Tiêu đề cập nhật thành công",
                Content = "Nội dung cập nhật thành công",
                Category = "Vệ sinh",
                Priority = "Cao"
            };
            var resSuccess = await controller.Edit(cA.ComplaintId, successModel);
            var redirectSuccess = Assert.IsType<RedirectToActionResult>(resSuccess);
            Assert.Equal("Details", redirectSuccess.ActionName);
            Assert.Equal(cA.ComplaintId, redirectSuccess.RouteValues?["id"]);
            Assert.True(controller.TempData.ContainsKey("SuccessMessage"));
        }

        // 44. Non-Resident không có quyền cập nhật phản ánh
        [Fact]
        public async Task Test44_NonResident_CannotUpdateComplaint()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, _, cA, _) = await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("user-manager");
            currentUserServiceMock.Setup(s => s.IsInRole("Resident")).Returns(false);
            currentUserServiceMock.Setup(s => s.IsInRole("Manager")).Returns(true);

            var service = new ComplaintService(context, currentUserServiceMock.Object);

            var model = new ComplaintEditViewModel
            {
                ComplaintId = cA.ComplaintId,
                Title = "Manager thử sửa",
                Content = "Nội dung",
                Category = "Vệ sinh"
            };

            var (success, error) = await service.UpdateComplaintAsync(model);
            Assert.False(success);
            Assert.Equal("Chỉ cư dân mới có quyền chỉnh sửa phản ánh.", error);
        }

        // 45. Xác minh Edit Action có phân quyền Role Resident
        [Fact]
        public void Test45_Controller_Edit_HasAuthorizeResident()
        {
            var editGetMethod = typeof(ComplaintController).GetMethod("Edit", new[] { typeof(int) });
            Assert.NotNull(editGetMethod);
            var authGetAttr = editGetMethod.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(authGetAttr);
            Assert.Equal("Resident", authGetAttr.Roles);

            var editPostMethod = typeof(ComplaintController).GetMethod("Edit", new[] { typeof(int), typeof(ComplaintEditViewModel) });
            Assert.NotNull(editPostMethod);
            var authPostAttr = editPostMethod.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(authPostAttr);
            Assert.Equal("Resident", authPostAttr.Roles);
        }
        // ==========================================
        // UC33 — BQL PHÂN LOẠI PHẢN ÁNH (RECLASSIFY)
        // ==========================================

        // 46. BQL cập nhật Category thành công
        [Fact]
        public async Task Test46_Manager_CanUpdateCategory_Successfully()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, _, cA, _) = await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("manager-1");
            currentUserServiceMock.Setup(s => s.IsInRole("Manager")).Returns(true);

            var service = new ComplaintService(context, currentUserServiceMock.Object);

            var model = new ComplaintProcessViewModel
            {
                ComplaintId = cA.ComplaintId,
                Category = "An ninh",
                Priority = cA.Priority,
                TargetStatus = "Đang xử lý",
                ResponseNotes = "Đã chuyển tổ bảo vệ kiểm tra"
            };

            var (success, error) = await service.ProcessComplaintAsync(model);

            Assert.True(success);
            Assert.Null(error);

            var updated = await context.Complaints.FindAsync(cA.ComplaintId);
            Assert.NotNull(updated);
            Assert.Equal("An ninh", updated.Category);
            Assert.Equal("Đang xử lý", updated.Status);
        }

        // 47. BQL cập nhật Priority thành công
        [Fact]
        public async Task Test47_Manager_CanUpdatePriority_Successfully()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, _, cA, _) = await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("manager-1");
            currentUserServiceMock.Setup(s => s.IsInRole("BuildingManager")).Returns(true);

            var service = new ComplaintService(context, currentUserServiceMock.Object);

            var model = new ComplaintProcessViewModel
            {
                ComplaintId = cA.ComplaintId,
                Category = cA.Category,
                Priority = "Khẩn cấp",
                TargetStatus = "Đang xử lý",
                ResponseNotes = "Nâng mức độ khẩn cấp xử lý ngay"
            };

            var (success, error) = await service.ProcessComplaintAsync(model);

            Assert.True(success);
            Assert.Null(error);

            var updated = await context.Complaints.FindAsync(cA.ComplaintId);
            Assert.NotNull(updated);
            Assert.Equal("Khẩn cấp", updated.Priority);
        }

        // 48. BQL cập nhật đồng thời Category và Priority thành công
        [Fact]
        public async Task Test48_Manager_CanUpdateBothCategoryAndPriority_Successfully()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, _, cA, _) = await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("manager-1");
            currentUserServiceMock.Setup(s => s.IsInRole("Manager")).Returns(true);

            var service = new ComplaintService(context, currentUserServiceMock.Object);

            var model = new ComplaintProcessViewModel
            {
                ComplaintId = cA.ComplaintId,
                Category = "Tiếng ồn",
                Priority = "Cao",
                TargetStatus = "Đang xử lý",
                ResponseNotes = "Điều chỉnh phân loại thành tiếng ồn mức cao"
            };

            var (success, error) = await service.ProcessComplaintAsync(model);

            Assert.True(success);
            Assert.Null(error);

            var updated = await context.Complaints.FindAsync(cA.ComplaintId);
            Assert.NotNull(updated);
            Assert.Equal("Tiếng ồn", updated.Category);
            Assert.Equal("Cao", updated.Priority);
        }

        // 49. Category không hợp lệ bị từ chối
        [Fact]
        public async Task Test49_ProcessComplaint_InvalidCategory_ReturnsError()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, _, cA, _) = await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("manager-1");
            currentUserServiceMock.Setup(s => s.IsInRole("Manager")).Returns(true);

            var service = new ComplaintService(context, currentUserServiceMock.Object);

            var model = new ComplaintProcessViewModel
            {
                ComplaintId = cA.ComplaintId,
                Category = "InvalidCategoryName",
                Priority = "Bình thường",
                TargetStatus = "Đang xử lý"
            };

            var (success, error) = await service.ProcessComplaintAsync(model);

            Assert.False(success);
            Assert.Equal("Danh mục phản ánh không hợp lệ.", error);
        }

        // 50. Priority không hợp lệ bị từ chối
        [Fact]
        public async Task Test50_ProcessComplaint_InvalidPriority_ReturnsError()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, _, cA, _) = await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("manager-1");
            currentUserServiceMock.Setup(s => s.IsInRole("Manager")).Returns(true);

            var service = new ComplaintService(context, currentUserServiceMock.Object);

            var model = new ComplaintProcessViewModel
            {
                ComplaintId = cA.ComplaintId,
                Category = "Vệ sinh",
                Priority = "SuperUltraUrgent",
                TargetStatus = "Đang xử lý"
            };

            var (success, error) = await service.ProcessComplaintAsync(model);

            Assert.False(success);
            Assert.Equal("Mức độ ưu tiên không hợp lệ.", error);
        }

        // 51. Cư dân không thể gọi Service để thay đổi phân loại (ProcessComplaintAsync)
        [Fact]
        public async Task Test51_Resident_CannotProcessOrReclassifyComplaint()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, _, cA, _) = await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("user-a");
            currentUserServiceMock.Setup(s => s.IsInRole("Resident")).Returns(true);
            currentUserServiceMock.Setup(s => s.IsInRole("Manager")).Returns(false);
            currentUserServiceMock.Setup(s => s.IsInRole("SystemAdmin")).Returns(false);

            var service = new ComplaintService(context, currentUserServiceMock.Object);

            var model = new ComplaintProcessViewModel
            {
                ComplaintId = cA.ComplaintId,
                Category = "An ninh",
                Priority = "Khẩn cấp",
                TargetStatus = "Đang xử lý"
            };

            var (success, error) = await service.ProcessComplaintAsync(model);

            Assert.False(success);
            Assert.Equal("Bạn không có quyền xử lý phản ánh của cư dân.", error);
        }

        // 52. Complaint không tồn tại bị xử lý đúng
        [Fact]
        public async Task Test52_ProcessComplaint_NonExistentComplaint_ReturnsNotFound()
        {
            using var context = CreateInMemoryDbContext();
            await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("manager-1");
            currentUserServiceMock.Setup(s => s.IsInRole("Manager")).Returns(true);

            var service = new ComplaintService(context, currentUserServiceMock.Object);

            var model = new ComplaintProcessViewModel
            {
                ComplaintId = 99999,
                Category = "Vệ sinh",
                Priority = "Bình thường",
                TargetStatus = "Đang xử lý"
            };

            var (success, error) = await service.ProcessComplaintAsync(model);

            Assert.False(success);
            Assert.Equal("Không tìm thấy phản ánh.", error);
        }

        // 53. Complaint đã bị hủy không thể bị xử lý trái với state machine hiện tại
        [Fact]
        public async Task Test53_ProcessComplaint_CancelledComplaint_CannotBeProcessed()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, _, cA, _) = await SeedDataAsync(context);

            cA.Status = "Đã hủy";
            await context.SaveChangesAsync();

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("manager-1");
            currentUserServiceMock.Setup(s => s.IsInRole("Manager")).Returns(true);

            var service = new ComplaintService(context, currentUserServiceMock.Object);

            var model = new ComplaintProcessViewModel
            {
                ComplaintId = cA.ComplaintId,
                Category = "Vệ sinh",
                Priority = "Bình thường",
                TargetStatus = "Đang xử lý"
            };

            var (success, error) = await service.ProcessComplaintAsync(model);

            Assert.False(success);
            Assert.Equal("Không thể xử lý phản ánh đã bị cư dân hủy.", error);
        }

        // 54. Complaint đã chuyển sang Repair Request không tạo thêm Repair Request trùng lặp
        [Fact]
        public async Task Test54_ConvertedComplaint_DoesNotCreateDuplicateRepairRequest()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, _, cA, _) = await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("manager-1");
            currentUserServiceMock.Setup(s => s.IsInRole("Manager")).Returns(true);

            var service = new ComplaintService(context, currentUserServiceMock.Object);

            // 1. Chuyển đổi lần 1 -> Thành công
            var (s1, reqId1, e1) = await service.ConvertToRepairRequestAsync(cA.ComplaintId);
            Assert.True(s1);
            Assert.NotNull(reqId1);

            // 2. Chuyển đổi lần 2 trên cùng một phản ánh -> Thất bại
            var (s2, reqId2, e2) = await service.ConvertToRepairRequestAsync(cA.ComplaintId);
            Assert.False(s2);
            Assert.Null(reqId2);
            Assert.Equal("Phản ánh này đã được chuyển tiếp sang Yêu cầu sửa chữa trước đó.", e2);

            var totalRepairRequests = context.RepairRequests.Count();
            Assert.Equal(1, totalRepairRequests);
        }

        // 55. Phân loại không làm thay đổi các trường hệ thống và đánh giá
        [Fact]
        public async Task Test55_ProcessComplaint_Reclassification_PreservesSystemFields()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, _, cA, _) = await SeedDataAsync(context);

            var originalResidentId = cA.ResidentId;
            var originalCreatedAt = cA.CreatedAt;
            var originalContent = cA.Content;
            var originalTitle = cA.Title;

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("manager-1");
            currentUserServiceMock.Setup(s => s.IsInRole("Manager")).Returns(true);

            var service = new ComplaintService(context, currentUserServiceMock.Object);

            var model = new ComplaintProcessViewModel
            {
                ComplaintId = cA.ComplaintId,
                Category = "Tiện ích / Thiết bị",
                Priority = "Khẩn cấp",
                TargetStatus = "Đang xử lý",
                ResponseNotes = "Phân loại lại sang thiết bị khẩn cấp"
            };

            var (success, error) = await service.ProcessComplaintAsync(model);

            Assert.True(success);
            Assert.Null(error);

            var updated = await context.Complaints.FindAsync(cA.ComplaintId);
            Assert.NotNull(updated);
            Assert.Equal(originalResidentId, updated.ResidentId);
            Assert.Equal(originalCreatedAt, updated.CreatedAt);
            Assert.Equal(originalTitle, updated.Title);
            Assert.Equal(originalContent, updated.Content);
            Assert.Null(updated.ResolvedAt);
            Assert.Null(updated.Rating);
            Assert.Null(updated.ResidentFeedback);
            Assert.Null(updated.RatedAt);
        }

        // 56. Quy tắc bắt buộc ResponseNotes khi chuyển sang 'Đã xử lý' hoặc 'Từ chối' vẫn được đảm bảo
        [Theory]
        [InlineData("Đã xử lý")]
        [InlineData("Từ chối")]
        public async Task Test56_ProcessComplaint_ResolvingOrRejecting_StillRequiresResponseNotes(string targetStatus)
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, _, cA, _) = await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("manager-1");
            currentUserServiceMock.Setup(s => s.IsInRole("Manager")).Returns(true);

            var service = new ComplaintService(context, currentUserServiceMock.Object);

            // 1. Không có ResponseNotes -> Bị từ chối
            var modelNoNotes = new ComplaintProcessViewModel
            {
                ComplaintId = cA.ComplaintId,
                Category = "Vệ sinh",
                Priority = "Bình thường",
                TargetStatus = targetStatus,
                ResponseNotes = "   "
            };

            var (s1, e1) = await service.ProcessComplaintAsync(modelNoNotes);
            Assert.False(s1);
            Assert.Equal($"Vui lòng nhập nội dung phản hồi / giải thích khi chuyển sang trạng thái '{targetStatus}'.", e1);

            // 2. Có ResponseNotes -> Thành công
            var modelWithNotes = new ComplaintProcessViewModel
            {
                ComplaintId = cA.ComplaintId,
                Category = "Vệ sinh",
                Priority = "Bình thường",
                TargetStatus = targetStatus,
                ResponseNotes = "Đã có giải pháp xử lý hoặc lý do từ chối rõ ràng."
            };

            var (s2, e2) = await service.ProcessComplaintAsync(modelWithNotes);
            Assert.True(s2);
            Assert.Null(e2);
        }

        // 57. BQL phân loại không được để trống Priority (bắt buộc chọn, không âm thầm gán mặc định)
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task Test57_ProcessComplaint_EmptyPriority_ReturnsError(string? priority)
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, _, cA, _) = await SeedDataAsync(context);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(s => s.UserId).Returns("manager-1");
            currentUserServiceMock.Setup(s => s.IsInRole("Manager")).Returns(true);

            var service = new ComplaintService(context, currentUserServiceMock.Object);

            var model = new ComplaintProcessViewModel
            {
                ComplaintId = cA.ComplaintId,
                Category = "Vệ sinh",
                Priority = priority!,
                TargetStatus = "Đang xử lý"
            };

            var (success, error) = await service.ProcessComplaintAsync(model);

            Assert.False(success);
            Assert.Equal("Vui lòng chọn mức độ ưu tiên.", error);
        }

        // 58. Model Complaint có HandlerUserId (nullable string) và quan hệ HandlerUser (ApplicationUser)
        [Fact]
        public async Task Test58_Complaint_HandlerUserId_Nullable_And_Navigation()
        {
            using var context = CreateInMemoryDbContext();
            var handlerUser = new ApplicationUser
            {
                Id = "staff-tech-01",
                UserName = "staff01@apartment.com",
                FullName = "Nguyễn Văn BQL"
            };
            context.Users.Add(handlerUser);

            var complaint = new Complaint
            {
                Title = "Kiểm tra gán người phụ trách",
                Content = "Nội dung kiểm tra HandlerUserId",
                ResidentId = 1,
                ApartmentId = 1,
                Category = "Kỹ thuật",
                Priority = "Bình thường",
                Status = "Chờ tiếp nhận",
                HandlerUserId = null // Ban đầu chưa gán
            };
            context.Complaints.Add(complaint);
            await context.SaveChangesAsync();

            Assert.Null(complaint.HandlerUserId);
            Assert.Null(complaint.HandlerUser);

            // Gán HandlerUserId
            complaint.HandlerUserId = handlerUser.Id;
            await context.SaveChangesAsync();

            var loaded = await context.Complaints
                .Include(c => c.HandlerUser)
                .FirstOrDefaultAsync(c => c.ComplaintId == complaint.ComplaintId);

            Assert.NotNull(loaded);
            Assert.Equal("staff-tech-01", loaded.HandlerUserId);
            Assert.NotNull(loaded.HandlerUser);
            Assert.Equal("Nguyễn Văn BQL", loaded.HandlerUser.FullName);
        }

        // 59. Complaint có StatusHistory collection được khởi tạo sẵn và kết nối được với ComplaintStatusHistory
        [Fact]
        public async Task Test59_Complaint_StatusHistory_CollectionNavigation()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, _, cA, _) = await SeedDataAsync(context);

            Assert.NotNull(cA.StatusHistory);
            Assert.Empty(cA.StatusHistory);

            var staffUser = new ApplicationUser
            {
                Id = "staff-manager-02",
                UserName = "manager02@apartment.com",
                FullName = "Trần Quản Lý"
            };
            context.Users.Add(staffUser);

            var historyRecord = new ComplaintStatusHistory
            {
                ComplaintId = cA.ComplaintId,
                PreviousStatus = "Chờ tiếp nhận",
                NewStatus = "Đang xử lý",
                ChangedByUserId = staffUser.Id,
                ChangedAt = DateTime.UtcNow,
                Note = "BQL đã phân công kỹ thuật viên xử lý",
                IsInternal = false
            };
            context.ComplaintStatusHistories.Add(historyRecord);
            await context.SaveChangesAsync();

            var loadedComplaint = await context.Complaints
                .Include(c => c.StatusHistory)
                .FirstOrDefaultAsync(c => c.ComplaintId == cA.ComplaintId);

            Assert.NotNull(loadedComplaint);
            Assert.Single(loadedComplaint.StatusHistory);
            var firstHistory = loadedComplaint.StatusHistory.First();
            Assert.Equal("Chờ tiếp nhận", firstHistory.PreviousStatus);
            Assert.Equal("Đang xử lý", firstHistory.NewStatus);
            Assert.Equal("staff-manager-02", firstHistory.ChangedByUserId);
            Assert.False(firstHistory.IsInternal);
        }

        // 60. ComplaintStatusHistory có IsInternal mặc định false, ChangedAt gán UTC, và navigation ngược về ChangedByUser
        [Fact]
        public async Task Test60_ComplaintStatusHistory_Defaults_And_Navigations()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, _, _, cA, _) = await SeedDataAsync(context);

            var user = new ApplicationUser
            {
                Id = "resident-user-01",
                UserName = "resident01@apartment.com",
                FullName = "Lê Cư Dân"
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var history = new ComplaintStatusHistory
            {
                ComplaintId = cA.ComplaintId,
                PreviousStatus = null,
                NewStatus = "Chờ tiếp nhận",
                ChangedByUserId = user.Id
            };

            // Kiểm tra giá trị mặc định
            Assert.False(history.IsInternal);
            Assert.True((DateTime.UtcNow - history.ChangedAt).TotalSeconds < 5);

            context.ComplaintStatusHistories.Add(history);
            await context.SaveChangesAsync();

            var loaded = await context.ComplaintStatusHistories
                .Include(h => h.Complaint)
                .Include(h => h.ChangedByUser)
                .FirstOrDefaultAsync(h => h.ComplaintStatusHistoryId == history.ComplaintStatusHistoryId);

            Assert.NotNull(loaded);
            Assert.Equal(cA.ComplaintId, loaded.Complaint.ComplaintId);
            Assert.Equal(user.Id, loaded.ChangedByUser.Id);
            Assert.Equal("Lê Cư Dân", loaded.ChangedByUser.FullName);
        }

        // 61. Metadata cấu hình DbContext: kiểm tra DeleteBehavior của HandlerUser, Complaint và ChangedByUser
        [Fact]
        public void Test61_DbContext_ModelConfiguration_VerifyDeleteBehaviorsAndKeys()
        {
            using var context = CreateInMemoryDbContext();
            var model = context.Model;

            // 1. Complaint.HandlerUser -> DeleteBehavior.SetNull
            var complaintEntity = model.FindEntityType(typeof(Complaint));
            Assert.NotNull(complaintEntity);

            var handlerFk = complaintEntity.GetForeignKeys()
                .FirstOrDefault(fk => fk.Properties.Any(p => p.Name == nameof(Complaint.HandlerUserId)));
            Assert.NotNull(handlerFk);
            Assert.Equal(DeleteBehavior.SetNull, handlerFk.DeleteBehavior);

            // 2. ComplaintStatusHistory -> Complaint (Cascade)
            var historyEntity = model.FindEntityType(typeof(ComplaintStatusHistory));
            Assert.NotNull(historyEntity);

            var complaintFk = historyEntity.GetForeignKeys()
                .FirstOrDefault(fk => fk.Properties.Any(p => p.Name == nameof(ComplaintStatusHistory.ComplaintId)));
            Assert.NotNull(complaintFk);
            Assert.Equal(DeleteBehavior.Cascade, complaintFk.DeleteBehavior);

            // 3. ComplaintStatusHistory -> ChangedByUser (Restrict)
            var changedByUserFk = historyEntity.GetForeignKeys()
                .FirstOrDefault(fk => fk.Properties.Any(p => p.Name == nameof(ComplaintStatusHistory.ChangedByUserId)));
            Assert.NotNull(changedByUserFk);
            Assert.Equal(DeleteBehavior.Restrict, changedByUserFk.DeleteBehavior);
        }
    }
}
