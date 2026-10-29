using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Privacidade;

namespace Prontuario.Application.Tests;

/// <summary>
/// Camada estatistica da sanitizacao. O reconhecedor e simulado com as
/// respostas que o modelo de portugues do spaCy realmente da para estas
/// frases (inclusive os erros dele) - o que se testa e o filtro clinico.
/// </summary>
public class ReconhecimentoEntidadesTests
{
    private static readonly IdentidadeAtendimento Identidade = new("Helena Souza", null, null, null, "Dra. Ana Ribeiro");

    private sealed class ReconhecedorFixo(params EntidadeReconhecida[] entidades) : IReconhecedorEntidades
    {
        public bool Habilitado => true;
        public int Chamadas { get; private set; }

        public Task<IReadOnlyList<EntidadeReconhecida>> ReconhecerAsync(string texto, CancellationToken cancellationToken = default)
        {
            Chamadas++;
            return Task.FromResult<IReadOnlyList<EntidadeReconhecida>>(
                entidades.Where(e => e.Fim <= texto.Length).ToList());
        }
    }

    private sealed class ReconhecedorForaDoAr : IReconhecedorEntidades
    {
        public bool Habilitado => true;

        public Task<IReadOnlyList<EntidadeReconhecida>> ReconhecerAsync(string texto, CancellationToken cancellationToken = default)
            => throw new HttpRequestException("connection refused");
    }

    private static EntidadeReconhecida Em(string texto, string trecho, string rotulo, string[] classes, bool inicioDeFrase = false)
    {
        var inicio = texto.IndexOf(trecho, StringComparison.Ordinal);
        return new EntidadeReconhecida(inicio, inicio + trecho.Length, rotulo, trecho, classes, inicioDeFrase);
    }

    private static async Task<string> Sanitizar(string texto, params EntidadeReconhecida[] entidades)
        => (await new SanitizadorDados(new ReconhecedorFixo(entidades)).SanitizarAsync(texto, Identidade)).Valor;

    [Fact]
    public async Task NomeQueNenhumaListaConhece_ViraMarcador()
    {
        const string texto = "Quem me trouxe foi o Gervásio, meu cunhado.";

        var resultado = await Sanitizar(texto, Em(texto, "Gervásio", "PER", ["PROPN"]));

        Assert.Equal("Quem me trouxe foi o [PESSOA], meu cunhado.", resultado);
    }

    [Fact]
    public async Task TituloAntesDoNome_Permanece()
    {
        const string texto = "O Dr. Ubirajara pediu ressonância.";

        var resultado = await Sanitizar(texto, Em(texto, "Dr. Ubirajara", "PER", ["NOUN", "PROPN"]));

        Assert.Equal("O Dr. [PESSOA] pediu ressonância.", resultado);
    }

    [Fact]
    public async Task VerboConfundidoComNome_NaoEhApagado()
    {
        const string texto = "Moro em Niterói há dez anos.";

        var resultado = await Sanitizar(texto, Em(texto, "Moro", "PER", ["VERB"], inicioDeFrase: true));

        Assert.Equal(texto, resultado);
    }

    [Theory]
    [InlineData("Tem doença de Parkinson controlada.", "Parkinson")]
    [InlineData("Sinal de Murphy negativo.", "Murphy")]
    [InlineData("Avaliar critérios de Duke para endocardite.", "Duke")]
    public async Task Eponimo_NaoEhApagado(string texto, string eponimo)
        => Assert.Equal(texto, await Sanitizar(texto, Em(texto, eponimo, "PER", ["PROPN"])));

    [Fact]
    public async Task PalavraComumNoInicioDaFrase_NaoEhApagada()
    {
        const string texto = "Dores nas costas há 3 dias. Clara melhora com dipirona.";

        var resultado = await Sanitizar(
            texto,
            Em(texto, "Dores", "PER", ["PROPN"], inicioDeFrase: true),
            Em(texto, "Clara", "PER", ["PROPN"], inicioDeFrase: true));

        Assert.Equal(texto, resultado);
    }

    [Fact]
    public async Task MarcadorJaAplicado_NaoEhTocadoDeNovo()
    {
        // O modelo enxerga "[PESSOA]" e devolve "PESSOA" como pessoa.
        const string texto = "A Mariana trouxe os exames.";
        var basico = new SanitizadorDados().Sanitizar(texto, Identidade).Valor;

        var resultado = await Sanitizar(texto, Em(basico, "PESSOA", "PER", ["PROPN"]));

        Assert.Equal("A [PESSOA] trouxe os exames.", resultado);
    }

    [Fact]
    public async Task LocalEOrganizacao_ViramMarcadorProprio()
    {
        const string texto = "Trabalha na Construtora Ipê e mora em Itaboraí.";

        var resultado = await Sanitizar(
            texto,
            Em(texto, "Construtora Ipê", "ORG", ["PROPN", "PROPN"]),
            Em(texto, "Itaboraí", "LOC", ["PROPN"]));

        Assert.Equal("Trabalha na [ORGANIZACAO] e mora em [LOCAL].", resultado);
    }

    [Fact]
    public async Task PosicaoDesalinhada_EhCorrigidaPeloTexto()
    {
        // O servico conta caracteres Unicode; um emoji antes desloca a posicao em .NET.
        const string texto = "👍 Quem veio foi o Gervásio.";
        var desalinhada = new EntidadeReconhecida(texto.IndexOf("Gervásio") - 1, texto.IndexOf("Gervásio") + 7, "PER", "Gervásio", ["PROPN"], false);

        var resultado = await Sanitizar(texto, desalinhada);

        Assert.Equal("👍 Quem veio foi o [PESSOA].", resultado);
    }

    [Fact]
    public async Task ReconhecedorForaDoAr_LancaProtecaoIndisponivel()
    {
        var sanitizador = new SanitizadorDados(new ReconhecedorForaDoAr());

        await Assert.ThrowsAsync<ProtecaoIndisponivelException>(
            () => sanitizador.SanitizarAsync("Quem veio foi o Gervásio.", Identidade));
    }

    [Fact]
    public async Task SemReconhecedor_SanitizarAsyncEhOMesmoQueOSincrono()
    {
        const string texto = "A Mariana ligou do 21 99876-5544.";
        var sanitizador = new SanitizadorDados();

        Assert.False(sanitizador.ReconhecimentoHabilitado);
        Assert.Equal(sanitizador.Sanitizar(texto, Identidade).Valor, (await sanitizador.SanitizarAsync(texto, Identidade)).Valor);
    }
}
