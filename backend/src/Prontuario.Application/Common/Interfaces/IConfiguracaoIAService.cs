using Prontuario.Application.Common.Models;

namespace Prontuario.Application.Common.Interfaces;

public interface IConfiguracaoIAService
{
    Task<ConfiguracaoIADto> ObterAsync(Guid clinicaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Grava a configuracao da clinica. ChaveApi nula ou vazia mantem a chave
    /// atual, permitindo trocar so o modelo sem redigitar a credencial.
    /// </summary>
    Task<ConfiguracaoIADto> SalvarAsync(
        Guid clinicaId, Guid usuarioId, SalvarConfiguracaoIADto dados, CancellationToken cancellationToken = default);

    Task<bool> RemoverChaveAsync(Guid clinicaId, CancellationToken cancellationToken = default);

    /// <summary>Credenciais em claro para uso interno do pipeline. Nunca exposto por rota.</summary>
    Task<CredenciaisIA?> ObterCredenciaisAsync(Guid clinicaId, CancellationToken cancellationToken = default);
}

public sealed record CredenciaisIA(string ChaveApi, string ModeloTranscricao, string ModeloTexto);
