using Microsoft.EntityFrameworkCore;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;
using Prontuario.Domain.Enums;
using Prontuario.Infrastructure.Persistence;

namespace Prontuario.Infrastructure.Services;

/// <summary>
/// Unica implementacao de IRegistroClinicoOutput registrada no DI. Le o
/// ModoOperacao da clinica do medico responsavel pelo atendimento e delega
/// para ProntuarioNativoOutput (Integrado) ou ExportacaoEmrOutput (Conector) -
/// ver docs/especificacao-mvp.md, secao 2.
/// </summary>
public class RegistroClinicoOutputResolver : IRegistroClinicoOutput
{
    private readonly ApplicationDbContext _db;
    private readonly ProntuarioNativoOutput _integrado;
    private readonly ExportacaoEmrOutput _conector;

    public RegistroClinicoOutputResolver(ApplicationDbContext db, ProntuarioNativoOutput integrado, ExportacaoEmrOutput conector)
    {
        _db = db;
        _integrado = integrado;
        _conector = conector;
    }

    public async Task ConfirmarAsync(Guid atendimentoId, RascunhoClinicoDto revisado, CancellationToken cancellationToken = default)
    {
        var modoOperacao = await _db.Atendimentos
            .Where(a => a.Id == atendimentoId)
            .Select(a => a.Medico!.Clinica!.ModoOperacao)
            .SingleAsync(cancellationToken);

        if (modoOperacao == ModoOperacao.Conector)
        {
            await _conector.ConfirmarAsync(atendimentoId, revisado, cancellationToken);
        }
        else
        {
            await _integrado.ConfirmarAsync(atendimentoId, revisado, cancellationToken);
        }
    }
}
