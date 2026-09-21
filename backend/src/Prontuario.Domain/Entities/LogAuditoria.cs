using Prontuario.Domain.Common;

namespace Prontuario.Domain.Entities;

/// <summary>
/// Trilha de auditoria imutavel de acesso a dado clinico (RF11/RNF06).
/// A aplicacao so insere: nao ha rota que altere ou apague uma entrada.
/// </summary>
public class LogAuditoria : BaseEntity
{
    public Guid UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    /// <summary>
    /// Nulo em acoes que nao sao sobre um atendimento - troca de credencial de
    /// IA, alteracao do destino de exportacao, expurgo de audio.
    /// </summary>
    public Guid? AtendimentoId { get; set; }
    public Atendimento? Atendimento { get; set; }

    public string Acao { get; set; } = string.Empty;

    /// <summary>Contexto curto da acao, quando ele ajuda a entender a entrada sem abrir o atendimento.</summary>
    public string? Detalhe { get; set; }
}
