using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApartmentManagement.Migrations
{
    /// <inheritdoc />
    public partial class ApartmentBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApartmentInvoices",
                columns: table => new
                {
                    ApartmentInvoiceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ApartmentId = table.Column<int>(type: "int", nullable: false),
                    ResidentId = table.Column<int>(type: "int", nullable: false),
                    BillingYear = table.Column<int>(type: "int", nullable: false),
                    BillingMonth = table.Column<int>(type: "int", nullable: false),
                    DueDate = table.Column<DateTime>(type: "date", nullable: false),
                    ApartmentArea = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IssuedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApartmentInvoices", x => x.ApartmentInvoiceId);
                    table.CheckConstraint("CK_ApartmentInvoices_Amounts", "[ApartmentArea] > 0 AND [TotalAmount] >= 0");
                    table.CheckConstraint("CK_ApartmentInvoices_IssueMetadata", "([Status] = N'Draft' AND [IssuedAtUtc] IS NULL AND [IssuedByUserId] IS NULL) OR ([Status] = N'Issued' AND [IssuedAtUtc] IS NOT NULL AND [IssuedByUserId] IS NOT NULL) OR [Status] = N'Void'");
                    table.CheckConstraint("CK_ApartmentInvoices_Period", "[BillingYear] BETWEEN 2000 AND 2200 AND [BillingMonth] BETWEEN 1 AND 12");
                    table.CheckConstraint("CK_ApartmentInvoices_Status", "[Status] IN (N'Draft', N'Issued', N'Void')");
                    table.ForeignKey(
                        name: "FK_ApartmentInvoices_Apartments_ApartmentId",
                        column: x => x.ApartmentId,
                        principalTable: "Apartments",
                        principalColumn: "ApartmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApartmentInvoices_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApartmentInvoices_AspNetUsers_IssuedByUserId",
                        column: x => x.IssuedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApartmentInvoices_Residents_ResidentId",
                        column: x => x.ResidentId,
                        principalTable: "Residents",
                        principalColumn: "ResidentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FeeTariffs",
                columns: table => new
                {
                    FeeTariffId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BuildingId = table.Column<int>(type: "int", nullable: false),
                    ChargeType = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "date", nullable: true),
                    MonthlyRatePerSquareMeter = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeTariffs", x => x.FeeTariffId);
                    table.CheckConstraint("CK_FeeTariffs_ChargeType", "[ChargeType] IN (N'Management', N'Electricity', N'Water')");
                    table.CheckConstraint("CK_FeeTariffs_DateRange", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                    table.CheckConstraint("CK_FeeTariffs_ManagementRate", "[MonthlyRatePerSquareMeter] >= 0");
                    table.ForeignKey(
                        name: "FK_FeeTariffs_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FeeTariffs_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "BuildingId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UtilityMeterReadings",
                columns: table => new
                {
                    UtilityMeterReadingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApartmentId = table.Column<int>(type: "int", nullable: false),
                    UtilityType = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    BillingYear = table.Column<int>(type: "int", nullable: false),
                    BillingMonth = table.Column<int>(type: "int", nullable: false),
                    PreviousReading = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CurrentReading = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecordedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UtilityMeterReadings", x => x.UtilityMeterReadingId);
                    table.CheckConstraint("CK_UtilityMeterReadings_Period", "[BillingYear] BETWEEN 2000 AND 2200 AND [BillingMonth] BETWEEN 1 AND 12");
                    table.CheckConstraint("CK_UtilityMeterReadings_UtilityType", "[UtilityType] IN (N'Electricity', N'Water')");
                    table.CheckConstraint("CK_UtilityMeterReadings_Values", "[PreviousReading] >= 0 AND [CurrentReading] >= [PreviousReading]");
                    table.ForeignKey(
                        name: "FK_UtilityMeterReadings_Apartments_ApartmentId",
                        column: x => x.ApartmentId,
                        principalTable: "Apartments",
                        principalColumn: "ApartmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UtilityMeterReadings_AspNetUsers_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FeeTariffTiers",
                columns: table => new
                {
                    FeeTariffTierId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FeeTariffId = table.Column<int>(type: "int", nullable: false),
                    TierOrder = table.Column<int>(type: "int", nullable: false),
                    UpperConsumption = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    UnitRate = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeTariffTiers", x => x.FeeTariffTierId);
                    table.CheckConstraint("CK_FeeTariffTiers_Values", "[TierOrder] > 0 AND ([UpperConsumption] IS NULL OR [UpperConsumption] > 0) AND [UnitRate] >= 0");
                    table.ForeignKey(
                        name: "FK_FeeTariffTiers_FeeTariffs_FeeTariffId",
                        column: x => x.FeeTariffId,
                        principalTable: "FeeTariffs",
                        principalColumn: "FeeTariffId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApartmentInvoiceLines",
                columns: table => new
                {
                    ApartmentInvoiceLineId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApartmentInvoiceId = table.Column<int>(type: "int", nullable: false),
                    ChargeType = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitRate = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PreviousReading = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    CurrentReading = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    RateBreakdown = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SourceTariffId = table.Column<int>(type: "int", nullable: true),
                    SourceMeterReadingId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApartmentInvoiceLines", x => x.ApartmentInvoiceLineId);
                    table.CheckConstraint("CK_ApartmentInvoiceLines_ChargeType", "[ChargeType] IN (N'Management', N'Electricity', N'Water')");
                    table.CheckConstraint("CK_ApartmentInvoiceLines_Readings", "([ChargeType] = N'Management' AND [PreviousReading] IS NULL AND [CurrentReading] IS NULL AND [SourceMeterReadingId] IS NULL) OR ([ChargeType] IN (N'Electricity', N'Water') AND [PreviousReading] IS NOT NULL AND [CurrentReading] IS NOT NULL AND [CurrentReading] >= [PreviousReading] AND [SourceMeterReadingId] IS NOT NULL)");
                    table.CheckConstraint("CK_ApartmentInvoiceLines_Values", "[Quantity] >= 0 AND [UnitRate] >= 0 AND [Amount] >= 0");
                    table.ForeignKey(
                        name: "FK_ApartmentInvoiceLines_ApartmentInvoices_ApartmentInvoiceId",
                        column: x => x.ApartmentInvoiceId,
                        principalTable: "ApartmentInvoices",
                        principalColumn: "ApartmentInvoiceId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ApartmentInvoiceLines_FeeTariffs_SourceTariffId",
                        column: x => x.SourceTariffId,
                        principalTable: "FeeTariffs",
                        principalColumn: "FeeTariffId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApartmentInvoiceLines_UtilityMeterReadings_SourceMeterReadingId",
                        column: x => x.SourceMeterReadingId,
                        principalTable: "UtilityMeterReadings",
                        principalColumn: "UtilityMeterReadingId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentInvoiceLines_ApartmentInvoiceId",
                table: "ApartmentInvoiceLines",
                column: "ApartmentInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentInvoiceLines_SourceMeterReadingId",
                table: "ApartmentInvoiceLines",
                column: "SourceMeterReadingId");

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentInvoiceLines_SourceTariffId",
                table: "ApartmentInvoiceLines",
                column: "SourceTariffId");

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentInvoices_ApartmentId_BillingYear_BillingMonth",
                table: "ApartmentInvoices",
                columns: new[] { "ApartmentId", "BillingYear", "BillingMonth" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentInvoices_CreatedByUserId",
                table: "ApartmentInvoices",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentInvoices_InvoiceCode",
                table: "ApartmentInvoices",
                column: "InvoiceCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentInvoices_IssuedByUserId",
                table: "ApartmentInvoices",
                column: "IssuedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentInvoices_ResidentId_Status_BillingYear_BillingMonth",
                table: "ApartmentInvoices",
                columns: new[] { "ResidentId", "Status", "BillingYear", "BillingMonth" });

            migrationBuilder.CreateIndex(
                name: "IX_FeeTariffs_BuildingId_ChargeType_EffectiveFrom",
                table: "FeeTariffs",
                columns: new[] { "BuildingId", "ChargeType", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeeTariffs_CreatedByUserId",
                table: "FeeTariffs",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeTariffTiers_FeeTariffId_TierOrder",
                table: "FeeTariffTiers",
                columns: new[] { "FeeTariffId", "TierOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UtilityMeterReadings_ApartmentId_UtilityType_BillingYear_BillingMonth",
                table: "UtilityMeterReadings",
                columns: new[] { "ApartmentId", "UtilityType", "BillingYear", "BillingMonth" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UtilityMeterReadings_RecordedByUserId",
                table: "UtilityMeterReadings",
                column: "RecordedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApartmentInvoiceLines");

            migrationBuilder.DropTable(
                name: "FeeTariffTiers");

            migrationBuilder.DropTable(
                name: "ApartmentInvoices");

            migrationBuilder.DropTable(
                name: "UtilityMeterReadings");

            migrationBuilder.DropTable(
                name: "FeeTariffs");
        }
    }
}
