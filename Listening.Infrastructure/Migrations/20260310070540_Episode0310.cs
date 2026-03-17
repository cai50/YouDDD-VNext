using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Listening.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Episode0310 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Subtitle",
                table: "T_Episodes",
                newName: "Subtitle_English");

            migrationBuilder.AddColumn<string>(
                name: "Subtitle_Chinese",
                table: "T_Episodes",
                type: "nvarchar(max)",
                maxLength: 2147483647,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Subtitle_Chinese",
                table: "T_Episodes");

            migrationBuilder.RenameColumn(
                name: "Subtitle_English",
                table: "T_Episodes",
                newName: "Subtitle");
        }
    }
}
