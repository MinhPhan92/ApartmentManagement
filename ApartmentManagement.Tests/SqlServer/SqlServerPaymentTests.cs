using System.Data.Common;
using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ApartmentManagement.Tests.SqlServer;

public sealed class SqlServerPaymentTests
{
    [SqlServerFact]
    public async Task MultiplePaymentAttempts_AreAllowed_WhileConfirmedFilteredUniqueIndex_RejectsSecondConfirmedForInvoice()
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

        // 1. Multiple Pending attempts for the same invoice are allowed sequentially
        await using (var context = database.CreateContext())
        {
            var service = new PaymentService(context, NullLogger<PaymentService>.Instance);
            var attempt1 = await service.CreateResidentPaymentAttemptAsync(residentUserId, invoiceId);
            var attempt2 = await service.CreateResidentPaymentAttemptAsync(residentUserId, invoiceId);

            Assert.True(attempt1.Success, attempt1.ErrorMessage);
            Assert.True(attempt2.Success, attempt2.ErrorMessage);
            Assert.NotEqual(attempt1.Payment!.Reference, attempt2.Payment!.Reference);

            var confirmFirst = await service.ConfirmSimulatedPaymentAsync(
                attempt1.Payment.PaymentTransactionId,
                staffUserId,
                attempt1.Payment.RowVersion);
            Assert.True(confirmFirst.Success, confirmFirst.ErrorMessage);
        }

        // 2. Direct DB insert of a second Confirmed transaction for the same invoice is rejected by UX_PaymentTransactions_ApartmentInvoiceId_Confirmed
        await using var duplicateConfirmedContext = database.CreateContext();
        duplicateConfirmedContext.PaymentTransactions.Add(new PaymentTransaction
        {
            ApartmentInvoiceId = invoiceId,
            Reference = $"AH{Guid.NewGuid():N}"[..25].ToUpperInvariant(),
            Amount = 125000,
            Status = PaymentTransactionStatus.Confirmed,
            CreatedAtUtc = DateTime.UtcNow,
            ConfirmedAtUtc = DateTime.UtcNow,
            ConfirmedByUserId = staffUserId
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => duplicateConfirmedContext.SaveChangesAsync());
    }

