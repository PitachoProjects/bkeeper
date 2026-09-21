using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BKeeper.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMembershipFreezesAndGoalExternalId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyPriceEur",
                table: "Memberships",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FreezesUpserted",
                table: "ImportRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GoalsUpserted",
                table: "ImportRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MembershipsUpserted",
                table: "ImportRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Goals",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MembershipFreezes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<string>(type: "text", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    BoxId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipFreezes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MembershipFreezes_Memberships_MembershipId",
                        column: x => x.MembershipId,
                        principalTable: "Memberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Goals_BoxId_ExternalId",
                table: "Goals",
                columns: new[] { "BoxId", "ExternalId" });

            migrationBuilder.CreateIndex(
                name: "IX_MembershipFreezes_BoxId_ExternalId",
                table: "MembershipFreezes",
                columns: new[] { "BoxId", "ExternalId" });

            migrationBuilder.CreateIndex(
                name: "IX_MembershipFreezes_MembershipId",
                table: "MembershipFreezes",
                column: "MembershipId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MembershipFreezes");

            migrationBuilder.DropIndex(
                name: "IX_Goals_BoxId_ExternalId",
                table: "Goals");

            migrationBuilder.DropColumn(
                name: "MonthlyPriceEur",
                table: "Memberships");

            migrationBuilder.DropColumn(
                name: "FreezesUpserted",
                table: "ImportRuns");

            migrationBuilder.DropColumn(
                name: "GoalsUpserted",
                table: "ImportRuns");

            migrationBuilder.DropColumn(
                name: "MembershipsUpserted",
                table: "ImportRuns");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Goals");
        }
    }
}
