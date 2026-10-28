using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShortLinks.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Links",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, collation: "BINARY"),
                    DestinationUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AccessCount = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0L),
                    ManagementKeyHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatorIpFingerprint = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Links", x => x.Id);
                    table.CheckConstraint("CK_Links_AccessCount_NonNegative", "\"AccessCount\" >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Links_Code",
                table: "Links",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Links_CreatorIpFingerprint_CreatedAtUtc",
                table: "Links",
                columns: new[] { "CreatorIpFingerprint", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Links");
        }
    }
}