    [SqlServerFact]
    public async Task PaymentStatusCheckConstraint_EnforcesPendingConfirmedAndRejectedMetadata()
    {
        await using var database = SqlServerTestDatabase.Create();
        int invoiceId;
        string staffUserId;
        await using (var setup = database.CreateContext())
        {
            await setup.Database.MigrateAsync();
            (invoiceId, _, staffUserId) = await SeedIssuedInvoiceAsync(setup);
        }

        // 1. Rejected without reason must fail CK_PaymentTransactions_Status
        await using (var invalidRejectedContext = database.CreateContext())
        {
            invalidRejectedContext.PaymentTransactions.Add(new PaymentTransaction
            {
                ApartmentInvoiceId = invoiceId,
                Reference = $"AH{Guid.NewGuid():N}"[..25].ToUpperInvariant(),
                Amount = 125000,
                Status = PaymentTransactionStatus.Rejected,
                CreatedAtUtc = DateTime.UtcNow,
                RejectedAtUtc = DateTime.UtcNow,
                RejectedByUserId = staffUserId,
                RejectionReason = "   "
            });
            await Assert.ThrowsAsync<DbUpdateException>(() => invalidRejectedContext.SaveChangesAsync());
        }

        // 2. Pending with RejectionReason must fail CK_PaymentTransactions_Status
        await using (var invalidPendingContext = database.CreateContext())
        {
            invalidPendingContext.PaymentTransactions.Add(new PaymentTransaction
            {
                ApartmentInvoiceId = invoiceId,
                Reference = $"AH{Guid.NewGuid():N}"[..25].ToUpperInvariant(),
                Amount = 125000,
                Status = PaymentTransactionStatus.Pending,
                CreatedAtUtc = DateTime.UtcNow,
                RejectionReason = "Lý do không hợp lệ cho Pending"
            });
            await Assert.ThrowsAsync<DbUpdateException>(() => invalidPendingContext.SaveChangesAsync());
        }

        // 3. Confirmed with RejectedAtUtc must fail CK_PaymentTransactions_Status
        await using (var invalidConfirmedContext = database.CreateContext())
        {
            invalidConfirmedContext.PaymentTransactions.Add(new PaymentTransaction
            {
                ApartmentInvoiceId = invoiceId,
                Reference = $"AH{Guid.NewGuid():N}"[..25].ToUpperInvariant(),
                Amount = 125000,
                Status = PaymentTransactionStatus.Confirmed,
                CreatedAtUtc = DateTime.UtcNow,
                ConfirmedAtUtc = DateTime.UtcNow,
                ConfirmedByUserId = staffUserId,
                RejectedAtUtc = DateTime.UtcNow
            });
            await Assert.ThrowsAsync<DbUpdateException>(() => invalidConfirmedContext.SaveChangesAsync());
        }

        // 4. Valid Rejected record succeeds
        await using (var validRejectedContext = database.CreateContext())
        {
            validRejectedContext.PaymentTransactions.Add(new PaymentTransaction
            {
                ApartmentInvoiceId = invoiceId,
                Reference = $"AH{Guid.NewGuid():N}"[..25].ToUpperInvariant(),
                Amount = 125000,
                Status = PaymentTransactionStatus.Rejected,
                CreatedAtUtc = DateTime.UtcNow,
                RejectedAtUtc = DateTime.UtcNow,
                RejectedByUserId = staffUserId,
                RejectionReason = "Sai nội dung chuyển khoản"
            });
            await validRejectedContext.SaveChangesAsync();
        }
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

    [SqlServerFact]
    public async Task ConcurrentConfirm_OfTwoPendingTransactionsForSameInvoice_AllowsOnlyOneConfirmed()
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

        int tx1Id;
        byte[] tx1RowVersion;
        int tx2Id;
        byte[] tx2RowVersion;
        byte[] initialInvoiceRowVersion;
        await using (var seedTx = database.CreateContext())
        {
            var service = new PaymentService(seedTx, NullLogger<PaymentService>.Instance);
            var attempt1 = await service.CreateResidentPaymentAttemptAsync(residentUserId, invoiceId);
            var attempt2 = await service.CreateResidentPaymentAttemptAsync(residentUserId, invoiceId);
            tx1Id = attempt1.Payment!.PaymentTransactionId;
            tx1RowVersion = attempt1.Payment.RowVersion.ToArray();
            tx2Id = attempt2.Payment!.PaymentTransactionId;
            tx2RowVersion = attempt2.Payment.RowVersion.ToArray();
            initialInvoiceRowVersion = await seedTx.ApartmentInvoices
                .AsNoTracking()
                .Where(x => x.ApartmentInvoiceId == invoiceId)
                .Select(x => x.RowVersion)
                .SingleAsync();
        }

        var barrier1 = new SqlWriteBarrierInterceptor();
        var barrier2 = new SqlWriteBarrierInterceptor();
        await using var context1 = database.CreateContext(barrier1);
        await using var context2 = database.CreateContext(barrier2);
        Assert.NotSame(context1.Database.GetDbConnection(), context2.Database.GetDbConnection());

        var service1 = new PaymentService(context1, NullLogger<PaymentService>.Instance);
        var service2 = new PaymentService(context2, NullLogger<PaymentService>.Instance);

        var task1 = Task.Run(() => service1.ConfirmSimulatedPaymentAsync(tx1Id, staffUserId, tx1RowVersion));
        var task2 = Task.Run(() => service2.ConfirmSimulatedPaymentAsync(tx2Id, staffUserId, tx2RowVersion));

        // Đợi cả hai luồng đọc xong dữ liệu (SELECT) và dừng ngay trước câu lệnh UPDATE đầu tiên
        await Task.WhenAll(barrier1.ReachedWritePhase, barrier2.ReachedWritePhase);

        // Xác minh cả 2 DbContext thực sự đọc và gửi cùng một RowVersion ban đầu của ApartmentInvoice
        var ctx1InvoiceRv = context1.ChangeTracker.Entries<ApartmentInvoice>()
            .Single().Property(x => x.RowVersion).OriginalValue;
        var ctx2InvoiceRv = context2.ChangeTracker.Entries<ApartmentInvoice>()
            .Single().Property(x => x.RowVersion).OriginalValue;
        Assert.Equal(initialInvoiceRowVersion, ctx1InvoiceRv);
        Assert.Equal(initialInvoiceRowVersion, ctx2InvoiceRv);

        // Cho phép luồng 1 ghi và commit trước, sau đó mới giải phóng luồng 2
        barrier1.ReleaseWrite();
        var result1 = await task1;
        Assert.True(result1.Success, result1.ErrorMessage);

        barrier2.ReleaseWrite();
        var result2 = await task2;
        Assert.False(result2.Success);
        Assert.Contains("đồng thời", result2.ErrorMessage);

        await using var verify = database.CreateContext();
        var transactions = await verify.PaymentTransactions
            .AsNoTracking()
            .Where(x => x.ApartmentInvoiceId == invoiceId)
            .ToListAsync();

        Assert.Equal(2, transactions.Count);
        var confirmedTx = Assert.Single(transactions, x => x.Status == PaymentTransactionStatus.Confirmed);
        var rejectedTx = Assert.Single(transactions, x => x.Status == PaymentTransactionStatus.Rejected);
        Assert.Equal(tx1Id, confirmedTx.PaymentTransactionId);
        Assert.Equal(tx2Id, rejectedTx.PaymentTransactionId);
        Assert.Equal($"Hóa đơn đã được thanh toán đủ qua giao dịch {confirmedTx.Reference}", rejectedTx.RejectionReason);
    }

