using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BKeeper.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRiskScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RiskScores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotWeek = table.Column<DateOnly>(type: "date", nullable: false),
                    ModelVersion = table.Column<string>(type: "text", nullable: false),
                    PChurn28d = table.Column<double>(type: "double precision", nullable: false),
                    Band = table.Column<string>(type: "text", nullable: false),
                    TopReasons = table.Column<string>(type: "jsonb", nullable: false),
                    BoxId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskScores", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RiskScores_BoxId_MemberId_SnapshotWeek",
                table: "RiskScores",
                columns: new[] { "BoxId", "MemberId", "SnapshotWeek" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RiskScores");
        }
    }
}
