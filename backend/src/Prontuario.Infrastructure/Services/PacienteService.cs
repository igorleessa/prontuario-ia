using Microsoft.EntityFrameworkCore;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;
using Prontuario.Domain.Entities;
using Prontuario.Domain.Enums;
using Prontuario.Infrastructure.Persistence;

namespace Prontuario.Infrastructure.Services;

public class PacienteService : IPacienteService
{
    private readonly ApplicationDbContext _db;

    public PacienteService(ApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<PacienteResumoDto>> ListarAsync(
        Guid clinicaId, string? busca = null, CancellationToken cancellationToken = default)
    {
        var consulta = _db.Pacientes.Where(p => p.ClinicaId == clinicaId);

        if (!string.IsNullOrWhiteSpace(busca))
        {
            var termo = busca.Trim();
            consulta = consulta.Where(p =>
                EF.Functions.ILike(p.Nome, $"%{termo}%") ||
                (p.Cpf != null && EF.Functions.ILike(p.Cpf, $"%{termo}%")));
        }

        return await consulta
            .OrderBy(p => p.Nome)
            .Select(p => new PacienteResumoDto(p.Id, p.Nome, p.Cpf, p.DataNascimento, p.Contato, p.IdExternoEmr))
            .ToListAsync(cancellationToken);
    }

    public async Task<PacienteResumoDto> CriarAsync(
        Guid clinicaId, NovoPacienteDto novo, CancellationToken cancellationToken = default)
    {
        var paciente = new PacienteRef
        {
            ClinicaId = clinicaId,
            Nome = novo.Nome.Trim(),
            Cpf = string.IsNullOrWhiteSpace(novo.Cpf) ? null : novo.Cpf.Trim(),
            DataNascimento = novo.DataNascimento,
            Contato = string.IsNullOrWhiteSpace(novo.Contato) ? null : novo.Contato.Trim(),
            IdExternoEmr = string.IsNullOrWhiteSpace(novo.IdExternoEmr) ? null : novo.IdExternoEmr.Trim(),
        };

        _db.Pacientes.Add(paciente);
        await _db.SaveChangesAsync(cancellationToken);

        return new PacienteResumoDto(
            paciente.Id, paciente.Nome, paciente.Cpf, paciente.DataNascimento, paciente.Contato, paciente.IdExternoEmr);
    }

    public async Task<HistoricoPacienteDto?> ObterHistoricoAsync(
        Guid pacienteId, Guid clinicaId, CancellationToken cancellationToken = default)
    {
        var paciente = await _db.Pacientes
            .SingleOrDefaultAsync(p => p.Id == pacienteId && p.ClinicaId == clinicaId, cancellationToken);

        if (paciente is null)
        {
            return null;
        }

        // A queixa e a hipotese vem do prontuario nativo quando existe; na
        // Modalidade B o registro definitivo esta no EMR do cliente, e a linha
        // do tempo mostra apenas o que aconteceu aqui.
        var atendimentos = await _db.Atendimentos
            .Where(a => a.PacienteRefId == pacienteId)
            .OrderByDescending(a => a.DataHora)
            .Select(a => new HistoricoAtendimentoDto(
                a.Id,
                a.DataHora,
                a.Status.ToString(),
                a.Medico!.Nome,
                a.Prontuario != null ? a.Prontuario.QueixaPrincipal : null,
                a.Prontuario != null ? a.Prontuario.HipoteseDiagnostica : null,
                a.Status == StatusAtendimento.Finalizado))
            .ToListAsync(cancellationToken);

        return new HistoricoPacienteDto(
            paciente.Id, paciente.Nome, paciente.Cpf, paciente.DataNascimento, paciente.IdExternoEmr, atendimentos);
    }
}
