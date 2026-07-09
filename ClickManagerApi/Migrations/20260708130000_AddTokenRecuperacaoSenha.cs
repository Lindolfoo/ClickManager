using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ClickManagerApi.Migrations
{
    /// <inheritdoc />
    public partial class AddTokenRecuperacaoSenha : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TokensRecuperacaoSenha",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FotografoId = table.Column<int>(type: "integer", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiraEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UsadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokensRecuperacaoSenha", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TokensRecuperacaoSenha_Fotografos_FotografoId",
                        column: x => x.FotografoId,
                        principalTable: "Fotografos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TokensRecuperacaoSenha_FotografoId",
                table: "TokensRecuperacaoSenha",
                column: "FotografoId");

            migrationBuilder.CreateIndex(
                name: "IX_TokensRecuperacaoSenha_TokenHash",
                table: "TokensRecuperacaoSenha",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "TokensRecuperacaoSenha");
        }
    }
}
