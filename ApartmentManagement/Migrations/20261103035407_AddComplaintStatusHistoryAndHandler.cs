using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApartmentManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddComplaintStatusHistoryAndHandler : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HandlerUserId",
                table: "Complaints",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ComplaintStatusHistories",
                columns: table => new
                {
                    ComplaintStatusHistoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ComplaintId = table.Column<int>(type: "int", nullable: false),
                    PreviousStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NewStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ChangedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsInternal = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplaintStatusHistories", x => x.ComplaintStatusHistoryId);
                    table.ForeignKey(
                        name: "FK_ComplaintStatusHistories_AspNetUsers_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComplaintStatusHistories_Complaints_ComplaintId",
                        column: x => x.ComplaintId,
                        principalTable: "Complaints",
                        principalColumn: "ComplaintId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_HandlerUserId",
                table: "Complaints",
                column: "HandlerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplaintStatusHistories_ChangedByUserId",
                table: "ComplaintStatusHistories",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplaintStatusHistories_ComplaintId_ChangedAt",
                table: "ComplaintStatusHistories",
                columns: new[] { "ComplaintId", "ChangedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Complaints_AspNetUsers_HandlerUserId",
                table: "Complaints",
                column: "HandlerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Complaints_AspNetUsers_HandlerUserId",
                table: "Complaints");

            migrationBuilder.DropTable(
                name: "ComplaintStatusHistories");

            migrationBuilder.DropIndex(
                name: "IX_Complaints_HandlerUserId",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "HandlerUserId",
                table: "Complaints");
        }
    }
}
