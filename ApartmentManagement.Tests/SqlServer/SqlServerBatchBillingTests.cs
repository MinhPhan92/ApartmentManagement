using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ApartmentManagement.Tests.SqlServer;

public sealed class SqlServerBatchBillingTests
{
    private static async Task<(ApplicationUser manager, Building building, Apartment apt1, Apartment apt2)> SeedBuildingApartmentsAndTariffsAsync(
        ApplicationDbContext context,
        int buildingId = 1)
    {
        var manager = new ApplicationUser
        {
            Id = $"user-sql-manager-{buildingId}",
            UserName = $"sqlmanager{buildingId}@test.local",
            Email = $"sqlmanager{buildingId}@test.local",
            FullName = "SQL Test Manager",
            EmailConfirmed = true,
            IsActive = true
        };

        var building = new Building
        {
            BuildingId = buildingId,
            BuildingCode = $"SQL-BLD-{buildingId}",
            BuildingName = $"Tòa SQL {buildingId}",
            Address = "123 SQL Street",
            NumberOfFloors = 10
        };

        context.AddRange(manager, building);

        // Management Tariff
        context.FeeTariffs.Add(new FeeTariff
        {
            Building = building,
            ChargeType = FeeChargeType.Management,
            EffectiveFrom = new DateTime(2026, 1, 1),
            MonthlyRatePerSquareMeter = 1000,
            CreatedByUser = manager
        });

        // Electricity Tariff
        context.FeeTariffs.Add(new FeeTariff
        {
            Building = building,
            ChargeType = FeeChargeType.Electricity,
            EffectiveFrom = new DateTime(2026, 1, 1),
            CreatedByUser = manager,
            Tiers =
            [
                new FeeTariffTier { TierOrder = 1, UpperConsumption = 50, UnitRate = 2000 },
                new FeeTariffTier { TierOrder = 2, UpperConsumption = null, UnitRate = 3000 }
            ]
        });

        // Water Tariff
        context.FeeTariffs.Add(new FeeTariff
        {
            Building = building,
            ChargeType = FeeChargeType.Water,
            EffectiveFrom = new DateTime(2026, 1, 1),
            CreatedByUser = manager,
            Tiers = [new FeeTariffTier { TierOrder = 1, UpperConsumption = null, UnitRate = 500 }]
        });

        // Apartment 1
        var apt1 = new Apartment
        {
            Building = building,
            ApartmentCode = "SQL-101",
            Floor = 1,
            Area = 50m,
            Status = "Đang sử dụng"
        };
        var user1 = new ApplicationUser
        {
            Id = "user-sql-apt-1",
            UserName = "sqlresident1@test.local",
            Email = "sqlresident1@test.local",
            FullName = "SQL Resident 1",
            EmailConfirmed = true,
            IsActive = true
        };
        var res1 = new Resident
        {
            User = user1,
            CitizenId = "001202600101"
        };
        context.AddRange(apt1, user1, res1);
        context.ApartmentResidents.Add(new ApartmentResident
        {
            Apartment = apt1,
            Resident = res1,
            Relationship = "Chủ hộ",
            MoveInDate = new DateTime(2026, 1, 1),
            IsOwner = true
        });

        // Apartment 2
        var apt2 = new Apartment
        {
            Building = building,
            ApartmentCode = "SQL-102",
            Floor = 1,
            Area = 60m,
            Status = "Đang sử dụng"
        };
        var user2 = new ApplicationUser
        {
            Id = "user-sql-apt-2",
            UserName = "sqlresident2@test.local",
            Email = "sqlresident2@test.local",
            FullName = "SQL Resident 2",
            EmailConfirmed = true,
            IsActive = true
        };
        var res2 = new Resident
        {
            User = user2,
            CitizenId = "001202600102"
        };
        context.AddRange(apt2, user2, res2);
        context.ApartmentResidents.Add(new ApartmentResident
        {
            Apartment = apt2,
            Resident = res2,
            Relationship = "Chủ hộ",
            MoveInDate = new DateTime(2026, 1, 1),
            IsOwner = true
        });

        // Meter readings for Apt 1 (period 2026-08)
        context.UtilityMeterReadings.AddRange(
            new UtilityMeterReading
            {
                Apartment = apt1,
                UtilityType = FeeChargeType.Electricity,
                BillingYear = 2026,
                BillingMonth = 8,
                PreviousReading = 0,
                CurrentReading = 60,
                ReadAtUtc = DateTime.UtcNow,
                RecordedByUser = manager
            },
            new UtilityMeterReading
            {
                Apartment = apt1,
                UtilityType = FeeChargeType.Water,
                BillingYear = 2026,
                BillingMonth = 8,
                PreviousReading = 0,
                CurrentReading = 10,
                ReadAtUtc = DateTime.UtcNow,
                RecordedByUser = manager
            });

        // Meter readings for Apt 2 (period 2026-08)
        context.UtilityMeterReadings.AddRange(
            new UtilityMeterReading
            {
                Apartment = apt2,
                UtilityType = FeeChargeType.Electricity,
                BillingYear = 2026,
                BillingMonth = 8,
                PreviousReading = 0,
                CurrentReading = 80,
                ReadAtUtc = DateTime.UtcNow,
                RecordedByUser = manager
            },
            new UtilityMeterReading
            {
                Apartment = apt2,
                UtilityType = FeeChargeType.Water,
                BillingYear = 2026,
                BillingMonth = 8,
                PreviousReading = 0,
                CurrentReading = 15,
                ReadAtUtc = DateTime.UtcNow,
                RecordedByUser = manager
            });

        await context.SaveChangesAsync();
        return (manager, building, apt1, apt2);
    }

