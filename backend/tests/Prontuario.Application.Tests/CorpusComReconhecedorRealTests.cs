using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Prontuario.Application.Privacidade;
using Prontuario.Infrastructure.Services;

namespace Prontuario.Application.Tests;

/// <summary>
/// O corpus de privacidade passando tambem pelo reconhecedor de verdade
/// (servicos/privacidade-ner). Roda quando NER_URL aponta para o servico -
/// localmente ou no CI - e nao faz nada sem ele, porque depende do modelo.
/// </summary>
public class CorpusComReconhecedorRealTests
{
    private static readonly string? Url = Environment.GetEnvironmentVariable("NER_URL");

    [Theory]
    [MemberData(nameof(CorpusPrivacidadeTests.Casos), MemberType = typeof(CorpusPrivacidadeTests))]
    public async Task Corpus_ComReconhecedorReal_NaoVazaNemApagaConteudoClinico(
        string texto, string[] vazamentos, string[] clinico)
    {
        if (string.IsNullOrWhiteSpace(Url))
        {
            return;
        }

        var opcoes = Options.Create(new ReconhecimentoEntidadesOptions { Url = Url, Categorias = "PER" });
        var provedor = new ServiceCollection()
            .AddHttpClient(ReconhecedorEntidadesSpacy.NomeCliente, cliente => cliente.BaseAddress = new Uri(Url.TrimEnd('/') + "/"))
            .Services.BuildServiceProvider();
        var sanitizador = new SanitizadorDados(
            new ReconhecedorEntidadesSpacy(provedor.GetRequiredService<IHttpClientFactory>(), opcoes));

        var resultado = (await sanitizador.SanitizarAsync(texto, CorpusPrivacidadeTests.Paciente)).Valor;

        foreach (var dado in vazamentos)
        {
            Assert.False(resultado.Contains(dado, StringComparison.OrdinalIgnoreCase), $"Vazou \"{dado}\" em: {resultado}");
        }

        foreach (var trecho in clinico)
        {
            Assert.True(resultado.Contains(trecho, StringComparison.Ordinal), $"Sumiu \"{trecho}\" em: {resultado}");
        }
    }
}
