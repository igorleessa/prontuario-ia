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
        string transcricao,
        CredenciaisIA credenciais,
        ContextoGeracao contexto,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            model = credenciais.ModeloTexto,
            temperature = 0,
            messages = new object[]
            {
                new { role = "system", content = ComContexto(Instrucao, contexto) },
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

    public async Task<string> GerarDocumentoAsync(
        string tipo,
        string transcricao,
        RascunhoClinicoDto registroClinico,
        CredenciaisIA credenciais,
        ContextoGeracao contexto,
        CancellationToken cancellationToken = default)
    {
        var instrucao = ComContexto(InstrucaoDocumento(tipo), contexto);

        var conteudoUsuario = $"""
            Transcricao da consulta:

            {transcricao}

            Registro clinico revisado ate aqui:

            Queixa principal: {registroClinico.QueixaPrincipal}
            HDA: {registroClinico.Hda}
            Antecedentes: {registroClinico.Antecedentes}
            Exame fisico: {registroClinico.ExameFisico}
            Hipotese diagnostica: {registroClinico.HipoteseDiagnostica}
            Conduta: {registroClinico.Conduta}
            """;

        var payload = new
        {
            model = credenciais.ModeloTexto,
            temperature = 0,
            messages = new object[]
            {
                new { role = "system", content = instrucao },
                new { role = "user", content = conteudoUsuario },
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

        return string.IsNullOrWhiteSpace(conteudo)
            ? throw new InvalidOperationException("A API de texto devolveu uma resposta vazia.")
            : conteudo.Trim();
    }

    /// <summary>
    /// Template da especialidade e estilo do medico entram depois das regras
    /// fixas, e nao no lugar delas: personalizar a redacao nao pode abrir espaco
    /// para a IA preencher o que a consulta nao disse.
    /// </summary>
    private static string ComContexto(string instrucaoBase, ContextoGeracao contexto)
    {
        var partes = new List<string> { instrucaoBase };

        if (!string.IsNullOrWhiteSpace(contexto.InstrucoesTemplate))
        {
            partes.Add($"Modelo da especialidade:\n{contexto.InstrucoesTemplate.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(contexto.InstrucoesEstilo))
        {
            partes.Add(
                "Preferencias de redacao deste medico (seguir quando nao conflitarem com as regras acima):\n"
                + contexto.InstrucoesEstilo.Trim());
        }

        return string.Join("\n\n", partes);
    }

    private static string InstrucaoDocumento(string tipo) => tipo switch
    {
        "Receita" => """
            Redija a receita medica correspondente a conduta da consulta, em portugues do Brasil.
            Liste um medicamento por linha, com apresentacao, posologia, via e duracao, seguindo
            exatamente o que foi prescrito na consulta. Nunca acrescente medicamento, dose ou
            duracao que nao tenha sido dita. Se nao houver prescricao, responda apenas
            "Nenhuma prescricao foi registrada nesta consulta.".
            Nao inclua cabecalho de clinica, assinatura ou carimbo: eles sao acrescentados depois.
            """,
        "PedidoExame" => """
            Redija a solicitacao de exames correspondente a conduta da consulta, em portugues do
            Brasil. Liste um exame por linha e, quando a consulta tiver dito, a indicacao clinica.
            Nunca acrescente exame que nao tenha sido solicitado. Se nenhum exame foi pedido,
            responda apenas "Nenhum exame foi solicitado nesta consulta.".
            """,
        "Atestado" => """
            Redija o atestado medico correspondente ao que foi dito na consulta, em portugues do
            Brasil, em texto corrido. Use apenas o periodo de afastamento efetivamente mencionado
            e so cite CID se o paciente tiver autorizado na consulta. Se nao houver afastamento,
            responda apenas "Nenhum afastamento foi registrado nesta consulta.".
            """,
        "Encaminhamento" => """
            Redija o encaminhamento correspondente a conduta da consulta, em portugues do Brasil:
            especialidade de destino, motivo do encaminhamento e resumo clinico pertinente.
            Se nenhum encaminhamento foi feito, responda apenas
            "Nenhum encaminhamento foi registrado nesta consulta.".
            """,
        _ => throw new InvalidOperationException($"Tipo de documento desconhecido: {tipo}."),
    };

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
