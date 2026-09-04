using Microsoft.EntityFrameworkCore;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;
using Prontuario.Domain.Entities;
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
}
