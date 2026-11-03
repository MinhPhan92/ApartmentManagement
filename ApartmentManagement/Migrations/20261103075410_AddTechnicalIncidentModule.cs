using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApartmentManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddTechnicalIncidentModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TechnicalIncidents",
                columns: table => new
                {
                    TechnicalIncidentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IncidentCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BuildingId = table.Column<int>(type: "int", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ReportedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ReportedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcknowledgedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AssignedTechnicianUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ScheduledDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    SpareParts = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    MaterialCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ClosedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicalIncidents", x => x.TechnicalIncidentId);
                    table.ForeignKey(
                        name: "FK_TechnicalIncidents_AspNetUsers_AcknowledgedByUserId",
                        column: x => x.AcknowledgedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TechnicalIncidents_AspNetUsers_AssignedTechnicianUserId",
                        column: x => x.AssignedTechnicianUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TechnicalIncidents_AspNetUsers_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TechnicalIncidents_AspNetUsers_ReportedByUserId",
                        column: x => x.ReportedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TechnicalIncidents_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "BuildingId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TechnicalIncidentStatusHistories",
                columns: table => new
                {
                    TechnicalIncidentStatusHistoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TechnicalIncidentId = table.Column<int>(type: "int", nullable: false),
                    PreviousStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    NewStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ChangedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PreviousTechnicianUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    NewTechnicianUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicalIncidentStatusHistories", x => x.TechnicalIncidentStatusHistoryId);
                    table.ForeignKey(
                        name: "FK_TechnicalIncidentStatusHistories_AspNetUsers_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TechnicalIncidentStatusHistories_AspNetUsers_NewTechnicianUserId",
                        column: x => x.NewTechnicianUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TechnicalIncidentStatusHistories_AspNetUsers_PreviousTechnicianUserId",
                        column: x => x.PreviousTechnicianUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TechnicalIncidentStatusHistories_TechnicalIncidents_TechnicalIncidentId",
                        column: x => x.TechnicalIncidentId,
                        principalTable: "TechnicalIncidents",
                        principalColumn: "TechnicalIncidentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalIncidents_AcknowledgedByUserId",
                table: "TechnicalIncidents",
                column: "AcknowledgedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalIncidents_AssignedTechnicianUserId",
                table: "TechnicalIncidents",
                column: "AssignedTechnicianUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalIncidents_BuildingId",
                table: "TechnicalIncidents",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalIncidents_ClosedByUserId",
                table: "TechnicalIncidents",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalIncidents_IncidentCode",
                table: "TechnicalIncidents",
                column: "IncidentCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalIncidents_ReportedByUserId",
                table: "TechnicalIncidents",
                column: "ReportedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalIncidents_Status_ReportedAt",
                table: "TechnicalIncidents",
                columns: new[] { "Status", "ReportedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalIncidentStatusHistories_ChangedByUserId",
                table: "TechnicalIncidentStatusHistories",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalIncidentStatusHistories_NewTechnicianUserId",
                table: "TechnicalIncidentStatusHistories",
                column: "NewTechnicianUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalIncidentStatusHistories_PreviousTechnicianUserId",
                table: "TechnicalIncidentStatusHistories",
                column: "PreviousTechnicianUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalIncidentStatusHistories_TechnicalIncidentId_ChangedAt",
                table: "TechnicalIncidentStatusHistories",
                columns: new[] { "TechnicalIncidentId", "ChangedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TechnicalIncidentStatusHistories");

            migrationBuilder.DropTable(
                name: "TechnicalIncidents");
        }
    }
}
