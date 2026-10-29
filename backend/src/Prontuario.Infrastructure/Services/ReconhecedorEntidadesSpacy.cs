using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Prontuario.Application.Common.Interfaces;

namespace Prontuario.Infrastructure.Services;

public class ReconhecimentoEntidadesOptions
{
    public const string SectionName = "Privacidade:Ner";

    /// <summary>Endereco do servico local (servicos/privacidade-ner). Vazio desliga a camada.</summary>
    public string? Url { get; set; }

    /// <summary>Categorias retiradas do texto, separadas por virgula: PER, LOC, ORG.</summary>
    public string Categorias { get; set; } = "PER";

    public int TimeoutSegundos { get; set; } = 5;
}

/// <summary>
/// Cliente do servico de reconhecimento de entidades que roda ao lado do
/// backend. A chamada fica na rede interna: o texto nao sai da infraestrutura
/// da clinica para ser protegido.
/// </summary>
public class ReconhecedorEntidadesSpacy : IReconhecedorEntidades
{
    public const string NomeCliente = "privacidade-ner";

    private readonly IHttpClientFactory _fabrica;
    private readonly ReconhecimentoEntidadesOptions _opcoes;
    private readonly HashSet<string> _categorias;

    public ReconhecedorEntidadesSpacy(IHttpClientFactory fabrica, IOptions<ReconhecimentoEntidadesOptions> opcoes)
    {
        _fabrica = fabrica;
        _opcoes = opcoes.Value;
        _categorias = _opcoes.Categorias
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(categoria => categoria.ToUpperInvariant())
            .ToHashSet();
    }

    public bool Habilitado => !string.IsNullOrWhiteSpace(_opcoes.Url);

    public async Task<IReadOnlyList<EntidadeReconhecida>> ReconhecerAsync(
        string texto, CancellationToken cancellationToken = default)
    {
        var cliente = _fabrica.CreateClient(NomeCliente);
        using var resposta = await cliente.PostAsJsonAsync("entidades", new { texto }, cancellationToken);
        resposta.EnsureSuccessStatusCode();

        var corpo = await resposta.Content.ReadFromJsonAsync<RespostaServico>(cancellationToken)
            ?? throw new InvalidOperationException("O servico de reconhecimento devolveu uma resposta vazia.");

        return corpo.Entidades
            .Where(entidade => _categorias.Contains(entidade.Rotulo))
            .Select(entidade => new EntidadeReconhecida(
                entidade.Inicio, entidade.Fim, entidade.Rotulo, entidade.Texto, entidade.Classes, entidade.InicioDeFrase))
            .ToList();
    }

    private sealed record RespostaServico(
        [property: JsonPropertyName("entidades")] List<EntidadeServico> Entidades);

    private sealed record EntidadeServico(
        [property: JsonPropertyName("inicio")] int Inicio,
        [property: JsonPropertyName("fim")] int Fim,
        [property: JsonPropertyName("rotulo")] string Rotulo,
        [property: JsonPropertyName("texto")] string Texto,
        [property: JsonPropertyName("classes")] List<string> Classes,
        [property: JsonPropertyName("inicio_de_frase")] bool InicioDeFrase);
}
