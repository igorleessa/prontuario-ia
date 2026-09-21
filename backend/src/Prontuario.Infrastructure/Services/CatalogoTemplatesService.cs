using Microsoft.EntityFrameworkCore;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;
using Prontuario.Infrastructure.Persistence;

namespace Prontuario.Infrastructure.Services;

public class CatalogoTemplatesService : ICatalogoTemplatesService
{
    private readonly ApplicationDbContext _db;

    public CatalogoTemplatesService(ApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<TemplateNotaDto>> ListarAsync(
        Guid clinicaId, CancellationToken cancellationToken = default)
        // Templates de catalogo (ClinicaId nulo) valem para todas as clinicas;
        // os proprios da clinica aparecem junto, na mesma lista.
        => await _db.TemplatesNota
            .Where(t => t.Ativo && (t.ClinicaId == null || t.ClinicaId == clinicaId))
            .OrderBy(t => t.Ordem)
            .ThenBy(t => t.Nome)
            .Select(t => new TemplateNotaDto(t.Id, t.Nome, t.Especialidade))
            .ToListAsync(cancellationToken);
}
