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
    }
}
