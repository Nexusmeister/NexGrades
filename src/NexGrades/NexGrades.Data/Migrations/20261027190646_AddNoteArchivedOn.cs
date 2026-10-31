using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexGrades.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNoteArchivedOn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedOn",
                table: "Notes",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchivedOn",
                table: "Notes");
        }
    }
}
