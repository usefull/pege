using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pege.Migrations
{
    /// <inheritdoc />
    public partial class AddUpdatedToSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "Updated",
                table: "Sessions",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Updated",
                table: "Sessions");
        }
    }
}