    [SqlServerFact]
    public async Task TC_SQL_BATCH_01_SqlServer_UniqueIndex_RejectsDuplicateInvoiceInSamePeriod()
    {
        await using var database = SqlServerTestDatabase.Create();
        int apartmentId;
        string managerId;
        await using (var setup = database.CreateContext())
        {
            await setup.Database.MigrateAsync();
            var (manager, _, apt1, _) = await SeedBuildingApartmentsAndTariffsAsync(setup);
            apartmentId = apt1.ApartmentId;
            managerId = manager.Id;
        }

        // 1. Create initial draft invoice
        await using (var context1 = database.CreateContext())
        {
            var service = new BillingService(context1, NullLogger<BillingService>.Instance);
            var result = await service.GenerateDraftAsync(apartmentId, 2026, 8, new DateTime(2026, 9, 15), managerId);
            Assert.True(result.Success);
            Assert.NotNull(result.Invoice);
        }

        // 2. Direct insert attempt to trigger physical SQL Server unique index violation
        await using (var context2 = database.CreateContext())
        {
            var residentId = await context2.ApartmentResidents
                .Where(x => x.ApartmentId == apartmentId && x.IsOwner)
                .Select(x => x.ResidentId)
                .FirstAsync();

            var duplicate = new ApartmentInvoice
            {
                InvoiceCode = $"INV-DUP-{Guid.NewGuid():N}"[..40],
                ApartmentId = apartmentId,
                ResidentId = residentId,
                BillingYear = 2026,
                BillingMonth = 8,
                DueDate = new DateTime(2026, 9, 15),
                ApartmentArea = 50m,
                TotalAmount = 150000m,
                Status = ApartmentInvoiceStatus.Draft,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = managerId
            };

            context2.ApartmentInvoices.Add(duplicate);
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context2.SaveChangesAsync());
            Assert.Contains("IX_ApartmentInvoices_ApartmentId_BillingYear_BillingMonth", ex.InnerException?.Message ?? ex.Message);
        }

