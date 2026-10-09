using ApartmentManagement.Models;
using ApartmentManagement.Common.Validation;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.Data
{
    public class ApplicationDbContext
        : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Building> Buildings => Set<Building>();

        public DbSet<Apartment> Apartments => Set<Apartment>();

        public DbSet<Resident> Residents => Set<Resident>();

        public DbSet<ApartmentResident> ApartmentResidents
            => Set<ApartmentResident>();

        public DbSet<ApartmentContract> ApartmentContracts
            => Set<ApartmentContract>();

        public DbSet<ContractParty> ContractParties
            => Set<ContractParty>();

        public DbSet<ContractStatusHistory> ContractStatusHistory
            => Set<ContractStatusHistory>();

        public DbSet<IncidentTicket> IncidentTickets => Set<IncidentTicket>();

        public DbSet<IncidentTicketStatusHistory> IncidentTicketStatusHistory
            => Set<IncidentTicketStatusHistory>();

        public DbSet<FeeTariff> FeeTariffs => Set<FeeTariff>();

        public DbSet<FeeTariffTier> FeeTariffTiers => Set<FeeTariffTier>();

        public DbSet<UtilityMeterReading> UtilityMeterReadings => Set<UtilityMeterReading>();

        public DbSet<ApartmentInvoice> ApartmentInvoices => Set<ApartmentInvoice>();

        public DbSet<ApartmentInvoiceLine> ApartmentInvoiceLines => Set<ApartmentInvoiceLine>();

        public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // BUILDING
            builder.Entity<Building>()
                .HasIndex(x => x.BuildingCode)
                .IsUnique();

            builder.Entity<Building>()
                .Property(x => x.BuildingCode)
                .HasMaxLength(50);

            // APARTMENT
            builder.Entity<Apartment>()
                .HasIndex(x => new
                {
                    x.BuildingId,
                    x.ApartmentCode
                })
                .IsUnique();

            builder.Entity<Apartment>()
                .Property(x => x.ApartmentCode)
                .HasMaxLength(50);

            builder.Entity<Apartment>()
                .Property(x => x.Area)
                .HasPrecision(18, 2);

            builder.Entity<Apartment>()
                .HasOne(x => x.Building)
                .WithMany(x => x.Apartments)
                .HasForeignKey(x => x.BuildingId)
                .OnDelete(DeleteBehavior.Restrict);

            // RESIDENT
            builder.Entity<Resident>()
                .HasIndex(x => x.CitizenId)
                .IsUnique();

            builder.Entity<Resident>()
                .Property(x => x.CitizenId)
                .HasMaxLength(ResidentValidation.CitizenIdMaxLength);

            builder.Entity<Resident>().Property(x => x.Gender)
                .HasMaxLength(ResidentValidation.GenderMaxLength);
            builder.Entity<Resident>().Property(x => x.Address)
                .HasMaxLength(ResidentValidation.AddressMaxLength);
            builder.Entity<Resident>().Property(x => x.EmergencyContact)
                .HasMaxLength(ResidentValidation.PhoneMaxLength);
            builder.Entity<Resident>().ToTable(table => table.HasCheckConstraint(
                "CK_Residents_CitizenId_Format",
                "[CitizenId] NOT LIKE '%[^0-9]%' AND LEN([CitizenId]) IN (9, 12)"));
            builder.Entity<Resident>().ToTable(table => table.HasCheckConstraint(
                "CK_Residents_DateOfBirth_Minimum",
                "[DateOfBirth] IS NULL OR [DateOfBirth] >= '1900-01-01'"));

            builder.Entity<ApplicationUser>().Property(x => x.FullName)
                .HasMaxLength(ResidentValidation.FullNameMaxLength);

            builder.Entity<ApartmentResident>().Property(x => x.Relationship)
                .HasMaxLength(ResidentValidation.RelationshipMaxLength);

            builder.Entity<Resident>()
                .HasOne(x => x.User)
                .WithOne(x => x.Resident)
                .HasForeignKey<Resident>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // APARTMENT_RESIDENT
            builder.Entity<ApartmentResident>()
                .HasKey(x => x.ApartmentResidentId);

            builder.Entity<ApartmentResident>()
                .HasIndex(x => new { x.ApartmentId, x.ResidentId })
                .IsUnique()
                .HasFilter("[MoveOutDate] IS NULL")
                .HasDatabaseName("UX_ApartmentResidents_ActiveAssignment");

            builder.Entity<ApartmentResident>().ToTable(table => table.HasCheckConstraint(
                "CK_ApartmentResidents_MoveOutDate",
                "[MoveOutDate] IS NULL OR [MoveOutDate] >= [MoveInDate]"));

            builder.Entity<ApartmentResident>()
                .HasOne(x => x.Apartment)
                .WithMany(x => x.ApartmentResidents)
                .HasForeignKey(x => x.ApartmentId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ApartmentResident>()
                .HasOne(x => x.Resident)
                .WithMany(x => x.ApartmentResidents)
                .HasForeignKey(x => x.ResidentId)
                .OnDelete(DeleteBehavior.Cascade);

            // CONTRACTS
            builder.Entity<ApartmentContract>(entity =>
            {
                entity.Property(x => x.ContractCode)
                    .HasMaxLength(50)
                    .IsRequired();
                entity.Property(x => x.ContractCodeNormalized)
                    .HasMaxLength(50)
                    .HasComputedColumnSql("UPPER(LTRIM(RTRIM([ContractCode])))", stored: true);
                entity.HasIndex(x => x.ContractCodeNormalized)
                    .IsUnique()
                    .HasDatabaseName("UX_ApartmentContracts_ContractCodeNormalized");
                entity.Property(x => x.StartDate).HasColumnType("date");
                entity.Property(x => x.EndDate).HasColumnType("date");
                entity.Property(x => x.MonthlyRent).HasPrecision(18, 2);
                entity.Property(x => x.DepositAmount).HasPrecision(18, 2);
                entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
                entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");
                entity.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");
                entity.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
                entity.Property(x => x.UpdatedByUserId).HasMaxLength(450);
                entity.Property(x => x.RowVersion).IsRowVersion();

                entity.ToTable(table =>
                {
                    table.HasCheckConstraint(
                        "CK_ApartmentContracts_ContractCode",
                        "LEN(LTRIM(RTRIM([ContractCode]))) > 0");
                    table.HasCheckConstraint(
                        "CK_ApartmentContracts_DateRange",
                        "[EndDate] IS NULL OR [EndDate] >= [StartDate]");
                    table.HasCheckConstraint(
                        "CK_ApartmentContracts_MonthlyRent",
                        "[MonthlyRent] > 0");
                    table.HasCheckConstraint(
                        "CK_ApartmentContracts_DepositAmount",
                        "[DepositAmount] >= 0");
                    table.HasCheckConstraint(
                        "CK_ApartmentContracts_Status",
                        "[Status] IN (N'Draft', N'Active', N'Completed', N'Terminated', N'Cancelled')");
                });

                entity.HasOne(x => x.Apartment)
                    .WithMany(x => x.Contracts)
                    .HasForeignKey(x => x.ApartmentId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(x => x.CreatedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.UpdatedByUser)
                    .WithMany()
                    .HasForeignKey(x => x.UpdatedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<ContractParty>(entity =>
            {
                entity.HasKey(x => new { x.ApartmentContractId, x.ResidentId });
                entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(16);
                entity.ToTable(table => table.HasCheckConstraint(
                    "CK_ContractParties_Role",
                    "[Role] IN (N'Lessor', N'Lessee')"));
                entity.HasOne(x => x.ApartmentContract)
                    .WithMany(x => x.Parties)
                    .HasForeignKey(x => x.ApartmentContractId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(x => x.Resident)
                    .WithMany(x => x.ContractParties)
                    .HasForeignKey(x => x.ResidentId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<ContractStatusHistory>(entity =>
            {
                entity.Property(x => x.PreviousStatus).HasConversion<string>().HasMaxLength(32);
                entity.Property(x => x.NewStatus).HasConversion<string>().HasMaxLength(32);
                entity.Property(x => x.ChangedAtUtc).HasColumnType("datetime2");
                entity.Property(x => x.ChangedByUserId).HasMaxLength(450).IsRequired();
                entity.Property(x => x.Reason).HasMaxLength(1000);
                entity.ToTable(table =>
                {
                    table.HasCheckConstraint(
                        "CK_ContractStatusHistory_PreviousStatus",
                        "[PreviousStatus] IS NULL OR [PreviousStatus] IN (N'Draft', N'Active', N'Completed', N'Terminated', N'Cancelled')");
                    table.HasCheckConstraint(
                        "CK_ContractStatusHistory_NewStatus",
                        "[NewStatus] IN (N'Draft', N'Active', N'Completed', N'Terminated', N'Cancelled')");
                });
                entity.HasOne(x => x.ApartmentContract)
                    .WithMany(x => x.StatusHistory)
                    .HasForeignKey(x => x.ApartmentContractId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(x => x.ChangedByUser)
                    .WithMany()
                    .HasForeignKey(x => x.ChangedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // INCIDENT TICKETS
            builder.Entity<IncidentTicket>(entity =>
            {
                entity.Property(x => x.TicketCode).HasMaxLength(24).IsRequired();
                entity.HasIndex(x => x.TicketCode).IsUnique();
                entity.Property(x => x.Category).HasConversion<string>().HasMaxLength(24);
                entity.Property(x => x.Title).HasMaxLength(120).IsRequired();
                entity.Property(x => x.Description).HasMaxLength(4000).IsRequired();
                entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
                entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");
                entity.Property(x => x.AssignedAtUtc).HasColumnType("datetime2");
                entity.Property(x => x.CompletedAtUtc).HasColumnType("datetime2");
                entity.Property(x => x.RatedAtUtc).HasColumnType("datetime2");
                entity.Property(x => x.AssignedToUserId).HasMaxLength(450);
                entity.Property(x => x.ResidentFeedback).HasMaxLength(1000);
                entity.Property(x => x.RowVersion).IsRowVersion();
                entity.HasIndex(x => new { x.Status, x.CreatedAtUtc });
                entity.HasIndex(x => new { x.ResidentId, x.CreatedAtUtc });
                entity.ToTable(table =>
                {
                    table.HasCheckConstraint(
                        "CK_IncidentTickets_Category",
                        "[Category] IN (N'Electricity', N'Water', N'Elevator', N'CommonArea', N'Other')");
                    table.HasCheckConstraint(
                        "CK_IncidentTickets_Status",
                        "[Status] IN (N'Submitted', N'InProgress', N'Completed')");
                    table.HasCheckConstraint(
                        "CK_IncidentTickets_Rating",
                        "[Rating] IS NULL OR [Rating] BETWEEN 1 AND 5");
                    table.HasCheckConstraint(
                        "CK_IncidentTickets_Feedback",
                        "([Rating] IS NULL AND [ResidentFeedback] IS NULL AND [RatedAtUtc] IS NULL) OR " +
                        "([Rating] IS NOT NULL AND [RatedAtUtc] IS NOT NULL)");
                });
                entity.HasOne(x => x.Apartment)
                    .WithMany(x => x.IncidentTickets)
                    .HasForeignKey(x => x.ApartmentId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.Resident)
                    .WithMany(x => x.IncidentTickets)
                    .HasForeignKey(x => x.ResidentId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.AssignedToUser)
                    .WithMany(x => x.AssignedIncidentTickets)
                    .HasForeignKey(x => x.AssignedToUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<IncidentTicketStatusHistory>(entity =>
            {
                entity.Property(x => x.PreviousStatus).HasConversion<string>().HasMaxLength(24);
                entity.Property(x => x.NewStatus).HasConversion<string>().HasMaxLength(24);
                entity.Property(x => x.ChangedAtUtc).HasColumnType("datetime2");
                entity.Property(x => x.ChangedByUserId).HasMaxLength(450).IsRequired();
                entity.ToTable(table =>
                {
                    table.HasCheckConstraint(
                        "CK_IncidentTicketStatusHistory_PreviousStatus",
                        "[PreviousStatus] IS NULL OR [PreviousStatus] IN (N'Submitted', N'InProgress', N'Completed')");
                    table.HasCheckConstraint(
                        "CK_IncidentTicketStatusHistory_NewStatus",
                        "[NewStatus] IN (N'Submitted', N'InProgress', N'Completed')");
                });
                entity.HasOne(x => x.IncidentTicket)
                    .WithMany(x => x.StatusHistory)
                    .HasForeignKey(x => x.IncidentTicketId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(x => x.ChangedByUser)
                    .WithMany()
                    .HasForeignKey(x => x.ChangedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<FeeTariff>(entity =>
            {
                entity.Property(x => x.ChargeType).HasConversion<string>().HasMaxLength(24);
                entity.Property(x => x.EffectiveFrom).HasColumnType("date");
                entity.Property(x => x.EffectiveTo).HasColumnType("date");
                entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");
                entity.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
                entity.Property(x => x.MonthlyRatePerSquareMeter).HasPrecision(18, 4);
                entity.HasIndex(x => new { x.BuildingId, x.ChargeType, x.EffectiveFrom }).IsUnique();
                entity.ToTable(table =>
                {
                    table.HasCheckConstraint(
                        "CK_FeeTariffs_DateRange",
                        "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                    table.HasCheckConstraint(
                        "CK_FeeTariffs_ManagementRate",
                        "[MonthlyRatePerSquareMeter] >= 0");
                    table.HasCheckConstraint(
                        "CK_FeeTariffs_ChargeType",
                        "[ChargeType] IN (N'Management', N'Electricity', N'Water')");
                });
                entity.HasOne(x => x.Building).WithMany(x => x.FeeTariffs)
                    .HasForeignKey(x => x.BuildingId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.CreatedByUser).WithMany()
                    .HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<FeeTariffTier>(entity =>
            {
                entity.Property(x => x.UpperConsumption).HasPrecision(18, 4);
                entity.Property(x => x.UnitRate).HasPrecision(18, 4);
                entity.HasIndex(x => new { x.FeeTariffId, x.TierOrder }).IsUnique();
                entity.ToTable(table => table.HasCheckConstraint(
                    "CK_FeeTariffTiers_Values",
                    "[TierOrder] > 0 AND ([UpperConsumption] IS NULL OR [UpperConsumption] > 0) AND [UnitRate] >= 0"));
                entity.HasOne(x => x.FeeTariff).WithMany(x => x.Tiers)
                    .HasForeignKey(x => x.FeeTariffId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<UtilityMeterReading>(entity =>
            {
                entity.Property(x => x.UtilityType).HasConversion<string>().HasMaxLength(24);
                entity.Property(x => x.PreviousReading).HasPrecision(18, 4);
                entity.Property(x => x.CurrentReading).HasPrecision(18, 4);
                entity.Property(x => x.ReadAtUtc).HasColumnType("datetime2");
                entity.Property(x => x.RecordedByUserId).HasMaxLength(450).IsRequired();
                entity.HasIndex(x => new { x.ApartmentId, x.UtilityType, x.BillingYear, x.BillingMonth }).IsUnique();
                entity.ToTable(table =>
                {
                    table.HasCheckConstraint(
                        "CK_UtilityMeterReadings_Period",
                        "[BillingYear] BETWEEN 2000 AND 2200 AND [BillingMonth] BETWEEN 1 AND 12");
                    table.HasCheckConstraint(
                        "CK_UtilityMeterReadings_Values",
                        "[PreviousReading] >= 0 AND [CurrentReading] >= [PreviousReading]");
                    table.HasCheckConstraint(
                        "CK_UtilityMeterReadings_UtilityType",
                        "[UtilityType] IN (N'Electricity', N'Water')");
                });
                entity.HasOne(x => x.Apartment).WithMany(x => x.UtilityMeterReadings)
                    .HasForeignKey(x => x.ApartmentId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.RecordedByUser).WithMany()
                    .HasForeignKey(x => x.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<ApartmentInvoice>(entity =>
            {
                entity.Property(x => x.InvoiceCode).HasMaxLength(40).IsRequired();
                entity.Property(x => x.DueDate).HasColumnType("date");
                entity.Property(x => x.ApartmentArea).HasPrecision(18, 2);
                entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
                entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
                entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");
                entity.Property(x => x.IssuedAtUtc).HasColumnType("datetime2");
                entity.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
                entity.Property(x => x.IssuedByUserId).HasMaxLength(450);
                entity.Property(x => x.RowVersion).IsRowVersion();
                entity.HasIndex(x => new { x.ApartmentId, x.BillingYear, x.BillingMonth }).IsUnique();
                entity.HasIndex(x => x.InvoiceCode).IsUnique();
                entity.HasIndex(x => new { x.ResidentId, x.Status, x.BillingYear, x.BillingMonth });
                entity.ToTable(table =>
                {
                    table.HasCheckConstraint(
                        "CK_ApartmentInvoices_Period",
                        "[BillingYear] BETWEEN 2000 AND 2200 AND [BillingMonth] BETWEEN 1 AND 12");
                    table.HasCheckConstraint(
                        "CK_ApartmentInvoices_Amounts",
                        "[ApartmentArea] > 0 AND [TotalAmount] >= 0");
                    table.HasCheckConstraint(
                        "CK_ApartmentInvoices_Status",
                        "[Status] IN (N'Draft', N'Issued', N'Void')");
                    table.HasCheckConstraint(
                        "CK_ApartmentInvoices_IssueMetadata",
                        "([Status] = N'Draft' AND [IssuedAtUtc] IS NULL AND [IssuedByUserId] IS NULL) OR " +
                        "([Status] = N'Issued' AND [IssuedAtUtc] IS NOT NULL AND [IssuedByUserId] IS NOT NULL) OR " +
                        "[Status] = N'Void'");
                });
                entity.HasOne(x => x.Apartment).WithMany(x => x.Invoices)
                    .HasForeignKey(x => x.ApartmentId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.Resident).WithMany(x => x.Invoices)
                    .HasForeignKey(x => x.ResidentId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.CreatedByUser).WithMany()
                    .HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.IssuedByUser).WithMany()
                    .HasForeignKey(x => x.IssuedByUserId).OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<ApartmentInvoiceLine>(entity =>
            {
                entity.Property(x => x.ChargeType).HasConversion<string>().HasMaxLength(24);
                entity.Property(x => x.Description).HasMaxLength(160).IsRequired();
                entity.Property(x => x.Quantity).HasPrecision(18, 4);
                entity.Property(x => x.UnitRate).HasPrecision(18, 4);
                entity.Property(x => x.Amount).HasPrecision(18, 2);
                entity.Property(x => x.PreviousReading).HasPrecision(18, 4);
                entity.Property(x => x.CurrentReading).HasPrecision(18, 4);
                entity.Property(x => x.RateBreakdown).HasMaxLength(1000);
                entity.ToTable(table => table.HasCheckConstraint(
                    "CK_ApartmentInvoiceLines_Values",
                    "[Quantity] >= 0 AND [UnitRate] >= 0 AND [Amount] >= 0"));
                entity.ToTable(table =>
                {
                    table.HasCheckConstraint(
                        "CK_ApartmentInvoiceLines_ChargeType",
                        "[ChargeType] IN (N'Management', N'Electricity', N'Water')");
                    table.HasCheckConstraint(
                        "CK_ApartmentInvoiceLines_Readings",
                        "([ChargeType] = N'Management' AND [PreviousReading] IS NULL AND [CurrentReading] IS NULL AND [SourceMeterReadingId] IS NULL) OR " +
                        "([ChargeType] IN (N'Electricity', N'Water') AND [PreviousReading] IS NOT NULL AND [CurrentReading] IS NOT NULL AND [CurrentReading] >= [PreviousReading] AND [SourceMeterReadingId] IS NOT NULL)");
                });
                entity.HasOne(x => x.Invoice).WithMany(x => x.Lines)
                    .HasForeignKey(x => x.ApartmentInvoiceId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(x => x.SourceTariff).WithMany(x => x.InvoiceLines)
                    .HasForeignKey(x => x.SourceTariffId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.SourceMeterReading).WithMany(x => x.InvoiceLines)
                    .HasForeignKey(x => x.SourceMeterReadingId).OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<PaymentTransaction>(entity =>
            {
                entity.Property(x => x.Reference).HasMaxLength(25).IsRequired();
                entity.Property(x => x.Amount).HasPrecision(18, 2);
                entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
                entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");
                entity.Property(x => x.ConfirmedAtUtc).HasColumnType("datetime2");
                entity.Property(x => x.ConfirmedByUserId).HasMaxLength(450);
                entity.Property(x => x.RowVersion).IsRowVersion();
                entity.HasIndex(x => x.ApartmentInvoiceId).IsUnique();
                entity.HasIndex(x => x.Reference).IsUnique();
                entity.ToTable(table =>
                {
                    table.HasCheckConstraint(
                        "CK_PaymentTransactions_Amount",
                        "[Amount] > 0");
                    table.HasCheckConstraint(
                        "CK_PaymentTransactions_Status",
                        "([Status] = N'Pending' AND [ConfirmedAtUtc] IS NULL AND [ConfirmedByUserId] IS NULL) OR " +
                        "([Status] = N'Confirmed' AND [ConfirmedAtUtc] IS NOT NULL AND [ConfirmedByUserId] IS NOT NULL)");
                });
                entity.HasOne(x => x.Invoice).WithMany(x => x.PaymentTransactions)
                    .HasForeignKey(x => x.ApartmentInvoiceId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.ConfirmedByUser).WithMany()
                    .HasForeignKey(x => x.ConfirmedByUserId).OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
