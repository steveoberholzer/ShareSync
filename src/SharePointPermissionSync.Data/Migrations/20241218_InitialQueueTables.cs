using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharePointPermissionSync.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialQueueTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ScyneShare");

            migrationBuilder.CreateTable(
                name: "ProcessingJobs",
                schema: "ScyneShare",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    UploadedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Environment = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    SiteUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TotalItems = table.Column<int>(type: "int", nullable: false),
                    ProcessedItems = table.Column<int>(type: "int", nullable: false),
                    FailedItems = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Queued"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessingJobs", x => x.Id);
                    table.UniqueConstraint("AK_ProcessingJobs_JobId", x => x.JobId);
                });

            migrationBuilder.CreateTable(
                name: "ProcessingJobItems",
                schema: "ScyneShare",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ItemIdentifier = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    MaxRetries = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessingJobItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessingJobItems_ProcessingJobs_JobId",
                        column: x => x.JobId,
                        principalSchema: "ScyneShare",
                        principalTable: "ProcessingJobs",
                        principalColumn: "JobId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingJobs_CreatedAt",
                schema: "ScyneShare",
                table: "ProcessingJobs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingJobs_JobId",
                schema: "ScyneShare",
                table: "ProcessingJobs",
                column: "JobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingJobs_Status",
                schema: "ScyneShare",
                table: "ProcessingJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingJobItems_JobId",
                schema: "ScyneShare",
                table: "ProcessingJobItems",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingJobItems_MessageId",
                schema: "ScyneShare",
                table: "ProcessingJobItems",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingJobItems_Status",
                schema: "ScyneShare",
                table: "ProcessingJobItems",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessingJobItems",
                schema: "ScyneShare");

            migrationBuilder.DropTable(
                name: "ProcessingJobs",
                schema: "ScyneShare");
        }
    }
}