    [SqlServerFact]
    public async Task ConcurrentConfirmAndVoid_AcrossTwoDbContexts_PreventsBothSucceedingAndPreservesIntegrity()
    {
        await using var database = SqlServerTestDatabase.Create();
        int invoiceId1;
        string residentUserId1;
        string staffUserId1;
        int invoiceId2;
        string residentUserId2;
        string staffUserId2;
        await using (var setup = database.CreateContext())
        {
            await setup.Database.MigrateAsync();
            (invoiceId1, residentUserId1, staffUserId1) = await SeedIssuedInvoiceAsync(setup);
            (invoiceId2, residentUserId2, staffUserId2) = await SeedIssuedInvoiceAsync(setup);
        }

        // --- Kịch bản A: Cả hai cùng đọc, Confirm ghi trước -> Void thất bại vì đụng độ RowVersion ---
        int paymentId1;
        byte[] paymentRv1;
        byte[] invoiceRv1;
        await using (var prepare1 = database.CreateContext())
        {
            var paymentService = new PaymentService(prepare1, NullLogger<PaymentService>.Instance);
            var attempt = await paymentService.CreateResidentPaymentAttemptAsync(residentUserId1, invoiceId1);
            paymentId1 = attempt.Payment!.PaymentTransactionId;
            paymentRv1 = attempt.Payment.RowVersion.ToArray();
            invoiceRv1 = await prepare1.ApartmentInvoices
                .AsNoTracking()
                .Where(x => x.ApartmentInvoiceId == invoiceId1)
                .Select(x => x.RowVersion)
                .SingleAsync();
        }

        var confirmBarrierA = new SqlWriteBarrierInterceptor();
        var voidBarrierA = new SqlWriteBarrierInterceptor();
        await using (var confirmCtxA = database.CreateContext(confirmBarrierA))
        await using (var voidCtxA = database.CreateContext(voidBarrierA))
        {
            Assert.NotSame(confirmCtxA.Database.GetDbConnection(), voidCtxA.Database.GetDbConnection());

            var confirmServiceA = new PaymentService(confirmCtxA, NullLogger<PaymentService>.Instance);
            var voidServiceA = new BillingService(voidCtxA, NullLogger<BillingService>.Instance);

            var confirmTaskA = Task.Run(() => confirmServiceA.ConfirmSimulatedPaymentAsync(paymentId1, staffUserId1, paymentRv1));
            var voidTaskA = Task.Run(() => voidServiceA.VoidAsync(invoiceId1, staffUserId1, invoiceRv1, "Hủy đồng thời A"));

            await Task.WhenAll(confirmBarrierA.ReachedWritePhase, voidBarrierA.ReachedWritePhase);

            Assert.Equal(
                invoiceRv1,
                confirmCtxA.ChangeTracker.Entries<ApartmentInvoice>().Single().Property(x => x.RowVersion).OriginalValue);
            Assert.Equal(
                invoiceRv1,
                voidCtxA.ChangeTracker.Entries<ApartmentInvoice>().Single().Property(x => x.RowVersion).OriginalValue);

            confirmBarrierA.ReleaseWrite();
            var confirmResA = await confirmTaskA;
            Assert.True(confirmResA.Success, confirmResA.ErrorMessage);

            voidBarrierA.ReleaseWrite();
            var voidResA = await voidTaskA;
            Assert.False(voidResA.Success);
            Assert.Contains("đồng thời", voidResA.ErrorMessage);
        }

        await using (var verifyA = database.CreateContext())
        {
            var finalInvoiceA = await verifyA.ApartmentInvoices
                .AsNoTracking()
                .Include(x => x.PaymentTransactions)
                .SingleAsync(x => x.ApartmentInvoiceId == invoiceId1);
            Assert.Equal(ApartmentInvoiceStatus.Issued, finalInvoiceA.Status);
            var finalTxA = Assert.Single(finalInvoiceA.PaymentTransactions);
            Assert.Equal(PaymentTransactionStatus.Confirmed, finalTxA.Status);
        }

        // --- Kịch bản B: Cả hai cùng đọc, Void ghi trước -> Confirm thất bại vì đụng độ RowVersion ---
        int paymentId2;
        byte[] paymentRv2;
        byte[] invoiceRv2;
        await using (var prepare2 = database.CreateContext())
        {
            var paymentService = new PaymentService(prepare2, NullLogger<PaymentService>.Instance);
            var attempt = await paymentService.CreateResidentPaymentAttemptAsync(residentUserId2, invoiceId2);
            paymentId2 = attempt.Payment!.PaymentTransactionId;
            paymentRv2 = attempt.Payment.RowVersion.ToArray();
            invoiceRv2 = await prepare2.ApartmentInvoices
                .AsNoTracking()
                .Where(x => x.ApartmentInvoiceId == invoiceId2)
                .Select(x => x.RowVersion)
                .SingleAsync();
        }

        var confirmBarrierB = new SqlWriteBarrierInterceptor();
        var voidBarrierB = new SqlWriteBarrierInterceptor();
        await using (var confirmCtxB = database.CreateContext(confirmBarrierB))
        await using (var voidCtxB = database.CreateContext(voidBarrierB))
        {
            Assert.NotSame(confirmCtxB.Database.GetDbConnection(), voidCtxB.Database.GetDbConnection());

            var confirmServiceB = new PaymentService(confirmCtxB, NullLogger<PaymentService>.Instance);
            var voidServiceB = new BillingService(voidCtxB, NullLogger<BillingService>.Instance);

            var confirmTaskB = Task.Run(() => confirmServiceB.ConfirmSimulatedPaymentAsync(paymentId2, staffUserId2, paymentRv2));
            var voidTaskB = Task.Run(() => voidServiceB.VoidAsync(invoiceId2, staffUserId2, invoiceRv2, "Hủy đồng thời B"));

            await Task.WhenAll(confirmBarrierB.ReachedWritePhase, voidBarrierB.ReachedWritePhase);

            voidBarrierB.ReleaseWrite();
            var voidResB = await voidTaskB;
            Assert.True(voidResB.Success, voidResB.ErrorMessage);

            confirmBarrierB.ReleaseWrite();
            var confirmResB = await confirmTaskB;
            Assert.False(confirmResB.Success);
            Assert.Contains("đồng thời", confirmResB.ErrorMessage);
        }

        await using (var verifyB = database.CreateContext())
        {
            var finalInvoiceB = await verifyB.ApartmentInvoices
                .AsNoTracking()
                .Include(x => x.PaymentTransactions)
                .SingleAsync(x => x.ApartmentInvoiceId == invoiceId2);
            Assert.Equal(ApartmentInvoiceStatus.Void, finalInvoiceB.Status);
            var finalTxB = Assert.Single(finalInvoiceB.PaymentTransactions);
            Assert.Equal(PaymentTransactionStatus.Rejected, finalTxB.Status);
            Assert.Contains("Hóa đơn đã bị hủy bởi BQL", finalTxB.RejectionReason);
        }
    }

