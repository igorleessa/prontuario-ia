using Microsoft.EntityFrameworkCore;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;
using Prontuario.Infrastructure.Persistence;

namespace Prontuario.Infrastructure.Services;

public class PerfilService : IPerfilService
{
    /// <summary>
    /// Limite generoso, mas finito: as preferencias entram em todo prompt de
    /// extracao, entao um texto muito longo encareceria cada consulta.
    /// </summary>
    private const int TamanhoMaximoEstilo = 2000;

    private readonly ApplicationDbContext _db;

    public PerfilService(ApplicationDbContext db) => _db = db;

    public async Task<PerfilMedicoDto?> ObterAsync(Guid usuarioId, CancellationToken cancellationToken = default)
        => await _db.Usuarios
            .Where(u => u.Id == usuarioId)
            .Select(u => new PerfilMedicoDto(u.Nome, u.Email, u.InstrucoesEstilo))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<PerfilMedicoDto?> SalvarEstiloAsync(
        Guid usuarioId, string? instrucoesEstilo, CancellationToken cancellationToken = default)
    {
        var usuario = await _db.Usuarios.SingleOrDefaultAsync(u => u.Id == usuarioId, cancellationToken);
        if (usuario is null)
        {
            return null;
        }

        var texto = instrucoesEstilo?.Trim();
        if (texto is { Length: > TamanhoMaximoEstilo })
        {
            throw new InvalidOperationException(
                $"As preferencias de redacao devem ter no maximo {TamanhoMaximoEstilo} caracteres.");
        }

        usuario.InstrucoesEstilo = string.IsNullOrEmpty(texto) ? null : texto;
        await _db.SaveChangesAsync(cancellationToken);

        return new PerfilMedicoDto(usuario.Nome, usuario.Email, usuario.InstrucoesEstilo);
    }
}
