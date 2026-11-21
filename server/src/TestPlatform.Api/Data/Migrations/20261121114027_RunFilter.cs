using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestPlatform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RunFilter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Filter",
                table: "Runs",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Filter",
                table: "Runs");
        }
    }
}
