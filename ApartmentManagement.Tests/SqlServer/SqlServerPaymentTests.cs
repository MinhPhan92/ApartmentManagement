using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ApartmentManagement.Tests.SqlServer;

public sealed class SqlServerPaymentTests
{
    [SqlServerFact]
    public async Task PaymentUniqueInvoiceIndex_RejectsSecondTransactionForInvoice()
    {
        await using var database = SqlServerTestDatabase.Create();
        int invoiceId;
        string residentUserId;
        await using (var setup = database.CreateContext())
        {
            await setup.Database.MigrateAsync();
            (invoiceId, residentUserId, _) = await SeedIssuedInvoiceAsync(setup);
        }

        await using (var context = database.CreateContext())
        {
            var service = new PaymentService(context, NullLogger<PaymentService>.Instance);
            var payment = await service.GetOrCreateResidentPaymentAsync(residentUserId, invoiceId);
            Assert.NotNull(payment);
        }

        await using var duplicateContext = database.CreateContext();
        duplicateContext.PaymentTransactions.Add(new PaymentTransaction
        {
            ApartmentInvoiceId = invoiceId,
            Reference = $"AH{Guid.NewGuid():N}"[..25].ToUpperInvariant(),
            Amount = 125000,
            Status = PaymentTransactionStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => duplicateContext.SaveChangesAsync());
    }

    [SqlServerFact]
    public async Task Confirmation_RejectsStaleSqlServerRowVersion()
    {
        await using var database = SqlServerTestDatabase.Create();
        int invoiceId;
        string residentUserId;
        string staffUserId;
        await using (var setup = database.CreateContext())
        {
            await setup.Database.MigrateAsync();
            (invoiceId, residentUserId, staffUserId) = await SeedIssuedInvoiceAsync(setup);
        }

        byte[] staleRowVersion;
        int paymentId;
        await using (var context = database.CreateContext())
        {
            var service = new PaymentService(context, NullLogger<PaymentService>.Instance);
            var payment = await service.GetOrCreateResidentPaymentAsync(residentUserId, invoiceId);
            Assert.NotNull(payment);
            staleRowVersion = payment.RowVersion.ToArray();
            paymentId = payment.PaymentTransactionId;
            Assert.Equal(8, staleRowVersion.Length);
        }

        await using (var concurrentUpdate = database.CreateContext())
        {
            await concurrentUpdate.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [PaymentTransactions] SET [CreatedAtUtc] = DATEADD(second, 1, [CreatedAtUtc]) WHERE [PaymentTransactionId] = {paymentId}");
        }

        await using (var context = database.CreateContext())
        {
            var service = new PaymentService(context, NullLogger<PaymentService>.Instance);
            var staleAttempt = await service.ConfirmSimulatedPaymentAsync(
                paymentId, staffUserId, staleRowVersion);
            Assert.False(staleAttempt.Success);
            Assert.Contains("đồng thời", staleAttempt.ErrorMessage);

            var current = await context.PaymentTransactions
                .AsNoTracking()
                .SingleAsync(x => x.PaymentTransactionId == paymentId);
            var validAttempt = await service.ConfirmSimulatedPaymentAsync(
                paymentId, staffUserId, current.RowVersion);
            Assert.True(validAttempt.Success, validAttempt.ErrorMessage);
            Assert.Equal(PaymentTransactionStatus.Confirmed, validAttempt.Payment?.Status);
        }
    }

    private static async Task<(int InvoiceId, string ResidentUserId, string StaffUserId)> SeedIssuedInvoiceAsync(
        ApplicationDbContext context)
    {
        const string residentUserId = "payment-resident";
        const string staffUserId = "payment-staff";
        var residentUser = CreateUser(residentUserId);
        var staffUser = CreateUser(staffUserId);
        var building = new Building
        {
            BuildingCode = "PAY-SQL",
            BuildingName = "SQL Payment Test",
            NumberOfFloors = 5
        };
        var apartment = new Apartment
        {
            Building = building,
            ApartmentCode = "P-101",
            Floor = 1,
            Area = 70,
            Status = "Đang sử dụng"
        };
        var resident = new Resident
        {
            User = residentUser,
            UserId = residentUser.Id,
            CitizenId = "123456789012"
        };
        var invoice = new ApartmentInvoice
        {
            InvoiceCode = $"INV-PAY-{Guid.NewGuid():N}"[..40],
            Apartment = apartment,
            Resident = resident,
            BillingYear = 2026,
            BillingMonth = 9,
            DueDate = new DateTime(2026, 10, 10),
            ApartmentArea = 70,
            TotalAmount = 125000,
            Status = ApartmentInvoiceStatus.Issued,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUser = staffUser,
            CreatedByUserId = staffUser.Id,
            IssuedAtUtc = DateTime.UtcNow,
            IssuedByUser = staffUser,
            IssuedByUserId = staffUser.Id
        };
        context.AddRange(residentUser, staffUser, building, apartment, resident, invoice);
        await context.SaveChangesAsync();
        return (invoice.ApartmentInvoiceId, residentUserId, staffUserId);
    }

    private static ApplicationUser CreateUser(string id) => new()
    {
        Id = id,
        UserName = $"{id}@test.local",
        NormalizedUserName = $"{id}@TEST.LOCAL".ToUpperInvariant(),
        Email = $"{id}@test.local",
        NormalizedEmail = $"{id}@TEST.LOCAL".ToUpperInvariant(),
        FullName = id,
        IsActive = true,
        EmailConfirmed = true
    };
}
