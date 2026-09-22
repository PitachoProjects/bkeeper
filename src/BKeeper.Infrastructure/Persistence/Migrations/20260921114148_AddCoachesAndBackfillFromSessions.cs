using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BKeeper.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCoachesAndBackfillFromSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CoachId",
                table: "ClassSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Coaches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ApplicationUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    BoxId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Coaches", x => x.Id);
                });

            // Backfill: promote each distinct non-blank ClassSession.CoachName (per box) into its own
            // Coach row, then link every session with that name to it. Additive and idempotent — never
            // touches CoachName, never overwrites a CoachId that's already set, and re-running this
            // migration (or the equivalent POST /coaches/backfill for data imported later) is a no-op
            // once every name has a matching Coach. See CoachBackfillPlanner for the same logic covered
            // by unit tests (this raw SQL mirrors it: distinct non-blank name per box, skip existing).
            migrationBuilder.Sql(
                """
                INSERT INTO "Coaches" ("Id", "BoxId", "Name", "Email", "Status", "ApplicationUserId", "CreatedAt", "UpdatedAt")
                SELECT gen_random_uuid(), names."BoxId", names."Name", NULL, 0, NULL, now(), now()
                FROM (
                    SELECT DISTINCT "BoxId", trim("CoachName") AS "Name"
                    FROM "ClassSessions"
                    WHERE "CoachName" IS NOT NULL AND trim("CoachName") <> ''
                ) AS names
                WHERE NOT EXISTS (
                    SELECT 1 FROM "Coaches" c WHERE c."BoxId" = names."BoxId" AND c."Name" = names."Name"
                );

                UPDATE "ClassSessions" cs
                SET "CoachId" = c."Id"
                FROM "Coaches" c
                WHERE cs."CoachId" IS NULL
                  AND cs."CoachName" IS NOT NULL
                  AND trim(cs."CoachName") <> ''
                  AND c."BoxId" = cs."BoxId"
                  AND c."Name" = trim(cs."CoachName");
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ClassSessions_CoachId",
                table: "ClassSessions",
                column: "CoachId");

            migrationBuilder.CreateIndex(
                name: "IX_Coaches_BoxId_Name",
                table: "Coaches",
                columns: new[] { "BoxId", "Name" });

            migrationBuilder.AddForeignKey(
                name: "FK_ClassSessions_Coaches_CoachId",
                table: "ClassSessions",
                column: "CoachId",
                principalTable: "Coaches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClassSessions_Coaches_CoachId",
                table: "ClassSessions");

            migrationBuilder.DropTable(
                name: "Coaches");

            migrationBuilder.DropIndex(
                name: "IX_ClassSessions_CoachId",
                table: "ClassSessions");

            migrationBuilder.DropColumn(
                name: "CoachId",
                table: "ClassSessions");
        }
    }
}
