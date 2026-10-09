using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ApartmentManagement.Tests;

public sealed class PaymentServiceTests
{
    [Fact]
    public async Task ResidentPayment_IsOwnerBoundAndIdempotentPerInvoice()
    {
        await using var context = CreateContext();
        SeedInvoice(context);
        var service = new PaymentService(context, NullLogger<PaymentService>.Instance);

        Assert.Null(await service.GetOrCreateResidentPaymentAsync("other-user", 1));

        var first = await service.GetOrCreateResidentPaymentAsync("resident-user", 1);
        var repeated = await service.GetOrCreateResidentPaymentAsync("resident-user", 1);

        Assert.NotNull(first);
        Assert.Equal(first.PaymentTransactionId, repeated?.PaymentTransactionId);
        Assert.Equal("AH", first.Reference[..2]);
        Assert.Equal(1294500, first.Amount);
        Assert.Equal(PaymentTransactionStatus.Pending, first.Status);
        Assert.Single(await context.PaymentTransactions.ToListAsync());
    }

    [Fact]
    public async Task SimulatedConfirmation_IsIdempotentAndDoesNotCreateAnotherPayment()
    {
        await using var context = CreateContext();
        SeedInvoice(context);
        var service = new PaymentService(context, NullLogger<PaymentService>.Instance);
        var payment = await service.GetOrCreateResidentPaymentAsync("resident-user", 1);
        Assert.NotNull(payment);

        var first = await service.ConfirmSimulatedPaymentAsync(
            payment.PaymentTransactionId, "staff-user", payment.RowVersion);
        var repeated = await service.ConfirmSimulatedPaymentAsync(
            payment.PaymentTransactionId, "staff-user", payment.RowVersion);

        Assert.True(first.Success, first.ErrorMessage);
        Assert.True(repeated.Success, repeated.ErrorMessage);
        Assert.Equal(PaymentTransactionStatus.Confirmed, repeated.Payment?.Status);
        Assert.Equal("staff-user", repeated.Payment?.ConfirmedByUserId);
        Assert.Single(await context.PaymentTransactions.ToListAsync());
    }

    [Fact]
    public void VietQrGenerator_ProducesPngWithoutExternalImageService()
    {
        var generator = new VietQrCodeGenerator(Options.Create(new VietQrOptions
        {
            BankBin = "970436",
            AccountNumber = "1234567890",
            AccountName = "CÔNG TY QUẢN LÝ"
        }));

        var png = generator.GeneratePng("AH0123456789ABCDEFGHIJKLM", 1294500);

        Assert.True(Options.Create(new VietQrOptions
        {
            BankBin = "970436",
            AccountNumber = "1234567890",
            AccountName = "CÔNG TY QUẢN LÝ"
        }).Value.IsConfigured);
        Assert.Equal(new byte[] { 137, 80, 78, 71 }, png[..4]);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static void SeedInvoice(ApplicationDbContext context)
    {
        var user = new ApplicationUser
        {
            Id = "resident-user",
            UserName = "resident@test.local",
            NormalizedUserName = "RESIDENT@TEST.LOCAL",
            Email = "resident@test.local",
            NormalizedEmail = "RESIDENT@TEST.LOCAL",
            FullName = "Test Resident",
            IsActive = true
        };
        var staff = new ApplicationUser
        {
            Id = "staff-user",
            UserName = "staff@test.local",
            NormalizedUserName = "STAFF@TEST.LOCAL",
            Email = "staff@test.local",
            NormalizedEmail = "STAFF@TEST.LOCAL",
            FullName = "Test Staff",
            IsActive = true
        };
        var building = new Building
        {
            BuildingId = 1,
            BuildingCode = "PAY-1",
            BuildingName = "Payment Test",
            Address = "Test",
            NumberOfFloors = 5
        };
        var apartment = new Apartment
        {
            ApartmentId = 1,
            BuildingId = 1,
            Building = building,
            ApartmentCode = "A-101",
            Floor = 1,
            Area = 75,
            Status = "Đang sử dụng"
        };
        var resident = new Resident
        {
            ResidentId = 1,
            UserId = user.Id,
            User = user,
            CitizenId = "123456789"
        };
        var invoice = new ApartmentInvoice
        {
            ApartmentInvoiceId = 1,
            InvoiceCode = "INV-PAY-1",
            ApartmentId = apartment.ApartmentId,
            Apartment = apartment,
            ResidentId = resident.ResidentId,
            Resident = resident,
            BillingYear = 2026,
            BillingMonth = 9,
            DueDate = new DateTime(2026, 10, 10),
            ApartmentArea = 75,
            TotalAmount = 1294500,
            Status = ApartmentInvoiceStatus.Issued,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = staff.Id,
            CreatedByUser = staff,
            IssuedAtUtc = DateTime.UtcNow,
            IssuedByUserId = staff.Id,
            IssuedByUser = staff
        };

        context.AddRange(user, staff, building, apartment, resident, invoice);
        context.SaveChanges();
    }
}
