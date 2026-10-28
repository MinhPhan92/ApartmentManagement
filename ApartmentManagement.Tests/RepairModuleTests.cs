using System.Security.Claims;
using ApartmentManagement.Common.Security;
using ApartmentManagement.Controllers;
using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using ApartmentManagement.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ApartmentManagement.Tests
{
    public class RepairModuleTests
    {
        private ApplicationDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        private Mock<UserManager<ApplicationUser>> CreateMockUserManager(ApplicationUser tech1, ApplicationUser tech2, ApplicationUser mgr, params ApplicationUser[] otherUsers)
        {
            var store = new Mock<IUserStore<ApplicationUser>>();
            var mock = new Mock<UserManager<ApplicationUser>>(
                store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

            mock.Setup(x => x.IsInRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                .ReturnsAsync((ApplicationUser user, string role) =>
                {
                    if ((user.Id == tech1.Id || user.Id == tech2.Id) && role == AppRoles.Technician)
                        return true;
                    if (user.Id == mgr.Id && (role == AppRoles.BuildingManager || role == AppRoles.SuperAdmin))
                        return true;
                    if (role == AppRoles.Resident && otherUsers.Any(u => u.Id == user.Id))
                        return true;
                    return false;
                });

            mock.Setup(x => x.FindByIdAsync(It.IsAny<string>()))
                .ReturnsAsync((string id) =>
                {
                    if (id == tech1.Id) return tech1;
                    if (id == tech2.Id) return tech2;
                    if (id == mgr.Id) return mgr;
                    return otherUsers.FirstOrDefault(u => u.Id == id);
                });

            mock.Setup(x => x.GetUsersInRoleAsync(AppRoles.Technician))
                .ReturnsAsync(new List<ApplicationUser> { tech1, tech2 });

            return mock;
        }

        private async Task<(Building bld, Apartment apt101, Apartment apt102, Resident resA, Resident resB, ApplicationUser tech1, ApplicationUser tech2, ApplicationUser mgr)> SeedBaseDataAsync(ApplicationDbContext context)
        {
            var building = new Building
            {
                BuildingCode = "BLD-R",
                BuildingName = "Tòa Ruby",
                NumberOfFloors = 15,
                Address = "456 Giải Phóng, Hà Nội"
            };
            context.Buildings.Add(building);
            await context.SaveChangesAsync();

            var apt101 = new Apartment
            {
                BuildingId = building.BuildingId,
                ApartmentCode = "R101",
                Floor = 1,
                Area = 70m,
                Status = "Đang sử dụng"
            };

            var apt102 = new Apartment
            {
                BuildingId = building.BuildingId,
                ApartmentCode = "R102",
                Floor = 1,
                Area = 85m,
                Status = "Đang sử dụng"
            };

            context.Apartments.AddRange(apt101, apt102);
            await context.SaveChangesAsync();

            var userResA = new ApplicationUser { Id = "res_user_a", UserName = "res_a", Email = "res_a@test.local", FullName = "Nguyễn Văn Resident A", IsActive = true };
            var userResB = new ApplicationUser { Id = "res_user_b", UserName = "res_b", Email = "res_b@test.local", FullName = "Trần Thị Resident B", IsActive = true };
            var userTech1 = new ApplicationUser { Id = "tech_user_1", UserName = "tech_1", Email = "tech_1@test.local", FullName = "Kỹ thuật viên 1", IsActive = true };
            var userTech2 = new ApplicationUser { Id = "tech_user_2", UserName = "tech_2", Email = "tech_2@test.local", FullName = "Kỹ thuật viên 2", IsActive = true };
            var userMgr = new ApplicationUser { Id = "mgr_user_1", UserName = "mgr_1", Email = "mgr_1@test.local", FullName = "Quản lý tòa nhà", IsActive = true };

            context.Users.AddRange(userResA, userResB, userTech1, userTech2, userMgr);
            await context.SaveChangesAsync();

            var resA = new Resident { UserId = userResA.Id, CitizenId = "001095000001", DateOfBirth = new DateTime(1990, 1, 1), Gender = "Nam" };
            var resB = new Resident { UserId = userResB.Id, CitizenId = "001095000002", DateOfBirth = new DateTime(1992, 2, 2), Gender = "Nữ" };

            context.Residents.AddRange(resA, resB);
            await context.SaveChangesAsync();

            // resA lives in apt101 (active)
            context.ApartmentResidents.Add(new ApartmentResident
            {
                ApartmentId = apt101.ApartmentId,
                ResidentId = resA.ResidentId,
                Relationship = "Chủ hộ",
                MoveInDate = new DateTime(2023, 1, 1),
                MoveOutDate = null,
                IsOwner = true
            });

            // resB lives in apt102 (active)
            context.ApartmentResidents.Add(new ApartmentResident
            {
                ApartmentId = apt102.ApartmentId,
                ResidentId = resB.ResidentId,
                Relationship = "Chủ hộ",
                MoveInDate = new DateTime(2023, 1, 1),
                MoveOutDate = null,
                IsOwner = true
            });

            await context.SaveChangesAsync();

            return (building, apt101, apt102, resA, resB, userTech1, userTech2, userMgr);
        }

        // ==========================================
        // 1. DOMAIN & SERVICE TESTS
        // ==========================================

        [Fact]
        public async Task CreateRequestAsync_WithValidResidency_CreatesPendingRequestAndStatusHistory()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt101, _, resA, _, tech1, tech2, mgr) = await SeedBaseDataAsync(context);
            var userManagerMock = CreateMockUserManager(tech1, tech2, mgr, context.Users.First(u => u.Id == resA.UserId));
            var service = new RepairRequestService(context, userManagerMock.Object, NullLogger<RepairRequestService>.Instance);

            var result = await service.CreateRequestAsync(
                resA.ResidentId,
                apt101.ApartmentId,
                "Hỏng bóng đèn phòng khách",
                "Bóng đèn led trần chớp tắt liên tục",
                RepairPriority.Normal);

            Assert.True(result.Success);
            Assert.NotNull(result.RequestId);

            var created = await context.RepairRequests
                .Include(r => r.StatusHistory)
                .FirstOrDefaultAsync(r => r.RepairRequestId == result.RequestId.Value);

            Assert.NotNull(created);
            Assert.Equal(RepairRequestStatus.Pending, created.Status);
            Assert.Equal("Hỏng bóng đèn phòng khách", created.Title);
            Assert.Equal(apt101.ApartmentId, created.ApartmentId);
            Assert.Equal(resA.ResidentId, created.ResidentId);

            // Verify initial status history was created
            Assert.Single(created.StatusHistory);
            var history = created.StatusHistory.First();
            Assert.Null(history.PreviousStatus);
            Assert.Equal(RepairRequestStatus.Pending, history.NewStatus);
            Assert.Equal(resA.UserId, history.ChangedByUserId);
        }

        [Fact]
        public async Task CreateRequestAsync_ResidentMovedOut_FailsValidation()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt101, _, resA, _, tech1, tech2, mgr) = await SeedBaseDataAsync(context);

            // Mark resident as moved out
            var link = await context.ApartmentResidents.FirstAsync(ar => ar.ResidentId == resA.ResidentId);
            link.MoveOutDate = DateTime.UtcNow.AddDays(-1);
            await context.SaveChangesAsync();

            var userManagerMock = CreateMockUserManager(tech1, tech2, mgr, context.Users.First(u => u.Id == resA.UserId));
            var service = new RepairRequestService(context, userManagerMock.Object, NullLogger<RepairRequestService>.Instance);

            var result = await service.CreateRequestAsync(
                resA.ResidentId,
                apt101.ApartmentId,
                "Rò rỉ nước",
                "Nước nhỏ giọt dưới bồn rửa",
                RepairPriority.High);

            Assert.False(result.Success);
            Assert.Contains("không cư trú", result.ErrorMessage);
        }

        [Fact]
        public async Task CreateRequestAsync_ResidentNeverLivedInApartment_FailsValidation()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, apt102, resA, _, tech1, tech2, mgr) = await SeedBaseDataAsync(context);
            var userManagerMock = CreateMockUserManager(tech1, tech2, mgr, context.Users.First(u => u.Id == resA.UserId));
            var service = new RepairRequestService(context, userManagerMock.Object, NullLogger<RepairRequestService>.Instance);

            // resA attempts to create request for apt102 (resB's apartment)
            var result = await service.CreateRequestAsync(
                resA.ResidentId,
                apt102.ApartmentId,
                "Kiểm tra điều hòa",
                "Điều hòa không mát",
                RepairPriority.Normal);

            Assert.False(result.Success);
            Assert.Contains("không cư trú", result.ErrorMessage);
        }

        [Fact]
        public async Task CreateRequestAsync_MissingTitleOrDescription_FailsValidation()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt101, _, resA, _, tech1, tech2, mgr) = await SeedBaseDataAsync(context);
            var userManagerMock = CreateMockUserManager(tech1, tech2, mgr, context.Users.First(u => u.Id == resA.UserId));
            var service = new RepairRequestService(context, userManagerMock.Object, NullLogger<RepairRequestService>.Instance);

            var resultEmptyTitle = await service.CreateRequestAsync(
                resA.ResidentId,
                apt101.ApartmentId,
                "   ",
                "Mô tả có nội dung",
                RepairPriority.Normal);
            Assert.False(resultEmptyTitle.Success);
            Assert.Contains("tiêu đề", resultEmptyTitle.ErrorMessage);

            var resultEmptyDesc = await service.CreateRequestAsync(
                resA.ResidentId,
                apt101.ApartmentId,
                "Tiêu đề hợp lệ",
                "  ",
                RepairPriority.Normal);
            Assert.False(resultEmptyDesc.Success);
            Assert.Contains("mô tả", resultEmptyDesc.ErrorMessage);
        }

        [Fact]
        public async Task AssignTechnicianAsync_WithValidTechnician_CreatesAssignmentAndUpdatesStatus()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt101, _, resA, _, tech1, tech2, mgr) = await SeedBaseDataAsync(context);
            var userManagerMock = CreateMockUserManager(tech1, tech2, mgr, context.Users.First(u => u.Id == resA.UserId));
            var service = new RepairRequestService(context, userManagerMock.Object, NullLogger<RepairRequestService>.Instance);

            var createRes = await service.CreateRequestAsync(
                resA.ResidentId,
                apt101.ApartmentId,
                "Ống nước vỡ",
                "Ống nước nhà vệ sinh bị rò rỉ",
                RepairPriority.Urgent);

            var reqId = createRes.RequestId!.Value;

            var assignRes = await service.AssignTechnicianAsync(
                reqId,
                tech1.Id,
                mgr.Id,
                "Ưu tiên xử lý trong sáng nay");

            Assert.True(assignRes.Success);

            var req = await context.RepairRequests
                .Include(r => r.Assignments)
                .Include(r => r.StatusHistory)
                .FirstAsync(r => r.RepairRequestId == reqId);

            Assert.Equal(RepairRequestStatus.Assigned, req.Status);
            Assert.Single(req.Assignments);
            Assert.Equal(tech1.Id, req.Assignments.First().TechnicianUserId);
            Assert.Equal(RepairAssignmentStatus.Assigned, req.Assignments.First().Status);
            Assert.Equal("Ưu tiên xử lý trong sáng nay", req.Assignments.First().Note);

            // Verify status history
            var histories = req.StatusHistory.OrderBy(h => h.ChangedAt).ToList();
            Assert.Equal(2, histories.Count);
            Assert.Equal(RepairRequestStatus.Pending, histories[1].PreviousStatus);
            Assert.Equal(RepairRequestStatus.Assigned, histories[1].NewStatus);
            Assert.Equal(mgr.Id, histories[1].ChangedByUserId);
        }

        [Fact]
        public async Task AssignTechnicianAsync_NonTechnicianUser_FailsValidation()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt101, _, resA, resB, tech1, tech2, mgr) = await SeedBaseDataAsync(context);
            var userManagerMock = CreateMockUserManager(tech1, tech2, mgr, context.Users.First(u => u.Id == resA.UserId), context.Users.First(u => u.Id == resB.UserId));
            var service = new RepairRequestService(context, userManagerMock.Object, NullLogger<RepairRequestService>.Instance);

            var createRes = await service.CreateRequestAsync(
                resA.ResidentId,
                apt101.ApartmentId,
                "Chập điện",
                "CB nhảy liên tục",
                RepairPriority.High);

            var reqId = createRes.RequestId!.Value;

            // userResA is a Resident, not a Technician
            var assignRes = await service.AssignTechnicianAsync(
                reqId,
                resB.UserId,
                mgr.Id,
                "Giao cho cư dân làm kỹ thuật viên (sai quy tắc)");

            Assert.False(assignRes.Success);
            Assert.Contains("Technician", assignRes.ErrorMessage);
        }

        [Fact]
        public async Task ReassignTechnicianAsync_PreservesAssignmentHistory()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt101, _, resA, _, tech1, tech2, mgr) = await SeedBaseDataAsync(context);
            var userManagerMock = CreateMockUserManager(tech1, tech2, mgr, context.Users.First(u => u.Id == resA.UserId));
            var service = new RepairRequestService(context, userManagerMock.Object, NullLogger<RepairRequestService>.Instance);

            var createRes = await service.CreateRequestAsync(
                resA.ResidentId,
                apt101.ApartmentId,
                "Khóa cửa kẹt",
                "Khóa từ không nhận thẻ",
                RepairPriority.Normal);

            var reqId = createRes.RequestId!.Value;

            // First assignment to Tech1
            await service.AssignTechnicianAsync(reqId, tech1.Id, mgr.Id, "Phân công Tech 1");

            // Reassign to Tech2
            await service.AssignTechnicianAsync(reqId, tech2.Id, mgr.Id, "Tech 1 bận, điều phối sang Tech 2");

            var assignments = await context.RepairAssignments
                .Where(a => a.RepairRequestId == reqId)
                .OrderBy(a => a.AssignedAt)
                .ToListAsync();

            Assert.Equal(2, assignments.Count);
            // First assignment must be Reassigned with CompletedAt set
            Assert.Equal(tech1.Id, assignments[0].TechnicianUserId);
            Assert.Equal(RepairAssignmentStatus.Reassigned, assignments[0].Status);
            Assert.NotNull(assignments[0].CompletedAt);

            // Second assignment must be Assigned (active)
            Assert.Equal(tech2.Id, assignments[1].TechnicianUserId);
            Assert.Equal(RepairAssignmentStatus.Assigned, assignments[1].Status);
            Assert.Null(assignments[1].CompletedAt);
        }

        [Fact]
        public async Task TechnicianProgress_ValidFlow_UpdatesToInProgressAndPendingConfirmation()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt101, _, resA, _, tech1, tech2, mgr) = await SeedBaseDataAsync(context);
            var userManagerMock = CreateMockUserManager(tech1, tech2, mgr, context.Users.First(u => u.Id == resA.UserId));
            var service = new RepairRequestService(context, userManagerMock.Object, NullLogger<RepairRequestService>.Instance);

            var createRes = await service.CreateRequestAsync(
                resA.ResidentId,
                apt101.ApartmentId,
                "Bình nóng lạnh không sáng đèn",
                "Bình Ariston không vào điện",
                RepairPriority.High);

            var reqId = createRes.RequestId!.Value;
            await service.AssignTechnicianAsync(reqId, tech1.Id, mgr.Id, "Kiểm tra rơ-le nhiệt");

            var assignment = await context.RepairAssignments.FirstAsync(a => a.RepairRequestId == reqId);

            // Step 1: Tech1 starts work
            var startRes = await service.StartProgressAsync(assignment.AssignmentId, tech1.Id, "Đang tới căn hộ kiểm tra rơ-le");
            Assert.True(startRes.Success);

            var reqInProgress = await context.RepairRequests.FirstAsync(r => r.RepairRequestId == reqId);
            Assert.Equal(RepairRequestStatus.InProgress, reqInProgress.Status);

            var activeAssignment = await context.RepairAssignments.FirstAsync(a => a.AssignmentId == assignment.AssignmentId);
            Assert.Equal(RepairAssignmentStatus.InProgress, activeAssignment.Status);

            // Step 2: Tech1 completes work
            var completeRes = await service.CompleteWorkAsync(
                assignment.AssignmentId,
                tech1.Id,
                "Đã thay rơ-le nhiệt và kiểm tra chống giật ELCB hoạt động tốt.",
                "Đã test nóng 15 phút, nước ra bình thường.");

            Assert.True(completeRes.Success);

            var reqPendingConfirm = await context.RepairRequests.FirstAsync(r => r.RepairRequestId == reqId);
            Assert.Equal(RepairRequestStatus.PendingConfirmation, reqPendingConfirm.Status);
            Assert.Equal("Đã thay rơ-le nhiệt và kiểm tra chống giật ELCB hoạt động tốt.", reqPendingConfirm.Result);

            var completedAssignment = await context.RepairAssignments.FirstAsync(a => a.AssignmentId == assignment.AssignmentId);
            Assert.Equal(RepairAssignmentStatus.Completed, completedAssignment.Status);
            Assert.NotNull(completedAssignment.CompletedAt);
        }

        [Fact]
        public async Task Technician_CannotUpdateAnotherTechniciansAssignment()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt101, _, resA, _, tech1, tech2, mgr) = await SeedBaseDataAsync(context);
            var userManagerMock = CreateMockUserManager(tech1, tech2, mgr, context.Users.First(u => u.Id == resA.UserId));
            var service = new RepairRequestService(context, userManagerMock.Object, NullLogger<RepairRequestService>.Instance);

            var createRes = await service.CreateRequestAsync(
                resA.ResidentId,
                apt101.ApartmentId,
                "Thay vòi chậu rửa",
                "Vòi nước gãy ren",
                RepairPriority.Normal);

            var reqId = createRes.RequestId!.Value;
            await service.AssignTechnicianAsync(reqId, tech1.Id, mgr.Id, "Giao tech 1");
            var assignment = await context.RepairAssignments.FirstAsync(a => a.RepairRequestId == reqId);

            // tech2 attempts to start work on tech1's assignment
            var startRes2 = await service.StartProgressAsync(assignment.AssignmentId, tech2.Id, "Tech 2 can thiệp");
            Assert.False(startRes2.Success);
            Assert.Contains("không có quyền", startRes2.ErrorMessage);

            // Start legally with tech1
            await service.StartProgressAsync(assignment.AssignmentId, tech1.Id, null);

            // tech2 attempts to complete work on tech1's assignment
            var completeRes2 = await service.CompleteWorkAsync(assignment.AssignmentId, tech2.Id, "Tech 2 kết luận", null);
            Assert.False(completeRes2.Success);
            Assert.Contains("không có quyền", completeRes2.ErrorMessage);
        }

        [Fact]
        public async Task CompleteWorkAsync_WithoutResult_FailsValidation()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt101, _, resA, _, tech1, tech2, mgr) = await SeedBaseDataAsync(context);
            var userManagerMock = CreateMockUserManager(tech1, tech2, mgr, context.Users.First(u => u.Id == resA.UserId));
            var service = new RepairRequestService(context, userManagerMock.Object, NullLogger<RepairRequestService>.Instance);

            var createRes = await service.CreateRequestAsync(
                resA.ResidentId,
                apt101.ApartmentId,
                "Sửa chuông cửa",
                "Chuông không reo",
                RepairPriority.Normal);

            var reqId = createRes.RequestId!.Value;
            await service.AssignTechnicianAsync(reqId, tech1.Id, mgr.Id, "Giao việc");
            var assignment = await context.RepairAssignments.FirstAsync(a => a.RepairRequestId == reqId);
            await service.StartProgressAsync(assignment.AssignmentId, tech1.Id, null);

            var completeRes = await service.CompleteWorkAsync(assignment.AssignmentId, tech1.Id, "   ", null);
            Assert.False(completeRes.Success);
            Assert.Contains("kết quả xử lý", completeRes.ErrorMessage);
        }

        [Fact]
        public async Task ResidentConfirmAsync_ValidRatingAndFeedback_CompletesRequest()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt101, _, resA, _, tech1, tech2, mgr) = await SeedBaseDataAsync(context);
            var userManagerMock = CreateMockUserManager(tech1, tech2, mgr, context.Users.First(u => u.Id == resA.UserId));
            var service = new RepairRequestService(context, userManagerMock.Object, NullLogger<RepairRequestService>.Instance);

            var createRes = await service.CreateRequestAsync(
                resA.ResidentId,
                apt101.ApartmentId,
                "Thông tắc cống ban công",
                "Nước thoát chậm khi trời mưa",
                RepairPriority.High);

            var reqId = createRes.RequestId!.Value;
            await service.AssignTechnicianAsync(reqId, tech1.Id, mgr.Id, null);
            var assignment = await context.RepairAssignments.FirstAsync(a => a.RepairRequestId == reqId);
            await service.StartProgressAsync(assignment.AssignmentId, tech1.Id, null);
            await service.CompleteWorkAsync(assignment.AssignmentId, tech1.Id, "Đã dùng lò xo thông cặn, thoát tốt", null);

            // Resident confirms
            var confirmRes = await service.ConfirmCompletionAsync(
                reqId,
                resA.ResidentId,
                5,
                "Kỹ thuật viên nhiệt tình, xử lý rất sạch sẽ.");

            Assert.True(confirmRes.Success);

            var completedReq = await context.RepairRequests
                .Include(r => r.StatusHistory)
                .FirstAsync(r => r.RepairRequestId == reqId);

            Assert.Equal(RepairRequestStatus.Completed, completedReq.Status);
            Assert.Equal((byte)5, completedReq.Rating);
            Assert.Equal("Kỹ thuật viên nhiệt tình, xử lý rất sạch sẽ.", completedReq.ResidentFeedback);
            Assert.NotNull(completedReq.CompletedAt);
            Assert.NotNull(completedReq.ConfirmedAt);

            // Verify status history
            var lastHistory = completedReq.StatusHistory.OrderByDescending(h => h.ChangedAt).First();
            Assert.Equal(RepairRequestStatus.PendingConfirmation, lastHistory.PreviousStatus);
            Assert.Equal(RepairRequestStatus.Completed, lastHistory.NewStatus);
            Assert.Equal(resA.UserId, lastHistory.ChangedByUserId);
        }

        [Fact]
        public async Task ResidentConfirmAsync_InvalidStatusOrRating_FailsValidation()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt101, _, resA, _, tech1, tech2, mgr) = await SeedBaseDataAsync(context);
            var userManagerMock = CreateMockUserManager(tech1, tech2, mgr, context.Users.First(u => u.Id == resA.UserId));
            var service = new RepairRequestService(context, userManagerMock.Object, NullLogger<RepairRequestService>.Instance);

            var createRes = await service.CreateRequestAsync(
                resA.ResidentId,
                apt101.ApartmentId,
                "Đèn cầu thang hỏng",
                "Đèn chập chờn",
                RepairPriority.Low);

            var reqId = createRes.RequestId!.Value;

            // Status is Pending, cannot confirm
            var confirmPendingRes = await service.ConfirmCompletionAsync(reqId, resA.ResidentId, 5, "Nghiệm thu sớm");
            Assert.False(confirmPendingRes.Success);
            Assert.Contains("Chờ nghiệm thu", confirmPendingRes.ErrorMessage, StringComparison.OrdinalIgnoreCase);

            // Move to PendingConfirmation
            await service.AssignTechnicianAsync(reqId, tech1.Id, mgr.Id, null);
            var assignment = await context.RepairAssignments.FirstAsync(a => a.RepairRequestId == reqId);
            await service.StartProgressAsync(assignment.AssignmentId, tech1.Id, null);
            await service.CompleteWorkAsync(assignment.AssignmentId, tech1.Id, "Đã sửa xong", null);

            // Rating out of range (0 or 6)
            var confirmInvalidRating = await service.ConfirmCompletionAsync(reqId, resA.ResidentId, 6, "Điểm 6");
            Assert.False(confirmInvalidRating.Success);
            Assert.Contains("1 đến 5", confirmInvalidRating.ErrorMessage);
        }

        [Fact]
        public async Task ResidentRequestRedoAsync_ValidReason_ReopensTicket()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt101, _, resA, _, tech1, tech2, mgr) = await SeedBaseDataAsync(context);
            var userManagerMock = CreateMockUserManager(tech1, tech2, mgr, context.Users.First(u => u.Id == resA.UserId));
            var service = new RepairRequestService(context, userManagerMock.Object, NullLogger<RepairRequestService>.Instance);

            var createRes = await service.CreateRequestAsync(
                resA.ResidentId,
                apt101.ApartmentId,
                "Sửa máy hút mùi",
                "Máy kêu to",
                RepairPriority.Normal);

            var reqId = createRes.RequestId!.Value;
            await service.AssignTechnicianAsync(reqId, tech1.Id, mgr.Id, null);
            var assignment = await context.RepairAssignments.FirstAsync(a => a.RepairRequestId == reqId);
            await service.StartProgressAsync(assignment.AssignmentId, tech1.Id, null);
            await service.CompleteWorkAsync(assignment.AssignmentId, tech1.Id, "Đã siết ốc", null);

            // Resident requests redo
            var redoRes = await service.RequestRedoAsync(reqId, resA.ResidentId, "Bật thử mức 2 vẫn còn tiếng rung rất lớn");
            Assert.True(redoRes.Success);

            var reopenedReq = await context.RepairRequests
                .Include(r => r.StatusHistory)
                .FirstAsync(r => r.RepairRequestId == reqId);

            Assert.Equal(RepairRequestStatus.Reopened, reopenedReq.Status);

            var lastHistory = reopenedReq.StatusHistory.OrderByDescending(h => h.ChangedAt).First();
            Assert.Equal(RepairRequestStatus.Reopened, lastHistory.NewStatus);
            Assert.Contains("Bật thử mức 2", lastHistory.Note);
        }

        [Fact]
        public async Task CancelAsync_WhenPending_Succeeds_WhenInProgress_Fails()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt101, _, resA, _, tech1, tech2, mgr) = await SeedBaseDataAsync(context);
            var userManagerMock = CreateMockUserManager(tech1, tech2, mgr, context.Users.First(u => u.Id == resA.UserId));
            var service = new RepairRequestService(context, userManagerMock.Object, NullLogger<RepairRequestService>.Instance);

            var createRes1 = await service.CreateRequestAsync(
                resA.ResidentId,
                apt101.ApartmentId,
                "Thay pin khóa",
                "Báo pin yếu",
                RepairPriority.Low);

            var req1Id = createRes1.RequestId!.Value;

            // Can cancel when Pending
            var cancelRes1 = await service.CancelRequestAsync(req1Id, resA.ResidentId, "Tự mua pin thay được rồi");
            Assert.True(cancelRes1.Success);

            var req1 = await context.RepairRequests.FirstAsync(r => r.RepairRequestId == req1Id);
            Assert.Equal(RepairRequestStatus.Cancelled, req1.Status);

            // Request 2 moved to InProgress
            var createRes2 = await service.CreateRequestAsync(
                resA.ResidentId,
                apt101.ApartmentId,
                "Sửa van rò rỉ",
                "Van rỉ nước",
                RepairPriority.Normal);

            var req2Id = createRes2.RequestId!.Value;
            await service.AssignTechnicianAsync(req2Id, tech1.Id, mgr.Id, null);
            var assignment = await context.RepairAssignments.FirstAsync(a => a.RepairRequestId == req2Id);
            await service.StartProgressAsync(assignment.AssignmentId, tech1.Id, null);

            var cancelRes2 = await service.CancelRequestAsync(req2Id, resA.ResidentId, "Muốn hủy");
            Assert.False(cancelRes2.Success);
            Assert.Contains("Chỉ có thể hủy yêu cầu khi đang ở trạng thái", cancelRes2.ErrorMessage);
        }

        [Fact]
        public async Task ResourceIsolation_ResidentCannotViewOrActOnOtherResidentTicket()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt101, _, resA, resB, tech1, tech2, mgr) = await SeedBaseDataAsync(context);
            var userManagerMock = CreateMockUserManager(tech1, tech2, mgr, context.Users.First(u => u.Id == resA.UserId));
            var service = new RepairRequestService(context, userManagerMock.Object, NullLogger<RepairRequestService>.Instance);

            var createRes = await service.CreateRequestAsync(
                resA.ResidentId,
                apt101.ApartmentId,
                "Phiếu của Resident A",
                "Mô tả A",
                RepairPriority.Normal);

            var reqAId = createRes.RequestId!.Value;

            // Resident B calls GetRequestByIdForResidentAsync with resB.ResidentId
            var retrievedByB = await service.GetRequestByIdForResidentAsync(reqAId, resB.ResidentId);
            Assert.Null(retrievedByB);

            // Resident B attempts to cancel Resident A's ticket
            var cancelRes = await service.CancelRequestAsync(requestId: reqAId, residentId: resB.ResidentId, reason: "B gian lận");
            Assert.False(cancelRes.Success);
            Assert.Contains("Không tìm thấy", cancelRes.ErrorMessage);
        }

        [Fact]
        public async Task ConcurrencyCheck_MismatchedRowVersion_FailsGracefully()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt101, _, resA, _, tech1, tech2, mgr) = await SeedBaseDataAsync(context);
            var userManagerMock = CreateMockUserManager(tech1, tech2, mgr, context.Users.First(u => u.Id == resA.UserId));
            var service = new RepairRequestService(context, userManagerMock.Object, NullLogger<RepairRequestService>.Instance);

            var createRes = await service.CreateRequestAsync(
                resA.ResidentId,
                apt101.ApartmentId,
                "Kiểm tra xung đột",
                "Mô tả kiểm tra optimistic concurrency",
                RepairPriority.Normal);

            var reqId = createRes.RequestId!.Value;

            // Set a simulated RowVersion on the entity
            var entity = await context.RepairRequests.FirstAsync(r => r.RepairRequestId == reqId);
            entity.RowVersion = new byte[] { 1, 2, 3, 4 };
            await context.SaveChangesAsync();

            // Attempt to assign with a stale / mismatched RowVersion
            var staleRowVersion = new byte[] { 9, 9, 9, 9 };
            var assignRes = await service.AssignTechnicianAsync(
                reqId,
                tech1.Id,
                mgr.Id,
                "Ghi chú",
                staleRowVersion);

            Assert.False(assignRes.Success);
            Assert.Contains("cập nhật bởi một người dùng khác", assignRes.ErrorMessage);
        }

        // ==========================================
        // 2. AUTHORIZATION & CONTROLLER ATTRIBUTES
        // ==========================================

        [Fact]
        public void ControllerAttributes_HaveProperPolicies()
        {
            // Verify RepairRequestsController has RequireResident policy
            var reqControllerType = typeof(RepairRequestsController);
            var reqAuthorizeAttr = reqControllerType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>()
                .FirstOrDefault();

            Assert.NotNull(reqAuthorizeAttr);
            Assert.Equal(AppPolicies.RequireResident, reqAuthorizeAttr.Policy);

            // Verify RepairQueueController has RequireTechnicianOrManagement policy
            var queueControllerType = typeof(RepairQueueController);
            var queueAuthorizeAttr = queueControllerType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>()
                .FirstOrDefault();

            Assert.NotNull(queueAuthorizeAttr);
            Assert.Equal(AppPolicies.RequireTechnicianOrManagement, queueAuthorizeAttr.Policy);

            // Verify Assign action in RepairQueueController requires RequireManagement
            var assignAction = queueControllerType.GetMethod("Assign", new[] { typeof(int) });
            Assert.NotNull(assignAction);
            var assignAuthorizeAttr = assignAction.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>()
                .FirstOrDefault();

            Assert.NotNull(assignAuthorizeAttr);
            Assert.Equal(AppPolicies.RequireManagement, assignAuthorizeAttr.Policy);
        }

        [Fact]
        public async Task RepairRequestsController_Details_ReturnsNotFoundForForeignResident()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt101, _, resA, resB, tech1, tech2, mgr) = await SeedBaseDataAsync(context);

            var userManagerMock = CreateMockUserManager(tech1, tech2, mgr, context.Users.First(u => u.Id == resA.UserId));
            var service = new RepairRequestService(context, userManagerMock.Object, NullLogger<RepairRequestService>.Instance);

            var createRes = await service.CreateRequestAsync(
                resA.ResidentId,
                apt101.ApartmentId,
                "Sự cố A",
                "Mô tả sự cố A",
                RepairPriority.Normal);

            var reqAId = createRes.RequestId!.Value;

            var residentServiceMock = new Mock<IResidentService>();
            residentServiceMock.Setup(s => s.GetByUserIdAsync(resB.UserId))
                .ReturnsAsync(resB);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(c => c.UserId).Returns(resB.UserId);

            var controller = new RepairRequestsController(service, residentServiceMock.Object, currentUserServiceMock.Object);

            var result = await controller.Details(reqAId);
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task RepairQueueController_Index_TechnicianOnlySeesAssignedTasksInMyTasks()
        {
            using var context = CreateInMemoryDbContext();
            var (_, apt101, apt102, resA, resB, tech1, tech2, mgr) = await SeedBaseDataAsync(context);

            var userManagerMock = CreateMockUserManager(tech1, tech2, mgr, context.Users.First(u => u.Id == resA.UserId));
            var service = new RepairRequestService(context, userManagerMock.Object, NullLogger<RepairRequestService>.Instance);

            // Req 1 assigned to tech1
            var res1 = await service.CreateRequestAsync(resA.ResidentId, apt101.ApartmentId, "Việc của Tech 1", "Mô tả 1", RepairPriority.Normal);
            await service.AssignTechnicianAsync(res1.RequestId!.Value, tech1.Id, mgr.Id, null);

            // Req 2 assigned to tech2
            var res2 = await service.CreateRequestAsync(resB.ResidentId, apt102.ApartmentId, "Việc của Tech 2", "Mô tả 2", RepairPriority.Normal);
            await service.AssignTechnicianAsync(res2.RequestId!.Value, tech2.Id, mgr.Id, null);

            var currentUserServiceMock = new Mock<ICurrentUserService>();
            currentUserServiceMock.Setup(c => c.UserId).Returns(tech1.Id);
            currentUserServiceMock.Setup(c => c.IsInRole(AppRoles.Technician)).Returns(true);
            currentUserServiceMock.Setup(c => c.IsInRole(AppRoles.BuildingManager)).Returns(false);
            currentUserServiceMock.Setup(c => c.IsInRole(AppRoles.SuperAdmin)).Returns(false);

            var controller = new RepairQueueController(service, currentUserServiceMock.Object);

            var viewResult = await controller.Index(null, null, null, "my-tasks") as ViewResult;
            Assert.NotNull(viewResult);
            Assert.Equal("MyTasks", viewResult.ViewName);

            var model = Assert.IsAssignableFrom<IEnumerable<RepairAssignment>>(viewResult.Model);
            var list = model.ToList();

            Assert.Single(list);
            Assert.Equal(tech1.Id, list[0].TechnicianUserId);
            Assert.Equal("Việc của Tech 1", list[0].RepairRequest.Title);
        }
    }
}
