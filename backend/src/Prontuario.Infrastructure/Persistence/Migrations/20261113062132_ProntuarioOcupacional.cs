using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prontuario.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProntuarioOcupacional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProntuariosOcupacionais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AtendimentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Modo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Sexo = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    RespostasJson = table.Column<string>(type: "text", nullable: false),
                    JustificativasJson = table.Column<string>(type: "text", nullable: false),
                    AuscultaCardiaca = table.Column<bool>(type: "boolean", nullable: false),
                    AuscultaPulmonar = table.Column<bool>(type: "boolean", nullable: false),
                    ConcluidoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProntuariosOcupacionais", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProntuariosOcupacionais_Atendimentos_AtendimentoId",
                        column: x => x.AtendimentoId,
                        principalTable: "Atendimentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProntuariosOcupacionais_AtendimentoId",
                table: "ProntuariosOcupacionais",
                column: "AtendimentoId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProntuariosOcupacionais");
        }
    }
}
