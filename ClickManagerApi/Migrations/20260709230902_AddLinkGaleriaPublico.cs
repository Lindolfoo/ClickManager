using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClickManagerApi.Migrations
{
    /// <inheritdoc />
    public partial class AddLinkGaleriaPublico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "LinkGaleriaAtivo",
                table: "Ensaios",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LinkGaleriaToken",
                table: "Ensaios",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Ensaios",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "LinkGaleriaAtivo", "LinkGaleriaToken" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "Ensaios",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "LinkGaleriaAtivo", "LinkGaleriaToken" },
                values: new object[] { false, null });

            migrationBuilder.CreateIndex(
                name: "IX_Ensaios_LinkGaleriaToken",
                table: "Ensaios",
                column: "LinkGaleriaToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Ensaios_LinkGaleriaToken",
                table: "Ensaios");

            migrationBuilder.DropColumn(
                name: "LinkGaleriaAtivo",
                table: "Ensaios");

            migrationBuilder.DropColumn(
                name: "LinkGaleriaToken",
                table: "Ensaios");
        }
    }
}
