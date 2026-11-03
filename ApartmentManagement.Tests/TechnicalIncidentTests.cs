using ApartmentManagement.Common.Security;
using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ApartmentManagement.Tests;

public class TechnicalIncidentTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static (ApplicationDbContext context, Mock<UserManager<ApplicationUser>> mockUserManager) CreateHarness()
    {
        var context = CreateContext();

        var building = new Building
        {
            BuildingId = 1,
            BuildingCode = "T1",
            BuildingName = "Tòa Tháp A",
            Address = "123 Test St",
            NumberOfFloors = 25
        };
        context.Buildings.Add(building);

        var tech1 = new ApplicationUser
        {
            Id = "tech-user-1",
            UserName = "tech1@test.local",
            Email = "tech1@test.local",
            FullName = "Nguyễn Văn Kỹ Thuật",
            IsActive = true
        };
        var tech2 = new ApplicationUser
        {
            Id = "tech-user-2",
            UserName = "tech2@test.local",
            Email = "tech2@test.local",
            FullName = "Trần Kỹ Thuật Viên 2",
            IsActive = true
        };
        var manager = new ApplicationUser
        {
            Id = "manager-user",
            UserName = "manager@test.local",
            Email = "manager@test.local",
            FullName = "Trưởng Ban Quản Lý",
            IsActive = true
        };
        context.Users.AddRange(tech1, tech2, manager);
        context.SaveChanges();

        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        mockUserManager.Setup(m => m.FindByIdAsync("tech-user-1")).ReturnsAsync(tech1);
        mockUserManager.Setup(m => m.FindByIdAsync("tech-user-2")).ReturnsAsync(tech2);
        mockUserManager.Setup(m => m.FindByIdAsync("manager-user")).ReturnsAsync(manager);
        mockUserManager.Setup(m => m.GetUsersInRoleAsync(AppRoles.Technician)).ReturnsAsync([tech1, tech2]);

        return (context, mockUserManager);
    }

    [Fact]
    public async Task UC47_TechnicianCanReportIncident_InitializesReportedStatusAndHistory()
    {
        var (context, mockUserManager) = CreateHarness();
        var service = new TechnicalIncidentService(context, mockUserManager.Object, NullLogger<TechnicalIncidentService>.Instance);

        var result = await service.CreateIncidentAsync(
            technicianUserId: "tech-user-1",
            buildingId: 1,
            location: "Tầng hầm B1 - Cụm bơm sinh hoạt",
            category: TechnicalIncidentCategory.Water,
            title: "Rò rỉ van khóa cụm bơm",
            description: "Phát hiện rò rỉ nước tại van khóa cụm bơm phụ khi đi tuần tra.",
            priority: TechnicalIncidentPriority.High);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.IncidentId);

        var incident = await service.GetIncidentDetailsAsync(result.IncidentId.Value);
        Assert.NotNull(incident);
        Assert.Equal("Reported", incident.Status);
        Assert.Equal("High", incident.Priority);
        Assert.Equal(TechnicalIncidentCategory.Water, incident.Category);
        Assert.Equal("tech-user-1", incident.ReportedByUserId);
        Assert.StartsWith("INC-", incident.IncidentCode);

        // Verify Status History logged
        Assert.Single(incident.StatusHistory);
        var history = incident.StatusHistory.First();
        Assert.Equal("Reported", history.NewStatus);
        Assert.Null(history.PreviousStatus);
        Assert.Equal("tech-user-1", history.ChangedByUserId);
    }

    [Fact]
    public async Task UC47_CreateIncident_FailsWhenInvalidBuildingOrEmptyFields()
    {
        var (context, mockUserManager) = CreateHarness();
        var service = new TechnicalIncidentService(context, mockUserManager.Object, NullLogger<TechnicalIncidentService>.Instance);

        // Invalid building
        var res1 = await service.CreateIncidentAsync("tech-user-1", 999, "Sảnh A", TechnicalIncidentCategory.CommonArea, "Hỏng đèn", "Mô tả", "Normal");
        Assert.False(res1.Success);
        Assert.Contains("Tòa nhà", res1.ErrorMessage);

        // Empty title
        var res2 = await service.CreateIncidentAsync("tech-user-1", 1, "Sảnh A", TechnicalIncidentCategory.CommonArea, "", "Mô tả", "Normal");
        Assert.False(res2.Success);
        Assert.Contains("tiêu đề", res2.ErrorMessage);

        // Empty location
        var res3 = await service.CreateIncidentAsync("tech-user-1", 1, "", TechnicalIncidentCategory.CommonArea, "Hỏng đèn", "Mô tả", "Normal");
        Assert.False(res3.Success);
        Assert.Contains("vị trí", res3.ErrorMessage);
    }

    [Fact]
    public async Task UC48_ManagementCanAcknowledgeReportedIncident()
    {
        var (context, mockUserManager) = CreateHarness();
        var service = new TechnicalIncidentService(context, mockUserManager.Object, NullLogger<TechnicalIncidentService>.Instance);

        var createResult = await service.CreateIncidentAsync(
            "tech-user-1", 1, "Thang máy số 2", TechnicalIncidentCategory.Elevator, "Kẹt cửa tầng 8", "Thang báo lỗi E03", "Critical");
        Assert.True(createResult.Success);

        // BQL tiếp nhận báo cáo sự cố (UC48)
        var ackResult = await service.AcknowledgeIncidentAsync(
            createResult.IncidentId!.Value,
            "manager-user",
            "Đã ghi nhận, yêu cầu phân công KTV kiểm tra khẩn cấp");

        Assert.True(ackResult.Success, ackResult.ErrorMessage);

        var incident = await service.GetIncidentDetailsAsync(createResult.IncidentId.Value);
        Assert.NotNull(incident);
        Assert.Equal(TechnicalIncidentStatus.Acknowledged, incident.Status);
        Assert.Equal("manager-user", incident.AcknowledgedByUserId);
        Assert.NotNull(incident.AcknowledgedAt);

        // Status history has 2 entries (Reported -> Acknowledged)
        Assert.Equal(2, incident.StatusHistory.Count);
        var latestHistory = incident.StatusHistory.First();
        Assert.Equal(TechnicalIncidentStatus.Acknowledged, latestHistory.NewStatus);
        Assert.Equal(TechnicalIncidentStatus.Reported, latestHistory.PreviousStatus);
        Assert.Equal("manager-user", latestHistory.ChangedByUserId);
    }

    [Fact]
    public async Task UC48_CannotAcknowledgeAlreadyAcknowledgedOrClosedIncident()
    {
        var (context, mockUserManager) = CreateHarness();
        var service = new TechnicalIncidentService(context, mockUserManager.Object, NullLogger<TechnicalIncidentService>.Instance);

        var create = await service.CreateIncidentAsync("tech-user-1", 1, "Khu vực chung", TechnicalIncidentCategory.CommonArea, "Vỡ kính sảnh", "Mô tả", "Normal");
        await service.AcknowledgeIncidentAsync(create.IncidentId!.Value, "manager-user");

        // Trying to acknowledge again
        var secondAck = await service.AcknowledgeIncidentAsync(create.IncidentId!.Value, "manager-user");
        Assert.False(secondAck.Success);
        Assert.Contains("không cần tiếp nhận lại", secondAck.ErrorMessage);
    }

    [Fact]
    public async Task UC49_ManagementCanAssignTechnician_UpdatesStatusToAssignedAndRecordsHistory()
    {
        var (context, mockUserManager) = CreateHarness();
        var service = new TechnicalIncidentService(context, mockUserManager.Object, NullLogger<TechnicalIncidentService>.Instance);

        var create = await service.CreateIncidentAsync("tech-user-1", 1, "Tầng hầm B1", TechnicalIncidentCategory.Electricity, "Cháy át tổng chiếu sáng", "Mô tả", "High");
        await service.AcknowledgeIncidentAsync(create.IncidentId!.Value, "manager-user");

        // BQL phân công KTV 2 phụ trách với hạn chót hoàn thành (UC49)
        var scheduledDate = DateTime.Today.AddDays(1);
        var assignResult = await service.AssignTechnicianAsync(
            create.IncidentId!.Value,
            "tech-user-2",
            "manager-user",
            scheduledDate,
            "Ưu tiên thay át trong ca sáng.");

        Assert.True(assignResult.Success, assignResult.ErrorMessage);

        var incident = await service.GetIncidentDetailsAsync(create.IncidentId.Value);
        Assert.NotNull(incident);
        Assert.Equal(TechnicalIncidentStatus.Assigned, incident.Status);
        Assert.Equal("tech-user-2", incident.AssignedTechnicianUserId);
        Assert.NotNull(incident.AssignedAt);
        Assert.Equal(scheduledDate, incident.ScheduledDate);

        // Verify status history
        var latestHistory = incident.StatusHistory.First();
        Assert.Equal(TechnicalIncidentStatus.Assigned, latestHistory.NewStatus);
        Assert.Equal(TechnicalIncidentStatus.Acknowledged, latestHistory.PreviousStatus);
        Assert.Equal("manager-user", latestHistory.ChangedByUserId);
        Assert.Contains("Trần Kỹ Thuật Viên 2", latestHistory.Note);
    }

    [Fact]
    public async Task UC49_AssignTechnician_FailsWhenTechnicianNotFound()
    {
        var (context, mockUserManager) = CreateHarness();
        var service = new TechnicalIncidentService(context, mockUserManager.Object, NullLogger<TechnicalIncidentService>.Instance);

        var create = await service.CreateIncidentAsync("tech-user-1", 1, "Vị trí", TechnicalIncidentCategory.Other, "Tiêu đề", "Mô tả", "Normal");

        var assignResult = await service.AssignTechnicianAsync(create.IncidentId!.Value, "nonexistent-user", "manager-user");
        Assert.False(assignResult.Success);
        Assert.Contains("Không tìm thấy kỹ thuật viên", assignResult.ErrorMessage);
    }

    [Fact]
    public async Task KTV_CannotStartOrResolveIncident_IfNotAssignedTechnician()
    {
        var (context, mockUserManager) = CreateHarness();
        var service = new TechnicalIncidentService(context, mockUserManager.Object, NullLogger<TechnicalIncidentService>.Instance);

        var create = await service.CreateIncidentAsync("tech-user-1", 1, "Vị trí", TechnicalIncidentCategory.Other, "Tiêu đề", "Mô tả", "Normal");
        await service.AssignTechnicianAsync(create.IncidentId!.Value, "tech-user-1", "manager-user");

        // Tech 2 attempts to start work on Tech 1's incident
        var unauthorizedStart = await service.StartWorkAsync(create.IncidentId!.Value, "tech-user-2");
        Assert.False(unauthorizedStart.Success);
        Assert.Contains("Chỉ kỹ thuật viên được phân công", unauthorizedStart.ErrorMessage);

        // Tech 2 attempts to resolve Tech 1's incident
        var unauthorizedResolve = await service.ResolveIncidentAsync(create.IncidentId!.Value, "tech-user-2", "Đã xong");
        Assert.False(unauthorizedResolve.Success);
        Assert.Contains("Chỉ kỹ thuật viên được phân công", unauthorizedResolve.ErrorMessage);
    }

    [Fact]
    public async Task UC50_FullEndToEndWorkflow_TechnicianResolvesAndManagementCloses()
    {
        var (context, mockUserManager) = CreateHarness();
        var service = new TechnicalIncidentService(context, mockUserManager.Object, NullLogger<TechnicalIncidentService>.Instance);

        // 1. KTV 1 báo cáo sự cố (UC47)
        var create = await service.CreateIncidentAsync(
            "tech-user-1", 1, "Tháp A - Tủ điện tầng 12", TechnicalIncidentCategory.Electricity, "Hỏng công tắc tơ", "Chi tiết hỏng", "High");
        int incidentId = create.IncidentId!.Value;

        // 2. BQL tiếp nhận (UC48)
        var ack = await service.AcknowledgeIncidentAsync(incidentId, "manager-user");
        Assert.True(ack.Success);

        // 3. BQL phân công KTV 1 (UC49)
        var assign = await service.AssignTechnicianAsync(incidentId, "tech-user-1", "manager-user", DateTime.Today.AddDays(1));
        Assert.True(assign.Success);

        // 4. KTV bắt đầu xử lý
        var start = await service.StartWorkAsync(incidentId, "tech-user-1", "Bắt đầu tháo rỡ tủ điện");
        Assert.True(start.Success);

        var incInProgress = await service.GetIncidentDetailsAsync(incidentId);
        Assert.Equal(TechnicalIncidentStatus.InProgress, incInProgress!.Status);

        // 5. KTV hoàn tất và báo cáo vật tư + chi phí
        var resolve = await service.ResolveIncidentAsync(
            incidentId, "tech-user-1", "Đã thay khởi động từ 3 pha Schneider 32A, chạy thử ổn định.", "Khởi động từ 3P 32A (1 chiếc)", 450000m);
        Assert.True(resolve.Success);

        var incResolved = await service.GetIncidentDetailsAsync(incidentId);
        Assert.Equal(TechnicalIncidentStatus.Resolved, incResolved!.Status);
        Assert.Equal(450000m, incResolved.MaterialCost);
        Assert.NotNull(incResolved.ResolvedAt);

        // 6. BQL theo dõi và đóng nghiệm thu sự cố (UC50)
        var close = await service.CloseIncidentAsync(incidentId, "manager-user", "Nghiệm thu đạt yêu cầu kỹ thuật và an toàn điện.");
        Assert.True(close.Success);

        var incClosed = await service.GetIncidentDetailsAsync(incidentId);
        Assert.Equal(TechnicalIncidentStatus.Closed, incClosed!.Status);
        Assert.Equal("manager-user", incClosed.ClosedByUserId);
        Assert.NotNull(incClosed.ClosedAt);

        // Verify status history contains all 5 transitions
        Assert.Equal(6, incClosed.StatusHistory.Count);
    }

    [Fact]
    public async Task CannotUpdateIncident_WhenAlreadyClosedOrCancelled()
    {
        var (context, mockUserManager) = CreateHarness();
        var service = new TechnicalIncidentService(context, mockUserManager.Object, NullLogger<TechnicalIncidentService>.Instance);

        var create = await service.CreateIncidentAsync("tech-user-1", 1, "Vị trí", TechnicalIncidentCategory.Other, "Tiêu đề", "Mô tả", "Normal");
        int incidentId = create.IncidentId!.Value;
        await service.AssignTechnicianAsync(incidentId, "tech-user-1", "manager-user");
        await service.ResolveIncidentAsync(incidentId, "tech-user-1", "Đã sửa xong");
        await service.CloseIncidentAsync(incidentId, "manager-user", "Đóng nghiệm thu");

        // Attempting to acknowledge a closed incident
        var ack = await service.AcknowledgeIncidentAsync(incidentId, "manager-user");
        Assert.False(ack.Success);
        Assert.Contains("đã đóng hoặc đã hủy", ack.ErrorMessage);

        // Attempting to assign a closed incident
        var assign = await service.AssignTechnicianAsync(incidentId, "tech-user-2", "manager-user");
        Assert.False(assign.Success);
        Assert.Contains("đã đóng hoặc đã hủy", assign.ErrorMessage);
    }

    [Fact]
    public async Task FilterIncidents_ByStatus_Priority_Building_AndTechnician()
    {
        var (context, mockUserManager) = CreateHarness();
        var service = new TechnicalIncidentService(context, mockUserManager.Object, NullLogger<TechnicalIncidentService>.Instance);

        await service.CreateIncidentAsync("tech-user-1", 1, "Vị trí 1", TechnicalIncidentCategory.Water, "Sự cố nước", "Mô tả", "Normal");
        var inc2 = await service.CreateIncidentAsync("tech-user-2", 1, "Vị trí 2", TechnicalIncidentCategory.Electricity, "Sự cố điện", "Mô tả", "Critical");
        await service.AssignTechnicianAsync(inc2.IncidentId!.Value, "tech-user-2", "manager-user");

        // Filter by Priority Critical
        var criticals = await service.GetIncidentsAsync(priority: "Critical");
        Assert.Single(criticals);

        // Filter by Category
        var waters = await service.GetIncidentsAsync(searchTerm: "sự cố nước");
        Assert.Single(waters);

        // Filter by Technician User 2
        var tech2Incidents = await service.GetIncidentsAsync(technicianUserId: "tech-user-2");
        Assert.Single(tech2Incidents);
    }

    [Fact]
    public async Task UC49_ReassignTechnician_TracksStructuredHistory_AndTransfersPermissionsFromOldToNewTechnician()
    {
        var (context, mockUserManager) = CreateHarness();
        var service = new TechnicalIncidentService(context, mockUserManager.Object, NullLogger<TechnicalIncidentService>.Instance);

        var create = await service.CreateIncidentAsync("tech-user-1", 1, "Khu vực tầng hầm", TechnicalIncidentCategory.Electricity, "Chập bảng điện", "Mô tả", "Urgent");
        int incidentId = create.IncidentId!.Value;

        // 1. Initial assignment to Tech 1
        var assign1 = await service.AssignTechnicianAsync(incidentId, "tech-user-1", "manager-user", DateTime.Today.AddDays(1), "Phân công ban đầu");
        Assert.True(assign1.Success);

        var incAfterAssign1 = await service.GetIncidentDetailsAsync(incidentId);
        Assert.NotNull(incAfterAssign1);
        Assert.Equal("tech-user-1", incAfterAssign1.AssignedTechnicianUserId);
        
        var history1 = incAfterAssign1.StatusHistory.First();
        Assert.Equal(TechnicalIncidentStatus.Assigned, history1.NewStatus);
        Assert.Null(history1.PreviousTechnicianUserId);
        Assert.Equal("tech-user-1", history1.NewTechnicianUserId);
        Assert.Equal("manager-user", history1.ChangedByUserId);

        // 2. Reassignment from Tech 1 to Tech 2
        var assign2 = await service.AssignTechnicianAsync(incidentId, "tech-user-2", "manager-user", DateTime.Today.AddDays(2), "Chuyển giao cho KTV 2 vì KTV 1 bận ca trực");
        Assert.True(assign2.Success);

        var incAfterAssign2 = await service.GetIncidentDetailsAsync(incidentId);
        Assert.NotNull(incAfterAssign2);
        Assert.Equal("tech-user-2", incAfterAssign2.AssignedTechnicianUserId);

        var history2 = incAfterAssign2.StatusHistory.First();
        Assert.Equal(TechnicalIncidentStatus.Assigned, history2.NewStatus);
        Assert.Equal("tech-user-1", history2.PreviousTechnicianUserId);
        Assert.Equal("tech-user-2", history2.NewTechnicianUserId);
        Assert.Equal("manager-user", history2.ChangedByUserId);
        Assert.Contains("chuyển giao", history2.Note);

        // 3. Tech 1 (previous technician) loses permission to StartWork or Resolve
        var tech1Start = await service.StartWorkAsync(incidentId, "tech-user-1");
        Assert.False(tech1Start.Success);
        Assert.Contains("Chỉ kỹ thuật viên được phân công", tech1Start.ErrorMessage);

        var tech1Resolve = await service.ResolveIncidentAsync(incidentId, "tech-user-1", "Tech 1 cố tình báo cáo xong");
        Assert.False(tech1Resolve.Success);
        Assert.Contains("Chỉ kỹ thuật viên được phân công", tech1Resolve.ErrorMessage);

        // 4. Tech 2 (newly assigned technician) has full permission
        var tech2Start = await service.StartWorkAsync(incidentId, "tech-user-2", "Tech 2 bắt đầu thực hiện");
        Assert.True(tech2Start.Success);

        var tech2Resolve = await service.ResolveIncidentAsync(incidentId, "tech-user-2", "Tech 2 hoàn tất sửa chữa");
        Assert.True(tech2Resolve.Success);

        var incFinal = await service.GetIncidentDetailsAsync(incidentId);
        Assert.Equal(TechnicalIncidentStatus.Resolved, incFinal!.Status);
    }

    [Fact]
    public async Task Concurrency_StaleRowVersion_RejectedAcrossAllLifecycleActions()
    {
        var (context, mockUserManager) = CreateHarness();
        var service = new TechnicalIncidentService(context, mockUserManager.Object, NullLogger<TechnicalIncidentService>.Instance);

        var create = await service.CreateIncidentAsync("tech-user-1", 1, "Vị trí", TechnicalIncidentCategory.FireSafety, "Chuông báo cháy kêu", "Mô tả", "Urgent");
        int incidentId = create.IncidentId!.Value;

        // Set simulated initial RowVersion
        var initialIncident = await context.TechnicalIncidents.FindAsync(incidentId);
        initialIncident!.RowVersion = new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 };
        await context.SaveChangesAsync();

        byte[] staleRowVersion = new byte[] { 9, 9, 9, 9, 9, 9, 9, 9 };

        // 1. Acknowledge with stale RowVersion -> Rejected
        var ackStale = await service.AcknowledgeIncidentAsync(incidentId, "manager-user", rowVersion: staleRowVersion);
        Assert.False(ackStale.Success);
        Assert.Contains("thay đổi bởi người khác", ackStale.ErrorMessage);

        // Acknowledge with correct RowVersion -> Success
        var ackOk = await service.AcknowledgeIncidentAsync(incidentId, "manager-user", rowVersion: initialIncident.RowVersion);
        Assert.True(ackOk.Success);

        // 2. Assign with stale RowVersion -> Rejected
        var assignStale = await service.AssignTechnicianAsync(incidentId, "tech-user-1", "manager-user", rowVersion: staleRowVersion);
        Assert.False(assignStale.Success);
        Assert.Contains("thay đổi bởi người khác", assignStale.ErrorMessage);

        // Assign with correct RowVersion -> Success
        var assignOk = await service.AssignTechnicianAsync(incidentId, "tech-user-1", "manager-user", rowVersion: initialIncident.RowVersion);
        Assert.True(assignOk.Success);

        // 3. StartWork with stale RowVersion -> Rejected
        var startStale = await service.StartWorkAsync(incidentId, "tech-user-1", rowVersion: staleRowVersion);
        Assert.False(startStale.Success);
        Assert.Contains("thay đổi bởi người khác", startStale.ErrorMessage);

        // StartWork with correct RowVersion -> Success
        var startOk = await service.StartWorkAsync(incidentId, "tech-user-1", rowVersion: initialIncident.RowVersion);
        Assert.True(startOk.Success);

        // 4. Resolve with stale RowVersion -> Rejected
        var resolveStale = await service.ResolveIncidentAsync(incidentId, "tech-user-1", "Đã xử lý", rowVersion: staleRowVersion);
        Assert.False(resolveStale.Success);
        Assert.Contains("thay đổi bởi người khác", resolveStale.ErrorMessage);

        // Resolve with correct RowVersion -> Success
        var resolveOk = await service.ResolveIncidentAsync(incidentId, "tech-user-1", "Đã xử lý", rowVersion: initialIncident.RowVersion);
        Assert.True(resolveOk.Success);

        // 5. Close with stale RowVersion -> Rejected
        var closeStale = await service.CloseIncidentAsync(incidentId, "manager-user", rowVersion: staleRowVersion);
        Assert.False(closeStale.Success);
        Assert.Contains("thay đổi bởi người khác", closeStale.ErrorMessage);

        // Close with correct RowVersion -> Success
        var closeOk = await service.CloseIncidentAsync(incidentId, "manager-user", rowVersion: initialIncident.RowVersion);
        Assert.True(closeOk.Success);
    }

    [Fact]
    public async Task StateTransitions_InvalidOrder_AreSafelyRejected()
    {
        var (context, mockUserManager) = CreateHarness();
        var service = new TechnicalIncidentService(context, mockUserManager.Object, NullLogger<TechnicalIncidentService>.Instance);

        var create = await service.CreateIncidentAsync("tech-user-1", 1, "Sảnh chính", TechnicalIncidentCategory.CommonArea, "Đèn chiếu sáng chập", "Mô tả", "Normal");
        int incidentId = create.IncidentId!.Value;

        // Incident is in 'Reported' status
        // Cannot StartWork directly from Reported
        var startEarly = await service.StartWorkAsync(incidentId, "tech-user-1");
        Assert.False(startEarly.Success);

        // Cannot Resolve directly from Reported
        var resolveEarly = await service.ResolveIncidentAsync(incidentId, "tech-user-1", "Xong");
        Assert.False(resolveEarly.Success);

        // Cannot Close directly from Reported
        var closeEarly = await service.CloseIncidentAsync(incidentId, "manager-user");
        Assert.False(closeEarly.Success);
        Assert.Contains("Chỉ có thể đóng nghiệm thu", closeEarly.ErrorMessage);

        // Acknowledge incident
        await service.AcknowledgeIncidentAsync(incidentId, "manager-user");

        // Incident is in 'Acknowledged' status
        // Cannot StartWork without being Assigned
        var startWithoutAssign = await service.StartWorkAsync(incidentId, "tech-user-1");
        Assert.False(startWithoutAssign.Success);

        // Cannot Close while in Acknowledged
        var closeFromAck = await service.CloseIncidentAsync(incidentId, "manager-user");
        Assert.False(closeFromAck.Success);

        // Assign to tech-user-1
        await service.AssignTechnicianAsync(incidentId, "tech-user-1", "manager-user");

        // Cannot Close while in Assigned
        var closeFromAssigned = await service.CloseIncidentAsync(incidentId, "manager-user");
        Assert.False(closeFromAssigned.Success);

        // StartWork -> InProgress
        await service.StartWorkAsync(incidentId, "tech-user-1");

        // Cannot Close while in InProgress
        var closeFromProgress = await service.CloseIncidentAsync(incidentId, "manager-user");
        Assert.False(closeFromProgress.Success);

        // Resolve -> Resolved
        await service.ResolveIncidentAsync(incidentId, "tech-user-1", "Khắc phục triệt để");

        // Now Close succeeds
        var closeOk = await service.CloseIncidentAsync(incidentId, "manager-user");
        Assert.True(closeOk.Success);
    }
}
