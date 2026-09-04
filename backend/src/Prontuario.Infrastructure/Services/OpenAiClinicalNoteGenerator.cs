using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;

namespace Prontuario.Infrastructure.Services;

/// <summary>
/// Extracao estruturada via Chat Completions da OpenAI (RF08). Usa Structured
/// Outputs (response_format json_schema, strict) para que a resposta sempre
/// chegue com exatamente os campos do formulario de revisao.
/// </summary>
public class OpenAiClinicalNoteGenerator : IClinicalNoteGenerator
{
    private const string Instrucao = """
        Voce e um assistente de documentacao clinica. A partir da transcricao de uma
        consulta medica em portugues do Brasil, preencha os campos do prontuario.

        Regras obrigatorias:
        - Use exclusivamente informacao presente na transcricao. Nunca invente sintomas,
          medicamentos, doses, resultados de exame ou diagnosticos.
        - Se um campo nao tiver respaldo na transcricao, devolva null. Campo vazio e
          preferivel a campo especulativo: o medico revisa e completa.
        - Escreva em portugues do Brasil, em terceira pessoa, no registro tecnico usado
          em prontuario, sem repetir o dialogo literalmente.
        - cid10Sugerido e apenas uma sugestao a partir da hipotese diagnostica descrita.
          Devolva null se a transcricao nao sustentar um codigo com seguranca.
        """;

    private static readonly JsonSerializerOptions OpcoesJson = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _fabrica;

    public OpenAiClinicalNoteGenerator(IHttpClientFactory fabrica) => _fabrica = fabrica;

    public async Task<RascunhoClinicoDto> GerarRascunhoAsync(
        string transcricao, CredenciaisIA credenciais, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            model = credenciais.ModeloTexto,
            temperature = 0,
            messages = new object[]
            {
                new { role = "system", content = Instrucao },
                new { role = "user", content = $"Transcricao da consulta:\n\n{transcricao}" },
            },
            response_format = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "rascunho_clinico",
                    strict = true,
                    schema = Esquema(),
                },
            },
        };

        using var requisicao = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(payload),
        };
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credenciais.ChaveApi);

        var cliente = _fabrica.CreateClient(OpenAiCliente.Nome);
        using var resposta = await cliente.SendAsync(requisicao, cancellationToken);
        var corpo = await resposta.Content.ReadAsStringAsync(cancellationToken);

        if (!resposta.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"A API de texto respondeu {(int)resposta.StatusCode}: {OpenAiCliente.DescreverErro(corpo)}");
        }

        using var json = JsonDocument.Parse(corpo);
        var conteudo = json.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(conteudo))
        {
            throw new InvalidOperationException("A API de texto devolveu uma resposta vazia.");
        }

        return JsonSerializer.Deserialize<RascunhoClinicoDto>(conteudo, OpcoesJson)
            ?? throw new InvalidOperationException("Nao foi possivel interpretar o rascunho devolvido pela IA.");
    }

    /// <summary>
    /// Structured Outputs exige todos os campos em "required"; a ausencia de
    /// informacao e expressa por null, o que o tipo ["string","null"] permite.
    /// </summary>
    private static object Esquema()
    {
        string[] campos =
        [
            "queixaPrincipal", "hda", "antecedentes", "exameFisico",
            "hipoteseDiagnostica", "cid10Sugerido", "conduta",
        ];

        return new
        {
            type = "object",
            additionalProperties = false,
            required = campos,
            properties = campos.ToDictionary(
                campo => campo,
                _ => (object)new { type = new[] { "string", "null" } }),
        };
    }
}
