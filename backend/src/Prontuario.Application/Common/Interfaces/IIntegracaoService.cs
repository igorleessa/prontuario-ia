using Prontuario.Application.Common.Models;

namespace Prontuario.Application.Common.Interfaces;

/// <summary>
/// Porta de entrada para o EMR que a clinica ja usa: permite abrir um
/// atendimento a partir do sistema do cliente, sem que ninguem precise
/// recadastrar o paciente aqui. Ver docs/integracao-emr.md.
/// </summary>
public interface IIntegracaoService
{
    /// <summary>
    /// Abre o atendimento para a clinica dona da chave de integracao. O paciente
    /// e reaproveitado quando a referencia externa ja existe, e criado quando nao.
    /// </summary>
    Task<AtendimentoExternoCriadoDto> AbrirAtendimentoAsync(
        Guid clinicaId, AbrirAtendimentoExternoDto pedido, CancellationToken cancellationToken = default);
}
