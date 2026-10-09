using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApartmentManagement.Migrations
{
    /// <inheritdoc />
    public partial class IncidentTicketing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IncidentTickets",
                columns: table => new
                {
                    IncidentTicketId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketCode = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    ApartmentId = table.Column<int>(type: "int", nullable: false),
                    ResidentId = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AssignedToUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Rating = table.Column<byte>(type: "tinyint", nullable: true),
                    ResidentFeedback = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentTickets", x => x.IncidentTicketId);
                    table.CheckConstraint("CK_IncidentTickets_Category", "[Category] IN (N'Electricity', N'Water', N'Elevator', N'CommonArea', N'Other')");
                    table.CheckConstraint("CK_IncidentTickets_Feedback", "([Rating] IS NULL AND [ResidentFeedback] IS NULL AND [RatedAtUtc] IS NULL) OR ([Rating] IS NOT NULL AND [RatedAtUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_IncidentTickets_Rating", "[Rating] IS NULL OR [Rating] BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_IncidentTickets_Status", "[Status] IN (N'Submitted', N'InProgress', N'Completed')");
                    table.ForeignKey(
                        name: "FK_IncidentTickets_Apartments_ApartmentId",
                        column: x => x.ApartmentId,
                        principalTable: "Apartments",
                        principalColumn: "ApartmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IncidentTickets_AspNetUsers_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IncidentTickets_Residents_ResidentId",
                        column: x => x.ResidentId,
                        principalTable: "Residents",
                        principalColumn: "ResidentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IncidentTicketStatusHistory",
                columns: table => new
                {
                    IncidentTicketStatusHistoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IncidentTicketId = table.Column<int>(type: "int", nullable: false),
                    PreviousStatus = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: true),
                    NewStatus = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChangedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentTicketStatusHistory", x => x.IncidentTicketStatusHistoryId);
                    table.CheckConstraint("CK_IncidentTicketStatusHistory_NewStatus", "[NewStatus] IN (N'Submitted', N'InProgress', N'Completed')");
                    table.CheckConstraint("CK_IncidentTicketStatusHistory_PreviousStatus", "[PreviousStatus] IS NULL OR [PreviousStatus] IN (N'Submitted', N'InProgress', N'Completed')");
                    table.ForeignKey(
                        name: "FK_IncidentTicketStatusHistory_AspNetUsers_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IncidentTicketStatusHistory_IncidentTickets_IncidentTicketId",
                        column: x => x.IncidentTicketId,
                        principalTable: "IncidentTickets",
                        principalColumn: "IncidentTicketId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IncidentTickets_ApartmentId",
                table: "IncidentTickets",
                column: "ApartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentTickets_AssignedToUserId",
                table: "IncidentTickets",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentTickets_ResidentId_CreatedAtUtc",
                table: "IncidentTickets",
                columns: new[] { "ResidentId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_IncidentTickets_Status_CreatedAtUtc",
                table: "IncidentTickets",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_IncidentTickets_TicketCode",
                table: "IncidentTickets",
                column: "TicketCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IncidentTicketStatusHistory_ChangedByUserId",
                table: "IncidentTicketStatusHistory",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentTicketStatusHistory_IncidentTicketId",
                table: "IncidentTicketStatusHistory",
                column: "IncidentTicketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IncidentTicketStatusHistory");

            migrationBuilder.DropTable(
                name: "IncidentTickets");
        }
    }
}
