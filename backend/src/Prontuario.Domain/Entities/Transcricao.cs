using Prontuario.Domain.Common;
using Prontuario.Domain.Enums;

namespace Prontuario.Domain.Entities;

/// <summary>
/// Texto da consulta, ja sanitizado: dados pessoais ficam trocados por
/// marcadores como [PACIENTE] e [CPF] antes de qualquer gravacao no banco.
/// </summary>
public class Transcricao : BaseEntity
{
    public Guid SessaoTranscricaoId { get; set; }
    public SessaoTranscricao? SessaoTranscricao { get; set; }

    /// <summary>Concatenacao dos trechos na ordem da fala. Mantida a cada trecho recebido.</summary>
    public string Texto { get; set; } = string.Empty;
    public StatusProcessamento Status { get; set; } = StatusProcessamento.Pendente;

    /// <summary>
    /// Falso apenas em transcricoes anteriores a camada de sanitizacao, que a
    /// inicializacao do banco sanitiza uma vez.
    /// </summary>
    public bool Sanitizada { get; set; } = true;

    public ICollection<TrechoTranscricao> Trechos { get; set; } = new List<TrechoTranscricao>();

    public RascunhoIA? RascunhoIA { get; set; }
}
