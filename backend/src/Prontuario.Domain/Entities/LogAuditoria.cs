using Prontuario.Domain.Common;

namespace Prontuario.Domain.Entities;

/// <summary>Trilha de auditoria imutavel de acesso a dado clinico (RF11/RNF06).</summary>
public class LogAuditoria : BaseEntity
{
    public Guid UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public Guid AtendimentoId { get; set; }
    public Atendimento? Atendimento { get; set; }

    public string Acao { get; set; } = string.Empty;
}
