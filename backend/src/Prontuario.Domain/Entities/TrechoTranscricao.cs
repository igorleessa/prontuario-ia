using Prontuario.Domain.Common;

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
}
