using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prontuario.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Marca por trecho se o reconhecimento de nomes ja passou por ele. Os
    /// trechos existentes ficam pendentes e passam pelo reconhecedor antes do
    /// proximo envio a IA.
    /// </summary>
    public partial class ReconhecimentoDeNomesPorTrecho : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ReconhecimentoAplicado",
                table: "TrechosTranscricao",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReconhecimentoAplicado",
                table: "TrechosTranscricao");
        }
    }
}
