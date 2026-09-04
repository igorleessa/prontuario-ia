using Microsoft.EntityFrameworkCore;
using Prontuario.Application.Common.Models;
using Prontuario.Domain.Entities;
using Prontuario.Infrastructure.Persistence;

namespace Prontuario.Infrastructure.Services;

/// <summary>
/// Saida da Modalidade A (Integrado): grava a revisao do medico no
/// ProntuarioRegistro nativo e o finaliza (RF15). Nunca chamada diretamente -
/// selecionada por RegistroClinicoOutputResolver a partir do ModoOperacao da clinica.
/// </summary>
public class ProntuarioNativoOutput
{
    private readonly ApplicationDbContext _db;

    public ProntuarioNativoOutput(ApplicationDbContext db) => _db = db;

    public async Task ConfirmarAsync(Guid atendimentoId, RascunhoClinicoDto revisado, CancellationToken cancellationToken = default)
    {
        var prontuario = await _db.Prontuarios.SingleOrDefaultAsync(p => p.AtendimentoId == atendimentoId, cancellationToken)
            ?? new ProntuarioRegistro { AtendimentoId = atendimentoId };

        prontuario.QueixaPrincipal = revisado.QueixaPrincipal;
        prontuario.Hda = revisado.Hda;
        prontuario.Antecedentes = revisado.Antecedentes;
        prontuario.ExameFisico = revisado.ExameFisico;
        prontuario.HipoteseDiagnostica = revisado.HipoteseDiagnostica;
        prontuario.Cid10 = revisado.Cid10Sugerido;
        prontuario.Conduta = revisado.Conduta;
        prontuario.Finalizado = true;
        prontuario.AssinadoEm = DateTime.UtcNow;

        if (_db.Entry(prontuario).State == EntityState.Detached)
        {
            _db.Prontuarios.Add(prontuario);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
