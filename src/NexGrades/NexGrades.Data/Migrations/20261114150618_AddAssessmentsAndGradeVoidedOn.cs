using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexGrades.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAssessmentsAndGradeVoidedOn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssessmentId",
                table: "Grades",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VoidedOn",
                table: "Grades",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Assessments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GradeBucketId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    VoidedOn = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Assessments_GradeBuckets_GradeBucketId",
                        column: x => x.GradeBucketId,
                        principalTable: "GradeBuckets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Grades_AssessmentId_StudentId",
                table: "Grades",
                columns: new[] { "AssessmentId", "StudentId" },
                unique: true,
                filter: "\"AssessmentId\" IS NOT NULL AND \"VoidedOn\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_GradeBucketId_Date",
                table: "Assessments",
                columns: new[] { "GradeBucketId", "Date" });

            migrationBuilder.AddForeignKey(
                name: "FK_Grades_Assessments_AssessmentId",
                table: "Grades",
                column: "AssessmentId",
                principalTable: "Assessments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Grades_Assessments_AssessmentId",
                table: "Grades");

            migrationBuilder.DropTable(
                name: "Assessments");

            migrationBuilder.DropIndex(
                name: "IX_Grades_AssessmentId_StudentId",
                table: "Grades");

            migrationBuilder.DropColumn(
                name: "AssessmentId",
                table: "Grades");

            migrationBuilder.DropColumn(
                name: "VoidedOn",
                table: "Grades");
        }
    }
}