    [SqlServerFact]
    public async Task ConcurrentCreateAttempt_WithConfirm_Void_AndAnotherCreateAttempt_RollsBackOnConflict()
    {
        await using var database = SqlServerTestDatabase.Create();
        int invConfirmId;
        string resConfirmId;
        string staffConfirmId;
        int invVoidId;
        string resVoidId;
        string staffVoidId;
        int invDoubleCreateId;
        string resDoubleCreateId;
        await using (var setup = database.CreateContext())
        {
            await setup.Database.MigrateAsync();
            (invConfirmId, resConfirmId, staffConfirmId) = await SeedIssuedInvoiceAsync(setup);
            (invVoidId, resVoidId, staffVoidId) = await SeedIssuedInvoiceAsync(setup);
            (invDoubleCreateId, resDoubleCreateId, _) = await SeedIssuedInvoiceAsync(setup);
        }

        // 1. CreatePaymentAttempt chạy đồng thời với Confirm
        int existingTxId;
        byte[] existingTxRv;
        await using (var seed1 = database.CreateContext())
        {
            var svc = new PaymentService(seed1, NullLogger<PaymentService>.Instance);
            var created = await svc.CreateResidentPaymentAttemptAsync(resConfirmId, invConfirmId);
            existingTxId = created.Payment!.PaymentTransactionId;
            existingTxRv = created.Payment.RowVersion.ToArray();
        }

        var createBarrier1 = new SqlWriteBarrierInterceptor();
        var confirmBarrier1 = new SqlWriteBarrierInterceptor();
        await using (var createCtx1 = database.CreateContext(createBarrier1))
        await using (var confirmCtx1 = database.CreateContext(confirmBarrier1))
        {
            var createSvc = new PaymentService(createCtx1, NullLogger<PaymentService>.Instance);
            var confirmSvc = new PaymentService(confirmCtx1, NullLogger<PaymentService>.Instance);

            var createTask = Task.Run(() => createSvc.CreateResidentPaymentAttemptAsync(resConfirmId, invConfirmId));
            var confirmTask = Task.Run(() => confirmSvc.ConfirmSimulatedPaymentAsync(existingTxId, staffConfirmId, existingTxRv));

            await Task.WhenAll(createBarrier1.ReachedWritePhase, confirmBarrier1.ReachedWritePhase);

            confirmBarrier1.ReleaseWrite();
            var confirmRes = await confirmTask;
            Assert.True(confirmRes.Success, confirmRes.ErrorMessage);

            createBarrier1.ReleaseWrite();
            var createRes = await createTask;
            Assert.False(createRes.Success);
            Assert.Contains("đồng thời", createRes.ErrorMessage);
        }

        await using (var verify1 = database.CreateContext())
        {
            var txs = await verify1.PaymentTransactions.AsNoTracking().Where(x => x.ApartmentInvoiceId == invConfirmId).ToListAsync();
            var onlyTx = Assert.Single(txs);
            Assert.Equal(PaymentTransactionStatus.Confirmed, onlyTx.Status);
        }

        // 2. CreatePaymentAttempt chạy đồng thời với Void
        byte[] invVoidRv;
        await using (var readVoid = database.CreateContext())
        {
            invVoidRv = await readVoid.ApartmentInvoices
                .AsNoTracking()
                .Where(x => x.ApartmentInvoiceId == invVoidId)
                .Select(x => x.RowVersion)
                .SingleAsync();
        }

        var createBarrier2 = new SqlWriteBarrierInterceptor();
        var voidBarrier2 = new SqlWriteBarrierInterceptor();
        await using (var createCtx2 = database.CreateContext(createBarrier2))
        await using (var voidCtx2 = database.CreateContext(voidBarrier2))
        {
            var createSvc = new PaymentService(createCtx2, NullLogger<PaymentService>.Instance);
            var voidSvc = new BillingService(voidCtx2, NullLogger<BillingService>.Instance);

            var createTask = Task.Run(() => createSvc.CreateResidentPaymentAttemptAsync(resVoidId, invVoidId));
            var voidTask = Task.Run(() => voidSvc.VoidAsync(invVoidId, staffVoidId, invVoidRv, "Hủy khi đang tạo QR"));

            await Task.WhenAll(createBarrier2.ReachedWritePhase, voidBarrier2.ReachedWritePhase);

            voidBarrier2.ReleaseWrite();
            var voidRes = await voidTask;
            Assert.True(voidRes.Success, voidRes.ErrorMessage);

            createBarrier2.ReleaseWrite();
            var createRes = await createTask;
            Assert.False(createRes.Success);
            Assert.Contains("đồng thời", createRes.ErrorMessage);
        }

        await using (var verify2 = database.CreateContext())
        {
            Assert.Empty(await verify2.PaymentTransactions.AsNoTracking().Where(x => x.ApartmentInvoiceId == invVoidId).ToListAsync());
        }

        // 3. CreatePaymentAttempt chạy đồng thời với một CreatePaymentAttempt khác
        var createBarrier3A = new SqlWriteBarrierInterceptor();
        var createBarrier3B = new SqlWriteBarrierInterceptor();
        await using (var createCtx3A = database.CreateContext(createBarrier3A))
        await using (var createCtx3B = database.CreateContext(createBarrier3B))
        {
            var svcA = new PaymentService(createCtx3A, NullLogger<PaymentService>.Instance);
            var svcB = new PaymentService(createCtx3B, NullLogger<PaymentService>.Instance);

            var taskA = Task.Run(() => svcA.CreateResidentPaymentAttemptAsync(resDoubleCreateId, invDoubleCreateId));
            var taskB = Task.Run(() => svcB.CreateResidentPaymentAttemptAsync(resDoubleCreateId, invDoubleCreateId));

            await Task.WhenAll(createBarrier3A.ReachedWritePhase, createBarrier3B.ReachedWritePhase);

            createBarrier3A.ReleaseWrite();
            var resA = await taskA;
            Assert.True(resA.Success, resA.ErrorMessage);

            createBarrier3B.ReleaseWrite();
            var resB = await taskB;
            Assert.False(resB.Success);
            Assert.Contains("đồng thời", resB.ErrorMessage);
        }

        await using (var verify3 = database.CreateContext())
        {
            var txs = await verify3.PaymentTransactions.AsNoTracking().Where(x => x.ApartmentInvoiceId == invDoubleCreateId).ToListAsync();
            var singlePending = Assert.Single(txs);
            Assert.Equal(PaymentTransactionStatus.Pending, singlePending.Status);
        }
    }

