using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixRestoreProgressIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "RestoreProgress");

            migrationBuilder.CreateTable(
                name: "RestoreProgress",
                columns: table => new
                {
                    OriginalChannelId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    RestoredCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestoreProgress", x => x.OriginalChannelId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "OriginalChannelId",
                table: "RestoreProgress",
                type: "decimal(20,0)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(20,0)")
                .Annotation("SqlServer:Identity", "1, 1");
        }
    }
}
