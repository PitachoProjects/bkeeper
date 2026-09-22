using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BKeeper.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRiskScoreModelType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RiskScores_BoxId_MemberId_SnapshotWeek",
                table: "RiskScores");

            // Every RiskScore row that predates this migration was scored by the (only, at the
            // time) LightGBM ensemble — backfill them as such rather than an empty string.
            migrationBuilder.AddColumn<string>(
                name: "ModelType",
                table: "RiskScores",
                type: "text",
                nullable: false,
                defaultValue: "lightgbm_ensemble");

            migrationBuilder.CreateIndex(
                name: "IX_RiskScores_BoxId_MemberId_SnapshotWeek_ModelType",
                table: "RiskScores",
                columns: new[] { "BoxId", "MemberId", "SnapshotWeek", "ModelType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RiskScores_BoxId_MemberId_SnapshotWeek_ModelType",
                table: "RiskScores");

            migrationBuilder.DropColumn(
                name: "ModelType",
                table: "RiskScores");

            migrationBuilder.CreateIndex(
                name: "IX_RiskScores_BoxId_MemberId_SnapshotWeek",
                table: "RiskScores",
                columns: new[] { "BoxId", "MemberId", "SnapshotWeek" },
                unique: true);
        }
    }
}
