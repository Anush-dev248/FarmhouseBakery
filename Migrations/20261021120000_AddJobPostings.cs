using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmhouseBakery.Migrations
{
    /// <inheritdoc />
    public partial class AddJobPostings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobPostings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    JobType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Pay = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    PostedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobPostings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobPostings");
        }
    }
}
