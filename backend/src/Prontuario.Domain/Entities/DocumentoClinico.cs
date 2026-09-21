using Prontuario.Domain.Common;
using Prontuario.Domain.Enums;

namespace Prontuario.Domain.Entities;

/// <summary>
/// Receita, pedido de exame, atestado ou encaminhamento redigido a partir da
/// consulta. Fica sempre vinculado ao atendimento que o originou, e o texto
/// guardado e o que o medico revisou - nao o que a IA sugeriu.
/// </summary>
public class DocumentoClinico : BaseEntity
{
    public Guid AtendimentoId { get; set; }
    public Atendimento? Atendimento { get; set; }

    public TipoDocumento Tipo { get; set; }
    public string Conteudo { get; set; } = string.Empty;

    /// <summary>Quem pediu a geracao; a revisao posterior nao troca a autoria do documento.</summary>
    public Guid? GeradoPorId { get; set; }
}
