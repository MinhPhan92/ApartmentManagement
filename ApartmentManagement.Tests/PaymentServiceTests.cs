using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
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

    [Fact]
    public async Task GetTransactionsAsync_WithStatusAndSearchTerm_FiltersCorrectly()
    {
        await using var context = CreateContext();
        SeedInvoice(context);
        var service = new PaymentService(context, NullLogger<PaymentService>.Instance);

        var payment = await service.GetOrCreateResidentPaymentAsync("resident-user", 1);
        Assert.NotNull(payment);

        var pendingList = await service.GetTransactionsAsync(PaymentTransactionStatus.Pending);
        Assert.Single(pendingList);

        var confirmedList = await service.GetTransactionsAsync(PaymentTransactionStatus.Confirmed);
        Assert.Empty(confirmedList);

        var searchMatch = await service.GetTransactionsAsync(searchTerm: "resident");
        Assert.Single(searchMatch);

        var searchNoMatch = await service.GetTransactionsAsync(searchTerm: "nonexistent");
        Assert.Empty(searchNoMatch);
    }

    [Fact]
    public async Task GetOrCreatePaymentForInvoiceAsync_GeneratesStructuredReferenceAndIsIdempotent()
    {
        await using var context = CreateContext();
        SeedInvoice(context);
        var service = new PaymentService(context, NullLogger<PaymentService>.Instance);

        var payment = await service.GetOrCreatePaymentForInvoiceAsync(1);
        Assert.NotNull(payment);
        Assert.StartsWith("AH00001", payment.Reference);
        Assert.Equal(25, payment.Reference.Length);
        Assert.True(payment.Reference.All(char.IsAsciiLetterOrDigit));
        Assert.Equal(1294500m, payment.Amount);
        Assert.Equal(PaymentTransactionStatus.Pending, payment.Status);

        var repeated = await service.GetOrCreatePaymentForInvoiceAsync(1);
        Assert.NotNull(repeated);
        Assert.Equal(payment.PaymentTransactionId, repeated.PaymentTransactionId);
        Assert.Equal(payment.Reference, repeated.Reference);
        Assert.Single(await context.PaymentTransactions.ToListAsync());
    }

    [Fact]
    public void VietQrOptions_BankName_ResolvesKnownBankBins()
    {
        var options1 = new VietQrOptions { BankBin = "970415" };
        Assert.Contains("VietinBank", options1.BankName);

        var options2 = new VietQrOptions { BankBin = "970436" };
        Assert.Contains("Vietcombank", options2.BankName);

        var options3 = new VietQrOptions { BankBin = "970422" };
        Assert.Contains("MB Bank", options3.BankName);

        var optionsUnknown = new VietQrOptions { BankBin = "999999" };
        Assert.Contains("999999", optionsUnknown.BankName);
    }

    [Fact]
    public async Task CreateResidentPaymentAttemptAsync_AllowsMultipleAttemptsWithDistinctReferences_AndPreservesHistory()
    {
        await using var context = CreateContext();
        SeedInvoice(context);
        var service = new PaymentService(context, NullLogger<PaymentService>.Instance);

        var attempt1 = await service.CreateResidentPaymentAttemptAsync("resident-user", 1);
        var attempt2 = await service.CreateResidentPaymentAttemptAsync("resident-user", 1);
        var attempt3 = await service.CreateResidentPaymentAttemptAsync("resident-user", 1);

        Assert.True(attempt1.Success, attempt1.ErrorMessage);
        Assert.True(attempt2.Success, attempt2.ErrorMessage);
        Assert.True(attempt3.Success, attempt3.ErrorMessage);

        var allTx = await context.PaymentTransactions.OrderBy(x => x.PaymentTransactionId).ToListAsync();
        Assert.Equal(3, allTx.Count);
        Assert.Equal(3, allTx.Select(x => x.Reference).Distinct().Count());
        Assert.All(allTx, tx =>
        {
            Assert.Equal(PaymentTransactionStatus.Pending, tx.Status);
            Assert.Equal(1294500m, tx.Amount);
            Assert.Null(tx.ConfirmedAtUtc);
            Assert.Null(tx.RejectedAtUtc);
        });
    }

    [Fact]
    public async Task ConfirmSimulatedPaymentAsync_WhenInvoicePaidInFull_AutoRejectsRemainingPendingAttempts()
    {
        await using var context = CreateContext();
        SeedInvoice(context);
        var service = new PaymentService(context, NullLogger<PaymentService>.Instance);

        var attempt1 = await service.CreateResidentPaymentAttemptAsync("resident-user", 1);
        var attempt2 = await service.CreateResidentPaymentAttemptAsync("resident-user", 1);
        var attempt3 = await service.CreateResidentPaymentAttemptAsync("resident-user", 1);

        var confirmResult = await service.ConfirmSimulatedPaymentAsync(
            attempt2.Payment!.PaymentTransactionId,
            "staff-user",
            attempt2.Payment.RowVersion);

        Assert.True(confirmResult.Success, confirmResult.ErrorMessage);
        Assert.Equal(PaymentTransactionStatus.Confirmed, confirmResult.Payment!.Status);

        var allTx = await context.PaymentTransactions.OrderBy(x => x.PaymentTransactionId).ToListAsync();
        Assert.Equal(3, allTx.Count);

        var tx1 = allTx.Single(x => x.PaymentTransactionId == attempt1.Payment!.PaymentTransactionId);
        var tx2 = allTx.Single(x => x.PaymentTransactionId == attempt2.Payment!.PaymentTransactionId);
        var tx3 = allTx.Single(x => x.PaymentTransactionId == attempt3.Payment!.PaymentTransactionId);

        Assert.Equal(PaymentTransactionStatus.Confirmed, tx2.Status);
        Assert.Equal("staff-user", tx2.ConfirmedByUserId);
        Assert.NotNull(tx2.ConfirmedAtUtc);

        foreach (var closedTx in new[] { tx1, tx3 })
        {
            Assert.Equal(PaymentTransactionStatus.Rejected, closedTx.Status);
            Assert.Equal("staff-user", closedTx.RejectedByUserId);
            Assert.NotNull(closedTx.RejectedAtUtc);
            Assert.Equal($"Hóa đơn đã được thanh toán đủ qua giao dịch {tx2.Reference}", closedTx.RejectionReason);
        }

        var secondConfirm = await service.ConfirmSimulatedPaymentAsync(
            tx1.PaymentTransactionId,
            "staff-user",
            tx1.RowVersion);
        Assert.False(secondConfirm.Success);

        var newAttemptAfterPaid = await service.CreateResidentPaymentAttemptAsync("resident-user", 1);
        Assert.False(newAttemptAfterPaid.Success);
        Assert.Contains("thanh toán đủ", newAttemptAfterPaid.ErrorMessage);
    }

    [Fact]
    public async Task RejectPaymentAsync_RequiresReason_PreservesHistory_AndAllowsNewAttemptAfterRejection()
    {
        await using var context = CreateContext();
        SeedInvoice(context);
        var service = new PaymentService(context, NullLogger<PaymentService>.Instance);

        var attempt1 = await service.GetOrCreateResidentPaymentAsync("resident-user", 1);
        Assert.NotNull(attempt1);

        var emptyReasonResult = await service.RejectPaymentAsync(
            attempt1.PaymentTransactionId, "staff-user", "   ", attempt1.RowVersion);
        Assert.False(emptyReasonResult.Success);
        Assert.Contains("lý do từ chối", emptyReasonResult.ErrorMessage);

        var rejectResult = await service.RejectPaymentAsync(
            attempt1.PaymentTransactionId,
            "staff-user",
            "Chưa nhận được khoản chuyển khoản tương ứng",
            attempt1.RowVersion);
        Assert.True(rejectResult.Success, rejectResult.ErrorMessage);
        Assert.Equal(PaymentTransactionStatus.Rejected, rejectResult.Payment!.Status);
        Assert.Equal("staff-user", rejectResult.Payment.RejectedByUserId);
        Assert.NotNull(rejectResult.Payment.RejectedAtUtc);
        Assert.Equal("Chưa nhận được khoản chuyển khoản tương ứng", rejectResult.Payment.RejectionReason);

        var confirmRejected = await service.ConfirmSimulatedPaymentAsync(
            attempt1.PaymentTransactionId, "staff-user", rejectResult.Payment.RowVersion);
        Assert.False(confirmRejected.Success);

        var attempt2 = await service.GetOrCreateResidentPaymentAsync("resident-user", 1);
        Assert.NotNull(attempt2);
        Assert.NotEqual(attempt1.PaymentTransactionId, attempt2.PaymentTransactionId);
        Assert.NotEqual(attempt1.Reference, attempt2.Reference);
        Assert.Equal(PaymentTransactionStatus.Pending, attempt2.Status);
        Assert.Equal(2, await context.PaymentTransactions.CountAsync());
    }

    [Fact]
    public async Task VoidInvoice_AutoRejectsAllPendingPayments_AndBlocksConfirmingAfterVoid()
    {
        await using var context = CreateContext();
        SeedInvoice(context);
        var paymentService = new PaymentService(context, NullLogger<PaymentService>.Instance);
        var billingService = new BillingService(context, NullLogger<BillingService>.Instance);

        var attempt1 = await paymentService.CreateResidentPaymentAttemptAsync("resident-user", 1);
        var attempt2 = await paymentService.CreateResidentPaymentAttemptAsync("resident-user", 1);
        Assert.True(attempt1.Success);
        Assert.True(attempt2.Success);

        var invoice = await context.ApartmentInvoices.AsNoTracking().SingleAsync(x => x.ApartmentInvoiceId == 1);
        var voidResult = await billingService.VoidAsync(1, "staff-user", invoice.RowVersion, "Sai chỉ số công tơ");
        Assert.True(voidResult.Success, voidResult.ErrorMessage);

        var allTx = await context.PaymentTransactions.ToListAsync();
        Assert.Equal(2, allTx.Count);
        Assert.All(allTx, tx =>
        {
            Assert.Equal(PaymentTransactionStatus.Rejected, tx.Status);
            Assert.Equal("staff-user", tx.RejectedByUserId);
            Assert.NotNull(tx.RejectedAtUtc);
            Assert.Contains("Hóa đơn đã bị hủy bởi BQL", tx.RejectionReason);
        });

        var confirmAfterVoid = await paymentService.ConfirmSimulatedPaymentAsync(
            attempt1.Payment!.PaymentTransactionId, "staff-user", attempt1.Payment.RowVersion);
        Assert.False(confirmAfterVoid.Success);
    }

    [Fact]
    public async Task CreateAttempt_CannotCreatePending_OnPaidOrVoidOrDraftInvoice()
    {
        await using var context = CreateContext();
        SeedInvoice(context);
        var paymentService = new PaymentService(context, NullLogger<PaymentService>.Instance);
        var billingService = new BillingService(context, NullLogger<BillingService>.Instance);

        // 1. Khi hóa đơn đã Paid (Confirmed)
        var attempt = await paymentService.CreateResidentPaymentAttemptAsync("resident-user", 1);
        Assert.True(attempt.Success);
        var confirm = await paymentService.ConfirmSimulatedPaymentAsync(
            attempt.Payment!.PaymentTransactionId, "staff-user", attempt.Payment.RowVersion);
        Assert.True(confirm.Success);

        var createOnPaidResident = await paymentService.CreateResidentPaymentAttemptAsync("resident-user", 1);
        var createOnPaidStaff = await paymentService.CreatePaymentAttemptForInvoiceAsync(1);
        Assert.False(createOnPaidResident.Success);
        Assert.Contains("thanh toán đủ", createOnPaidResident.ErrorMessage);
        Assert.False(createOnPaidStaff.Success);
        Assert.Contains("thanh toán đủ", createOnPaidStaff.ErrorMessage);

        // 2. Khi hóa đơn ở trạng thái Draft hoặc Void
        var invoice2 = new ApartmentInvoice
        {
            ApartmentInvoiceId = 2,
            InvoiceCode = "INV-PAY-2",
            ApartmentId = 1,
            ResidentId = 1,
            BillingYear = 2026,
            BillingMonth = 10,
            DueDate = new DateTime(2026, 11, 10),
            ApartmentArea = 75,
            TotalAmount = 500000,
            Status = ApartmentInvoiceStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = "staff-user"
        };
        context.ApartmentInvoices.Add(invoice2);
        await context.SaveChangesAsync();

        var createOnDraft = await paymentService.CreateResidentPaymentAttemptAsync("resident-user", 2);
        Assert.False(createOnDraft.Success);
        Assert.Contains("phát hành", createOnDraft.ErrorMessage);

        var voidRes = await billingService.VoidAsync(2, "staff-user", invoice2.RowVersion);
        Assert.True(voidRes.Success);

        var createOnVoid = await paymentService.CreateResidentPaymentAttemptAsync("resident-user", 2);
        var createOnVoidStaff = await paymentService.CreatePaymentAttemptForInvoiceAsync(2);
        Assert.False(createOnVoid.Success);
        Assert.Contains("phát hành", createOnVoid.ErrorMessage);
        Assert.False(createOnVoidStaff.Success);
        Assert.Contains("phát hành", createOnVoidStaff.ErrorMessage);
        Assert.Empty(await context.PaymentTransactions.Where(x => x.ApartmentInvoiceId == 2).ToListAsync());
    }

    [Fact]
    public async Task CreateAttempt_WhenConcurrentConfirmCommitsBeforeSave_DetectsInvoiceConcurrencyConflictAndLeavesNoPending()
    {
        var dbName = Guid.NewGuid().ToString();
        var root = new InMemoryDatabaseRoot();

        await using (var setup = CreateSharedContext(dbName, root))
        {
            SeedInvoice(setup, [1, 0, 0, 0, 0, 0, 0, 1]);
            setup.PaymentTransactions.Add(new PaymentTransaction
            {
                PaymentTransactionId = 10,
                ApartmentInvoiceId = 1,
                Reference = "AH00001EXISTINGPENDING001",
                Amount = 1294500m,
                Status = PaymentTransactionStatus.Pending,
                CreatedAtUtc = DateTime.UtcNow,
                RowVersion = [1, 0, 0, 0, 0, 0, 0, 10]
            });
            await setup.SaveChangesAsync();
        }

        var saveBarrier = new InMemoryBeforeSaveInterceptor();
        await using var createContext = CreateSharedContext(dbName, root, saveBarrier);
        var createService = new PaymentService(createContext, NullLogger<PaymentService>.Instance);

        var createTask = Task.Run(() => createService.CreateResidentPaymentAttemptAsync("resident-user", 1));
        await saveBarrier.ReachedSavingChanges;

        // Trong khi CreateResidentPaymentAttemptAsync đã đọc hóa đơn (RowVersion = 1) và chuẩn bị SaveChanges,
        // một DbContext khác thực hiện Confirm giao dịch hiện có và làm tăng RowVersion của hóa đơn
        await using (var confirmContext = CreateSharedContext(dbName, root))
        {
            var confirmService = new PaymentService(confirmContext, NullLogger<PaymentService>.Instance);
            var confirmRes = await confirmService.ConfirmSimulatedPaymentAsync(10, "staff-user", [1, 0, 0, 0, 0, 0, 0, 10]);
            Assert.True(confirmRes.Success, confirmRes.ErrorMessage);

            // Mô phỏng SQL Server tự động tăng rowversion của ApartmentInvoices sau UPDATE
            var inv = await confirmContext.ApartmentInvoices.SingleAsync(x => x.ApartmentInvoiceId == 1);
            inv.RowVersion = [1, 0, 0, 0, 0, 0, 0, 2];
            await confirmContext.SaveChangesAsync();
        }

        saveBarrier.ReleaseSave();
        var createResult = await createTask;

        Assert.False(createResult.Success);
        Assert.Contains("đồng thời", createResult.ErrorMessage);

        await using var verifyContext = CreateSharedContext(dbName, root);
        var allTx = await verifyContext.PaymentTransactions.Where(x => x.ApartmentInvoiceId == 1).ToListAsync();
        var onlyConfirmed = Assert.Single(allTx);
        Assert.Equal(PaymentTransactionStatus.Confirmed, onlyConfirmed.Status);
    }

    [Fact]
    public async Task CreateAttempt_WhenConcurrentVoidCommitsBeforeSave_DetectsInvoiceConcurrencyConflictAndLeavesNoPending()
    {
        var dbName = Guid.NewGuid().ToString();
        var root = new InMemoryDatabaseRoot();

        await using (var setup = CreateSharedContext(dbName, root))
        {
            SeedInvoice(setup, [1, 0, 0, 0, 0, 0, 0, 1]);
        }

        var saveBarrier = new InMemoryBeforeSaveInterceptor();
        await using var createContext = CreateSharedContext(dbName, root, saveBarrier);
        var createService = new PaymentService(createContext, NullLogger<PaymentService>.Instance);

        var createTask = Task.Run(() => createService.CreateResidentPaymentAttemptAsync("resident-user", 1));
        await saveBarrier.ReachedSavingChanges;

        // Trong khi CreateResidentPaymentAttemptAsync đang chuẩn bị SaveChanges, DbContext khác thực hiện Void hóa đơn
        await using (var voidContext = CreateSharedContext(dbName, root))
        {
            var billingService = new BillingService(voidContext, NullLogger<BillingService>.Instance);
            var voidRes = await billingService.VoidAsync(1, "staff-user", [1, 0, 0, 0, 0, 0, 0, 1], "Hủy hóa đơn");
            Assert.True(voidRes.Success, voidRes.ErrorMessage);

            var inv = await voidContext.ApartmentInvoices.SingleAsync(x => x.ApartmentInvoiceId == 1);
            inv.RowVersion = [1, 0, 0, 0, 0, 0, 0, 2];
            await voidContext.SaveChangesAsync();
        }

        saveBarrier.ReleaseSave();
        var createResult = await createTask;

        Assert.False(createResult.Success);
        Assert.Contains("đồng thời", createResult.ErrorMessage);

        await using var verifyContext = CreateSharedContext(dbName, root);
        var invFinal = await verifyContext.ApartmentInvoices.Include(x => x.PaymentTransactions).SingleAsync(x => x.ApartmentInvoiceId == 1);
        Assert.Equal(ApartmentInvoiceStatus.Void, invFinal.Status);
        Assert.Empty(invFinal.PaymentTransactions);
    }

    [Fact]
    public async Task CreateAttempt_ConcurrentWithAnotherCreateAttempt_DetectsConflictAndPreventsCorruptData()
    {
        var dbName = Guid.NewGuid().ToString();
        var root = new InMemoryDatabaseRoot();

        await using (var setup = CreateSharedContext(dbName, root))
        {
            SeedInvoice(setup, [1, 0, 0, 0, 0, 0, 0, 1]);
        }

        var saveBarrier = new InMemoryBeforeSaveInterceptor();
        await using var createContext2 = CreateSharedContext(dbName, root, saveBarrier);
        var createService2 = new PaymentService(createContext2, NullLogger<PaymentService>.Instance);

        var task2 = Task.Run(() => createService2.CreateResidentPaymentAttemptAsync("resident-user", 1));
        await saveBarrier.ReachedSavingChanges;

        // Luồng 1 hoàn tất tạo giao dịch và làm tăng RowVersion của hóa đơn trước khi luồng 2 ghi
        await using (var createContext1 = CreateSharedContext(dbName, root))
        {
            var createService1 = new PaymentService(createContext1, NullLogger<PaymentService>.Instance);
            var res1 = await createService1.CreateResidentPaymentAttemptAsync("resident-user", 1);
            Assert.True(res1.Success, res1.ErrorMessage);

            var inv = await createContext1.ApartmentInvoices.SingleAsync(x => x.ApartmentInvoiceId == 1);
            inv.RowVersion = [1, 0, 0, 0, 0, 0, 0, 2];
            await createContext1.SaveChangesAsync();
        }

        saveBarrier.ReleaseSave();
        var res2 = await task2;

        Assert.False(res2.Success);
        Assert.Contains("đồng thời", res2.ErrorMessage);

        await using var verifyContext = CreateSharedContext(dbName, root);
        var allTx = await verifyContext.PaymentTransactions.Where(x => x.ApartmentInvoiceId == 1).ToListAsync();
        var singleTx = Assert.Single(allTx);
        Assert.Equal(PaymentTransactionStatus.Pending, singleTx.Status);
    }

    [Fact]
    public async Task ConfirmAndAutoReject_WhenSecondaryPendingHasConcurrencyConflict_FailsAndClearsTrackedChanges()
    {
        var dbName = Guid.NewGuid().ToString();
        var root = new InMemoryDatabaseRoot();

        await using (var setup = CreateSharedContext(dbName, root))
        {
            SeedInvoice(setup, [1, 0, 0, 0, 0, 0, 0, 1]);
            setup.PaymentTransactions.AddRange(
                new PaymentTransaction
                {
                    PaymentTransactionId = 101,
                    ApartmentInvoiceId = 1,
                    Reference = "AH00001MAINPENDING0000001",
                    Amount = 1294500m,
                    Status = PaymentTransactionStatus.Pending,
                    CreatedAtUtc = DateTime.UtcNow,
                    RowVersion = [1, 0, 0, 0, 0, 0, 0, 11]
                },
                new PaymentTransaction
                {
                    PaymentTransactionId = 102,
                    ApartmentInvoiceId = 1,
                    Reference = "AH00001SUBPENDING00000002",
                    Amount = 1294500m,
                    Status = PaymentTransactionStatus.Pending,
                    CreatedAtUtc = DateTime.UtcNow,
                    RowVersion = [1, 0, 0, 0, 0, 0, 0, 12]
                });
            await setup.SaveChangesAsync();
        }

        var saveBarrier = new InMemoryBeforeSaveInterceptor();
        await using var confirmContext = CreateSharedContext(dbName, root, saveBarrier);
        var confirmService = new PaymentService(confirmContext, NullLogger<PaymentService>.Instance);

        var confirmTask = Task.Run(() => confirmService.ConfirmSimulatedPaymentAsync(101, "staff-user", [1, 0, 0, 0, 0, 0, 0, 11]));
        await saveBarrier.ReachedSavingChanges;

        // Cố tình thay đổi RowVersion của giao dịch phụ 102 trên một DbContext độc lập trước khi SaveChanges thực thi
        await using (var competingContext = CreateSharedContext(dbName, root))
        {
            var subTx = await competingContext.PaymentTransactions.SingleAsync(x => x.PaymentTransactionId == 102);
            subTx.RowVersion = [9, 9, 9, 9, 9, 9, 9, 99];
            await competingContext.SaveChangesAsync();
        }

        saveBarrier.ReleaseSave();
        var confirmResult = await confirmTask;

        Assert.False(confirmResult.Success);
        Assert.Contains("đồng thời", confirmResult.ErrorMessage);
        Assert.Empty(confirmContext.ChangeTracker.Entries());
    }

    private sealed class InMemoryBeforeSaveInterceptor : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource<bool> _reached =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _proceed =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _intercepted;

        public Task ReachedSavingChanges => _reached.Task;

        public void ReleaseSave() => _proceed.TrySetResult(true);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.CompareExchange(ref _intercepted, 1, 0) == 0)
            {
                _reached.TrySetResult(true);
                await _proceed.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }

            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static ApplicationDbContext CreateSharedContext(
        string dbName,
        InMemoryDatabaseRoot root,
        params IInterceptor[] interceptors)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName, root);
        if (interceptors.Length > 0)
        {
            builder.AddInterceptors(interceptors);
        }

        return new ApplicationDbContext(builder.Options);
    }

    private static void SeedInvoice(ApplicationDbContext context, byte[]? invoiceRowVersion = null)
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
            IssuedByUser = staff,
            RowVersion = invoiceRowVersion ?? []
        };

        context.AddRange(user, staff, building, apartment, resident, invoice);
        context.SaveChanges();
    }
}
