using Prontuario.Domain.Common;

namespace Prontuario.Domain.Entities;

/// <summary>
/// Prontuario do exame ocupacional de um atendimento: respostas das 9 secoes,
/// de onde veio cada uma (IA ou medico) e o que o medico justificou. Da conversa
/// so ficam sinais (ausculta identificada ou nao) - nunca audio nem texto.
/// </summary>
public class ProntuarioOcupacional : BaseEntity
{
    public Guid AtendimentoId { get; set; }
    public Atendimento? Atendimento { get; set; }

    /// <summary>"Tradicional", "Guiada" ou "Escriba": a forma escolhida pelo medico.</summary>
    public string Modo { get; set; } = string.Empty;

    /// <summary>"F" ou "M": define a secao de saude e as faixas por sexo.</summary>
    public string Sexo { get; set; } = string.Empty;

    /// <summary>JSON: campo -> { valor, origem ("ia", "medico", "tradicional"), em }.</summary>
    public string RespostasJson { get; set; } = "{}";

    /// <summary>JSON: chave do alerta -> justificativa do medico.</summary>
    public string JustificativasJson { get; set; } = "{}";

    public bool AuscultaCardiaca { get; set; }
    public bool AuscultaPulmonar { get; set; }

    public DateTime? ConcluidoEm { get; set; }
}
