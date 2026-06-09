using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CDRS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SiteWorkerId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReportDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WorkDescription = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    WorkerCount = table.Column<int>(type: "int", nullable: false),
                    WeatherCondition = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyReports", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyReports_ProjectId",
                table: "DailyReports",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyReports_ProjectId_ReportDate",
                table: "DailyReports",
                columns: new[] { "ProjectId", "ReportDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyReports_Status",
                table: "DailyReports",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyReports");
        }
    }
}
