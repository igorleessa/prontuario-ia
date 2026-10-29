using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prontuario.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Quem disse cada trecho (medico, paciente, acompanhante) e se o medico o
    /// confirmou. Trechos existentes ficam indefinidos ate o proximo rascunho.
    /// </summary>
    public partial class FalantesDaTranscricao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Falante",
                table: "TrechosTranscricao",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Indefinido");

            migrationBuilder.AddColumn<bool>(
                name: "FalanteConfirmado",
                table: "TrechosTranscricao",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Falante",
                table: "TrechosTranscricao");

            migrationBuilder.DropColumn(
                name: "FalanteConfirmado",
                table: "TrechosTranscricao");
        }
    }
}
