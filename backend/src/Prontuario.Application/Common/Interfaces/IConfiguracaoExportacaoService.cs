using Prontuario.Application.Common.Models;

namespace Prontuario.Application.Common.Interfaces;

public interface IConfiguracaoExportacaoService
{
    Task<ConfiguracaoExportacaoDto> ObterAsync(Guid clinicaId, CancellationToken cancellationToken = default);

    /// <summary>Segredo nulo ou vazio mantem o atual, permitindo trocar so a URL sem redigita-lo.</summary>
    Task<ConfiguracaoExportacaoDto> SalvarAsync(
        Guid clinicaId, SalvarConfiguracaoExportacaoDto dados, CancellationToken cancellationToken = default);

    /// <summary>Gera uma nova chave de integracao e invalida a anterior. O valor em claro so existe nesta resposta.</summary>
    Task<ChaveIntegracaoGeradaDto> GerarChaveIntegracaoAsync(Guid clinicaId, CancellationToken cancellationToken = default);

    Task<bool> RevogarChaveIntegracaoAsync(Guid clinicaId, CancellationToken cancellationToken = default);

    /// <summary>Resolve a clinica dona de uma chave de integracao. Null quando a chave nao existe.</summary>
    Task<Guid?> ResolverClinicaPorChaveAsync(string chave, CancellationToken cancellationToken = default);

    /// <summary>Segredo em claro para assinar o payload. Uso interno do exportador.</summary>
    Task<string?> ObterSegredoWebhookAsync(Guid clinicaId, CancellationToken cancellationToken = default);
}
