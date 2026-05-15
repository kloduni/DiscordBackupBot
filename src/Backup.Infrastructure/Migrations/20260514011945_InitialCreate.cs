using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServerBackups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GuildId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    GuildName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerBackups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BackupChannels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServerBackupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    ParentId = table.Column<decimal>(type: "decimal(20,0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupChannels", x => x.Id);
                    table.UniqueConstraint("AK_BackupChannels_ChannelId", x => x.ChannelId);
                    table.ForeignKey(
                        name: "FK_BackupChannels_ServerBackups_ServerBackupId",
                        column: x => x.ServerBackupId,
                        principalTable: "ServerBackups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BackupRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServerBackupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Color = table.Column<long>(type: "bigint", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Permissions = table.Column<decimal>(type: "decimal(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BackupRoles_ServerBackups_ServerBackupId",
                        column: x => x.ServerBackupId,
                        principalTable: "ServerBackups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BackupMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    MessageId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    Author = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AvatarUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BackupMessages_BackupChannels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "BackupChannels",
                        principalColumn: "ChannelId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BackupChannels_ServerBackupId",
                table: "BackupChannels",
                column: "ServerBackupId");

            migrationBuilder.CreateIndex(
                name: "IX_BackupMessages_ChannelId",
                table: "BackupMessages",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_BackupMessages_MessageId",
                table: "BackupMessages",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BackupRoles_ServerBackupId",
                table: "BackupRoles",
                column: "ServerBackupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackupMessages");

            migrationBuilder.DropTable(
                name: "BackupRoles");

            migrationBuilder.DropTable(
                name: "BackupChannels");

            migrationBuilder.DropTable(
                name: "ServerBackups");
        }
    }
}
