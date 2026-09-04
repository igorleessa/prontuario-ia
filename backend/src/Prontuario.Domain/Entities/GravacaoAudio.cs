using Prontuario.Domain.Common;

namespace Prontuario.Domain.Entities;

public class GravacaoAudio : BaseEntity
{
    public Guid AtendimentoId { get; set; }
    public Atendimento? Atendimento { get; set; }

    /// <summary>Caminho/chave no object storage (MinIO/S3) - o audio nao fica no Postgres.</summary>
    public string StoragePath { get; set; } = string.Empty;
    public int DuracaoSegundos { get; set; }

    public Transcricao? Transcricao { get; set; }
}
