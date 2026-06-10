using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace Anthology.Modules.Catalog.Migrations
{
    /// <inheritdoc />
    public partial class Catalog_AddEnrichmentColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.AddColumn<Vector>(
                name: "embedding",
                schema: "catalog",
                table: "titles",
                type: "vector(1536)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "embedding_model",
                schema: "catalog",
                table: "titles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "genres",
                schema: "catalog",
                table: "titles",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "keywords",
                schema: "catalog",
                table: "titles",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "popularity",
                schema: "catalog",
                table: "titles",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "vote_average",
                schema: "catalog",
                table: "titles",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "title_credits",
                schema: "catalog",
                columns: table => new
                {
                    title_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_person_id = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_title_credits", x => new { x.title_id, x.external_person_id, x.role });
                });

            migrationBuilder.CreateIndex(
                name: "ix_titles_genres",
                schema: "catalog",
                table: "titles",
                column: "genres")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_titles_keywords",
                schema: "catalog",
                table: "titles",
                column: "keywords")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_title_credits_external_person_id",
                schema: "catalog",
                table: "title_credits",
                column: "external_person_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "title_credits",
                schema: "catalog");

            migrationBuilder.DropIndex(
                name: "ix_titles_genres",
                schema: "catalog",
                table: "titles");

            migrationBuilder.DropIndex(
                name: "ix_titles_keywords",
                schema: "catalog",
                table: "titles");

            migrationBuilder.DropColumn(
                name: "embedding",
                schema: "catalog",
                table: "titles");

            migrationBuilder.DropColumn(
                name: "embedding_model",
                schema: "catalog",
                table: "titles");

            migrationBuilder.DropColumn(
                name: "genres",
                schema: "catalog",
                table: "titles");

            migrationBuilder.DropColumn(
                name: "keywords",
                schema: "catalog",
                table: "titles");

            migrationBuilder.DropColumn(
                name: "popularity",
                schema: "catalog",
                table: "titles");

            migrationBuilder.DropColumn(
                name: "vote_average",
                schema: "catalog",
                table: "titles");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}
