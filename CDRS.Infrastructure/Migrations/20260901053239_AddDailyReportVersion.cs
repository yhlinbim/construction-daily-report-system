using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CDRS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyReportVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "DailyReports",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                table: "DailyReports");
        }
    }
}
