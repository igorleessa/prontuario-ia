using Prontuario.Domain.Common;

namespace Prontuario.Domain.Entities;

/// <summary>
/// Sugestao gerada pela IA a partir da transcricao (RF08). Nunca e gravada
/// diretamente em ProntuarioRegistro/NotaExportavel - a copia so ocorre
/// apos revisao do medico (RF10).
/// </summary>
public class RascunhoIA : BaseEntity
{
    public Guid TranscricaoId { get; set; }
    public Transcricao? Transcricao { get; set; }

    public string? QueixaPrincipal { get; set; }
    public string? Hda { get; set; }
    public string? Antecedentes { get; set; }
    public string? ExameFisico { get; set; }
    public string? HipoteseDiagnostica { get; set; }
    public string? Cid10Sugerido { get; set; }
    public string? Conduta { get; set; }
}
