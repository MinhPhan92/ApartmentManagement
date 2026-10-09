using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApartmentManagement.Migrations
{
    /// <inheritdoc />
    public partial class SimulatedPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentTransactions",
                columns: table => new
                {
                    PaymentTransactionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApartmentInvoiceId = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTransactions", x => x.PaymentTransactionId);
                    table.CheckConstraint("CK_PaymentTransactions_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_PaymentTransactions_Status", "([Status] = N'Pending' AND [ConfirmedAtUtc] IS NULL AND [ConfirmedByUserId] IS NULL) OR ([Status] = N'Confirmed' AND [ConfirmedAtUtc] IS NOT NULL AND [ConfirmedByUserId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_ApartmentInvoices_ApartmentInvoiceId",
                        column: x => x.ApartmentInvoiceId,
                        principalTable: "ApartmentInvoices",
                        principalColumn: "ApartmentInvoiceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_AspNetUsers_ConfirmedByUserId",
                        column: x => x.ConfirmedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_ApartmentInvoiceId",
                table: "PaymentTransactions",
                column: "ApartmentInvoiceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_ConfirmedByUserId",
                table: "PaymentTransactions",
                column: "ConfirmedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_Reference",
                table: "PaymentTransactions",
                column: "Reference",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentTransactions");
        }
    }
}
