using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;
using Prontuario.Application.Privacidade;

namespace Prontuario.Infrastructure.Services;

/// <summary>
/// Extracao estruturada via Chat Completions da OpenAI (RF08). Usa Structured
/// Outputs (response_format json_schema, strict) para que a resposta sempre
/// chegue com exatamente os campos do formulario de revisao.
/// </summary>
public partial class OpenAiClinicalNoteGenerator : IClinicalNoteGenerator
{
    private const string Instrucao = """
        Voce e um assistente de documentacao clinica. A partir da transcricao de uma
        consulta medica em portugues do Brasil, preencha os campos do prontuario.

        Sobre a transcricao:
        - Ela vem de reconhecimento automatico de fala e, em geral, sem marcar quem fala.
          Deduza pelo contexto o que e fala do medico (perguntas, achados do exame,
          raciocinio, prescricao) e o que e relato do paciente ou acompanhante.
        - Pode conter erros foneticos. Corrija a grafia de medicamentos, exames e termos
          tecnicos apenas quando a correcao for inequivoca (ex.: "lozartana" -> losartana).
          Na duvida, mantenha o que foi transcrito.

        Sobre os dados pessoais:
        - A transcricao foi pseudonimizada antes de chegar aqui. Marcadores entre colchetes
          ([PACIENTE], [MEDICO], [PESSOA], [CPF], [TELEFONE], [ENDERECO], [DOCUMENTO]...)
          substituem nomes e identificadores. Nunca tente adivinhar o valor original.
        - Refira-se ao paciente como "paciente" e nao copie marcadores para o registro, a nao
          ser que o dado seja clinicamente necessario (ex.: "acompanhado pela filha").

        Regras obrigatorias:
        - Use exclusivamente informacao presente na transcricao. Nunca invente sintomas,
          medicamentos, doses, resultados de exame ou diagnosticos.
        - Se um campo nao tiver respaldo na transcricao, devolva null. Campo vazio e
          preferivel a campo especulativo: o medico revisa e completa.
        - Registre negativas pertinentes que foram ditas (ex.: "nega febre", "sem dispneia"):
          elas sao parte do raciocinio clinico.
        - Preserve exatamente numeros, doses, unidades, frequencias, duracoes e sinais vitais,
          escritos em algarismos e com a unidade usual (ex.: "dipirona 500 mg, 1 comprimido de
          6/6 h se dor ou febre", "PA 130x80 mmHg", "Tax 37,2 °C", "SpO2 97%").
        - Hipotese do paciente ("acho que e sinusite") nao e diagnostico: so registre como
          hipotese diagnostica o que o medico formulou.
        - Nao repita a mesma informacao em mais de um campo; coloque-a no campo mais especifico.
        - Escreva em portugues do Brasil, em terceira pessoa, no registro tecnico usado
          em prontuario, de forma concisa, sem repetir o dialogo literalmente.

        O que vai em cada campo:
        - queixaPrincipal: o motivo da consulta em uma frase curta, com a duracao quando dita
          (ex.: "Tosse e dor de garganta ha 4 dias.").
        - hda: historia da doenca atual em ordem cronologica - inicio, caracteristicas,
          intensidade, fatores de melhora e piora, sintomas associados, negativas pertinentes
          e o que ja foi feito ou tomado para o quadro.
        - antecedentes: doencas previas e cronicas, medicamentos de uso continuo com dose,
          alergias (inclusive "nega alergias" quando dito), cirurgias, internacoes, historia
          familiar e habitos (tabagismo, etilismo, atividade fisica).
        - exameFisico: sinais vitais e achados do exame que o medico verbalizou, por sistema.
          Nunca preencha exame que nao foi descrito na consulta.
        - hipoteseDiagnostica: a(s) hipotese(s) formulada(s) pelo medico, a principal primeiro.
        - cid10Sugerido: somente o codigo CID-10 da hipotese principal, no formato letra,
          dois digitos e subcategoria opcional (ex.: "J06.9"), sem descricao. null se a
          hipotese nao sustentar um codigo com seguranca.
        - conduta: o plano, em itens separados por quebra de linha: prescricoes (medicamento,
          dose, via, posologia, duracao), exames solicitados, orientacoes, sinais de alarme,
          atestado, encaminhamentos e retorno.
        """;

    /// <summary>
    /// Nos documentos o nome do paciente e preenchido depois, a partir do
    /// cadastro: o marcador e o que garante que ele saia do lugar certo.
    /// </summary>
    private const string InstrucaoMarcadores = """

        Dados pessoais chegam pseudonimizados por marcadores entre colchetes ([PACIENTE], [PESSOA],
        [CPF]...). Quando o documento precisar do nome do paciente, escreva exatamente [PACIENTE];
        nunca invente nome, documento ou endereco.
        """;

