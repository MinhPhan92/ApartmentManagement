using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApartmentManagement.Migrations
{
    /// <inheritdoc />
    public partial class ApartmentResidentActiveOwnerConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT [ApartmentId] FROM [ApartmentResidents] WHERE [MoveOutDate] IS NULL AND [IsOwner] = 1 GROUP BY [ApartmentId] HAVING COUNT(*) > 1)
                    THROW 51000, N'Không thể migration: có căn hộ đang có nhiều hơn một chủ hộ hoạt động.', 1;
                """);

            migrationBuilder.CreateIndex(
                name: "UX_ApartmentResidents_ActiveOwner",
                table: "ApartmentResidents",
                column: "ApartmentId",
                unique: true,
                filter: "[MoveOutDate] IS NULL AND [IsOwner] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_ApartmentResidents_ActiveOwner",
                table: "ApartmentResidents");
        }
    }
}
