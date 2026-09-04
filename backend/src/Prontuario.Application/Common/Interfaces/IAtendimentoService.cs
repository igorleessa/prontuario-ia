using Prontuario.Application.Common.Models;

namespace Prontuario.Application.Common.Interfaces;

/// <summary>
/// Todas as operacoes recebem a clinica do usuario logado e so enxergam
/// atendimentos dela: o identificador vindo da URL nunca e suficiente para
/// alcancar o dado de outra clinica.
/// </summary>
public interface IAtendimentoService
{
    /// <summary>Abre um atendimento vinculado a um paciente e ao medico logado (RF04). Null se o paciente nao e da clinica.</summary>
    Task<Guid?> AbrirAsync(Guid pacienteRefId, Guid medicoId, Guid clinicaId, CancellationToken cancellationToken = default);

    /// <summary>Lista os atendimentos da clinica, do mais recente para o mais antigo.</summary>
    Task<IReadOnlyList<AtendimentoResumoDto>> ListarAsync(Guid clinicaId, CancellationToken cancellationToken = default);

    Task<AtendimentoDetalheDto?> ObterAsync(Guid atendimentoId, Guid clinicaId, CancellationToken cancellationToken = default);

    /// <summary>Registra a aceitacao do consentimento antes de permitir a gravacao (RF05).</summary>
    Task<bool> RegistrarConsentimentoAsync(Guid atendimentoId, Guid clinicaId, CancellationToken cancellationToken = default);

    /// <summary>Marca o fim da captura de audio e a entrada na revisao do medico (RF09).</summary>
    Task<bool> IniciarRevisaoAsync(Guid atendimentoId, Guid clinicaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarda o audio da consulta (RF06) e enfileira a transcricao e a extracao
    /// estruturada (RF07/RF08). O atendimento passa a ProcessandoIA.
    /// </summary>
    Task<bool> RegistrarAudioAsync(
        Guid atendimentoId, Guid clinicaId, Stream audio, string tipoConteudo, int duracaoSegundos,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirma a revisao do medico (RF10) e delega a persistencia para
    /// IRegistroClinicoOutput, que escolhe Modalidade A ou B conforme a clinica.
    /// </summary>
    Task<bool> ConfirmarAsync(Guid atendimentoId, RascunhoClinicoDto revisado, Guid clinicaId, CancellationToken cancellationToken = default);

    Task<bool> CancelarAsync(Guid atendimentoId, Guid clinicaId, CancellationToken cancellationToken = default);
}
