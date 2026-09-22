using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pege.Migrations
{
    /// <inheritdoc />
    public partial class AddSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StreamId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Ip = table.Column<string>(type: "TEXT", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Geo = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Connected = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Closed = table.Column<DateTime>(type: "TEXT", nullable: true),
                    BytesSent = table.Column<long>(type: "INTEGER", nullable: false),
                    CloseReason = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_Closed",
                table: "Sessions",
                column: "Closed",
                filter: "\"Closed\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_Connected",
                table: "Sessions",
                column: "Connected");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_StreamId_Connected",
                table: "Sessions",
                columns: new[] { "StreamId", "Connected" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Sessions");
        }
    }
}
