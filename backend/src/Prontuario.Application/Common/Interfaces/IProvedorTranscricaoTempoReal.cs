using Prontuario.Application.Common.Models;

namespace Prontuario.Application.Common.Interfaces;

/// <summary>
/// Emite a credencial temporaria com que o navegador se conecta direto ao
/// provedor de transcricao em tempo real. A chave da clinica fica no servidor.
/// </summary>
public interface IProvedorTranscricaoTempoReal
{
    Task<CredencialTempoReal> CriarSessaoAsync(CredenciaisIA credenciais, CancellationToken cancellationToken = default);
}
