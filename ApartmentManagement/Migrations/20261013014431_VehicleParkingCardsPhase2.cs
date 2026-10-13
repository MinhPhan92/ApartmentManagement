using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApartmentManagement.Migrations
{
    /// <inheritdoc />
    public partial class VehicleParkingCardsPhase2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IncidentTicketStatusHistory");

            migrationBuilder.DropTable(
                name: "IncidentTickets");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_LicensePlate",
                table: "Vehicles");

            migrationBuilder.AddColumn<int>(
                name: "ApartmentId",
                table: "Vehicles",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE v
                SET ApartmentId = occupancy.ApartmentId
                FROM Vehicles AS v
                CROSS APPLY (
                    SELECT TOP (1) ar.ApartmentId
                    FROM ApartmentResidents AS ar
                    WHERE ar.ResidentId = v.ResidentId
                    ORDER BY CASE WHEN ar.MoveOutDate IS NULL THEN 0 ELSE 1 END,
                             ar.MoveInDate DESC,
                             ar.ApartmentResidentId DESC
                ) AS occupancy;

                IF EXISTS (SELECT 1 FROM Vehicles WHERE ApartmentId IS NULL)
                    THROW 51000, 'Cannot assign an apartment to one or more existing vehicles.', 1;

                UPDATE Vehicles
                SET LicensePlate = UPPER(REPLACE(LTRIM(RTRIM(LicensePlate)), ' ', ''));
                """);

            migrationBuilder.AlterColumn<int>(
                name: "ApartmentId",
                table: "Vehicles",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "ParkingCards",
                columns: table => new
                {
                    ParkingCardId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VehicleId = table.Column<int>(type: "int", nullable: false),
                    CardCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParkingCards", x => x.ParkingCardId);
                    table.ForeignKey(
                        name: "FK_ParkingCards_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "VehicleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_ApartmentId",
                table: "Vehicles",
                column: "ApartmentId");

            migrationBuilder.CreateIndex(
                name: "UX_Vehicles_LicensePlate",
                table: "Vehicles",
                column: "LicensePlate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ParkingCards_CardCode",
                table: "ParkingCards",
                column: "CardCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ParkingCards_VehicleId",
                table: "ParkingCards",
                column: "VehicleId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Vehicles_Apartments_ApartmentId",
                table: "Vehicles",
                column: "ApartmentId",
                principalTable: "Apartments",
                principalColumn: "ApartmentId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Vehicles_Apartments_ApartmentId",
                table: "Vehicles");

            migrationBuilder.DropTable(
                name: "ParkingCards");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_ApartmentId",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "UX_Vehicles_LicensePlate",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "ApartmentId",
                table: "Vehicles");

            migrationBuilder.CreateTable(
                name: "IncidentTickets",
                columns: table => new
                {
                    IncidentTicketId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApartmentId = table.Column<int>(type: "int", nullable: false),
                    AssignedToUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ResidentId = table.Column<int>(type: "int", nullable: false),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    RatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Rating = table.Column<byte>(type: "tinyint", nullable: true),
                    ResidentFeedback = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    TicketCode = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
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
                    ChangedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    IncidentTicketId = table.Column<int>(type: "int", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NewStatus = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    PreviousStatus = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: true)
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
                name: "IX_Vehicles_LicensePlate",
                table: "Vehicles",
                column: "LicensePlate",
                unique: true,
                filter: "[Status] IN (N'Chờ duyệt', N'Đã duyệt')");

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
    }
}
