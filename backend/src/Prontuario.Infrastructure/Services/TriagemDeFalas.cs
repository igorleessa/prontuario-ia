using System.Text.RegularExpressions;

namespace Prontuario.Infrastructure.Services;

/// <summary>
/// Triagem local, sem custo, antes de chamar a IA durante a consulta: falas que
/// sao so confirmacao ou cumprimento ("uhum", "ta bom", "bom dia") nao mudam o
/// prontuario e nao justificam uma chamada. Elas nao sao descartadas: ficam
/// aguardando e seguem junto com a proxima fala que tiver conteudo.
/// </summary>
public static partial class TriagemDeFalas
{
    private static readonly HashSet<string> SemConteudo = new(StringComparer.OrdinalIgnoreCase)
    {
        "uhum", "aham", "hum", "hm", "hmm", "ah", "eh", "é", "ó", "tá", "ta", "ok", "okay", "certo", "isso",
        "bom", "boa", "bem", "dia", "tarde", "noite", "oi", "olá", "ola", "então", "entao", "né", "ne",
        "pois", "entendi", "sei", "perfeito", "beleza", "tudo", "obrigado", "obrigada", "tchau", "licença",
        "licenca", "com", "de", "nada", "pode", "claro", "a", "o", "e", "um", "uma",
    };

    /// <summary>
    /// Basta uma palavra fora da lista: "nao" e "sim" sao respostas, e numero
    /// e sempre conteudo ("120 por 80"). Na duvida, a fala vai a IA.
    /// </summary>
    public static bool TemConteudo(IEnumerable<string> falas)
        => falas.Any(fala => Palavra().Matches(fala).Any(palavra => !SemConteudo.Contains(palavra.Value)));

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex Palavra();
}
