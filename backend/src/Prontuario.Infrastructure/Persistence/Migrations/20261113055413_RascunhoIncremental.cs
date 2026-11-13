using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prontuario.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RascunhoIncremental : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ConsideradoNoRascunho",
                table: "TrechosTranscricao",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConsideradoNoRascunho",
                table: "TrechosTranscricao");
        }
    }
}
