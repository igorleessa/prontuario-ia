using Prontuario.Application.Common.Models;

namespace Prontuario.Application.Common.Interfaces;

/// <summary>
/// Camada de saida desacoplada do core de IA (ver docs/plano-desenvolvimento.md,
/// secao "Arquitetura de alto nivel"). A implementacao usada por atendimento e
/// escolhida em tempo de execucao a partir do ModoOperacao da clinica:
/// - Modalidade A (Integrado): grava no ProntuarioRegistro nativo.
/// - Modalidade B (Conector): gera uma NotaExportavel (copiar/PDF/webhook).
/// </summary>
public interface IRegistroClinicoOutput
{
    Task ConfirmarAsync(Guid atendimentoId, RascunhoClinicoDto revisado, CancellationToken cancellationToken = default);
}
