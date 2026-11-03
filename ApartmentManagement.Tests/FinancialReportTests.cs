using System.Security.Claims;
using System.Text;
using ApartmentManagement.Common.Security;
using ApartmentManagement.Controllers;
using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ApartmentManagement.Tests;

public sealed class FinancialReportTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static (Building building, Apartment apartment, Resident resident, ApartmentInvoice invoice) SeedSampleData(ApplicationDbContext context)
    {
        var building = new Building
        {
            BuildingCode = "T1",
            BuildingName = "Tòa Tháp Hoa Sen",
            NumberOfFloors = 20
        };
        context.Buildings.Add(building);
        context.SaveChanges();

        var user = new ApplicationUser
        {
            Id = "resident-user-1",
            UserName = "nguyenvana@gmail.com",
            FullName = "Nguyễn Văn A, Trưởng Hộ",
            PhoneNumber = "0988123456"
        };
        context.Users.Add(user);
        context.SaveChanges();

        var resident = new Resident
        {
            UserId = user.Id,
            CitizenId = "001200001234",
            User = user
        };
        context.Residents.Add(resident);
        context.SaveChanges();

        var apartment = new Apartment
        {
            BuildingId = building.BuildingId,
            ApartmentCode = "P101",
            Floor = 1,
            Area = 75.5m,
            Building = building
        };
        context.Apartments.Add(apartment);
        context.SaveChanges();

        var invoice = new ApartmentInvoice
        {
            InvoiceCode = "INV-202610-001",
            ApartmentId = apartment.ApartmentId,
            ResidentId = resident.ResidentId,
            BillingYear = 2026,
            BillingMonth = 10,
            DueDate = new DateTime(2026, 11, 15),
            ApartmentArea = apartment.Area,
            Status = ApartmentInvoiceStatus.Issued,
            CreatedAtUtc = DateTime.UtcNow,
            TotalAmount = 2500000m,
            Apartment = apartment,
            Resident = resident
        };

        // Dòng 1: Quản lý gộp gửi xe
        invoice.Lines.Add(new ApartmentInvoiceLine
        {
            ChargeType = FeeChargeType.Management,
            Description = "Phí quản lý diện tích\n[Bao gồm] 1 Ô tô, 1 Xe máy",
            Quantity = 75.5m,
            UnitRate = 15000m,
            Amount = 1132500m + 500000m, // 1,132,500 quản lý + 500,000 xe
            RateBreakdown = "75.50 m² × 15,000.0000 VND/m²/tháng; Phí gửi xe: 500,000 VND (2 phương tiện)"
        });

        // Dòng 2: Điện
        invoice.Lines.Add(new ApartmentInvoiceLine
        {
            ChargeType = FeeChargeType.Electricity,
            Description = "Điện sinh hoạt bậc thang",
            Quantity = 250m,
            UnitRate = 2500m,
            Amount = 625000m
        });

        // Dòng 3: Nước
        invoice.Lines.Add(new ApartmentInvoiceLine
        {
            ChargeType = FeeChargeType.Water,
            Description = "Nước sinh hoạt",
            Quantity = 20m,
            UnitRate = 12000m,
            Amount = 240000m
        });

        // Thanh toán xác nhận: 1,500,000 (còn nợ 1,000,000)
        invoice.PaymentTransactions.Add(new PaymentTransaction
        {
            Amount = 1500000m,
            Status = PaymentTransactionStatus.Confirmed,
            CreatedAtUtc = new DateTime(2026, 10, 25),
            ConfirmedAtUtc = new DateTime(2026, 10, 25, 14, 30, 0)
        });

        context.ApartmentInvoices.Add(invoice);
        context.SaveChanges();

        return (building, apartment, resident, invoice);
    }

    [Fact]
    public async Task GenerateMonthlyStatementAsync_ExtractsParkingAndManagementCorrectly()
    {
        using var context = CreateInMemoryDbContext();
        SeedSampleData(context);

        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(x => x.UserId).Returns("accountant-1");

        var service = new FinancialReportService(context, currentUserServiceMock.Object);

        var filter = new MonthlyStatementFilterInput
        {
            BillingYear = 2026,
            BillingMonth = 10
        };

        var report = await service.GenerateMonthlyStatementAsync(filter);

        Assert.NotNull(report);
        Assert.Single(report.Items);
        var item = report.Items[0];

        Assert.Equal("INV-202610-001", item.InvoiceCode);
        Assert.Equal("P101", item.ApartmentCode);
        Assert.Equal("Tòa Tháp Hoa Sen", item.BuildingName);
        Assert.Equal("Nguyễn Văn A, Trưởng Hộ", item.ResidentName);
        Assert.Equal("0988123456", item.PhoneNumber);

        // Kiểm tra bóc tách phí
        Assert.Equal(1132500m, item.PureManagementFee);
        Assert.Equal(500000m, item.ParkingFee);
        Assert.Equal(625000m, item.ElectricityFee);
        Assert.Equal(240000m, item.WaterFee);

        // Kiểm tra thanh toán và công nợ
        Assert.Equal(1500000m, item.PaidAmount);
        Assert.Equal(1000000m, item.OutstandingDebt);
        Assert.Equal("Thanh toán một phần", item.PaymentStatus);
        Assert.Equal("Đã phát hành", item.InvoiceStatus);
        Assert.NotNull(item.LastPaymentDate);

        // Kiểm tra tổng cộng lũy kế
        Assert.Equal(1132500m, report.TotalPureManagementFee);
        Assert.Equal(500000m, report.TotalParkingFee);
        Assert.Equal(625000m, report.TotalElectricityFee);
        Assert.Equal(240000m, report.TotalWaterFee);
        Assert.Equal(2500000m, report.GrandTotalAmount);
        Assert.Equal(1500000m, report.GrandTotalPaidAmount);
        Assert.Equal(1000000m, report.GrandTotalOutstandingDebt);
    }

    [Fact]
    public async Task GenerateMonthlyStatementAsync_FiltersByPaymentStatus_PaidAndUnpaid()
    {
        using var context = CreateInMemoryDbContext();
        var (_, _, _, inv) = SeedSampleData(context);

        var currentUserServiceMock = new Mock<ICurrentUserService>();
        var service = new FinancialReportService(context, currentUserServiceMock.Object);

        // 1. Lọc Unpaid: inv còn nợ 1,000,000 -> có kết quả
        var unpaidReport = await service.GenerateMonthlyStatementAsync(new MonthlyStatementFilterInput { PaymentStatus = "Unpaid" });
        Assert.Single(unpaidReport.Items);

        // 2. Lọc Paid: inv chưa trả đủ -> 0 kết quả
        var paidReport = await service.GenerateMonthlyStatementAsync(new MonthlyStatementFilterInput { PaymentStatus = "Paid" });
        Assert.Empty(paidReport.Items);

        // 3. Thanh toán nốt số còn lại
        inv.PaymentTransactions.Add(new PaymentTransaction
        {
            Amount = 1000000m,
            Status = PaymentTransactionStatus.Confirmed,
            CreatedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var paidReportAfter = await service.GenerateMonthlyStatementAsync(new MonthlyStatementFilterInput { PaymentStatus = "Paid" });
        Assert.Single(paidReportAfter.Items);
        Assert.Equal("Đã thanh toán", paidReportAfter.Items[0].PaymentStatus);
        Assert.Equal(0m, paidReportAfter.Items[0].OutstandingDebt);
    }

    [Fact]
    public async Task GenerateMonthlyStatementAsync_FiltersByBuildingAndSearchTerm()
    {
        using var context = CreateInMemoryDbContext();
        var (building, _, _, _) = SeedSampleData(context);

        var currentUserServiceMock = new Mock<ICurrentUserService>();
        var service = new FinancialReportService(context, currentUserServiceMock.Object);

        // Lọc đúng Tòa nhà
        var rep1 = await service.GenerateMonthlyStatementAsync(new MonthlyStatementFilterInput { BuildingId = building.BuildingId });
        Assert.Single(rep1.Items);
        Assert.Equal("Tòa Tháp Hoa Sen", rep1.BuildingFilterName);

        // Lọc Tòa nhà khác -> 0 kết quả
        var rep2 = await service.GenerateMonthlyStatementAsync(new MonthlyStatementFilterInput { BuildingId = 9999 });
        Assert.Empty(rep2.Items);

        // Tìm kiếm theo từ khóa
        var rep3 = await service.GenerateMonthlyStatementAsync(new MonthlyStatementFilterInput { SearchTerm = "Trưởng Hộ" });
        Assert.Single(rep3.Items);

        var rep4 = await service.GenerateMonthlyStatementAsync(new MonthlyStatementFilterInput { SearchTerm = "KhongTonTai" });
        Assert.Empty(rep4.Items);
    }

    [Fact]
    public void ExportMonthlyStatementToCsv_EmitsUtf8BomAndRfc4180Escaping()
    {
        var report = new MonthlyStatementReportDto
        {
            ReportTitle = "BẢNG KÊ THU PHÍ & CÔNG NỢ THÁNG 10/2026",
            BuildingFilterName = "Tòa Tháp A, Khu Đô Thị Mới", // có dấu phẩy cần escape
            BillingPeriodText = "Tháng 10/2026",
            GeneratedBy = "Kế toán \"Trưởng\"", // có dấu ngoặc kép cần escape
            Items =
            {
                new MonthlyStatementItemDto
                {
                    Stt = 1,
                    InvoiceCode = "INV-001",
                    ApartmentCode = "P,101", // có dấu phẩy
                    BuildingName = "Tòa Tháp A",
                    FloorNumber = 1,
                    ResidentName = "Nguyễn Văn \"Đại\", Cư dân", // có ngoặc kép và phẩy
                    PhoneNumber = "0912345678",
                    ApartmentArea = 80.5m,
                    PureManagementFee = 1000000m,
                    ParkingFee = 200000m,
                    ElectricityFee = 500000m,
                    WaterFee = 150000m,
                    OtherFee = 0m,
                    TotalAmount = 1850000m,
                    PaidAmount = 1850000m,
                    OutstandingDebt = 0m,
                    InvoiceStatus = "Đã phát hành",
                    PaymentStatus = "Đã thanh toán",
                    LastPaymentDate = new DateTime(2026, 10, 20),
                    DueDate = new DateTime(2026, 11, 15)
                }
            }
        };

        var currentUserServiceMock = new Mock<ICurrentUserService>();
        using var context = CreateInMemoryDbContext();
        var service = new FinancialReportService(context, currentUserServiceMock.Object);

        var bytes = service.ExportMonthlyStatementToCsv(report);

        // 1. Kiểm tra UTF-8 BOM (0xEF, 0xBB, 0xBF)
        Assert.NotNull(bytes);
        Assert.True(bytes.Length >= 3);
        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);

        // 2. Đọc nội dung CSV
        var text = Encoding.UTF8.GetString(bytes);

        // Kiểm tra tiếng Việt hiển thị trơn tru
        Assert.Contains("BẢNG KÊ THU PHÍ & CÔNG NỢ", text);
        Assert.Contains("TỔNG CỘNG", text);

        // Kiểm tra escape RFC 4180
        // "Tòa Tháp A, Khu Đô Thị Mới" -> có dấu phẩy -> phải bọc ngoặc kép
        Assert.Contains("\"Tòa Tháp A, Khu Đô Thị Mới\"", text);

        // "Kế toán \"Trưởng\"" -> có dấu nháy kép -> phải thành "Kế toán ""Trưởng"""
        Assert.Contains("\"Kế toán \"\"Trưởng\"\"\"", text);

        // "Nguyễn Văn \"Đại\", Cư dân" -> bọc ngoặc kép và double quote
        Assert.Contains("\"Nguyễn Văn \"\"Đại\"\", Cư dân\"", text);

        // "P,101" -> bọc ngoặc kép
        Assert.Contains("\"P,101\"", text);
    }

    [Fact]
    public async Task BillingController_ExportMonthlyStatement_ReturnsCsvFileResult()
    {
        var mockBilling = new Mock<IBillingService>();
        var mockBuilding = new Mock<IBuildingService>();
        var mockApartment = new Mock<IApartmentService>();
        var mockReport = new Mock<IFinancialReportService>();

        var reportDto = new MonthlyStatementReportDto
        {
            BuildingFilterName = "Tòa A",
            BillingPeriodText = "Tháng 10/2026"
        };
        var dummyBytes = new byte[] { 0xEF, 0xBB, 0xBF, 65, 66, 67 };

        mockReport.Setup(r => r.GenerateMonthlyStatementAsync(It.IsAny<MonthlyStatementFilterInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(reportDto);
        mockReport.Setup(r => r.ExportMonthlyStatementToCsv(reportDto))
            .Returns(dummyBytes);
        mockReport.Setup(r => r.GetCsvFileName(reportDto))
            .Returns("BangKe_ToaA_10_2026.csv");

        var controller = new BillingController(
            mockBilling.Object,
            mockBuilding.Object,
            mockApartment.Object,
            mockReport.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, "accountant-1"),
                        new Claim(ClaimTypes.Role, AppRoles.Accountant)
                    ], "TestAuth"))
                }
            }
        };

        var result = await controller.ExportMonthlyStatement(buildingId: 1, year: 2026, month: 10);

        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/csv; charset=utf-8", fileResult.ContentType);
        Assert.Equal("BangKe_ToaA_10_2026.csv", fileResult.FileDownloadName);
        Assert.Equal(dummyBytes, fileResult.FileContents);
    }
}