        // 3. Verify exactly 1 invoice remains in database
        await using (var verifyContext = database.CreateContext())
        {
            var count = await verifyContext.ApartmentInvoices.CountAsync(x =>
                x.ApartmentId == apartmentId && x.BillingYear == 2026 && x.BillingMonth == 8);
            Assert.Equal(1, count);
        }
    }

    /// <summary>
    /// TC_SQL_BATCH_02: Kiểm chứng race condition khi 2 luồng đồng thời gọi GenerateBuildingDraftsAsync trên SQL Server thật.
    /// Sử dụng TaskCompletionSource barrier để đồng bộ hóa 2 task khởi động cùng một thời điểm.
    /// Xác minh:
    /// - Đúng 2 hóa đơn được tạo trong DB (1 hóa đơn cho mỗi căn).
    /// - Không có hóa đơn trùng lặp nào vi phạm unique index lọt vào DB.
    /// - Cả 2 luồng đều không báo lỗi (FailedCount == 0); các căn hộ được phân loại vào Created hoặc AlreadyExists.
    /// - Tổng CreatedCount + AlreadyExistsCount giữa 2 luồng đúng bằng 4 (2 căn hộ x 2 luồng).
    /// </summary>
    [SqlServerFact]
    public async Task TC_SQL_BATCH_02_SqlServer_ConcurrentBatchGeneration_OnlyOneInvoiceCreatedAndNoCorruptState()
    {
        await using var database = SqlServerTestDatabase.Create();
        int buildingId;
        int apartmentId;
        string managerId;
        await using (var setup = database.CreateContext())
        {
            await setup.Database.MigrateAsync();
            var (manager, building, apt1, _) = await SeedBuildingApartmentsAndTariffsAsync(setup);
            buildingId = building.BuildingId;
            apartmentId = apt1.ApartmentId;
            managerId = manager.Id;
        }

        // Đồng bộ hóa thời điểm khởi động của 2 luồng cạnh tranh
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ready = 0;

        async Task<BatchBillingOperationResult> RunBatchAsync()
        {
            await using var context = database.CreateContext();
            var service = new BillingService(context, NullLogger<BillingService>.Instance);
            if (Interlocked.Increment(ref ready) == 2)
            {
                gate.TrySetResult();
            }
            await gate.Task;
            return await service.GenerateBuildingDraftsAsync(buildingId, 2026, 8, new DateTime(2026, 9, 15), managerId);
        }

        var taskA = RunBatchAsync();
        var taskB = RunBatchAsync();

        var results = await Task.WhenAll(taskA, taskB);
        var resultA = results[0];
        var resultB = results[1];

        Assert.True(resultA.Success);
        Assert.True(resultB.Success);

        // Verify across both results, exactly 2 apartments exist and exactly 2 total invoices created in DB
        await using (var verifyContext = database.CreateContext())
        {
            var allInvoices = await verifyContext.ApartmentInvoices.ToListAsync();
            Assert.Equal(2, allInvoices.Count);
            Assert.Single(allInvoices, x => x.ApartmentId == apartmentId && x.BillingYear == 2026 && x.BillingMonth == 8);
        }

        // Neither thread should report any Failed item
        Assert.Equal(0, resultA.FailedCount);
        Assert.Equal(0, resultB.FailedCount);

        // Sum of CreatedCount + AlreadyExistsCount for both threads must equal total apartments scanned (2 * 2 = 4)
        Assert.Equal(4, resultA.CreatedCount + resultA.AlreadyExistsCount + resultB.CreatedCount + resultB.AlreadyExistsCount);
    }

    /// <summary>
    /// TC_SQL_BATCH_03: Kiểm chứng tính nguyên tử (Atomicity) và Transaction Rollback ở tầng EF Core + SQL Server.
    /// LƯU Ý PHẠM VI:
    /// - Test này thực thi ở tầng DbContext / SQL Server thực tế (không đi qua BillingService).
    /// - Mục đích là chứng minh: Khi lưu một ApartmentInvoice có dòng con ApartmentInvoiceLine vi phạm
    ///   Check Constraint của SQL Server ("CK_ApartmentInvoiceLines_Readings"), SQL Server sẽ rollback
    ///   toàn bộ transaction của SaveChanges, không để lại Header hóa đơn mồ côi hay Line con mồ côi.
    /// - Ràng buộc CK_ApartmentInvoiceLines_Readings được định nghĩa trong migration 20261009083116_ApartmentBilling.
    /// </summary>
    [SqlServerFact]
    public async Task TC_SQL_BATCH_03_SqlServer_EfCoreTransactionAtomicity_RollbackOnInvoiceLineFailureLeavesNoOrphans()
    {
        await using var database = SqlServerTestDatabase.Create();
        int apartmentId;
        string managerId;
        await using (var setup = database.CreateContext())
        {
            await setup.Database.MigrateAsync();
            var (manager, _, apt1, _) = await SeedBuildingApartmentsAndTariffsAsync(setup);
            apartmentId = apt1.ApartmentId;
            managerId = manager.Id;
        }

        await using (var context = database.CreateContext())
        {
            var residentId = await context.ApartmentResidents
                .Where(x => x.ApartmentId == apartmentId && x.IsOwner)
                .Select(x => x.ResidentId)
                .FirstAsync();

            var tariff = await context.FeeTariffs.FirstAsync(x => x.ChargeType == FeeChargeType.Electricity);
            var reading = await context.UtilityMeterReadings.FirstAsync(x => x.ApartmentId == apartmentId && x.UtilityType == FeeChargeType.Electricity);

            // Construct invoice with a child line that deliberately violates SQL Server check constraint
            // CK_ApartmentInvoiceLines_Readings: CurrentReading >= PreviousReading
            var invoice = new ApartmentInvoice
            {
                InvoiceCode = $"INV-FAIL-{Guid.NewGuid():N}"[..40],
                ApartmentId = apartmentId,
                ResidentId = residentId,
                BillingYear = 2026,
                BillingMonth = 9,
                DueDate = new DateTime(2026, 10, 15),
                ApartmentArea = 50m,
                TotalAmount = 50000m,
                Status = ApartmentInvoiceStatus.Draft,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = managerId,
                Lines =
                [
                    new ApartmentInvoiceLine
                    {
                        ChargeType = FeeChargeType.Electricity,
                        Description = "Test invalid readings check constraint",
                        Quantity = 10,
                        UnitRate = 5000,
                        Amount = 50000,
                        PreviousReading = 100, // Invalid: Previous > Current
                        CurrentReading = 50,
                        SourceTariffId = tariff.FeeTariffId,
                        SourceMeterReadingId = reading.UtilityMeterReadingId
                    }
                ]
            };

            context.ApartmentInvoices.Add(invoice);

            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            Assert.Contains("CK_ApartmentInvoiceLines_Readings", ex.InnerException?.Message ?? ex.Message);
        }

        // Verify SQL Server rolled back the entire transaction: No orphan invoice and no orphan lines exist
        await using (var verifyContext = database.CreateContext())
        {
            Assert.Empty(await verifyContext.ApartmentInvoices.Where(x => x.BillingMonth == 9).ToListAsync());
            Assert.Empty(await verifyContext.ApartmentInvoiceLines.ToListAsync());
        }
    }

    /// <summary>
    /// TC_SQL_BATCH_04: Kiểm chứng tính cô lập khi gặp lỗi lưu trữ cơ sở dữ liệu (Persistence Failure Isolation).
    /// Kịch bản:
    /// - Căn hộ 101 gặp lỗi lưu trữ vật lý (DbUpdateException do SQL Server Trigger chủ động RAISERROR khi chèn ApartmentId = 101).
    /// - Căn hộ 102 hợp lệ và phải được tạo hóa đơn thành công.
    /// - Kết quả:
    ///   + Apartment 101: BatchBillingItemStatus.Failed
    ///   + Apartment 102: BatchBillingItemStatus.Created
    /// - Database SQL Server chỉ lưu duy nhất hóa đơn của Apartment 102.
    /// - Không có hóa đơn hay hóa đơn chi tiết mồ côi nào của Apartment 101 lọt vào DB.
    /// - Batch không bị ngắt quãng sau sự cố lưu trữ của Apartment 101.
    /// </summary>
    [SqlServerFact]
    public async Task TC_SQL_BATCH_04_SqlServer_PersistenceFailureIsolation_AllowsSubsequentApartmentToSucceed()
    {
        await using var database = SqlServerTestDatabase.Create();
        int buildingId;
        int apt1Id;
        int apt2Id;
        string managerId;
        await using (var setup = database.CreateContext())
        {
            await setup.Database.MigrateAsync();
            var (manager, building, apt1, apt2) = await SeedBuildingApartmentsAndTariffsAsync(setup);
            buildingId = building.BuildingId;
            apt1Id = apt1.ApartmentId;
            apt2Id = apt2.ApartmentId;
            managerId = manager.Id;

            // Cài đặt trigger có phạm vi hẹp trực tiếp trên test database:
            // Chỉ từ chối lệnh INSERT vào ApartmentInvoices khi ApartmentId khớp với apt1Id.
            // Điều này đảm bảo gây ra DbUpdateException thực sự từ SQL Server engine mà không ảnh hưởng apt2Id.
            var triggerSql = $"""
                CREATE OR ALTER TRIGGER TR_Test_SimulatePersistenceFailure_Apt1
                ON [ApartmentInvoices]
                AFTER INSERT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM inserted WHERE [ApartmentId] = {apt1Id})
                    BEGIN
                        RAISERROR(N'Mô phỏng lỗi lưu trữ SQL Server vật lý khi chèn hóa đơn cho căn hộ mục tiêu.', 16, 1);
                        ROLLBACK TRANSACTION;
                    END
                END;
                """;
            await setup.Database.ExecuteSqlRawAsync(triggerSql);
        }

        await using (var context = database.CreateContext())
        {
            var service = new BillingService(context, NullLogger<BillingService>.Instance);
            var result = await service.GenerateBuildingDraftsAsync(buildingId, 2026, 8, new DateTime(2026, 9, 15), managerId);

            Assert.True(result.Success);
            Assert.Equal(2, result.TotalScanned);
            Assert.Equal(1, result.CreatedCount);
            Assert.Equal(0, result.SkippedCount);
            Assert.Equal(1, result.FailedCount);

            // Apt 1: Bị lỗi lưu trữ vật lý do SQL Server từ chối -> Phân loại chính xác là Failed
            var apt1Item = result.Items.Single(x => x.ApartmentId == apt1Id);
            Assert.Equal(BatchBillingItemStatus.Failed, apt1Item.Status);
            Assert.Contains("lỗi cơ sở dữ liệu", apt1Item.Message);

            // Apt 2: Hợp lệ -> Được tạo mới bình thường trong cùng batch
            var apt2Item = result.Items.Single(x => x.ApartmentId == apt2Id);
            Assert.Equal(BatchBillingItemStatus.Created, apt2Item.Status);
            Assert.NotNull(apt2Item.InvoiceId);
        }

        // Verify in SQL Server that Apt 2 invoice is committed, Apt 1 has no invoice or orphan lines
        await using (var verifyContext = database.CreateContext())
        {
            Assert.False(await verifyContext.ApartmentInvoices.AnyAsync(x => x.ApartmentId == apt1Id));
            Assert.True(await verifyContext.ApartmentInvoices.AnyAsync(x => x.ApartmentId == apt2Id));

            // Đảm bảo không có dòng chi tiết mồ côi nào của Apt 1 còn sót lại
            var apt1InvoiceIds = await verifyContext.ApartmentInvoices
                .Where(x => x.ApartmentId == apt1Id)
                .Select(x => x.ApartmentInvoiceId)
                .ToListAsync();
            Assert.Empty(apt1InvoiceIds);
            Assert.False(await verifyContext.ApartmentInvoiceLines.AnyAsync(x => apt1InvoiceIds.Contains(x.ApartmentInvoiceId)));
        }
    }

    [SqlServerFact]
    public async Task TC_SQL_BATCH_05_SqlServer_ExistingVoidInvoice_IsPreservedAndClassifiedAsAlreadyExists()
    {
        await using var database = SqlServerTestDatabase.Create();
        int buildingId;
        int apt1Id;
        string managerId;
        int voidInvoiceId;
        await using (var setup = database.CreateContext())
        {
            await setup.Database.MigrateAsync();
            var (manager, building, apt1, _) = await SeedBuildingApartmentsAndTariffsAsync(setup);
            buildingId = building.BuildingId;
            apt1Id = apt1.ApartmentId;
            managerId = manager.Id;

            var residentId = await setup.ApartmentResidents
                .Where(x => x.ApartmentId == apt1Id && x.IsOwner)
                .Select(x => x.ResidentId)
                .FirstAsync();

            var voidInvoice = new ApartmentInvoice
            {
                InvoiceCode = "SQL-INV-VOID-001",
                ApartmentId = apt1Id,
                ResidentId = residentId,
                BillingYear = 2026,
                BillingMonth = 8,
                DueDate = new DateTime(2026, 9, 15),
                ApartmentArea = 50m,
                TotalAmount = 250000m,
                Status = ApartmentInvoiceStatus.Void,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = managerId
            };
            setup.ApartmentInvoices.Add(voidInvoice);
            await setup.SaveChangesAsync();
            voidInvoiceId = voidInvoice.ApartmentInvoiceId;
        }

        await using (var context = database.CreateContext())
        {
            var service = new BillingService(context, NullLogger<BillingService>.Instance);
            var result = await service.GenerateBuildingDraftsAsync(buildingId, 2026, 8, new DateTime(2026, 9, 15), managerId);

            Assert.True(result.Success);
            var item = result.Items.Single(x => x.ApartmentId == apt1Id);
            Assert.Equal(BatchBillingItemStatus.AlreadyExists, item.Status);
            Assert.Equal(voidInvoiceId, item.InvoiceId);
            Assert.Equal("SQL-INV-VOID-001", item.InvoiceCode);
        }

        // Verify in SQL Server that status is still Void and data is completely unchanged
        await using (var verifyContext = database.CreateContext())
        {
            var invoiceInDb = await verifyContext.ApartmentInvoices.SingleAsync(x => x.ApartmentInvoiceId == voidInvoiceId);
            Assert.Equal(ApartmentInvoiceStatus.Void, invoiceInDb.Status);
            Assert.Equal("SQL-INV-VOID-001", invoiceInDb.InvoiceCode);
            Assert.Equal(250000m, invoiceInDb.TotalAmount);
        }
    }
}

