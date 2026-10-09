using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApartmentManagement.Migrations
{
    /// <inheritdoc />
    public partial class ResidentValidationAndIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM [Residents] WHERE LEN([Gender]) > 20)
                    THROW 51000, N'Không thể migration: Gender vượt quá 20 ký tự.', 1;
                IF EXISTS (SELECT 1 FROM [Residents] WHERE LEN([EmergencyContact]) > 50)
                    THROW 51000, N'Không thể migration: EmergencyContact vượt quá 50 ký tự.', 1;
                IF EXISTS (SELECT 1 FROM [Residents] WHERE LEN([Address]) > 255)
                    THROW 51000, N'Không thể migration: Address vượt quá 255 ký tự.', 1;
                IF EXISTS (SELECT 1 FROM [AspNetUsers] WHERE LEN([FullName]) > 200)
                    THROW 51000, N'Không thể migration: FullName vượt quá 200 ký tự.', 1;
                IF EXISTS (SELECT 1 FROM [ApartmentResidents] WHERE LEN([Relationship]) > 100)
                    THROW 51000, N'Không thể migration: Relationship vượt quá 100 ký tự.', 1;
                IF EXISTS (SELECT 1 FROM [Residents] WHERE [CitizenId] LIKE '%[^0-9]%' OR LEN([CitizenId]) NOT IN (9, 12))
                    THROW 51000, N'Không thể migration: CitizenId phải gồm 9 hoặc 12 chữ số.', 1;
                IF EXISTS (SELECT 1 FROM [Residents] WHERE [DateOfBirth] < '1900-01-01')
                    THROW 51000, N'Không thể migration: DateOfBirth nhỏ hơn 01/01/1900.', 1;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Gender",
                table: "Residents",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EmergencyContact",
                table: "Residents",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "Residents",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "AspNetUsers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Relationship",
                table: "ApartmentResidents",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Residents_CitizenId_Format",
                table: "Residents",
                sql: "[CitizenId] NOT LIKE '%[^0-9]%' AND LEN([CitizenId]) IN (9, 12)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Residents_DateOfBirth_Minimum",
                table: "Residents",
                sql: "[DateOfBirth] IS NULL OR [DateOfBirth] >= '1900-01-01'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Residents_DateOfBirth_Minimum",
                table: "Residents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Residents_CitizenId_Format",
                table: "Residents");

            migrationBuilder.AlterColumn<string>(
                name: "Gender",
                table: "Residents",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EmergencyContact",
                table: "Residents",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "Residents",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Relationship",
                table: "ApartmentResidents",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);
        }
    }
}
