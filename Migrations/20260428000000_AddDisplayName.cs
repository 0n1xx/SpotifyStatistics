using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpotifyStatisticsWebApp.Migrations
{
    /// <inheritdoc />
    public partial class AddDisplayName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add DisplayName column — nullable, max 100 chars
            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "UserProfiles",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "UserProfiles");
        }
    }
}
