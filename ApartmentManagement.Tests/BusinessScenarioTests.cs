using System.Security.Claims;
using ApartmentManagement.Common.Security;
using ApartmentManagement.Controllers;
using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ApartmentManagement.Tests;

public class BusinessScenarioTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static (Building Building, Apartment Apt101, Apartment Apt102, Resident Res1, Resident Res2) SeedBaseData(ApplicationDbContext context)
    {
        var building = new Building
        {
            BuildingCode = "BLD-TEST",
            BuildingName = "Tòa Nhà Thử Nghiệm",
            NumberOfFloors = 10,
            Address = "123 Phố Vọng, Hà Nội"
        };
        context.Buildings.Add(building);
        context.SaveChanges();

        var apt101 = new Apartment
        {
            BuildingId = building.BuildingId,
            ApartmentCode = "P101",
            Floor = 1,
            Area = 85.5m,
            Status = ApartmentStatus.Occupied
        };

        var apt102 = new Apartment
        {
            BuildingId = building.BuildingId,
            ApartmentCode = "P102",
            Floor = 1,
            Area = 70.0m,
            Status = ApartmentStatus.Vacant
        };

        context.Apartments.AddRange(apt101, apt102);
        context.SaveChanges();

        var user1 = new ApplicationUser { Id = "user-1", UserName = "resident1", FullName = "Nguyễn Văn Một", Email = "one@apartment.local", IsActive = true };
        var user2 = new ApplicationUser { Id = "user-2", UserName = "resident2", FullName = "Trần Thị Hai", Email = "two@apartment.local", IsActive = true };
        var adminUser = new ApplicationUser { Id = "admin-1", UserName = "admin", FullName = "Ban Quản Lý", Email = "admin@apartment.local", IsActive = true };

        context.Users.AddRange(user1, user2, adminUser);
        context.SaveChanges();

        var adminRole = new IdentityRole { Id = "role-admin", Name = AppRoles.SuperAdmin, NormalizedName = AppRoles.SuperAdmin.ToUpperInvariant() };
        context.Roles.Add(adminRole);
        context.UserRoles.Add(new IdentityUserRole<string> { RoleId = adminRole.Id, UserId = adminUser.Id });
        context.SaveChanges();

        var res1 = new Resident { UserId = user1.Id, CitizenId = "001090000001", DateOfBirth = new DateTime(1990, 1, 1), Gender = "Nam" };
        var res2 = new Resident { UserId = user2.Id, CitizenId = "001090000002", DateOfBirth = new DateTime(1992, 2, 2), Gender = "Nữ" };

        context.Residents.AddRange(res1, res2);
        context.SaveChanges();

        // res1 lives in apt101 (Active)
        context.ApartmentResidents.Add(new ApartmentResident
        {
            ApartmentId = apt101.ApartmentId,
            ResidentId = res1.ResidentId,
            Relationship = "Chủ hộ",
            MoveInDate = new DateTime(2023, 1, 1),
            MoveOutDate = null,
            IsOwner = true
        });

        context.SaveChanges();

        return (building, apt101, apt102, res1, res2);
    }



    /// <summary>
    /// TC-01: Chặn cập nhật thông tin đối với căn hộ không tồn tại
    /// Kết quả kỳ vọng: Hệ thống từ chối cập nhật, trả về thông báo "Không tìm thấy căn hộ." (hoặc NotFound từ Controller).
    /// </summary>
    [Fact]
    public async Task TC_01_BlockUpdateForNonExistentApartment()
    {
        using var context = CreateInMemoryDbContext();
        var (_, _, _, _, _) = SeedBaseData(context);
        var service = new ApartmentService(context, NullLogger<ApartmentService>.Instance);

        var nonExistentApartment = new Apartment
        {
            ApartmentId = 99999, // Không tồn tại
            BuildingId = 1,
            ApartmentCode = "P99999",
            Floor = 5,
            Area = 80m,
            Status = ApartmentStatus.Vacant
        };

        var (success, errorMessage) = await service.UpdateApartmentAsync(nonExistentApartment);

        Assert.False(success);
        Assert.Equal("Không tìm thấy căn hộ.", errorMessage);

        // Kiểm tra qua Controller
        var mockBuildingService = new Mock<IBuildingService>();
        var controller = new ApartmentsController(service, mockBuildingService.Object);
        var result = await controller.Edit(nonExistentApartment.ApartmentId, nonExistentApartment);

        Assert.IsType<NotFoundResult>(result);
    }

    /// <summary>
    /// TC-02: Chặn chuyển trạng thái sang "Trống" khi căn hộ vẫn còn cư dân
    /// Kết quả kỳ vọng: Hệ thống chặn thao tác, hiển thị cảnh báo lỗi nghiệp vụ, trạng thái căn hộ vẫn giữ nguyên "Đang ở".
    /// </summary>
    [Fact]
    public async Task TC_02_BlockChangeStatusToVacantWhenResidentsExist()
    {
        using var context = CreateInMemoryDbContext();
        var (_, apt101, _, _, _) = SeedBaseData(context);
        var service = new ApartmentService(context, NullLogger<ApartmentService>.Instance);

        // Thao tác đổi trạng thái sang "Trống" (Vacant)
        var apartmentToUpdate = new Apartment
        {
            ApartmentId = apt101.ApartmentId,
            BuildingId = apt101.BuildingId,
            ApartmentCode = apt101.ApartmentCode,
            Floor = apt101.Floor,
            Area = apt101.Area,
            Status = ApartmentStatus.Vacant
        };

        var (success, errorMessage) = await service.UpdateApartmentAsync(apartmentToUpdate);

        Assert.False(success);
        Assert.Contains("Không thể chuyển trạng thái căn hộ", errorMessage);
        Assert.Contains("vẫn còn cư dân đang lưu trú", errorMessage);

        // Xác minh trạng thái căn hộ trong DB vẫn giữ nguyên là "Đang ở" (Occupied)
        var currentApartment = await context.Apartments.FindAsync(apt101.ApartmentId);
        Assert.NotNull(currentApartment);
        Assert.Equal(ApartmentStatus.Occupied, currentApartment.Status);
    }

    /// <summary>
    /// TC-03: Chặn tạo hợp đồng mới khi căn hộ đang có hợp đồng thuê còn hiệu lực
    /// Kết quả kỳ vọng: Hệ thống từ chối kích hoạt bản ghi mới, hiển thị thông báo thời hạn bị trùng với hợp đồng đang hiệu lực.
    /// </summary>
    [Fact]
    public async Task TC_03_BlockNewContractWhenActiveContractOverlaps()
    {
        using var context = CreateInMemoryDbContext();
        var (_, apt101, _, res1, res2) = SeedBaseData(context);
        var contractService = new ContractService(context, NullLogger<ContractService>.Instance);

        // Hợp đồng 1: Đang hiệu lực (Active) từ 01/01/2026 đến 31/12/2026
        var draft1Input = new ContractDraftInput
        {
            ContractCode = "HD-2026-001",
            ApartmentId = apt101.ApartmentId,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
            MonthlyRent = 15000000m,
            DepositAmount = 15000000m,
            Parties = [
                new ContractPartyInput(res1.ResidentId, ContractPartyRole.Lessor),
                new ContractPartyInput(res2.ResidentId, ContractPartyRole.Lessee)
            ]
        };

        var draft1Res = await contractService.CreateDraftAsync(draft1Input, "admin-1");
        Assert.True(draft1Res.Success);
        draft1Res.Contract!.RowVersion = [1];
        var activateRes1 = await contractService.ActivateAsync(draft1Res.Contract.ApartmentContractId, "admin-1", [1]);
        Assert.True(activateRes1.Success);

        // Hợp đồng 2: Tạo trùng thời hạn với hợp đồng 1 (bắt đầu 01/06/2026)
        var draft2Input = new ContractDraftInput
        {
            ContractCode = "HD-2026-002",
            ApartmentId = apt101.ApartmentId,
            StartDate = new DateTime(2026, 6, 1),
            EndDate = new DateTime(2027, 5, 31),
            MonthlyRent = 16000000m,
            DepositAmount = 16000000m,
            Parties = [
                new ContractPartyInput(res1.ResidentId, ContractPartyRole.Lessor),
                new ContractPartyInput(res2.ResidentId, ContractPartyRole.Lessee)
            ]
        };

        var draft2Res = await contractService.CreateDraftAsync(draft2Input, "admin-1");
        Assert.True(draft2Res.Success);
        draft2Res.Contract!.RowVersion = [2];

        // Cố gắng kích hoạt hợp đồng 2
        var activateRes2 = await contractService.ActivateAsync(draft2Res.Contract.ApartmentContractId, "admin-1", [2]);

        Assert.False(activateRes2.Success);
        Assert.Contains("Thời hạn hợp đồng bị trùng với một hợp đồng đang hiệu lực của căn hộ", activateRes2.ErrorMessage);
    }

    /// <summary>
    /// TC-04: Thanh lý/Chấm dứt hợp đồng hợp lệ và tự động giải phóng căn hộ
    /// Kết quả kỳ vọng: Trạng thái hợp đồng chuyển thành Đã chấm dứt (Terminated), trạng thái căn hộ tự động cập nhật về Trống (Vacant).
    /// </summary>
    [Fact]
    public async Task TC_04_TerminateContract_AutomaticallyReleasesApartmentToVacant()
    {
        using var context = CreateInMemoryDbContext();
        var (_, _, apt102, res1, res2) = SeedBaseData(context);
        var contractService = new ContractService(context, NullLogger<ContractService>.Instance);

        // Tạo và kích hoạt hợp đồng cho căn hộ apt102
        var draftInput = new ContractDraftInput
        {
            ContractCode = "HD-TL-001",
            ApartmentId = apt102.ApartmentId,
            StartDate = new DateTime(2025, 1, 1),
            EndDate = new DateTime(2025, 12, 31),
            MonthlyRent = 12000000m,
            DepositAmount = 12000000m,
            Parties = [
                new ContractPartyInput(res1.ResidentId, ContractPartyRole.Lessor),
                new ContractPartyInput(res2.ResidentId, ContractPartyRole.Lessee)
            ]
        };

        var draftRes = await contractService.CreateDraftAsync(draftInput, "admin-1");
        Assert.True(draftRes.Success);
        draftRes.Contract!.RowVersion = [1];

        // Kích hoạt hợp đồng -> Căn hộ chuyển sang Occupied
        var activateRes = await contractService.ActivateAsync(draftRes.Contract.ApartmentContractId, "admin-1", [1]);
        Assert.True(activateRes.Success);

        var aptOccupied = await context.Apartments.FindAsync(apt102.ApartmentId);
        Assert.Equal(ApartmentStatus.Occupied, aptOccupied!.Status);

        // Thực hiện chấm dứt / thanh lý hợp đồng
        activateRes.Contract!.RowVersion = [2];
        var terminateRes = await contractService.TerminateAsync(
            activateRes.Contract.ApartmentContractId,
            "admin-1",
            [2],
            "Hai bên đã hoàn tất thanh lý hợp đồng và bàn giao mặt bằng");

        Assert.True(terminateRes.Success);
        Assert.Equal(ApartmentContractStatus.Terminated, terminateRes.Contract!.Status);

        // Xác minh trạng thái căn hộ tự động cập nhật về Trống (Vacant)
        var aptVacant = await context.Apartments.FindAsync(apt102.ApartmentId);
        Assert.Equal(ApartmentStatus.Vacant, aptVacant!.Status);
    }

    /// <summary>
    /// TC-05: Kiểm tra ràng buộc ngày bắt đầu và ngày kết thúc hợp đồng
    /// Kết quả kỳ vọng: Form chặn gửi dữ liệu hoặc hệ thống trả về lỗi: "Ngày kết thúc không thể trước ngày bắt đầu."
    /// </summary>
    [Fact]
    public async Task TC_05_ContractDatesConstraint_EndDateCannotBeBeforeStartDate()
    {
        using var context = CreateInMemoryDbContext();
        var (_, apt101, _, res1, res2) = SeedBaseData(context);
        var contractService = new ContractService(context, NullLogger<ContractService>.Instance);

        var invalidDateInput = new ContractDraftInput
        {
            ContractCode = "HD-DATE-ERR",
            ApartmentId = apt101.ApartmentId,
            StartDate = new DateTime(2026, 5, 20),
            EndDate = new DateTime(2026, 5, 10), // Ngày kết thúc trước ngày bắt đầu
            MonthlyRent = 10000000m,
            DepositAmount = 10000000m,
            Parties = [
                new ContractPartyInput(res1.ResidentId, ContractPartyRole.Lessor),
                new ContractPartyInput(res2.ResidentId, ContractPartyRole.Lessee)
            ]
        };

        var result = await contractService.CreateDraftAsync(invalidDateInput, "admin-1");

        Assert.False(result.Success);
        Assert.Equal("Ngày kết thúc không thể trước ngày bắt đầu.", result.ErrorMessage);
    }

    /// <summary>
    /// TC-06: Tính phí quản lý chính xác theo diện tích căn hộ
    /// Kết quả kỳ vọng: 85.5 m² x 12.000 VNĐ = 1.026.000 VNĐ, hiển thị rõ công thức diện tích và đơn giá đi kèm.
    /// </summary>
    [Fact]
    public async Task TC_06_ManagementFeeCalculation_ExactByArea()
    {
        using var context = CreateInMemoryDbContext();
        var (building, apt101, _, res1, _) = SeedBaseData(context);
        var billingService = new BillingService(context, NullLogger<BillingService>.Instance);

        var periodStart = new DateTime(2026, 1, 1);
        var periodEnd = new DateTime(2026, 1, 31);

        // Biểu phí quản lý: 12.000 VNĐ / m² / tháng
        var managementTariff = new FeeTariff
        {
            BuildingId = building.BuildingId,
            ChargeType = FeeChargeType.Management,
            EffectiveFrom = periodStart,
            EffectiveTo = null,
            MonthlyRatePerSquareMeter = 12000m,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = "admin-1"
        };

        // Biểu phí điện & nước cơ bản (cần có để lập hóa đơn)
        var elecTariff = new FeeTariff
        {
            BuildingId = building.BuildingId,
            ChargeType = FeeChargeType.Electricity,
            EffectiveFrom = periodStart,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = "admin-1",
            Tiers = [new FeeTariffTier { TierOrder = 1, UpperConsumption = null, UnitRate = 2000m }]
        };

        var waterTariff = new FeeTariff
        {
            BuildingId = building.BuildingId,
            ChargeType = FeeChargeType.Water,
            EffectiveFrom = periodStart,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = "admin-1",
            Tiers = [new FeeTariffTier { TierOrder = 1, UpperConsumption = null, UnitRate = 10000m }]
        };

        context.FeeTariffs.AddRange(managementTariff, elecTariff, waterTariff);

        // Chỉ số điện nước kỳ 01/2026
        context.UtilityMeterReadings.AddRange(
            new UtilityMeterReading { ApartmentId = apt101.ApartmentId, UtilityType = FeeChargeType.Electricity, BillingYear = 2026, BillingMonth = 1, PreviousReading = 0, CurrentReading = 100, ReadAtUtc = DateTime.UtcNow, RecordedByUserId = "admin-1" },
            new UtilityMeterReading { ApartmentId = apt101.ApartmentId, UtilityType = FeeChargeType.Water, BillingYear = 2026, BillingMonth = 1, PreviousReading = 0, CurrentReading = 10, ReadAtUtc = DateTime.UtcNow, RecordedByUserId = "admin-1" }
        );

        await context.SaveChangesAsync();

        // Lập hóa đơn nháp
        var dueDate = new DateTime(2026, 2, 15);
        var result = await billingService.GenerateDraftAsync(apt101.ApartmentId, 2026, 1, dueDate, "admin-1");

        Assert.True(result.Success);
        Assert.NotNull(result.Invoice);

        var managementLine = result.Invoice.Lines.FirstOrDefault(l => l.ChargeType == FeeChargeType.Management);
        Assert.NotNull(managementLine);

        // Số tiền tính ra: 85.5 * 12.000 = 1.026.000 VNĐ
        var expectedAmount = 85.5m * 12000m;
        Assert.Equal(expectedAmount, managementLine.Amount);
        Assert.Equal(1026000m, managementLine.Amount);

        // Mục phí hiển thị rõ công thức diện tích và đơn giá đi kèm
        Assert.Contains("85.50 m²", managementLine.RateBreakdown);
        Assert.Contains("12,000.0000 VND/m²/tháng", managementLine.RateBreakdown);
    }

    /// <summary>
    /// TC-07: Tính tiền điện/nước lũy tiến theo từng bậc tiêu thụ
    /// Tiền điều kiện: Bậc 1 (0-50): 1.800 đ/đơn vị; Bậc 2 (>50): 2.200 đ/đơn vị. Tiêu thụ 75 đơn vị.
    /// Kết quả kỳ vọng: Bậc 1: 50 * 1.800 = 90.000 đ; Bậc 2: 25 * 2.200 = 55.000 đ; Tổng: 145.000 đ.
    /// </summary>
    [Fact]
    public void TC_07_UtilityProgressiveCalculation_TieredConsumption()
    {
        var tiers = new List<FeeTariffTier>
        {
            new() { TierOrder = 1, UpperConsumption = 50m, UnitRate = 1800m },
            new() { TierOrder = 2, UpperConsumption = null, UnitRate = 2200m }
        };

        const decimal consumption = 75m;

        var totalAmount = ProgressiveTariffCalculator.Calculate(consumption, tiers);

        // Bậc 1: 50 * 1800 = 90.000
        // Bậc 2: 25 * 2200 = 55.000
        // Tổng: 145.000
        Assert.Equal(145000m, totalAmount);
    }

    /// <summary>
    /// TC-08: Chặn hủy (Void) đối với hóa đơn đã hoàn tất thanh toán
    /// Kết quả kỳ vọng: Hệ thống từ chối với thông báo "Không thể hủy hóa đơn đã hoàn tất thanh toán.", trạng thái không đổi.
    /// </summary>
    [Fact]
    public async Task TC_08_BlockVoidForPaidInvoice()
    {
        using var context = CreateInMemoryDbContext();
        var (_, apt101, _, res1, _) = SeedBaseData(context);
        var billingService = new BillingService(context, NullLogger<BillingService>.Instance);

        var invoice = new ApartmentInvoice
        {
            InvoiceCode = "INV-PAID-001",
            ApartmentId = apt101.ApartmentId,
            ResidentId = res1.ResidentId,
            BillingYear = 2026,
            BillingMonth = 1,
            DueDate = DateTime.UtcNow.AddDays(15),
            ApartmentArea = 85.5m,
            TotalAmount = 1500000m,
            Status = ApartmentInvoiceStatus.Issued,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = "admin-1",
            PaymentTransactions = [
                new PaymentTransaction
                {
                    Reference = "AHPAID001",
                    Amount = 1500000m,
                    Status = PaymentTransactionStatus.Confirmed,
                    CreatedAtUtc = DateTime.UtcNow,
                    ConfirmedAtUtc = DateTime.UtcNow,
                    ConfirmedByUserId = "admin-1"
                }
            ]
        };
        context.ApartmentInvoices.Add(invoice);
        await context.SaveChangesAsync();

        var result = await billingService.VoidAsync(invoice.ApartmentInvoiceId, "admin-1", [1], "Muốn hủy hóa đơn đã thu");

        Assert.False(result.Success);
        Assert.Contains("Không thể hủy hóa đơn đã hoàn tất thanh toán", result.ErrorMessage);

        // Trạng thái hóa đơn vẫn giữ nguyên là Issued (chưa bị hủy)
        var updatedInvoice = await context.ApartmentInvoices.FindAsync(invoice.ApartmentInvoiceId);
        Assert.Equal(ApartmentInvoiceStatus.Issued, updatedInvoice!.Status);
    }

    /// <summary>
    /// TC-09: Xử lý xung đột ghi đồng thời (Concurrency Control) khi thanh toán
    /// Kết quả kỳ vọng: Giao dịch của người dùng A với RowVersion cũ bị từ chối, yêu cầu tải lại trang, không bị sai lệch số tiền.
    /// </summary>
    [Fact]
    public async Task TC_09_ConcurrencyControl_RejectsStalePaymentConfirmation()
    {
        using var context = CreateInMemoryDbContext();
        var (_, apt101, _, res1, _) = SeedBaseData(context);
        var paymentService = new PaymentService(context, NullLogger<PaymentService>.Instance);

        var invoice = new ApartmentInvoice
        {
            InvoiceCode = "INV-CONCUR-001",
            ApartmentId = apt101.ApartmentId,
            ResidentId = res1.ResidentId,
            BillingYear = 2026,
            BillingMonth = 2,
            DueDate = DateTime.UtcNow.AddDays(15),
            ApartmentArea = 85.5m,
            TotalAmount = 1000000m,
            Status = ApartmentInvoiceStatus.Issued,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = "admin-1"
        };
        context.ApartmentInvoices.Add(invoice);
        await context.SaveChangesAsync();

        var payment = new PaymentTransaction
        {
            ApartmentInvoiceId = invoice.ApartmentInvoiceId,
            Reference = "AHCONCURRENCY001",
            Amount = 1000000m,
            Status = PaymentTransactionStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };
        context.PaymentTransactions.Add(payment);
        await context.SaveChangesAsync();

        // Giả lập phiên bản dữ liệu cũ của Người dùng A: [1, 1, 1, 1]
        var staleRowVersionUserA = new byte[] { 1, 1, 1, 1 };

        // Giao dịch B cập nhật trước vào hệ thống, đổi RowVersion thành [2, 2, 2, 2]
        payment.RowVersion = new byte[] { 2, 2, 2, 2 };
        await context.SaveChangesAsync();

        // Người dùng A gửi xác nhận với RowVersion cũ
        var confirmResultA = await paymentService.ConfirmSimulatedPaymentAsync(
            payment.PaymentTransactionId,
            "user-a",
            staleRowVersionUserA);

        Assert.False(confirmResultA.Success);
        Assert.Contains("Giao dịch đã thay đổi đồng thời", confirmResultA.ErrorMessage);
        Assert.Contains("Tải lại dữ liệu trước khi thử lại", confirmResultA.ErrorMessage);
    }

    /// <summary>
    /// TC-10: Tối ưu thanh tìm kiếm toàn cục khi từ khóa quá ngắn hoặc rỗng
    /// Kết quả kỳ vọng: Hệ thống không gửi truy vấn tìm kiếm ngầm về DB, trả về 0 kết quả ngay lập tức mà không giật lag.
    /// </summary>
    [Fact]
    public async Task TC_10_GlobalSearch_OptimizedForEmptyOrShortQuery()
    {
        using var context = CreateInMemoryDbContext();
        SeedBaseData(context);
        var searchService = new SearchService(context);

        var adminUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "admin-1"),
            new Claim(ClaimTypes.Role, AppRoles.SuperAdmin)
        }, "TestAuth"));

        // Bước 1: Để trống hoặc nhập toàn khoảng trắng
        var emptyResult = await searchService.SearchAsync("   ", adminUser);
        Assert.Equal(0, emptyResult.TotalCount);
        Assert.Empty(emptyResult.Groups);

        // Bước 2: Nhập duy nhất 1 ký tự
        var singleCharResult = await searchService.SearchAsync("a", adminUser);
        Assert.Equal(0, singleCharResult.TotalCount);
        Assert.Empty(singleCharResult.Groups);

        var nullResult = await searchService.SearchAsync(null, adminUser);
        Assert.Equal(0, nullResult.TotalCount);
        Assert.Empty(nullResult.Groups);
    }
}