    [SqlServerFact]
    public async Task ConfirmAndAutoReject_WhenSecondaryPendingTransactionHasConcurrencyConflict_RollsBackEntireTransactionAtomically()
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

        int mainTxId;
        byte[] mainTxRv;
        int secondaryTx1Id;
        int secondaryTx2Id;
        await using (var seed = database.CreateContext())
        {
            var service = new PaymentService(seed, NullLogger<PaymentService>.Instance);
            var mainAttempt = await service.CreateResidentPaymentAttemptAsync(residentUserId, invoiceId);
            var secAttempt1 = await service.CreateResidentPaymentAttemptAsync(residentUserId, invoiceId);
            var secAttempt2 = await service.CreateResidentPaymentAttemptAsync(residentUserId, invoiceId);

            mainTxId = mainAttempt.Payment!.PaymentTransactionId;
            mainTxRv = mainAttempt.Payment.RowVersion.ToArray();
            secondaryTx1Id = secAttempt1.Payment!.PaymentTransactionId;
            secondaryTx2Id = secAttempt2.Payment!.PaymentTransactionId;
        }

        var confirmBarrier = new SqlWriteBarrierInterceptor();
        await using (var confirmContext = database.CreateContext(confirmBarrier))
        {
            var confirmService = new PaymentService(confirmContext, NullLogger<PaymentService>.Instance);
            var confirmTask = Task.Run(() => confirmService.ConfirmSimulatedPaymentAsync(mainTxId, staffUserId, mainTxRv));

            // Đợi confirmService đọc xong tất cả dữ liệu và chuẩn bị bước vào SaveChangesAsync
            await confirmBarrier.ReachedWritePhase;

            // Dùng một DbContext độc lập thứ hai cập nhật giao dịch Pending phụ thứ 2 để làm lệch RowVersion của riêng bản ghi phụ này
            await using (var competingContext = database.CreateContext())
            {
                Assert.NotSame(confirmContext.Database.GetDbConnection(), competingContext.Database.GetDbConnection());
                await competingContext.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE [PaymentTransactions] SET [CreatedAtUtc] = DATEADD(second, 1, [CreatedAtUtc]) WHERE [PaymentTransactionId] = {secondaryTx2Id}");
            }

            // Cho phép SaveChangesAsync tiếp tục chạy và gặp lỗi DbUpdateConcurrencyException ở giao dịch phụ thứ 2
            confirmBarrier.ReleaseWrite();
            var confirmResult = await confirmTask;

            Assert.False(confirmResult.Success);
            Assert.Contains("đồng thời", confirmResult.ErrorMessage);
        }

