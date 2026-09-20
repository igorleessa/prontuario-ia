using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prontuario.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExportacaoConectorEChaveIntegracao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "WebhookSecret",
                table: "Clinicas",
                newName: "WebhookSecretProtegido");

            migrationBuilder.AlterColumn<string>(
                name: "Destino",
                table: "NotasExportaveis",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RevisadaEm",
                table: "NotasExportaveis",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TentativasExportacao",
                table: "NotasExportaveis",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "UltimoErroExportacao",
                table: "NotasExportaveis",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ChaveIntegracaoCriadaEm",
                table: "Clinicas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChaveIntegracaoHash",
                table: "Clinicas",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChaveIntegracaoPrefixo",
                table: "Clinicas",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clinicas_ChaveIntegracaoHash",
                table: "Clinicas",
                column: "ChaveIntegracaoHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Clinicas_ChaveIntegracaoHash",
                table: "Clinicas");

            migrationBuilder.DropColumn(
                name: "RevisadaEm",
                table: "NotasExportaveis");

            migrationBuilder.DropColumn(
                name: "TentativasExportacao",
                table: "NotasExportaveis");

            migrationBuilder.DropColumn(
                name: "UltimoErroExportacao",
                table: "NotasExportaveis");

            migrationBuilder.DropColumn(
                name: "ChaveIntegracaoCriadaEm",
                table: "Clinicas");

            migrationBuilder.DropColumn(
                name: "ChaveIntegracaoHash",
                table: "Clinicas");

            migrationBuilder.DropColumn(
                name: "ChaveIntegracaoPrefixo",
                table: "Clinicas");

            migrationBuilder.RenameColumn(
                name: "WebhookSecretProtegido",
                table: "Clinicas",
                newName: "WebhookSecret");

            migrationBuilder.AlterColumn<string>(
                name: "Destino",
                table: "NotasExportaveis",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }
    }
}
