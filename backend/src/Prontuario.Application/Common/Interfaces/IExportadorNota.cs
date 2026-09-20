using Prontuario.Application.Common.Models;

namespace Prontuario.Application.Common.Interfaces;

/// <summary>
/// Envia a nota revisada ao EMR de destino (RF19). O envio e assinado com o
/// segredo da clinica (RNF10) e nunca acontece sozinho: parte sempre de uma
/// confirmacao do medico ou de um pedido explicito de reenvio.
/// </summary>
public interface IExportadorNota
{
    /// <summary>Envia a nota do atendimento. Devolve o resultado sem lancar - a falha e informacao para o medico, nao erro de servidor.</summary>
    Task<ResultadoExportacaoDto> EnviarAsync(Guid atendimentoId, CancellationToken cancellationToken = default);

    /// <summary>Dispara um evento de teste contra o webhook configurado, sem tocar em dado clinico real.</summary>
    Task<ResultadoExportacaoDto> TestarAsync(Guid clinicaId, CancellationToken cancellationToken = default);
}
