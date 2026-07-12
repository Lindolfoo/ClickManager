using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClickManagerApi.Migrations
{
    /// <inheritdoc />
    public partial class AddFotoUrlFotografo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FotoUrl",
                table: "Fotografos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Fotografos",
                keyColumn: "Id",
                keyValue: 1,
                column: "FotoUrl",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FotoUrl",
                table: "Fotografos");
        }
    }
}
