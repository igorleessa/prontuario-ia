using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prontuario.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TemplatesEstiloEDocumentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InstrucoesEstilo",
                table: "Usuarios",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TemplateNotaId",
                table: "Atendimentos",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Documentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AtendimentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Conteudo = table.Column<string>(type: "text", nullable: false),
                    GeradoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documentos_Atendimentos_AtendimentoId",
                        column: x => x.AtendimentoId,
                        principalTable: "Atendimentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TemplatesNota",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClinicaId = table.Column<Guid>(type: "uuid", nullable: true),
                    Nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Especialidade = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Instrucoes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplatesNota", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TemplatesNota_Clinicas_ClinicaId",
                        column: x => x.ClinicaId,
                        principalTable: "Clinicas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Atendimentos_TemplateNotaId",
                table: "Atendimentos",
                column: "TemplateNotaId");

            migrationBuilder.CreateIndex(
                name: "IX_Documentos_AtendimentoId_Tipo",
                table: "Documentos",
                columns: new[] { "AtendimentoId", "Tipo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TemplatesNota_ClinicaId",
                table: "TemplatesNota",
                column: "ClinicaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Atendimentos_TemplatesNota_TemplateNotaId",
                table: "Atendimentos",
                column: "TemplateNotaId",
                principalTable: "TemplatesNota",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Atendimentos_TemplatesNota_TemplateNotaId",
                table: "Atendimentos");

            migrationBuilder.DropTable(
                name: "Documentos");

            migrationBuilder.DropTable(
                name: "TemplatesNota");

            migrationBuilder.DropIndex(
                name: "IX_Atendimentos_TemplateNotaId",
                table: "Atendimentos");

            migrationBuilder.DropColumn(
                name: "InstrucoesEstilo",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "TemplateNotaId",
                table: "Atendimentos");
        }
    }
}
