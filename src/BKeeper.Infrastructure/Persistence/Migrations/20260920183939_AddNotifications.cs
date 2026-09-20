using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BKeeper.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MemberConsents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    Granted = table.Column<bool>(type: "boolean", nullable: false),
                    BoxId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberConsents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Outreaches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    AlertId = table.Column<Guid>(type: "uuid", nullable: true),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    TemplateKey = table.Column<string>(type: "text", nullable: true),
                    Body = table.Column<string>(type: "text", nullable: false),
                    SentBy = table.Column<int>(type: "integer", nullable: false),
                    SentByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ProviderMessageId = table.Column<string>(type: "text", nullable: true),
                    IsHoldout = table.Column<bool>(type: "boolean", nullable: false),
                    BoxId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Outreaches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Outreaches_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemberConsents_BoxId_MemberId_Channel",
                table: "MemberConsents",
                columns: new[] { "BoxId", "MemberId", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Outreaches_MemberId",
                table: "Outreaches",
                column: "MemberId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MemberConsents");

            migrationBuilder.DropTable(
                name: "Outreaches");
        }
    }
}
