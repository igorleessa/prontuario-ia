using Prontuario.Domain.Common;
using Prontuario.Domain.Enums;

namespace Prontuario.Domain.Entities;

public class Atendimento : BaseEntity
{
    public Guid PacienteRefId { get; set; }
    public PacienteRef? PacienteRef { get; set; }

    public Guid MedicoId { get; set; }
    public Usuario? Medico { get; set; }

    /// <summary>Template de especialidade escolhido na abertura; nulo usa o modelo generico (SOAP).</summary>
    public Guid? TemplateNotaId { get; set; }
    public TemplateNota? TemplateNota { get; set; }

    public DateTime DataHora { get; set; } = DateTime.UtcNow;
    public StatusAtendimento Status { get; set; } = StatusAtendimento.AguardandoConsentimento;

    /// <summary>Aceite do paciente para a transcricao da consulta (sem gravacao de audio).</summary>
    public bool ConsentimentoTranscricao { get; set; }
    public DateTime? ConsentimentoEm { get; set; }

    /// <summary>Motivo da falha do pipeline de IA, mostrado ao medico para que ele saiba por que precisa preencher manualmente.</summary>
    public string? ErroProcessamentoIA { get; set; }

    public SessaoTranscricao? SessaoTranscricao { get; set; }
    public ProntuarioRegistro? Prontuario { get; set; }
    public NotaExportavel? NotaExportavel { get; set; }
    public ICollection<LogAuditoria> LogsAuditoria { get; set; } = new List<LogAuditoria>();
    public ICollection<DocumentoClinico> Documentos { get; set; } = new List<DocumentoClinico>();
}
