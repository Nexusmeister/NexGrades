using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexGrades.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClassIdToStudent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add ClassId column as nullable first
            migrationBuilder.AddColumn<int>(
                name: "ClassId",
                table: "Students",
                type: "INTEGER",
                nullable: true);

            // Update existing students to use the first available ClassId
            migrationBuilder.Sql(
                @"UPDATE Students 
                  SET ClassId = (SELECT MIN(Id) FROM Classes)
                  WHERE ClassId IS NULL");

            // Make ClassId non-nullable
            migrationBuilder.AlterColumn<int>(
                name: "ClassId",
                table: "Students",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_ClassId",
                table: "Students",
                column: "ClassId");

            migrationBuilder.AddForeignKey(
                name: "FK_Students_Classes_ClassId",
                table: "Students",
                column: "ClassId",
                principalTable: "Classes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Students_Classes_ClassId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_ClassId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "ClassId",
                table: "Students");
        }
    }
}
