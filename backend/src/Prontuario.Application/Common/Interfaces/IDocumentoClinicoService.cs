using Prontuario.Application.Common.Models;

namespace Prontuario.Application.Common.Interfaces;

/// <summary>
/// Documentos auxiliares da consulta (receita, pedido de exame, atestado,
/// encaminhamento). Sempre gerados sob pedido do medico e sempre editaveis por
/// ele antes de valerem.
/// </summary>
public interface IDocumentoClinicoService
{
    Task<IReadOnlyList<DocumentoClinicoDto>> ListarAsync(
        Guid atendimentoId, Guid clinicaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Redige o documento a partir da transcricao e do registro clinico ja
    /// revisado. Regerar substitui a versao anterior do mesmo tipo.
    /// </summary>
    Task<DocumentoClinicoDto?> GerarAsync(
        Guid atendimentoId, Guid clinicaId, string tipo, CancellationToken cancellationToken = default);

    /// <summary>Grava a versao revisada pelo medico, que passa a ser o conteudo oficial do documento.</summary>
    Task<DocumentoClinicoDto?> SalvarAsync(
        Guid atendimentoId, Guid clinicaId, Guid documentoId, string conteudo, CancellationToken cancellationToken = default);
}
