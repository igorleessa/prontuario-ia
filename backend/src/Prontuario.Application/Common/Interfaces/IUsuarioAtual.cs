namespace Prontuario.Application.Common.Interfaces;

/// <summary>
/// Identidade de quem esta fazendo a requisicao, lida do JWT. Existe para que
/// os servicos possam auditar autoria sem receber o usuario em cada assinatura
/// de metodo. Fora de uma requisicao (worker de IA, por exemplo) devolve null.
/// </summary>
public interface IUsuarioAtual
{
    Guid? Id { get; }
    Guid? ClinicaId { get; }
    string? Papel { get; }
}
