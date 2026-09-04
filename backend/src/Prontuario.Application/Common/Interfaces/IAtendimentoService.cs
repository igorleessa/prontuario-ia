using Prontuario.Application.Common.Models;

namespace Prontuario.Application.Common.Interfaces;

public interface IAtendimentoService
{
    /// <summary>Abre um atendimento vinculado a um paciente e ao medico logado (RF04).</summary>
    Task<Guid> AbrirAsync(Guid pacienteRefId, Guid medicoId, CancellationToken cancellationToken = default);

    /// <summary>Registra a aceitacao do consentimento antes de permitir a gravacao (RF05).</summary>
    Task RegistrarConsentimentoAsync(Guid atendimentoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirma a revisao do medico (RF10) e delega a persistencia para
    /// IRegistroClinicoOutput, que escolhe Modalidade A ou B conforme a clinica.
    /// </summary>
    Task ConfirmarAsync(Guid atendimentoId, RascunhoClinicoDto revisado, CancellationToken cancellationToken = default);
}
