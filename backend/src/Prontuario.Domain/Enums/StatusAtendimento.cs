namespace Prontuario.Domain.Enums;

public enum StatusAtendimento
{
    AguardandoConsentimento = 1,

    /// <summary>Consulta em andamento, com a transcricao ao vivo preenchendo o prontuario.</summary>
    EmConsulta = 2,

    EmRevisao = 4,
    Finalizado = 5,
    Cancelado = 6
}
