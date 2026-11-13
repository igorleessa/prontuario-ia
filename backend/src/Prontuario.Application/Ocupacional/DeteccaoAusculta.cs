using System.Text.RegularExpressions;

namespace Prontuario.Application.Ocupacional;

/// <summary>
/// Reconhece, na fala, que a ausculta foi feita. E a rede de seguranca local e
/// sem custo da deteccao feita pela IA: frase com negacao ("nao auscultei o
/// pulmao") nao conta.
/// </summary>
public static partial class DeteccaoAusculta
{
    public static bool Cardiaca(string fala) => Afirmada(fala, AuscultaCardiaca());

    public static bool Pulmonar(string fala) => Afirmada(fala, AuscultaPulmonar());

    private static bool Afirmada(string fala, Regex padrao)
        => Frase().Split(fala).Any(frase =>
            padrao.Matches(frase).Any(achado => !NegacaoAntes().IsMatch(frase[..achado.Index])));

    [GeneratedRegex(@"ausculta\s+card[ií]aca|auscult\w*\s+(o\s+)?cora[cç][aã]o|bulhas|estetosc[oó]pio", RegexOptions.IgnoreCase)]
    private static partial Regex AuscultaCardiaca();

    [GeneratedRegex(@"ausculta\s+(card[ií]aca\s+e\s+)?pulmonar|murm[uú]rio\s+vesicular|auscult\w*\s+(o\s+)?pulm[aã]o", RegexOptions.IgnoreCase)]
    private static partial Regex AuscultaPulmonar();

    /// <summary>
    /// A negacao so vale logo antes do termo, na mesma oracao: "bulhas
    /// normofoneticas, sem sopros" e ausculta feita.
    /// </summary>
    [GeneratedRegex(@"\b(n[aã]o|sem|nem|faltou|falta)\b[^,]{0,25}$", RegexOptions.IgnoreCase)]
    private static partial Regex NegacaoAntes();

    [GeneratedRegex(@"[.!?;]+")]
    private static partial Regex Frase();
}
