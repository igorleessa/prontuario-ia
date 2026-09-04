using Prontuario.Domain.Common;
using Prontuario.Domain.Enums;

namespace Prontuario.Domain.Entities;

public class Atendimento : BaseEntity
{
    public Guid PacienteRefId { get; set; }
    public PacienteRef? PacienteRef { get; set; }

    public Guid MedicoId { get; set; }
    public Usuario? Medico { get; set; }

    public DateTime DataHora { get; set; } = DateTime.UtcNow;
    public StatusAtendimento Status { get; set; } = StatusAtendimento.AguardandoConsentimento;

    public bool ConsentimentoGravacao { get; set; }
    public DateTime? ConsentimentoEm { get; set; }

    public GravacaoAudio? GravacaoAudio { get; set; }
    public ProntuarioRegistro? Prontuario { get; set; }
    public NotaExportavel? NotaExportavel { get; set; }
    public ICollection<LogAuditoria> LogsAuditoria { get; set; } = new List<LogAuditoria>();
}
