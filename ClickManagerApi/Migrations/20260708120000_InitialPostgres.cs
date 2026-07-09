using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ClickManagerApi.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Fotografos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SenhaHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Instagram = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Bio = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PlanoAtivo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fotografos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Leads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Telefone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Plano = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Mensagem = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EmailEnviado = table.Column<bool>(type: "boolean", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Clientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FotografoId = table.Column<int>(type: "integer", nullable: false),
                    Nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Telefone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    TipoEnsaio = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Observacoes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clientes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Clientes_Fotografos_FotografoId",
                        column: x => x.FotografoId,
                        principalTable: "Fotografos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Disponibilidades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FotografoId = table.Column<int>(type: "integer", nullable: false),
                    DiaSemana = table.Column<byte>(type: "smallint", nullable: false),
                    HoraInicio = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    HoraFim = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Disponibilidades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Disponibilidades_Fotografos_FotografoId",
                        column: x => x.FotografoId,
                        principalTable: "Fotografos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImagensPortfolio",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FotografoId = table.Column<int>(type: "integer", nullable: false),
                    NomeArquivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    TamanhoBytes = table.Column<long>(type: "bigint", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImagensPortfolio", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImagensPortfolio_Fotografos_FotografoId",
                        column: x => x.FotografoId,
                        principalTable: "Fotografos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Ensaios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FotografoId = table.Column<int>(type: "integer", nullable: false),
                    ClienteId = table.Column<int>(type: "integer", nullable: false),
                    Titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DataHora = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Local = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Valor = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TotalImagens = table.Column<int>(type: "integer", nullable: false),
                    Observacoes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ensaios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ensaios_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Ensaios_Fotografos_FotografoId",
                        column: x => x.FotografoId,
                        principalTable: "Fotografos",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Contratos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EnsaioId = table.Column<int>(type: "integer", nullable: false),
                    Numero = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Conteudo = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DataAssinatura = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contratos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contratos_Ensaios_EnsaioId",
                        column: x => x.EnsaioId,
                        principalTable: "Ensaios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImagensGaleria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EnsaioId = table.Column<int>(type: "integer", nullable: false),
                    NomeArquivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    TamanhoBytes = table.Column<long>(type: "bigint", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImagensGaleria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImagensGaleria_Ensaios_EnsaioId",
                        column: x => x.EnsaioId,
                        principalTable: "Ensaios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Pagamentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EnsaioId = table.Column<int>(type: "integer", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Metodo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DataPagamento = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Observacoes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pagamentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pagamentos_Ensaios_EnsaioId",
                        column: x => x.EnsaioId,
                        principalTable: "Ensaios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ── Seed: dados de demonstração ─────────────────────────────
            migrationBuilder.InsertData(
                table: "Fotografos",
                columns: new[] { "Id", "AtualizadoEm", "Bio", "CriadoEm", "Email", "Instagram", "Nome", "PlanoAtivo", "SenhaHash", "Telefone" },
                values: new object[] { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "matheus@studio.com", null, "Matheus", "pro", "$2a$11$demo_hash_substituir_por_bcrypt", null });

            migrationBuilder.InsertData(
                table: "Clientes",
                columns: new[] { "Id", "CriadoEm", "Email", "FotografoId", "Nome", "Observacoes", "Status", "Telefone", "TipoEnsaio" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 1, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), "marcela@cliente.local", 1, "MARCELA", null, "Pausado", null, "Casamento" },
                    { 2, new DateTime(2026, 1, 12, 0, 0, 0, 0, DateTimeKind.Unspecified), "virtor@cliente.local", 1, "Virtor", null, "Ativo", null, "Casamento" }
                });

            migrationBuilder.InsertData(
                table: "Ensaios",
                columns: new[] { "Id", "AtualizadoEm", "ClienteId", "CriadoEm", "DataHora", "FotografoId", "Local", "Observacoes", "Status", "Titulo", "TotalImagens", "Valor" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 1, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, new DateTime(2026, 1, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 3, 25, 15, 0, 0, 0, DateTimeKind.Unspecified), 1, "Sorocaba", null, "Agendado", "Festa 15 Anos Marcela", 0, 700m },
                    { 2, new DateTime(2026, 1, 12, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, new DateTime(2026, 1, 12, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 3, 24, 15, 0, 0, 0, DateTimeKind.Unspecified), 1, "Igreja", null, "Agendado", "casamento", 0, 200m }
                });

            migrationBuilder.InsertData(
                table: "Contratos",
                columns: new[] { "Id", "Conteudo", "CriadoEm", "DataAssinatura", "EnsaioId", "Numero", "Status" },
                values: new object[,]
                {
                    { 1, null, new DateTime(2026, 1, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1, "CTR16778", "Assinado" },
                    { 2, null, new DateTime(2026, 1, 12, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 2, "CTR13245673", "Assinado" }
                });

            migrationBuilder.InsertData(
                table: "Pagamentos",
                columns: new[] { "Id", "CriadoEm", "DataPagamento", "EnsaioId", "Metodo", "Observacoes", "Status", "Tipo", "Valor" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 1, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 1, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "Pix", null, "Pago", "Total", 700m },
                    { 2, new DateTime(2026, 1, 12, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 1, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, "Pix", null, "Pago", "Total", 200m }
                });

            // ── Ajuste das sequences após seed com Ids explícitos ───────
            // No PostgreSQL, inserir Ids explícitos NÃO avança a sequence da
            // coluna identity. Sem este ajuste, o primeiro INSERT normal em
            // qualquer tabela com seed falharia com "duplicate key" (a sequence
            // tentaria gerar o Id 1, que já existe).
            migrationBuilder.Sql("""
                SELECT setval(pg_get_serial_sequence('"Fotografos"', 'Id'), (SELECT MAX("Id") FROM "Fotografos"));
                SELECT setval(pg_get_serial_sequence('"Clientes"',   'Id'), (SELECT MAX("Id") FROM "Clientes"));
                SELECT setval(pg_get_serial_sequence('"Ensaios"',    'Id'), (SELECT MAX("Id") FROM "Ensaios"));
                SELECT setval(pg_get_serial_sequence('"Contratos"',  'Id'), (SELECT MAX("Id") FROM "Contratos"));
                SELECT setval(pg_get_serial_sequence('"Pagamentos"', 'Id'), (SELECT MAX("Id") FROM "Pagamentos"));
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_FotografoId",
                table: "Clientes",
                column: "FotografoId");

            migrationBuilder.CreateIndex(
                name: "IX_Contratos_EnsaioId",
                table: "Contratos",
                column: "EnsaioId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contratos_Numero",
                table: "Contratos",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Disponibilidades_FotografoId",
                table: "Disponibilidades",
                column: "FotografoId");

            migrationBuilder.CreateIndex(
                name: "IX_Ensaios_ClienteId",
                table: "Ensaios",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Ensaios_DataHora",
                table: "Ensaios",
                column: "DataHora");

            migrationBuilder.CreateIndex(
                name: "IX_Ensaios_FotografoId",
                table: "Ensaios",
                column: "FotografoId");

            migrationBuilder.CreateIndex(
                name: "IX_ImagensGaleria_EnsaioId",
                table: "ImagensGaleria",
                column: "EnsaioId");

            migrationBuilder.CreateIndex(
                name: "IX_ImagensPortfolio_FotografoId",
                table: "ImagensPortfolio",
                column: "FotografoId");

            migrationBuilder.CreateIndex(
                name: "IX_Pagamentos_EnsaioId",
                table: "Pagamentos",
                column: "EnsaioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Contratos");
            migrationBuilder.DropTable(name: "Disponibilidades");
            migrationBuilder.DropTable(name: "ImagensGaleria");
            migrationBuilder.DropTable(name: "ImagensPortfolio");
            migrationBuilder.DropTable(name: "Leads");
            migrationBuilder.DropTable(name: "Pagamentos");
            migrationBuilder.DropTable(name: "Ensaios");
            migrationBuilder.DropTable(name: "Clientes");
            migrationBuilder.DropTable(name: "Fotografos");
        }
    }
}