        // Reload dữ liệu bằng một DbContext mới hoàn toàn để kiểm chứng toàn bộ transaction đã được rollback nguyên tử
        await using (var verifyContext = database.CreateContext())
        {
            var invoice = await verifyContext.ApartmentInvoices
                .AsNoTracking()
                .Include(x => x.PaymentTransactions)
                .SingleAsync(x => x.ApartmentInvoiceId == invoiceId);

            Assert.Equal(ApartmentInvoiceStatus.Issued, invoice.Status);
            Assert.Equal(125000m, invoice.TotalAmount);
            Assert.Equal(3, invoice.PaymentTransactions.Count);

            var mainTx = invoice.PaymentTransactions.Single(x => x.PaymentTransactionId == mainTxId);
            var secTx1 = invoice.PaymentTransactions.Single(x => x.PaymentTransactionId == secondaryTx1Id);
            var secTx2 = invoice.PaymentTransactions.Single(x => x.PaymentTransactionId == secondaryTx2Id);

            foreach (var tx in new[] { mainTx, secTx1, secTx2 })
            {
                Assert.Equal(PaymentTransactionStatus.Pending, tx.Status);
                Assert.Null(tx.ConfirmedAtUtc);
                Assert.Null(tx.ConfirmedByUserId);
                Assert.Null(tx.RejectedAtUtc);
                Assert.Null(tx.RejectedByUserId);
                Assert.Null(tx.RejectionReason);
            }
        }
    }

    private sealed class SqlWriteBarrierInterceptor : DbCommandInterceptor
    {
        private readonly TaskCompletionSource<bool> _reachedWritePhase =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _allowWriteToProceed =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _intercepted;

        public Task ReachedWritePhase => _reachedWritePhase.Task;

        public void ReleaseWrite() => _allowWriteToProceed.TrySetResult(true);

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (IsWriteCommand(command.CommandText) &&
                Interlocked.CompareExchange(ref _intercepted, 1, 0) == 0)
            {
                _reachedWritePhase.TrySetResult(true);
                await _allowWriteToProceed.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }

            return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (IsWriteCommand(command.CommandText) &&
                Interlocked.CompareExchange(ref _intercepted, 1, 0) == 0)
            {
                _reachedWritePhase.TrySetResult(true);
                await _allowWriteToProceed.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }

            return await base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        private static bool IsWriteCommand(string commandText) =>
            commandText.Contains("UPDATE [", StringComparison.OrdinalIgnoreCase) ||
            commandText.Contains("INSERT INTO [", StringComparison.OrdinalIgnoreCase) ||
            commandText.Contains("DELETE FROM [", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<(int InvoiceId, string ResidentUserId, string StaffUserId)> SeedIssuedInvoiceAsync(
        ApplicationDbContext context)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var residentUserId = $"payment-resident-{suffix}";
        var staffUserId = $"payment-staff-{suffix}";
        var residentUser = CreateUser(residentUserId);
        var staffUser = CreateUser(staffUserId);
        var building = new Building
        {
            BuildingCode = $"PAY-{suffix}"[..10],
            BuildingName = "SQL Payment Test",
            NumberOfFloors = 5
        };
        var apartment = new Apartment
        {
            Building = building,
            ApartmentCode = $"P-{suffix[..4]}",
            Floor = 1,
            Area = 70,
            Status = "Đang sử dụng"
        };
        var resident = new Resident
        {
            User = residentUser,
            UserId = residentUser.Id,
            CitizenId = Random.Shared.NextInt64(100000000000L, 999999999999L).ToString()
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
