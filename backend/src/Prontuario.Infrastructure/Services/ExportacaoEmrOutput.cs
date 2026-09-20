using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;
using Prontuario.Domain.Entities;
using Prontuario.Domain.Enums;
using Prontuario.Infrastructure.Persistence;

namespace Prontuario.Infrastructure.Services;

/// <summary>
/// Saida da Modalidade B (Conector): formata a revisao do medico como
/// NotaExportavel (RF18/RF20) e, quando a clinica tem webhook configurado,
/// envia a nota ao EMR de destino (RF19). Nunca chamada diretamente -
/// selecionada por RegistroClinicoOutputResolver a partir do ModoOperacao.
/// </summary>
public class ExportacaoEmrOutput
{
    private readonly ApplicationDbContext _db;
    private readonly INotaClinicaFormatter _formatador;
    private readonly IExportadorNota _exportador;
    private readonly ILogger<ExportacaoEmrOutput> _logger;

    public ExportacaoEmrOutput(
        ApplicationDbContext db,
        INotaClinicaFormatter formatador,
        IExportadorNota exportador,
        ILogger<ExportacaoEmrOutput> logger)
    {
        _db = db;
        _formatador = formatador;
        _exportador = exportador;
        _logger = logger;
    }

    public async Task ConfirmarAsync(Guid atendimentoId, RascunhoClinicoDto revisado, CancellationToken cancellationToken = default)
    {
        var nota = await _db.NotasExportaveis.SingleOrDefaultAsync(n => n.AtendimentoId == atendimentoId, cancellationToken)
            ?? new NotaExportavel { AtendimentoId = atendimentoId };

        nota.ConteudoFormatado = _formatador.Formatar(revisado);
        nota.Status = StatusNota.Revisada;
        nota.RevisadaEm = DateTime.UtcNow;

        if (_db.Entry(nota).State == EntityState.Detached)
        {
            _db.NotasExportaveis.Add(nota);
        }

        await _db.SaveChangesAsync(cancellationToken);

        // O envio e best-effort: se o EMR estiver fora do ar, a nota fica
        // Revisada com o erro registrado e o medico reenvia ou exporta a mao.
        var resultado = await _exportador.EnviarAsync(atendimentoId, cancellationToken);
        if (!resultado.Sucesso)
        {
            _logger.LogWarning(
                "Nota do atendimento {AtendimentoId} revisada mas nao exportada: {Erro}", atendimentoId, resultado.Erro);
        }
    }
}
