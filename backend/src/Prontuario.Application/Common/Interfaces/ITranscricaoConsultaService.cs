using Prontuario.Application.Common.Models;

namespace Prontuario.Application.Common.Interfaces;

/// <summary>
/// Transcricao da consulta enquanto ela acontece, sem gravacao: cada trecho de
/// fala chega ja em texto (ou em audio curto, transcrito em memoria), passa pela
/// sanitizacao e so entao e guardado. O rascunho do prontuario e refeito sobre o
/// texto acumulado, para o formulario ir se preenchendo durante a conversa.
/// </summary>
public interface ITranscricaoConsultaService
{
    /// <summary>Abre (ou retoma) a escuta. So depois do consentimento do paciente.</summary>
    Task<ResultadoOperacao<SessaoTranscricaoDto>> IniciarAsync(
        Guid atendimentoId, Guid clinicaId, CancellationToken cancellationToken = default);

    /// <summary>Sanitiza e guarda um trecho ja transcrito pelo provedor em tempo real.</summary>
    Task<ResultadoOperacao<TrechoRegistradoDto>> RegistrarTrechoAsync(
        Guid atendimentoId, Guid clinicaId, RegistrarTrechoDto trecho, CancellationToken cancellationToken = default);

    /// <summary>
    /// Modo alternativo: transcreve um trecho curto de audio em memoria e o
    /// descarta. Valor nulo quando o trecho nao tinha fala.
    /// </summary>
    Task<ResultadoOperacao<TrechoRegistradoDto>> TranscreverTrechoAsync(
        Guid atendimentoId, Guid clinicaId, int sequencia, Stream audio, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refaz o rascunho do prontuario sobre tudo o que foi dito ate agora. Usado
    /// durante a consulta (preenchimento progressivo), ao encerrar e para tentar
    /// de novo quando a IA falhou.
    /// </summary>
    Task<ResultadoOperacao<RascunhoClinicoDto>> GerarRascunhoAsync(
        Guid atendimentoId, Guid clinicaId, CancellationToken cancellationToken = default);

    /// <summary>Fecha a escuta e leva o atendimento a revisao do medico.</summary>
    Task<ResultadoAtendimento> EncerrarAsync(
        Guid atendimentoId, Guid clinicaId, int duracaoSegundos, CancellationToken cancellationToken = default);
}
