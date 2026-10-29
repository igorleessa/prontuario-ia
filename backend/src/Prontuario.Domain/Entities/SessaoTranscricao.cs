using Prontuario.Domain.Common;

namespace Prontuario.Domain.Entities;

/// <summary>
/// Periodo em que a consulta foi ouvida e transcrita. Nao existe gravacao: o
/// audio vira texto enquanto a consulta acontece e e descartado em seguida, de
/// modo que o atendimento guarda apenas quando a escuta comecou e terminou.
/// </summary>
public class SessaoTranscricao : BaseEntity
{
    public Guid AtendimentoId { get; set; }
    public Atendimento? Atendimento { get; set; }

    public DateTime IniciadaEm { get; set; } = DateTime.UtcNow;
    public DateTime? EncerradaEm { get; set; }

    /// <summary>Tempo efetivo de escuta, sem as pausas pedidas pelo medico.</summary>
    public int DuracaoSegundos { get; set; }

    public Transcricao? Transcricao { get; set; }
}
