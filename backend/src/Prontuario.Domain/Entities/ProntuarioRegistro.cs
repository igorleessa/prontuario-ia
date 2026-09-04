using Prontuario.Domain.Common;

namespace Prontuario.Domain.Entities;

/// <summary>
/// Prontuario nativo - usado apenas na Modalidade A (Integrado). Ver RF15/RF16.
/// Somente leitura apos Finalizado = true.
/// </summary>
public class ProntuarioRegistro : BaseEntity
{
    public Guid AtendimentoId { get; set; }
    public Atendimento? Atendimento { get; set; }

    public string? QueixaPrincipal { get; set; }
    public string? Hda { get; set; }
    public string? Antecedentes { get; set; }
    public string? ExameFisico { get; set; }
    public string? HipoteseDiagnostica { get; set; }
    public string? Cid10 { get; set; }
    public string? Conduta { get; set; }

    public bool Finalizado { get; set; }
    public DateTime? AssinadoEm { get; set; }
    public Guid? AssinadoPorId { get; set; }
}
