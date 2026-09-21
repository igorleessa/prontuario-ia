using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Domain.Enums;
using Prontuario.Infrastructure.Persistence;

namespace Prontuario.Infrastructure.Services;

/// <summary>
/// Apaga o audio bruto das consultas ja transcritas depois do prazo de retencao
/// configurado. A LGPD pede que o dado sensivel nao seja guardado alem do
/// necessario, e depois da transcricao confirmada o audio deixa de ser preciso -
/// o registro clinico e o texto revisado pelo medico, nao a gravacao.
/// </summary>
public class RetencaoAudioWorker : BackgroundService
{
    /// <summary>Uma varredura por dia basta: o prazo e contado em dias.</summary>
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _escopos;
    private readonly ArmazenamentoAudioOptions _opcoes;
    private readonly ILogger<RetencaoAudioWorker> _logger;

    public RetencaoAudioWorker(
        IServiceScopeFactory escopos,
        IOptions<ArmazenamentoAudioOptions> opcoes,
        ILogger<RetencaoAudioWorker> logger)
    {
        _escopos = escopos;
        _opcoes = opcoes.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_opcoes.RetencaoDias <= 0)
        {
            _logger.LogInformation("Retencao de audio desligada: as gravacoes sao mantidas ate expurgo manual.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExpurgarAsync(stoppingToken);
            }
            catch (Exception excecao) when (excecao is not OperationCanceledException)
            {
                _logger.LogError(excecao, "Falha na varredura de retencao de audio.");
            }

            try
            {
                await Task.Delay(Intervalo, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task ExpurgarAsync(CancellationToken cancellationToken)
    {
        using var escopo = _escopos.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var armazenamento = escopo.ServiceProvider.GetRequiredService<IArmazenamentoAudio>();

        var limite = DateTime.UtcNow.AddDays(-_opcoes.RetencaoDias);

        // So apaga o que ja foi transcrito com sucesso: audio de atendimento com
        // falha de transcricao ainda pode ser reprocessado (RF13).
        var vencidas = await db.GravacoesAudio
            .Where(g => g.StoragePath != ""
                && g.CriadoEm < limite
                && g.Transcricao != null
                && g.Transcricao.Status == StatusProcessamento.Concluido)
            .ToListAsync(cancellationToken);

        foreach (var gravacao in vencidas)
        {
            try
            {
                await armazenamento.RemoverAsync(gravacao.StoragePath, cancellationToken);

                // A gravacao continua no banco como evidencia de que houve audio;
                // o que sai e o arquivo. Caminho vazio marca o expurgo.
                gravacao.StoragePath = string.Empty;
            }
            catch (Exception excecao)
            {
                _logger.LogError(
                    excecao, "Nao foi possivel expurgar o audio {Chave}.", gravacao.StoragePath);
            }
        }

        if (vencidas.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Retencao de audio: {Total} gravacoes expurgadas.", vencidas.Count);
        }
    }
}
