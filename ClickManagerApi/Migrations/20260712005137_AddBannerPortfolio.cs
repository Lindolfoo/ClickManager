using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClickManagerApi.Migrations
{
    /// <inheritdoc />
    public partial class AddBannerPortfolio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BannerPreset",
                table: "Fotografos",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "roxo");

            migrationBuilder.AddColumn<string>(
                name: "BannerUrl",
                table: "Fotografos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Fotografos",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "BannerPreset", "BannerUrl" },
                values: new object[] { "roxo", null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BannerPreset",
                table: "Fotografos");

            migrationBuilder.DropColumn(
                name: "BannerUrl",
                table: "Fotografos");
        }
    }
}
