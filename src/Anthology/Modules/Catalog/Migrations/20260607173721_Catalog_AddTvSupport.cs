using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Anthology.Modules.Catalog.Migrations
{
    /// <inheritdoc />
    public partial class Catalog_AddTvSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "media_data",
                schema: "catalog",
                table: "titles",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "parent_title_id",
                schema: "catalog",
                table: "titles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                schema: "catalog",
                table: "titles",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_titles_parent_title_id_sort_order",
                schema: "catalog",
                table: "titles",
                columns: new[] { "parent_title_id", "sort_order" });

            migrationBuilder.AddForeignKey(
                name: "fk_titles_titles_parent_title_id",
                schema: "catalog",
                table: "titles",
                column: "parent_title_id",
                principalSchema: "catalog",
                principalTable: "titles",
                principalColumn: "title_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_titles_titles_parent_title_id",
                schema: "catalog",
                table: "titles");

            migrationBuilder.DropIndex(
                name: "ix_titles_parent_title_id_sort_order",
                schema: "catalog",
                table: "titles");

            migrationBuilder.DropColumn(
                name: "media_data",
                schema: "catalog",
                table: "titles");

            migrationBuilder.DropColumn(
                name: "parent_title_id",
                schema: "catalog",
                table: "titles");

            migrationBuilder.DropColumn(
                name: "sort_order",
                schema: "catalog",
                table: "titles");
        }
    }
}
