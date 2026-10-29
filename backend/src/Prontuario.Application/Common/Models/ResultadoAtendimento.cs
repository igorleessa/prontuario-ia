namespace Prontuario.Application.Common.Models;

/// <summary>
/// Desfecho de uma operacao sobre um atendimento. Separa "nao existe nesta
/// clinica" de "existe, mas o estado nao permite" - a segunda situacao precisa
/// de uma mensagem propria, porque e o que protege o registro ja assinado.
/// </summary>
public enum ResultadoAtendimento
{
    Ok = 1,
    NaoEncontrado = 2,
    Conflito = 3,

    /// <summary>O provedor externo (IA) falhou; o atendimento segue e pode ser documentado manualmente.</summary>
    Falha = 4
}
