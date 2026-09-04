using Microsoft.EntityFrameworkCore;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;
using Prontuario.Domain.Entities;
using Prontuario.Domain.Enums;
using Prontuario.Infrastructure.Persistence;

namespace Prontuario.Infrastructure.Services;

/// <summary>
/// Saida da Modalidade B (Conector): formata a revisao do medico como
/// NotaExportavel (RF18/RF20). O envio via webhook (RF19) e um TODO -
/// ver especificacao-mvp.md, secao 12, item 6, para o formato do payload.
/// Nunca chamada diretamente - selecionada por RegistroClinicoOutputResolver
/// a partir do ModoOperacao da clinica.
/// </summary>
public class ExportacaoEmrOutput
{
    private readonly ApplicationDbContext _db;
    private readonly INotaClinicaFormatter _formatador;

    public ExportacaoEmrOutput(ApplicationDbContext db, INotaClinicaFormatter formatador)
    {
        _db = db;
        _formatador = formatador;
    }

    public async Task ConfirmarAsync(Guid atendimentoId, RascunhoClinicoDto revisado, CancellationToken cancellationToken = default)
    {
        var nota = await _db.NotasExportaveis.SingleOrDefaultAsync(n => n.AtendimentoId == atendimentoId, cancellationToken)
            ?? new NotaExportavel { AtendimentoId = atendimentoId };

        nota.ConteudoFormatado = _formatador.Formatar(revisado);
        nota.Status = StatusNota.Revisada;

        if (_db.Entry(nota).State == EntityState.Detached)
        {
            _db.NotasExportaveis.Add(nota);
        }

        await _db.SaveChangesAsync(cancellationToken);

        // TODO: quando a clinica tiver Clinica.WebhookUrl configurado, enviar o
        // payload assinado com Clinica.WebhookSecret e marcar Status = Exportada.
    }
}
