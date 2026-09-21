using Prontuario.Application.Common.Models;

namespace Prontuario.Application.Common.Interfaces;

/// <summary>Preferencias do proprio usuario - diferente de ConfiguracaoIA, que e da clinica.</summary>
public interface IPerfilService
{
    Task<PerfilMedicoDto?> ObterAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    Task<PerfilMedicoDto?> SalvarEstiloAsync(
        Guid usuarioId, string? instrucoesEstilo, CancellationToken cancellationToken = default);
}
