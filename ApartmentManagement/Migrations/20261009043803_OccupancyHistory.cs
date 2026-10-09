using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApartmentManagement.Migrations
{
    /// <inheritdoc />
    public partial class OccupancyHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM [ApartmentResidents] WHERE [MoveOutDate] IS NOT NULL AND [MoveOutDate] < [MoveInDate])
                    THROW 51000, N'Không thể migration: có hồ sơ cư trú với ngày chuyển đi trước ngày chuyển vào.', 1;
                """);

            migrationBuilder.DropPrimaryKey(
                name: "PK_ApartmentResidents",
                table: "ApartmentResidents");

            migrationBuilder.AddColumn<int>(
                name: "ApartmentResidentId",
                table: "ApartmentResidents",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ApartmentResidents",
                table: "ApartmentResidents",
                column: "ApartmentResidentId");

            migrationBuilder.CreateIndex(
                name: "UX_ApartmentResidents_ActiveAssignment",
                table: "ApartmentResidents",
                columns: new[] { "ApartmentId", "ResidentId" },
                unique: true,
                filter: "[MoveOutDate] IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ApartmentResidents_MoveOutDate",
                table: "ApartmentResidents",
                sql: "[MoveOutDate] IS NULL OR [MoveOutDate] >= [MoveInDate]");

            migrationBuilder.Sql(
                """
                UPDATE a SET [Status] = N'Đang sử dụng'
                FROM [Apartments] a
                WHERE EXISTS (
                    SELECT 1 FROM [ApartmentResidents] ar
                    WHERE ar.[ApartmentId] = a.[ApartmentId] AND ar.[MoveOutDate] IS NULL);

                UPDATE a SET [Status] = N'Trống'
                FROM [Apartments] a
                WHERE a.[Status] = N'Đang sử dụng' AND NOT EXISTS (
                    SELECT 1 FROM [ApartmentResidents] ar
                    WHERE ar.[ApartmentId] = a.[ApartmentId] AND ar.[MoveOutDate] IS NULL);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1 FROM [ApartmentResidents]
                    GROUP BY [ApartmentId], [ResidentId]
                    HAVING COUNT(*) > 1)
                    THROW 51000, N'Không thể rollback: dữ liệu đã có nhiều lần cư trú cho cùng cư dân và căn hộ.', 1;
                """);

            migrationBuilder.DropPrimaryKey(
                name: "PK_ApartmentResidents",
                table: "ApartmentResidents");

            migrationBuilder.DropIndex(
                name: "UX_ApartmentResidents_ActiveAssignment",
                table: "ApartmentResidents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ApartmentResidents_MoveOutDate",
                table: "ApartmentResidents");

            migrationBuilder.DropColumn(
                name: "ApartmentResidentId",
                table: "ApartmentResidents");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ApartmentResidents",
                table: "ApartmentResidents",
                columns: new[] { "ApartmentId", "ResidentId" });
        }
    }
}
