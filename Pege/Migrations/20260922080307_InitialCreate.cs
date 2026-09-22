using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pege.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Streams",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Country = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ImplType = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Registered = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Stopped = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TelegramChannelId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Discriminator = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    MetadataSwap = table.Column<bool>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Streams", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Streams");
        }
    }
}
