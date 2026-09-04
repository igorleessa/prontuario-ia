using Prontuario.Domain.Common;
using Prontuario.Domain.Enums;

namespace Prontuario.Domain.Entities;

public class Transcricao : BaseEntity
{
    public Guid GravacaoAudioId { get; set; }
    public GravacaoAudio? GravacaoAudio { get; set; }

    public string Texto { get; set; } = string.Empty;
    public StatusProcessamento Status { get; set; } = StatusProcessamento.Pendente;

    public RascunhoIA? RascunhoIA { get; set; }
}
