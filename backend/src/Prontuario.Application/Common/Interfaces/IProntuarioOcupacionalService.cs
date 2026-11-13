using Prontuario.Application.Common.Models;
using Prontuario.Application.Privacidade;

namespace Prontuario.Application.Common.Interfaces;

/// <summary>
/// Extrai respostas do prontuario ocupacional das falas novas. So aceita texto
/// sanitizado. Devolve apenas o que mudou: nunca rele a conversa inteira.
/// </summary>
public interface IExtratorOcupacional
{
    Task<ExtracaoOcupacionalDto> ExtrairAsync(
        string sexo,
        string? secaoEmFoco,
        TextoSanitizado respostasAtuais,
        TextoSanitizado falasDeContexto,
        TextoSanitizado falasNovas,
        CredenciaisIA credenciais,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Exame ocupacional nas tres formas (tradicional, IA guiada, escriba). A IA
/// sugere e o medico valida; resposta do medico nunca e sobrescrita pela IA.
/// </summary>
public interface IProntuarioOcupacionalService
{
    /// <summary>Transforma o atendimento em exame ocupacional. Chamar de novo so devolve o estado.</summary>
    Task<ResultadoOperacao<EstadoOcupacionalDto>> IniciarAsync(
        Guid atendimentoId, Guid clinicaId, IniciarOcupacionalDto pedido, CancellationToken cancellationToken = default);

    Task<ResultadoOperacao<EstadoOcupacionalDto>> ObterAsync(
        Guid atendimentoId, Guid clinicaId, CancellationToken cancellationToken = default);

    /// <summary>Resposta dada pelo medico na tela. Valor vazio apaga a resposta.</summary>
    Task<ResultadoOperacao<EstadoOcupacionalDto>> ResponderAsync(
        Guid atendimentoId, Guid clinicaId, string campo, string? valor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Leva um lote de falas novas a IA e aplica o que ela extraiu. O texto das
    /// falas nao e gravado em lugar nenhum.
    /// </summary>
    Task<ResultadoOperacao<FalasProcessadasDto>> ProcessarFalasAsync(
        Guid atendimentoId, Guid clinicaId, ProcessarFalasDto falas, CancellationToken cancellationToken = default);

    /// <summary>Transcreve um trecho curto de audio em memoria (modo alternativo ao tempo real) e o descarta.</summary>
    Task<ResultadoOperacao<string>> TranscreverAsync(
        Guid atendimentoId, Guid clinicaId, Stream audio, CancellationToken cancellationToken = default);

    /// <summary>Justificativa do medico para um alerta critico que ele decidiu manter.</summary>
    Task<ResultadoOperacao<EstadoOcupacionalDto>> JustificarAsync(
        Guid atendimentoId, Guid clinicaId, string chave, string? texto, CancellationToken cancellationToken = default);

    /// <summary>Salva o atendimento. Conflito enquanto houver obrigatorio pendente ou alerta critico sem justificativa.</summary>
    Task<ResultadoOperacao<EstadoOcupacionalDto>> ConcluirAsync(
        Guid atendimentoId, Guid clinicaId, CancellationToken cancellationToken = default);
}