    private static readonly JsonSerializerOptions OpcoesJson = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _fabrica;

    public OpenAiClinicalNoteGenerator(IHttpClientFactory fabrica) => _fabrica = fabrica;

    public async Task<RascunhoClinicoDto> GerarRascunhoAsync(
        TextoSanitizado transcricao,
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
                new { role = "user", content = $"Transcricao da consulta:\n\n<transcricao>\n{transcricao.Valor}\n</transcricao>" },
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

        var rascunho = JsonSerializer.Deserialize<RascunhoClinicoDto>(conteudo, OpcoesJson)
            ?? throw new InvalidOperationException("Nao foi possivel interpretar o rascunho devolvido pela IA.");

        return Normalizar(rascunho);
    }

    /// <summary>
    /// Limpa o que o modelo devolve antes de chegar a tela: texto em branco vira
    /// null (o campo aparece vazio para o medico preencher) e o CID e reduzido ao
    /// codigo, para que "J06.9 - IVAS" ou "j069" nao entrem no prontuario como estao.
    /// </summary>
    internal static RascunhoClinicoDto Normalizar(RascunhoClinicoDto rascunho) => new(
        Limpar(rascunho.QueixaPrincipal),
        Limpar(rascunho.Hda),
        Limpar(rascunho.Antecedentes),
        Limpar(rascunho.ExameFisico),
        Limpar(rascunho.HipoteseDiagnostica),
        NormalizarCid(rascunho.Cid10Sugerido),
        Limpar(rascunho.Conduta));

    private static string? Limpar(string? texto)
    {
        var limpo = texto?.Trim();
        return string.IsNullOrEmpty(limpo) || limpo.Equals("null", StringComparison.OrdinalIgnoreCase)
            ? null
            : limpo;
    }

    internal static string? NormalizarCid(string? cid)
    {
        if (string.IsNullOrWhiteSpace(cid))
        {
            return null;
        }

        var achado = CodigoCid().Match(cid.ToUpperInvariant());
        if (!achado.Success)
        {
            return null;
        }

        var subcategoria = achado.Groups[3].Value;
        return subcategoria.Length > 0
            ? $"{achado.Groups[1].Value}{achado.Groups[2].Value}.{subcategoria}"
            : $"{achado.Groups[1].Value}{achado.Groups[2].Value}";
    }

    [GeneratedRegex(@"\b([A-Z])(\d{2})(?:\.?(\d{1,2}))?\b")]
    private static partial Regex CodigoCid();

    public async Task<string> GerarDocumentoAsync(
        string tipo,
        TextoSanitizado transcricao,
        RegistroSanitizado registroClinico,
        CredenciaisIA credenciais,
        ContextoGeracao contexto,
        CancellationToken cancellationToken = default)
    {
        var instrucao = ComContexto(InstrucaoDocumento(tipo) + InstrucaoMarcadores, contexto);

        var conteudoUsuario = $"""
            Transcricao da consulta:

            {transcricao.Valor}

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
    /// A descricao de cada campo vai junto no esquema e reforca, campo a campo,
    /// o que a instrucao diz - e o que mais reduz informacao no campo errado.
    /// </summary>
    private static object Esquema()
    {
        var campos = new Dictionary<string, string>
        {
            ["queixaPrincipal"] = "Motivo da consulta em uma frase, com duracao quando dita.",
            ["hda"] = "Historia da doenca atual, cronologica, com sintomas associados e negativas pertinentes.",
            ["antecedentes"] = "Comorbidades, medicamentos de uso continuo com dose, alergias, cirurgias, historia familiar e habitos.",
            ["exameFisico"] = "Sinais vitais e achados verbalizados pelo medico. null se nao houve exame descrito.",
            ["hipoteseDiagnostica"] = "Hipoteses formuladas pelo medico, a principal primeiro.",
            ["cid10Sugerido"] = "Apenas o codigo CID-10 da hipotese principal, ex.: J06.9. null se incerto.",
            ["conduta"] = "Prescricoes, exames, orientacoes, sinais de alarme, atestado, encaminhamento e retorno, um item por linha.",
        };

        return new
        {
            type = "object",
            additionalProperties = false,
            required = campos.Keys.ToArray(),
            properties = campos.ToDictionary(
                campo => campo.Key,
                campo => (object)new { type = new[] { "string", "null" }, description = campo.Value }),
        };
    }
}
