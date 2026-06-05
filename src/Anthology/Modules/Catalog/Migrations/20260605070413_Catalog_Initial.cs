using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Anthology.Modules.Catalog.Migrations
{
    /// <inheritdoc />
    public partial class Catalog_Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.CreateTable(
                name: "titles",
                schema: "catalog",
                columns: table => new
                {
                    title_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_id = table.Column<string>(type: "text", nullable: false),
                    media_type = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: true),
                    poster_path = table.Column<string>(type: "text", nullable: true),
                    overview = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_titles", x => x.title_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_titles_external_id",
                schema: "catalog",
                table: "titles",
                column: "external_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "titles",
                schema: "catalog");
        }
    }
}
