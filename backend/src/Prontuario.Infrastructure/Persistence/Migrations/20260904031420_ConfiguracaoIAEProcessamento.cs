using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prontuario.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConfiguracaoIAEProcessamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ErroProcessamentoIA",
                table: "Atendimentos",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ConfiguracoesIA",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClinicaId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChaveApiProtegida = table.Column<string>(type: "text", nullable: false),
                    ChaveApiSufixo = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    ModeloTranscricao = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ModeloTexto = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AtualizadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracoesIA", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConfiguracoesIA_Clinicas_ClinicaId",
                        column: x => x.ClinicaId,
                        principalTable: "Clinicas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracoesIA_ClinicaId",
                table: "ConfiguracoesIA",
                column: "ClinicaId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracoesIA");

            migrationBuilder.DropColumn(
                name: "ErroProcessamentoIA",
                table: "Atendimentos");
        }
    }
}
