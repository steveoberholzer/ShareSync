using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharePointPermissionSync.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPriorityColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Priority",
                schema: "ScyneShare",
                table: "ProcessingJobs",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Medium");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingJobs_Priority",
                schema: "ScyneShare",
                table: "ProcessingJobs",
                column: "Priority");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProcessingJobs_Priority",
                schema: "ScyneShare",
                table: "ProcessingJobs");

            migrationBuilder.DropColumn(
                name: "Priority",
                schema: "ScyneShare",
                table: "ProcessingJobs");
        }
    }
}
