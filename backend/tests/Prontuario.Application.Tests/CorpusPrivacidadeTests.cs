using Prontuario.Application.Privacidade;

namespace Prontuario.Application.Tests;

/// <summary>
/// Base de teste da sanitizacao: trechos de consulta ficticios, cada um com o
/// que nao pode sobreviver (vazamento) e o que nao pode sumir (dano clinico).
/// Um caso novo de vazamento encontrado em uso deve virar uma linha aqui.
/// </summary>
public class CorpusPrivacidadeTests
{
    internal static readonly IdentidadeAtendimento Paciente = new(
        "Maria Aparecida dos Santos", "390.533.447-05", new DateOnly(1958, 9, 4), "maria.santos@email.com", "Dr. Paulo Henrique Costa");

    public static TheoryData<string, string[], string[]> Casos() => new()
    {
        {
            "Bom dia, dona Maria Aparecida. O senhor aqui é seu marido? Sim, sou o Sebastião.",
            ["Maria", "Aparecida", "Sebastião"],
            ["Bom dia", "marido"]
        },
        {
            "Meu CPF é 390.533.447-05 e nasci em 04/09/1958. O e-mail é maria.santos@email.com.",
            ["390.533.447-05", "04/09/1958", "maria.santos@email.com"],
            ["CPF", "e-mail"]
        },
        {
            "Pode ligar no (21) 99654-1122 ou falar com a minha nora Patrícia no 21 3344-5566.",
            ["99654-1122", "Patrícia", "3344-5566"],
            ["ligar"]
        },
        {
            "Moro na Rua Barão de Mesquita, 512, apto 301, Tijuca. CEP 20540-002.",
            ["Barão de Mesquita", "512", "20540-002"],
            ["Moro"]
        },
        {
            "O cartão do SUS é 898 0010 0000 0001 2 e o convênio é 0012345678901.",
            ["898 0010 0000 0001 2", "0012345678901"],
            ["cartão do SUS", "convênio"]
        },
        {
            "Quem me trouxe foi a Juliana Ribeiro, vizinha. O Dr. Paulo Henrique pediu os exames.",
            ["Juliana", "Ribeiro", "Paulo Henrique"],
            ["vizinha", "pediu os exames"]
        },
        {
            "Uso losartana 50 mg, metformina 850 mg 2x ao dia e AAS 100 mg. Pressão 140 por 90, glicemia 186.",
            [],
            ["losartana 50 mg", "metformina 850 mg", "AAS 100 mg", "140 por 90", "glicemia 186"]
        },
        {
            "Tem doença de Chagas? Não. Sinal de Murphy negativo, Giordano negativo. Escala de Glasgow 15.",
            [],
            ["Chagas", "Murphy", "Giordano", "Glasgow 15"]
        },
        {
            "Benzetacil 1.200.000 UI dose única. Plaquetas 180.000, hemoglobina 11,2, HbA1c 8,1%.",
            [],
            ["1.200.000 UI", "180.000", "11,2", "8,1%"]
        },
        {
            "Clara melhora da dispneia. Dores articulares esparsas. Exame em 12/08/2026.",
            [],
            ["Clara melhora", "Dores articulares", "12/08/2026"]
        },
    };

    [Theory]
    [MemberData(nameof(Casos))]
    public void Corpus_NaoVazaDadoPessoalNemApagaConteudoClinico(string texto, string[] vazamentos, string[] clinico)
    {
        var resultado = new SanitizadorDados().Sanitizar(texto, Paciente).Valor;

        foreach (var dado in vazamentos)
        {
            Assert.False(resultado.Contains(dado, StringComparison.OrdinalIgnoreCase),
                $"Vazou \"{dado}\" em: {resultado}");
        }

        foreach (var trecho in clinico)
        {
            Assert.True(resultado.Contains(trecho, StringComparison.Ordinal),
                $"Sumiu \"{trecho}\" em: {resultado}");
        }
    }
}
