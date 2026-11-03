using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApartmentManagement.Migrations
{
    /// <inheritdoc />
    public partial class PaymentMultipleAttemptsAndRejection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT [ApartmentInvoiceId]
                    FROM [PaymentTransactions]
                    WHERE [Status] = N'Confirmed'
                    GROUP BY [ApartmentInvoiceId]
                    HAVING COUNT(*) > 1
                )
                    THROW 51000, N'Không thể migration: có hóa đơn đang có nhiều hơn một giao dịch đã xác nhận.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM [PaymentTransactions]
                    WHERE [Status] NOT IN (N'Pending', N'Confirmed', N'Rejected')
                       OR ([Status] = N'Pending' AND ([ConfirmedAtUtc] IS NOT NULL OR [ConfirmedByUserId] IS NOT NULL))
                       OR ([Status] = N'Confirmed' AND ([ConfirmedAtUtc] IS NULL OR [ConfirmedByUserId] IS NULL))
                )
                    THROW 51000, N'Không thể migration: có giao dịch thanh toán không hợp lệ với ràng buộc trạng thái.', 1;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentTransactions_Status",
                table: "PaymentTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PaymentTransactions_ApartmentInvoiceId",
                table: "PaymentTransactions");

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedAtUtc",
                table: "PaymentTransactions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectedByUserId",
                table: "PaymentTransactions",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "PaymentTransactions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_ApartmentInvoiceId",
                table: "PaymentTransactions",
                column: "ApartmentInvoiceId");

            migrationBuilder.CreateIndex(
                name: "UX_PaymentTransactions_ApartmentInvoiceId_Confirmed",
                table: "PaymentTransactions",
                column: "ApartmentInvoiceId",
                unique: true,
                filter: "[Status] = N'Confirmed'");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_RejectedByUserId",
                table: "PaymentTransactions",
                column: "RejectedByUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentTransactions_Status",
                table: "PaymentTransactions",
                sql: "([Status] = N'Pending' AND [ConfirmedAtUtc] IS NULL AND [ConfirmedByUserId] IS NULL AND [RejectedAtUtc] IS NULL AND [RejectedByUserId] IS NULL AND [RejectionReason] IS NULL) OR ([Status] = N'Confirmed' AND [ConfirmedAtUtc] IS NOT NULL AND [ConfirmedByUserId] IS NOT NULL AND [RejectedAtUtc] IS NULL AND [RejectedByUserId] IS NULL AND [RejectionReason] IS NULL) OR ([Status] = N'Rejected' AND [ConfirmedAtUtc] IS NULL AND [ConfirmedByUserId] IS NULL AND [RejectedAtUtc] IS NOT NULL AND [RejectedByUserId] IS NOT NULL AND [RejectionReason] IS NOT NULL AND LTRIM(RTRIM([RejectionReason])) <> N'')");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentTransactions_AspNetUsers_RejectedByUserId",
                table: "PaymentTransactions",
                column: "RejectedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1 FROM [PaymentTransactions] WHERE [Status] = N'Rejected'
                ) OR EXISTS (
                    SELECT [ApartmentInvoiceId]
                    FROM [PaymentTransactions]
                    GROUP BY [ApartmentInvoiceId]
                    HAVING COUNT(*) > 1
                )
                    THROW 51000, N'Không thể rollback: dữ liệu đã có giao dịch bị từ chối hoặc nhiều giao dịch cho cùng một hóa đơn.', 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentTransactions_AspNetUsers_RejectedByUserId",
                table: "PaymentTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentTransactions_Status",
                table: "PaymentTransactions");

            migrationBuilder.DropIndex(
                name: "UX_PaymentTransactions_ApartmentInvoiceId_Confirmed",
                table: "PaymentTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PaymentTransactions_ApartmentInvoiceId",
                table: "PaymentTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PaymentTransactions_RejectedByUserId",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "RejectedAtUtc",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "RejectedByUserId",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "PaymentTransactions");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_ApartmentInvoiceId",
                table: "PaymentTransactions",
                column: "ApartmentInvoiceId",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentTransactions_Status",
                table: "PaymentTransactions",
                sql: "([Status] = N'Pending' AND [ConfirmedAtUtc] IS NULL AND [ConfirmedByUserId] IS NULL) OR ([Status] = N'Confirmed' AND [ConfirmedAtUtc] IS NOT NULL AND [ConfirmedByUserId] IS NOT NULL)");
        }
    }
}

