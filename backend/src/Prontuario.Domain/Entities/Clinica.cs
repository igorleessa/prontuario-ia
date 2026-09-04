using Prontuario.Domain.Common;
using Prontuario.Domain.Enums;

namespace Prontuario.Domain.Entities;

public class Clinica : BaseEntity
{
    public string Nome { get; set; } = string.Empty;
    public ModoOperacao ModoOperacao { get; set; } = ModoOperacao.Integrado;

    // Configuracao de exportacao - relevante apenas para ModoOperacao.Conector (RF19).
    public string? WebhookUrl { get; set; }
    public string? WebhookSecret { get; set; }

    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
    public ICollection<PacienteRef> Pacientes { get; set; } = new List<PacienteRef>();
}
