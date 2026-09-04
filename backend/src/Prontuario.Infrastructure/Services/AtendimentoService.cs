using Microsoft.EntityFrameworkCore;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;
using Prontuario.Domain.Entities;
using Prontuario.Domain.Enums;
using Prontuario.Infrastructure.Persistence;

namespace Prontuario.Infrastructure.Services;

public class AtendimentoService : IAtendimentoService
{
    private readonly ApplicationDbContext _db;
    private readonly IRegistroClinicoOutput _output;

    public AtendimentoService(ApplicationDbContext db, IRegistroClinicoOutput output)
    {
        _db = db;
        _output = output;
    }

    public async Task<Guid> AbrirAsync(Guid pacienteRefId, Guid medicoId, CancellationToken cancellationToken = default)
    {
        var atendimento = new Atendimento
        {
            PacienteRefId = pacienteRefId,
            MedicoId = medicoId,
            Status = StatusAtendimento.AguardandoConsentimento,
        };

        _db.Atendimentos.Add(atendimento);
        await _db.SaveChangesAsync(cancellationToken);

        return atendimento.Id;
    }

    public async Task RegistrarConsentimentoAsync(Guid atendimentoId, CancellationToken cancellationToken = default)
    {
        var atendimento = await _db.Atendimentos.SingleAsync(a => a.Id == atendimentoId, cancellationToken);

        atendimento.ConsentimentoGravacao = true;
        atendimento.ConsentimentoEm = DateTime.UtcNow;
        atendimento.Status = StatusAtendimento.EmGravacao;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ConfirmarAsync(Guid atendimentoId, RascunhoClinicoDto revisado, CancellationToken cancellationToken = default)
    {
        await _output.ConfirmarAsync(atendimentoId, revisado, cancellationToken);

        var atendimento = await _db.Atendimentos.SingleAsync(a => a.Id == atendimentoId, cancellationToken);
        atendimento.Status = StatusAtendimento.Finalizado;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
