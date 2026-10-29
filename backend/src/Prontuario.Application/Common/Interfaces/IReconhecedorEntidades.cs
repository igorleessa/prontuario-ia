namespace Prontuario.Application.Common.Interfaces;

/// <summary>
/// Reconhecedor estatistico de entidades (pessoas, lugares, organizacoes) que
/// roda dentro da infraestrutura da clinica. Aponta o que as listas e os
/// padroes da sanitizacao nao conhecem; quem decide o que vira marcador e o
/// <see cref="ISanitizadorDados"/>, com o filtro clinico.
/// </summary>
public interface IReconhecedorEntidades
{
    /// <summary>Falso quando o ambiente nao tem o servico configurado.</summary>
    bool Habilitado { get; }

    /// <summary>Entidades das categorias habilitadas. Lanca se o servico estiver fora do ar.</summary>
    Task<IReadOnlyList<EntidadeReconhecida>> ReconhecerAsync(string texto, CancellationToken cancellationToken = default);
}

/// <summary>
/// Entidade apontada pelo reconhecedor. <paramref name="Classes"/> e a classe
/// gramatical de cada palavra (PROPN, NOUN, VERB...), usada para descartar
/// falsos positivos como "Moro em Niteroi".
/// </summary>
public sealed record EntidadeReconhecida(
    int Inicio,
    int Fim,
    string Rotulo,
    string Texto,
    IReadOnlyList<string> Classes,
    bool InicioDeFrase);

/// <summary>
/// O reconhecimento de nomes esta habilitado e nao respondeu. Quem chama nao
/// pode mandar o texto a um provedor externo: a protecao estaria incompleta.
/// </summary>
public sealed class ProtecaoIndisponivelException(string mensagem, Exception? causa = null)
    : Exception(mensagem, causa);
