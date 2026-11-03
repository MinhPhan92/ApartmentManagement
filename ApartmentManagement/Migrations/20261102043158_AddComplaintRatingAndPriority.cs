using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApartmentManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddComplaintRatingAndPriority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Priority",
                table: "Complaints",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Bình thường");

            migrationBuilder.AddColumn<DateTime>(
                name: "RatedAt",
                table: "Complaints",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Rating",
                table: "Complaints",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResidentFeedback",
                table: "Complaints",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Complaints_Priority",
                table: "Complaints",
                sql: "[Priority] IN (N'Thấp', N'Bình thường', N'Cao', N'Khẩn cấp')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Complaints_Rating",
                table: "Complaints",
                sql: "[Rating] IS NULL OR ([Rating] >= 1 AND [Rating] <= 5)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Complaints_Priority",
                table: "Complaints");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Complaints_Rating",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "RatedAt",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "Rating",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "ResidentFeedback",
                table: "Complaints");
        }
    }
}
