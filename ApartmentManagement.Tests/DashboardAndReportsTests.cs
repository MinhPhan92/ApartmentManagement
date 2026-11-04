using System.Security.Claims;
using ApartmentManagement.Common.Security;
using ApartmentManagement.Controllers;
using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using ApartmentManagement.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ApartmentManagement.Tests;

public class DashboardAndReportsTests
{
    private ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private Mock<UserManager<ApplicationUser>> CreateUserManagerMock(ApplicationUser? returnedUser = null)
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var userManagerMock = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        userManagerMock.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(returnedUser);

        return userManagerMock;
    }

    private ControllerContext CreateControllerContext(string userId, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, $"user_{userId}")
        };

        foreach (var r in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, r));
        }

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    [Fact]
    public async Task ManagementDashboard_FinancialMetrics_OnlyCountsConfirmedPayments_ExcludesPendingAndRejected()
    {
        using var context = CreateInMemoryDbContext();
        var adminUser = new ApplicationUser { Id = "admin-1", UserName = "admin_1", FullName = "Admin User" };
        var userManagerMock = CreateUserManagerMock(adminUser);

        var invoice = new ApartmentInvoice
        {
            ApartmentId = 1,
            ResidentId = 1,
            InvoiceCode = "INV-202611-101",
            BillingYear = 2026,
            BillingMonth = 11,
            TotalAmount = 1000000m,
            Status = ApartmentInvoiceStatus.Issued
        };

        var confirmedTx = new PaymentTransaction
        {
            Invoice = invoice,
            Amount = 400000m,
            Reference = "REF-CONFIRMED",
            Status = PaymentTransactionStatus.Confirmed,
            CreatedAtUtc = DateTime.UtcNow
        };

        var pendingTx = new PaymentTransaction
        {
            Invoice = invoice,
            Amount = 300000m,
            Reference = "REF-PENDING",
            Status = PaymentTransactionStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };

        var rejectedTx = new PaymentTransaction
        {
            Invoice = invoice,
            Amount = 300000m,
            Reference = "REF-REJECTED",
            Status = PaymentTransactionStatus.Rejected,
            CreatedAtUtc = DateTime.UtcNow
        };

        context.AddRange(adminUser, invoice, confirmedTx, pendingTx, rejectedTx);
        await context.SaveChangesAsync();

        var controller = new DashboardController(context, userManagerMock.Object)
        {
            ControllerContext = CreateControllerContext("admin-1", AppRoles.BuildingManager)
        };

        var actionResult = await controller.Index();
        var viewResult = Assert.IsType<ViewResult>(actionResult);
        var model = Assert.IsType<ManagementDashboardViewModel>(viewResult.Model);

        Assert.Equal(1, model.TotalIssuedInvoices);
        Assert.Equal(1000000m, model.TotalBilledAmount);
        // Doanh thu đã thu CHỈ tính confirmed payment (400,000 đ), pending và rejected bị loại bỏ hoàn toàn
        Assert.Equal(400000m, model.TotalCollectedAmount);
        Assert.Equal(600000m, model.TotalOutstandingAmount);
        Assert.Equal(40.0, model.CollectionRate);
        Assert.Equal(1, model.PartialPaidInvoices);
        Assert.Equal(0, model.FullyPaidInvoices);
    }

    [Fact]
    public async Task ManagementDashboard_MultipleConfirmedTransactions_DoesNotDoubleCountInvoice()
    {
        using var context = CreateInMemoryDbContext();
        var adminUser = new ApplicationUser { Id = "admin-2", UserName = "admin_2", FullName = "Admin User 2" };
        var userManagerMock = CreateUserManagerMock(adminUser);

        var invoice = new ApartmentInvoice
        {
            ApartmentId = 1,
            ResidentId = 1,
            InvoiceCode = "INV-202611-102",
            BillingYear = 2026,
            BillingMonth = 11,
            TotalAmount = 1000000m,
            Status = ApartmentInvoiceStatus.Issued
        };

        // 2 giao dịch Confirmed trên cùng 1 hóa đơn (300k + 700k = 1M)
        var tx1 = new PaymentTransaction
        {
            Invoice = invoice,
            Amount = 300000m,
            Reference = "REF-TX-1",
            Status = PaymentTransactionStatus.Confirmed,
            CreatedAtUtc = DateTime.UtcNow
        };
        var tx2 = new PaymentTransaction
        {
            Invoice = invoice,
            Amount = 700000m,
            Reference = "REF-TX-2",
            Status = PaymentTransactionStatus.Confirmed,
            CreatedAtUtc = DateTime.UtcNow
        };

        context.AddRange(adminUser, invoice, tx1, tx2);
        await context.SaveChangesAsync();

        var controller = new DashboardController(context, userManagerMock.Object)
        {
            ControllerContext = CreateControllerContext("admin-2", AppRoles.SuperAdmin)
        };

        var actionResult = await controller.Index();
        var viewResult = Assert.IsType<ViewResult>(actionResult);
        var model = Assert.IsType<ManagementDashboardViewModel>(viewResult.Model);

        // Đảm bảo không đếm trùng hóa đơn: chỉ có 1 hóa đơn duy nhất
        Assert.Equal(1, model.TotalIssuedInvoices);
        Assert.Equal(1, model.FullyPaidInvoices);
        Assert.Equal(0, model.PartialPaidInvoices);
        Assert.Equal(0, model.UnpaidInvoices);
        Assert.Equal(1000000m, model.TotalCollectedAmount);
        Assert.Equal(0m, model.TotalOutstandingAmount);
        Assert.Equal(100.0, model.CollectionRate);
    }

    [Fact]
    public async Task ManagementDashboard_EmptyDatabase_HandlesGracefullyWithoutDivisionByZero()
    {
        using var context = CreateInMemoryDbContext();
        var adminUser = new ApplicationUser { Id = "admin-empty", UserName = "admin_empty" };
        var userManagerMock = CreateUserManagerMock(adminUser);

        var controller = new DashboardController(context, userManagerMock.Object)
        {
            ControllerContext = CreateControllerContext("admin-empty", AppRoles.BuildingManager)
        };

        var actionResult = await controller.Index();
        var viewResult = Assert.IsType<ViewResult>(actionResult);
        var model = Assert.IsType<ManagementDashboardViewModel>(viewResult.Model);

        Assert.Equal(0, model.TotalBuildings);
        Assert.Equal(0, model.TotalApartments);
        Assert.Equal(0.0, model.OccupancyRate);
        Assert.Equal(0m, model.TotalBilledAmount);
        Assert.Equal(0m, model.TotalCollectedAmount);
        Assert.Equal(0.0, model.CollectionRate);
        Assert.Empty(model.RecentBuildings);
    }

    [Fact]
    public async Task ResidentDashboard_DataIsolation_OnlyReturnsDataForCurrentResident()
    {
        using var context = CreateInMemoryDbContext();

        var building = new Building { BuildingCode = "T1", BuildingName = "Tòa T1" };
        var userA = new ApplicationUser { Id = "res-user-A", FullName = "Cư Dân A" };
        var userB = new ApplicationUser { Id = "res-user-B", FullName = "Cư Dân B" };

        var residentA = new Resident { UserId = userA.Id, CitizenId = "001122334411" };
        var residentB = new Resident { UserId = userB.Id, CitizenId = "001122334422" };

        var aptA = new Apartment { ApartmentCode = "A-101", Floor = 1, Area = 70m, Status = "Đang ở", Building = building };
        var aptB = new Apartment { ApartmentCode = "B-202", Floor = 2, Area = 90m, Status = "Đang ở", Building = building };

        var occA = new ApartmentResident { Resident = residentA, Apartment = aptA, MoveInDate = DateTime.Today, IsOwner = true };
        var occB = new ApartmentResident { Resident = residentB, Apartment = aptB, MoveInDate = DateTime.Today, IsOwner = true };

        var vehicleA = new Vehicle { Resident = residentA, Apartment = aptA, LicensePlate = "30A-11111", VehicleType = "Ô tô", Status = "Đã duyệt" };
        var vehicleB = new Vehicle { Resident = residentB, Apartment = aptB, LicensePlate = "30B-22222", VehicleType = "Xe máy", Status = "Đã duyệt" };

        var repairA = new RepairRequest { Resident = residentA, Apartment = aptA, Title = "Hỏng đèn căn A", Status = RepairRequestStatus.Pending };
        var repairB = new RepairRequest { Resident = residentB, Apartment = aptB, Title = "Hỏng vòi căn B", Status = RepairRequestStatus.Pending };

        context.AddRange(building, userA, userB, residentA, residentB, aptA, aptB, occA, occB, vehicleA, vehicleB, repairA, repairB);
        await context.SaveChangesAsync();

        var userManagerMock = CreateUserManagerMock(userA);

        var controller = new DashboardController(context, userManagerMock.Object)
        {
            ControllerContext = CreateControllerContext(userA.Id, AppRoles.Resident)
        };

        var actionResult = await controller.Resident();
        var viewResult = Assert.IsType<ViewResult>(actionResult);
        var model = Assert.IsType<ResidentDashboardViewModel>(viewResult.Model);

        Assert.NotNull(model.ActiveApartment);
        Assert.Equal("A-101", model.ActiveApartment.ApartmentCode);
        Assert.Single(model.RegisteredVehicles);
        Assert.Equal("30A-11111", model.RegisteredVehicles[0].LicensePlate);
        Assert.Single(model.RecentRepairRequests);
        Assert.Equal("Hỏng đèn căn A", model.RecentRepairRequests[0].Title);
    }

    [Fact]
    public async Task TechnicianDashboard_DataIsolation_OnlyReturnsTasksAssignedToCurrentTechnician()
    {
        using var context = CreateInMemoryDbContext();

        var building = new Building { BuildingCode = "T1", BuildingName = "Tòa T1" };
        var apt = new Apartment { ApartmentCode = "A-101", Floor = 1, Area = 70m, Status = "Đang ở", Building = building };
        var resUser = new ApplicationUser { Id = "res-1", FullName = "Cư Dân 1" };
        var resident = new Resident { UserId = resUser.Id, CitizenId = "001122334400" };

        var techUser1 = new ApplicationUser { Id = "tech-1", FullName = "KTV Nguyễn Văn A" };
        var techUser2 = new ApplicationUser { Id = "tech-2", FullName = "KTV Trần Văn B" };

        var req1 = new RepairRequest { Resident = resident, Apartment = apt, Title = "Sự cố thang máy", Status = RepairRequestStatus.InProgress, Priority = RepairPriority.Urgent };
        var req2 = new RepairRequest { Resident = resident, Apartment = apt, Title = "Rò nước hành lang", Status = RepairRequestStatus.Assigned, Priority = RepairPriority.Normal };
        var reqOther = new RepairRequest { Resident = resident, Apartment = apt, Title = "Cháy bóng đèn sảnh", Status = RepairRequestStatus.Assigned, Priority = RepairPriority.Low };

        var assign1 = new RepairAssignment { TechnicianUserId = techUser1.Id, RepairRequest = req1, Status = RepairAssignmentStatus.InProgress, AssignedAt = DateTime.UtcNow };
        var assign2 = new RepairAssignment { TechnicianUserId = techUser1.Id, RepairRequest = req2, Status = RepairAssignmentStatus.Assigned, AssignedAt = DateTime.UtcNow };
        var assignOther = new RepairAssignment { TechnicianUserId = techUser2.Id, RepairRequest = reqOther, Status = RepairAssignmentStatus.Assigned, AssignedAt = DateTime.UtcNow };

        context.AddRange(building, apt, resUser, resident, techUser1, techUser2, req1, req2, reqOther, assign1, assign2, assignOther);
        await context.SaveChangesAsync();

        var userManagerMock = CreateUserManagerMock(techUser1);

        var controller = new DashboardController(context, userManagerMock.Object)
        {
            ControllerContext = CreateControllerContext(techUser1.Id, AppRoles.Technician)
        };

        var actionResult = await controller.Technician();
        var viewResult = Assert.IsType<ViewResult>(actionResult);
        var model = Assert.IsType<TechnicianDashboardViewModel>(viewResult.Model);

        Assert.Equal(1, model.AssignedCount);
        Assert.Equal(1, model.InProgressCount);
        Assert.Equal(2, model.ActiveTasks.Count);
        // Đảm bảo không nhìn thấy việc của KTV khác
        Assert.DoesNotContain(model.ActiveTasks, t => t.RepairRequest.Title == "Cháy bóng đèn sảnh");
    }

    [Fact]
    public async Task DashboardController_ResidentAction_ReturnsResidentViewModel_WithScopedData()
    {
        using var context = CreateInMemoryDbContext();
        var residentUser = new ApplicationUser { Id = "res-direct", FullName = "Cư Dân Thử Nghiệm", Email = "resident@test.local" };
        var userManagerMock = CreateUserManagerMock(residentUser);

        var controller = new DashboardController(context, userManagerMock.Object)
        {
            ControllerContext = CreateControllerContext(residentUser.Id, AppRoles.Resident)
        };

        var result = await controller.Resident();
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ResidentDashboardViewModel>(viewResult.Model);
        Assert.Equal("Cư Dân Thử Nghiệm", model.UserFullName);
        Assert.Equal("resident@test.local", model.UserEmail);
    }

    [Fact]
    public async Task DashboardController_TechnicianAction_ReturnsTechnicianViewModel_WithAssignedTasks()
    {
        using var context = CreateInMemoryDbContext();
        var techUser = new ApplicationUser { Id = "tech-direct", FullName = "KTV Thử Nghiệm", Email = "ktv@test.local" };
        var userManagerMock = CreateUserManagerMock(techUser);

        var controller = new DashboardController(context, userManagerMock.Object)
        {
            ControllerContext = CreateControllerContext(techUser.Id, AppRoles.Technician)
        };

        var result = await controller.Technician();
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<TechnicianDashboardViewModel>(viewResult.Model);
        Assert.Equal("KTV Thử Nghiệm", model.UserFullName);
        Assert.Equal("ktv@test.local", model.UserEmail);
        Assert.Equal(0, model.AssignedCount);
    }

    [Fact]
    public async Task ReportsController_FilterValidation_ClampsInvalidYearAndMonth()
    {
        using var context = CreateInMemoryDbContext();
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(s => s.UserName).Returns("admin_test");

        var financialReportService = new FinancialReportService(context, currentUserServiceMock.Object);
        var controller = new ReportsController(context, financialReportService);

        // Nhập năm/tháng vượt dải hợp lệ (-99 và 99)
        var result = await controller.Index(buildingId: null, year: -99, month: 99, searchTerm: null);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ReportsViewModel>(viewResult.Model);

        // Năm phải được đưa về năm hiện tại, tháng về tháng hiện tại
        Assert.Equal(DateTime.UtcNow.Year, model.SelectedYear);
        Assert.Equal(DateTime.UtcNow.Month, model.SelectedMonth);
    }
}

