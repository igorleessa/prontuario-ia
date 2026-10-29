using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prontuario.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Origem de cada campo do rascunho, termos que o medico marcou como dado
    /// pessoal (cifrados) e o modelo das atualizacoes durante a consulta.
    /// </summary>
    public partial class OrigensTermosEModeloAoVivo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OrigensJson",
                table: "RascunhosIA",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModeloTextoAoVivo",
                table: "ConfiguracoesIA",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                // Clinicas ja configuradas passam a usar o modelo rapido durante a consulta.
                defaultValue: "gpt-4o-mini");

            migrationBuilder.AddColumn<string>(
                name: "TermosProtegidosCifrados",
                table: "Atendimentos",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrigensJson",
                table: "RascunhosIA");

            migrationBuilder.DropColumn(
                name: "ModeloTextoAoVivo",
                table: "ConfiguracoesIA");

            migrationBuilder.DropColumn(
                name: "TermosProtegidosCifrados",
                table: "Atendimentos");
        }
    }
}
