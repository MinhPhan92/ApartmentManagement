using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApartmentManagement.Migrations
{
    /// <inheritdoc />
    public partial class ApartmentContracts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApartmentContracts",
                columns: table => new
                {
                    ApartmentContractId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ContractCodeNormalized = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, computedColumnSql: "UPPER(LTRIM(RTRIM([ContractCode])))", stored: true),
                    ApartmentId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "date", nullable: false),
                    EndDate = table.Column<DateTime>(type: "date", nullable: true),
                    MonthlyRent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DepositAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApartmentContracts", x => x.ApartmentContractId);
                    table.CheckConstraint("CK_ApartmentContracts_ContractCode", "LEN(LTRIM(RTRIM([ContractCode]))) > 0");
                    table.CheckConstraint("CK_ApartmentContracts_DateRange", "[EndDate] IS NULL OR [EndDate] >= [StartDate]");
                    table.CheckConstraint("CK_ApartmentContracts_DepositAmount", "[DepositAmount] >= 0");
                    table.CheckConstraint("CK_ApartmentContracts_MonthlyRent", "[MonthlyRent] > 0");
                    table.CheckConstraint("CK_ApartmentContracts_Status", "[Status] IN (N'Draft', N'Active', N'Completed', N'Terminated', N'Cancelled')");
                    table.ForeignKey(
                        name: "FK_ApartmentContracts_Apartments_ApartmentId",
                        column: x => x.ApartmentId,
                        principalTable: "Apartments",
                        principalColumn: "ApartmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApartmentContracts_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApartmentContracts_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContractParties",
                columns: table => new
                {
                    ApartmentContractId = table.Column<int>(type: "int", nullable: false),
                    ResidentId = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractParties", x => new { x.ApartmentContractId, x.ResidentId });
                    table.CheckConstraint("CK_ContractParties_Role", "[Role] IN (N'Lessor', N'Lessee')");
                    table.ForeignKey(
                        name: "FK_ContractParties_ApartmentContracts_ApartmentContractId",
                        column: x => x.ApartmentContractId,
                        principalTable: "ApartmentContracts",
                        principalColumn: "ApartmentContractId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContractParties_Residents_ResidentId",
                        column: x => x.ResidentId,
                        principalTable: "Residents",
                        principalColumn: "ResidentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContractStatusHistory",
                columns: table => new
                {
                    ContractStatusHistoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApartmentContractId = table.Column<int>(type: "int", nullable: false),
                    PreviousStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    NewStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChangedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractStatusHistory", x => x.ContractStatusHistoryId);
                    table.CheckConstraint("CK_ContractStatusHistory_NewStatus", "[NewStatus] IN (N'Draft', N'Active', N'Completed', N'Terminated', N'Cancelled')");
                    table.CheckConstraint("CK_ContractStatusHistory_PreviousStatus", "[PreviousStatus] IS NULL OR [PreviousStatus] IN (N'Draft', N'Active', N'Completed', N'Terminated', N'Cancelled')");
                    table.ForeignKey(
                        name: "FK_ContractStatusHistory_ApartmentContracts_ApartmentContractId",
                        column: x => x.ApartmentContractId,
                        principalTable: "ApartmentContracts",
                        principalColumn: "ApartmentContractId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContractStatusHistory_AspNetUsers_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentContracts_ApartmentId",
                table: "ApartmentContracts",
                column: "ApartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentContracts_CreatedByUserId",
                table: "ApartmentContracts",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentContracts_UpdatedByUserId",
                table: "ApartmentContracts",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_ApartmentContracts_ContractCodeNormalized",
                table: "ApartmentContracts",
                column: "ContractCodeNormalized",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractParties_ResidentId",
                table: "ContractParties",
                column: "ResidentId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractStatusHistory_ApartmentContractId",
                table: "ContractStatusHistory",
                column: "ApartmentContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractStatusHistory_ChangedByUserId",
                table: "ContractStatusHistory",
                column: "ChangedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractParties");

            migrationBuilder.DropTable(
                name: "ContractStatusHistory");

            migrationBuilder.DropTable(
                name: "ApartmentContracts");
        }
    }
}
