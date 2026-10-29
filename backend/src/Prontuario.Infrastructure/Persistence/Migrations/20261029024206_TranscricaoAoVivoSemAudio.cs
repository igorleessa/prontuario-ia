using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prontuario.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Fim da gravacao de audio: a gravacao vira sessao de transcricao (mesmas
    /// linhas, sem o caminho do arquivo), os trechos ao vivo ganham tabela e as
    /// transcricoes antigas ficam marcadas para a sanitizacao na inicializacao.
    /// Os dados existentes sao preservados - nada e recriado do zero.
    /// </summary>
    public partial class TranscricaoAoVivoSemAudio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transcricoes_GravacoesAudio_GravacaoAudioId",
                table: "Transcricoes");

            migrationBuilder.DropForeignKey(
                name: "FK_GravacoesAudio_Atendimentos_AtendimentoId",
                table: "GravacoesAudio");

            migrationBuilder.DropPrimaryKey(
                name: "PK_GravacoesAudio",
                table: "GravacoesAudio");

            migrationBuilder.RenameTable(
                name: "GravacoesAudio",
                newName: "SessoesTranscricao");

            migrationBuilder.RenameIndex(
                name: "IX_GravacoesAudio_AtendimentoId",
                table: "SessoesTranscricao",
                newName: "IX_SessoesTranscricao_AtendimentoId");

            // O caminho do arquivo deixa de existir junto com o arquivo.
            migrationBuilder.DropColumn(
                name: "StoragePath",
                table: "SessoesTranscricao");

            migrationBuilder.AddColumn<DateTime>(
                name: "IniciadaEm",
                table: "SessoesTranscricao",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "EncerradaEm",
                table: "SessoesTranscricao",
                type: "timestamp with time zone",
                nullable: true);

            // No fluxo antigo o registro nascia no envio do audio, que era o fim
            // da consulta; o inicio e estimado pela duracao gravada.
            migrationBuilder.Sql("""
                UPDATE "SessoesTranscricao"
                SET "EncerradaEm" = "CriadoEm",
                    "IniciadaEm" = "CriadoEm" - make_interval(secs => "DuracaoSegundos");
                """);

            migrationBuilder.AddPrimaryKey(
                name: "PK_SessoesTranscricao",
                table: "SessoesTranscricao",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SessoesTranscricao_Atendimentos_AtendimentoId",
                table: "SessoesTranscricao",
                column: "AtendimentoId",
                principalTable: "Atendimentos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.RenameColumn(
                name: "GravacaoAudioId",
                table: "Transcricoes",
                newName: "SessaoTranscricaoId");

            migrationBuilder.RenameIndex(
                name: "IX_Transcricoes_GravacaoAudioId",
                table: "Transcricoes",
                newName: "IX_Transcricoes_SessaoTranscricaoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Transcricoes_SessoesTranscricao_SessaoTranscricaoId",
                table: "Transcricoes",
                column: "SessaoTranscricaoId",
                principalTable: "SessoesTranscricao",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // Falso para todas as linhas existentes: o texto delas e o bruto, e a
            // inicializacao do banco o sanitiza uma vez.
            migrationBuilder.AddColumn<bool>(
                name: "Sanitizada",
                table: "Transcricoes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.RenameColumn(
                name: "ConsentimentoGravacao",
                table: "Atendimentos",
                newName: "ConsentimentoTranscricao");

            // Sem gravacao em lote nao ha mais fila de processamento: o que estava
            // gravando segue como consulta em andamento, e o que esperava a IA vai
            // para a revisao, onde o medico pode pedir o rascunho de novo.
            migrationBuilder.Sql("""
                UPDATE "Atendimentos" SET "Status" = 'EmConsulta' WHERE "Status" = 'EmGravacao';
                UPDATE "Atendimentos" SET "Status" = 'EmRevisao' WHERE "Status" = 'ProcessandoIA';
                """);

            migrationBuilder.CreateTable(
                name: "TrechosTranscricao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TranscricaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequencia = table.Column<int>(type: "integer", nullable: false),
                    Texto = table.Column<string>(type: "text", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrechosTranscricao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrechosTranscricao_Transcricoes_TranscricaoId",
                        column: x => x.TranscricaoId,
                        principalTable: "Transcricoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrechosTranscricao_TranscricaoId_Sequencia",
                table: "TrechosTranscricao",
                columns: new[] { "TranscricaoId", "Sequencia" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrechosTranscricao");

            migrationBuilder.Sql("""
                UPDATE "Atendimentos" SET "Status" = 'EmGravacao' WHERE "Status" = 'EmConsulta';
                """);

            migrationBuilder.RenameColumn(
                name: "ConsentimentoTranscricao",
                table: "Atendimentos",
                newName: "ConsentimentoGravacao");

            migrationBuilder.DropColumn(
                name: "Sanitizada",
                table: "Transcricoes");

            migrationBuilder.DropForeignKey(
                name: "FK_Transcricoes_SessoesTranscricao_SessaoTranscricaoId",
                table: "Transcricoes");

            migrationBuilder.RenameIndex(
                name: "IX_Transcricoes_SessaoTranscricaoId",
                table: "Transcricoes",
                newName: "IX_Transcricoes_GravacaoAudioId");

            migrationBuilder.RenameColumn(
                name: "SessaoTranscricaoId",
                table: "Transcricoes",
                newName: "GravacaoAudioId");

            migrationBuilder.DropForeignKey(
                name: "FK_SessoesTranscricao_Atendimentos_AtendimentoId",
                table: "SessoesTranscricao");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SessoesTranscricao",
                table: "SessoesTranscricao");

            migrationBuilder.DropColumn(
                name: "IniciadaEm",
                table: "SessoesTranscricao");

            migrationBuilder.DropColumn(
                name: "EncerradaEm",
                table: "SessoesTranscricao");

            // O audio ja nao existe: o caminho volta vazio.
            migrationBuilder.AddColumn<string>(
                name: "StoragePath",
                table: "SessoesTranscricao",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.RenameIndex(
                name: "IX_SessoesTranscricao_AtendimentoId",
                table: "SessoesTranscricao",
                newName: "IX_GravacoesAudio_AtendimentoId");

            migrationBuilder.RenameTable(
                name: "SessoesTranscricao",
                newName: "GravacoesAudio");

            migrationBuilder.AddPrimaryKey(
                name: "PK_GravacoesAudio",
                table: "GravacoesAudio",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_GravacoesAudio_Atendimentos_AtendimentoId",
                table: "GravacoesAudio",
                column: "AtendimentoId",
                principalTable: "Atendimentos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Transcricoes_GravacoesAudio_GravacaoAudioId",
                table: "Transcricoes",
                column: "GravacaoAudioId",
                principalTable: "GravacoesAudio",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
