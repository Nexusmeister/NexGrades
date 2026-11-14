using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexGrades.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSchoolYearUnlockReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UnlockReason",
                table: "SchoolYears",
                type: "TEXT",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnlockReason",
                table: "SchoolYears");
        }
    }
}
