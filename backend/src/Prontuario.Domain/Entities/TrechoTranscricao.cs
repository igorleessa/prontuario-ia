using Prontuario.Domain.Common;
using Prontuario.Domain.Enums;

namespace Prontuario.Domain.Entities;

/// <summary>
/// Um trecho de fala ja transcrito e sanitizado. A sequencia vem do navegador,
/// na ordem em que a fala aconteceu: reenviar o mesmo trecho (rede instavel)
/// substitui em vez de duplicar, e um trecho que chega atrasado entra no lugar certo.
/// </summary>
public class TrechoTranscricao : BaseEntity
{
    public Guid TranscricaoId { get; set; }
    public Transcricao? Transcricao { get; set; }

    public int Sequencia { get; set; }
    public string Texto { get; set; } = string.Empty;

    /// <summary>
    /// O reconhecimento estatistico de nomes ja passou por este trecho. Falso
    /// quando o servico estava fora do ar ao guardar a fala: o trecho so vai
    /// para a IA depois de passar por ele.
    /// </summary>
    public bool ReconhecimentoAplicado { get; set; }

    /// <summary>Quem disse o trecho, segundo a IA ou o medico.</summary>
    public Falante Falante { get; set; } = Falante.Indefinido;

    /// <summary>O medico definiu o falante: a IA nao o muda mais.</summary>
    public bool FalanteConfirmado { get; set; }
}
