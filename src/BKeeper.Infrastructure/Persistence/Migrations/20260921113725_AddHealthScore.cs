using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BKeeper.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHealthScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HealthScoreConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    AttendanceWeight = table.Column<double>(type: "double precision", nullable: false),
                    ConsistencyWeight = table.Column<double>(type: "double precision", nullable: false),
                    BookingBehaviourWeight = table.Column<double>(type: "double precision", nullable: false),
                    ProgressWeight = table.Column<double>(type: "double precision", nullable: false),
                    EngagementWeight = table.Column<double>(type: "double precision", nullable: false),
                    AttendanceEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ConsistencyEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    BookingBehaviourEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ProgressEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    EngagementEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AttendanceWindowDays = table.Column<int>(type: "integer", nullable: false),
                    ConsistencyWindowWeeks = table.Column<int>(type: "integer", nullable: false),
                    BookingBehaviourWindowDays = table.Column<int>(type: "integer", nullable: false),
                    ProgressWindowDays = table.Column<int>(type: "integer", nullable: false),
                    EngagementWindowDays = table.Column<int>(type: "integer", nullable: false),
                    MinTenureDays = table.Column<int>(type: "integer", nullable: false),
                    MinSessions = table.Column<int>(type: "integer", nullable: false),
                    ActivatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    BoxId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HealthScoreConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HealthScores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    CalculationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ConfigVersion = table.Column<int>(type: "integer", nullable: false),
                    OverallScore = table.Column<double>(type: "double precision", nullable: true),
                    InsufficientData = table.Column<bool>(type: "boolean", nullable: false),
                    TenureDays = table.Column<int>(type: "integer", nullable: false),
                    SessionCount = table.Column<int>(type: "integer", nullable: false),
                    Factors = table.Column<string>(type: "jsonb", nullable: false),
                    CalculatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    BoxId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HealthScores", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HealthScoreConfigurations_BoxId_IsActive",
                table: "HealthScoreConfigurations",
                columns: new[] { "BoxId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_HealthScoreConfigurations_BoxId_Version",
                table: "HealthScoreConfigurations",
                columns: new[] { "BoxId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HealthScores_BoxId_MemberId_CalculationDate",
                table: "HealthScores",
                columns: new[] { "BoxId", "MemberId", "CalculationDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HealthScoreConfigurations");

            migrationBuilder.DropTable(
                name: "HealthScores");
        }
    }
}
