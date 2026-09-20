using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;
using Prontuario.Domain.Entities;
using Prontuario.Domain.Enums;
using Prontuario.Infrastructure.Persistence;

namespace Prontuario.Infrastructure.Services;

/// <summary>
/// Worker do pipeline de IA (RF07/RF08): busca o audio no storage, transcreve,
/// extrai o rascunho estruturado e deixa o atendimento pronto para a revisao do
/// medico. Falhas nao travam o atendimento - ele segue para EmRevisao com o erro
/// registrado, para que a consulta possa ser documentada manualmente.
/// </summary>
public class ProcessamentoIAWorker : BackgroundService
{
    private readonly IFilaProcessamentoIA _fila;
    private readonly IServiceScopeFactory _escopos;
    private readonly ILogger<ProcessamentoIAWorker> _logger;

    public ProcessamentoIAWorker(
        IFilaProcessamentoIA fila, IServiceScopeFactory escopos, ILogger<ProcessamentoIAWorker> logger)
    {
        _fila = fila;
        _escopos = escopos;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            Guid atendimentoId;

            try
            {
                atendimentoId = await _fila.ProximoAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                await ProcessarAsync(atendimentoId, stoppingToken);
            }
            catch (Exception excecao)
            {
                // Uma falha em um atendimento nao pode derrubar o worker.
                _logger.LogError(excecao, "Falha ao processar a IA do atendimento {AtendimentoId}.", atendimentoId);
                await RegistrarFalhaAsync(atendimentoId, excecao.Message, stoppingToken);
            }
        }
    }

    private async Task ProcessarAsync(Guid atendimentoId, CancellationToken cancellationToken)
    {
        using var escopo = _escopos.CreateScope();
        var provedor = escopo.ServiceProvider;
        var db = provedor.GetRequiredService<ApplicationDbContext>();

        var atendimento = await db.Atendimentos
            .Include(a => a.GravacaoAudio)
            .Include(a => a.Medico)
            .Include(a => a.TemplateNota)
            .SingleOrDefaultAsync(a => a.Id == atendimentoId, cancellationToken);

        if (atendimento?.GravacaoAudio is null)
        {
            _logger.LogWarning("Atendimento {AtendimentoId} sem gravacao; nada a processar.", atendimentoId);
            return;
        }

        if (string.IsNullOrEmpty(atendimento.GravacaoAudio.StoragePath))
        {
            throw new InvalidOperationException(
                "O audio desta consulta ja foi expurgado pela politica de retencao e nao pode ser reprocessado.");
        }

        var configuracoes = provedor.GetRequiredService<IConfiguracaoIAService>();
        var credenciais = await configuracoes.ObterCredenciaisAsync(atendimento.Medico!.ClinicaId, cancellationToken);

        if (credenciais is null)
        {
            throw new InvalidOperationException(
                "Nenhuma chave de API configurada para a clinica. Cadastre a chave em Configuracoes.");
        }

        var transcricao = new Transcricao
        {
            GravacaoAudioId = atendimento.GravacaoAudio.Id,
            Status = StatusProcessamento.Processando,
        };
        db.Transcricoes.Add(transcricao);
        await db.SaveChangesAsync(cancellationToken);

        var armazenamento = provedor.GetRequiredService<IArmazenamentoAudio>();
        var stt = provedor.GetRequiredService<ITranscriptionService>();
        var llm = provedor.GetRequiredService<IClinicalNoteGenerator>();

        await using var audio = await armazenamento.AbrirAsync(atendimento.GravacaoAudio.StoragePath, cancellationToken);
        transcricao.Texto = await stt.TranscreverAsync(audio, credenciais, cancellationToken);
        transcricao.Status = StatusProcessamento.Concluido;
        await db.SaveChangesAsync(cancellationToken);

        // O template da especialidade e o estilo do medico moldam a redacao; o
        // pipeline em si e o mesmo para toda consulta.
        var contexto = new ContextoGeracao(
            atendimento.TemplateNota?.Instrucoes, atendimento.Medico.InstrucoesEstilo);

        var rascunho = await llm.GerarRascunhoAsync(transcricao.Texto, credenciais, contexto, cancellationToken);

        db.RascunhosIA.Add(new RascunhoIA
        {
            TranscricaoId = transcricao.Id,
            QueixaPrincipal = rascunho.QueixaPrincipal,
            Hda = rascunho.Hda,
            Antecedentes = rascunho.Antecedentes,
            ExameFisico = rascunho.ExameFisico,
            HipoteseDiagnostica = rascunho.HipoteseDiagnostica,
            Cid10Sugerido = rascunho.Cid10Sugerido,
            Conduta = rascunho.Conduta,
        });

        atendimento.Status = StatusAtendimento.EmRevisao;
        atendimento.ErroProcessamentoIA = null;
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Rascunho de IA pronto para o atendimento {AtendimentoId}.", atendimentoId);
    }

    private async Task RegistrarFalhaAsync(Guid atendimentoId, string erro, CancellationToken cancellationToken)
    {
        try
        {
            using var escopo = _escopos.CreateScope();
            var db = escopo.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var atendimento = await db.Atendimentos
                .Include(a => a.GravacaoAudio!.Transcricao)
                .SingleOrDefaultAsync(a => a.Id == atendimentoId, cancellationToken);

            if (atendimento is null)
            {
                return;
            }

            if (atendimento.GravacaoAudio?.Transcricao is { } transcricao)
            {
                transcricao.Status = StatusProcessamento.Falhou;
            }

            atendimento.Status = StatusAtendimento.EmRevisao;
            atendimento.ErroProcessamentoIA = erro.Length > 500 ? erro[..500] : erro;

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception excecao)
        {
            _logger.LogError(excecao, "Nao foi possivel registrar a falha do atendimento {AtendimentoId}.", atendimentoId);
        }
    }
}
