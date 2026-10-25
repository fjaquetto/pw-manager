using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PWManager.Infra.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVaultMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VaultMetadata",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    FormatVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    Kdf = table.Column<string>(type: "TEXT", nullable: false),
                    KdfIterations = table.Column<int>(type: "INTEGER", nullable: false),
                    KdfSalt = table.Column<string>(type: "TEXT", nullable: false),
                    Verifier = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VaultMetadata", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VaultMetadata");
        }
    }
}
