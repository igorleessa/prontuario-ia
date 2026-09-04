using Prontuario.Domain.Common;
using Prontuario.Domain.Enums;

namespace Prontuario.Domain.Entities;

/// <summary>
/// Nota clinica exportavel - usada apenas na Modalidade B (Conector).
/// O registro legal definitivo passa a ser responsabilidade do EMR de
/// destino a partir da exportacao. Ver RF17-RF20.
/// </summary>
public class NotaExportavel : BaseEntity
{
    public Guid AtendimentoId { get; set; }
    public Atendimento? Atendimento { get; set; }

    public string ConteudoFormatado { get; set; } = string.Empty;
    public StatusNota Status { get; set; } = StatusNota.Rascunho;

    public DateTime? ExportadoEm { get; set; }
    public string? Destino { get; set; }
}
