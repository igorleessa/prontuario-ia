namespace Prontuario.Application.Common.Interfaces;

/// <summary>
/// Enfileira atendimentos para o worker de IA. A implementacao atual e em
/// memoria: um reinicio do processo perde a fila, e o atendimento fica em
/// ProcessandoIA ate o medico optar por preencher manualmente. Trocar por
/// Hangfire quando a durabilidade da fila passar a importar.
/// </summary>
public interface IFilaProcessamentoIA
{
    ValueTask EnfileirarAsync(Guid atendimentoId, CancellationToken cancellationToken = default);

    ValueTask<Guid> ProximoAsync(CancellationToken cancellationToken);
}
