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
    private const string Cabecalho = """
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
        """;

    private const string OrigensEFalantes = """
        Origem de cada campo:
        - Cada trecho da transcricao comeca com o seu numero entre colchetes ("[12] ...").
        - Em "origens", para cada campo, liste os numeros dos trechos de onde tirou o conteudo
          dele, em ordem. Campo null tem lista vazia. Nao cite trecho que nao sustente o campo.

        Quem fala:
        - A transcricao nao separa as vozes. Em "falantes", diga quem disse cada trecho
          ("medico", "paciente" ou "acompanhante"), pelo conteudo: perguntas, exame, raciocinio
          e prescricao costumam ser do medico; relato de sintomas e historia, do paciente;
          quem fala do paciente na terceira pessoa ("ela esta com febre") e acompanhante.
        - Trecho que ja chega com o falante entre parenteses foi confirmado pelo medico: use
          como esta. Trecho que mistura falas fica com quem fala a maior parte.
        """;

    private const string Campos = """
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

    private const string Instrucao = Cabecalho + "\n\n" + OrigensEFalantes + "\n\n" + Campos;

    /// <summary>
    /// Durante a consulta a IA nao rele a conversa inteira: recebe o prontuario
    /// como esta e so as falas novas, e devolve apenas o que mudou. A entrada
    /// deixa de crescer com a duracao da consulta, e a saida (o que mais custa
    /// e mais demora) fica restrita ao campo alterado.
    /// </summary>
    private const string Incremental = """
        Atualizacao durante a consulta:
        - A consulta esta em andamento. Voce recebe o prontuario como esta preenchido ate agora,
          as ultimas falas ja consideradas (apenas contexto) e as falas novas.
        - Em cada campo, devolva o texto completo e atualizado SOMENTE se as falas novas mudarem
          esse campo, integrando a informacao nova ao que ja estava, sem perder o que continua
          valido. Campo que nao muda: null. Aqui null significa "manter como esta", nunca "apagar".
        - Se a fala nova corrige algo ja registrado ("na verdade sao cinco dias"), reescreva o
          campo com a correcao.
        - Resposta curta ("sim", "nao", "faz tempo") so tem sentido com a pergunta que veio
          antes: use as falas de contexto para interpreta-la.
        - Falas novas sem informacao clinica (cumprimentos, conversa lateral): todos os campos null.
        - Nao reprocesse as falas de contexto: o que elas diziam ja esta no prontuario.

        Origem de cada campo:
        - Cada fala comeca com o seu numero entre colchetes ("[12] ...").
        - Em "origens", para cada campo que voce alterou, liste os numeros das falas (novas ou
          de contexto) que sustentam a alteracao. Campo null tem lista vazia.

        Quem fala:
        - Em "falantes", diga quem disse cada fala NOVA ("medico", "paciente" ou "acompanhante"),
          pelo conteudo e pela sequencia do dialogo. Nao inclua as falas de contexto.
        - Fala que ja chega com o falante entre parenteses: use como esta. Fala que mistura
          vozes fica com quem fala a maior parte.
        """;

    private const string InstrucaoIncremental = Cabecalho + "\n\n" + Incremental + "\n\n" + Campos;

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

    private readonly OpenAiChat _chat;

    public OpenAiClinicalNoteGenerator(OpenAiChat chat) => _chat = chat;

    public async Task<RascunhoComOrigensDto> GerarRascunhoAsync(
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
                    schema = Esquema(incremental: false),
                },
            },
        };

        var conteudo = await ChamarAsync(payload, credenciais, "rascunho", cancellationToken);

        if (string.IsNullOrWhiteSpace(conteudo))
        {
            throw new InvalidOperationException("A API de texto devolveu uma resposta vazia.");
        }

        var rascunho = JsonSerializer.Deserialize<RascunhoClinicoDto>(conteudo, OpcoesJson)
            ?? throw new InvalidOperationException("Nao foi possivel interpretar o rascunho devolvido pela IA.");

        return new RascunhoComOrigensDto(Normalizar(rascunho), LerOrigens(conteudo), LerFalantes(conteudo));
    }

    public async Task<AtualizacaoRascunhoDto> AtualizarRascunhoAsync(
        RegistroSanitizado rascunhoAtual,
        string? cidAtual,
        TextoSanitizado falasDeContexto,
        TextoSanitizado falasNovas,
        CredenciaisIA credenciais,
        ContextoGeracao contexto,
        CancellationToken cancellationToken = default)
    {
        // A instrucao (fixa durante a consulta) vai inteira na mensagem de
        // sistema e o que muda vai depois: o provedor reaproveita o prefixo
        // repetido e cobra menos por ele.
        var payload = new
        {
            model = credenciais.ModeloTexto,
            temperature = 0,
            messages = new object[]
            {
                new { role = "system", content = ComContexto(InstrucaoIncremental, contexto) },
                new { role = "user", content = MensagemIncremental(rascunhoAtual, cidAtual, falasDeContexto, falasNovas) },
            },
            response_format = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "atualizacao_rascunho",
                    strict = true,
                    schema = Esquema(incremental: true),
                },
            },
        };

        var conteudo = await ChamarAsync(payload, credenciais, "atualizacao", cancellationToken);

        if (string.IsNullOrWhiteSpace(conteudo))
        {
            throw new InvalidOperationException("A API de texto devolveu uma resposta vazia.");
        }

        var alteracoes = JsonSerializer.Deserialize<RascunhoClinicoDto>(conteudo, OpcoesJson)
            ?? throw new InvalidOperationException("Nao foi possivel interpretar a atualizacao devolvida pela IA.");

        return new AtualizacaoRascunhoDto(Normalizar(alteracoes), LerOrigens(conteudo), LerFalantes(conteudo));
    }

    internal static string MensagemIncremental(
        RegistroSanitizado atual, string? cidAtual, TextoSanitizado falasDeContexto, TextoSanitizado falasNovas)
    {
        static string Campo(string nome, string? valor)
            => $"{nome}: {(string.IsNullOrWhiteSpace(valor) ? "(vazio)" : valor.Trim())}";

        var prontuario = string.Join("\n", new[]
        {
            Campo("queixaPrincipal", atual.QueixaPrincipal),
            Campo("hda", atual.Hda),
            Campo("antecedentes", atual.Antecedentes),
            Campo("exameFisico", atual.ExameFisico),
            Campo("hipoteseDiagnostica", atual.HipoteseDiagnostica),
            Campo("cid10Sugerido", cidAtual),
            Campo("conduta", atual.Conduta),
        });

        var partes = new List<string> { $"Prontuario preenchido ate agora:\n\n<prontuario>\n{prontuario}\n</prontuario>" };

        if (!string.IsNullOrWhiteSpace(falasDeContexto.Valor))
        {
            partes.Add($"Falas anteriores, ja consideradas (apenas contexto):\n\n<contexto>\n{falasDeContexto.Valor}\n</contexto>");
        }

        partes.Add($"Falas novas:\n\n<falas_novas>\n{falasNovas.Valor}\n</falas_novas>");
        return string.Join("\n\n", partes);
    }

    private Task<string?> ChamarAsync(
        object payload, CredenciaisIA credenciais, string operacao, CancellationToken cancellationToken)
        => _chat.EnviarAsync(payload, credenciais, operacao, cancellationToken);

    /// <summary>Falante por trecho; entrada fora do formato e ignorada.</summary>
    internal static IReadOnlyDictionary<int, string> LerFalantes(string conteudo)
    {
        using var json = JsonDocument.Parse(conteudo);
        return LerFalantes(json.RootElement);
    }

    internal static IReadOnlyDictionary<int, string> LerFalantes(JsonElement raiz)
    {
        var resultado = new Dictionary<int, string>();

        if (!raiz.TryGetProperty("falantes", out var falantes) || falantes.ValueKind != JsonValueKind.Array)
        {
            return resultado;
        }

        foreach (var item in falantes.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object
                && item.TryGetProperty("trecho", out var trecho)
                && trecho.ValueKind == JsonValueKind.Number && trecho.TryGetInt32(out var sequencia)
                && item.TryGetProperty("falante", out var falante)
                && falante.ValueKind == JsonValueKind.String && falante.GetString() is { } nome
                && NomesFalantes.TryGetValue(nome, out var normalizado))
            {
                resultado[sequencia] = normalizado;
            }
        }

        return resultado;
    }

    private static readonly Dictionary<string, string> NomesFalantes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["medico"] = "Medico",
        ["paciente"] = "Paciente",
        ["acompanhante"] = "Acompanhante",
    };

    /// <summary>Origens por campo; o que nao for lista de inteiros e ignorado, nao derruba o rascunho.</summary>
    internal static IReadOnlyDictionary<string, IReadOnlyList<int>> LerOrigens(string conteudo)
    {
        using var json = JsonDocument.Parse(conteudo);
        if (!json.RootElement.TryGetProperty("origens", out var origens) || origens.ValueKind != JsonValueKind.Object)
        {
            return RascunhoComOrigensDto.SemOrigens;
        }

        var resultado = new Dictionary<string, IReadOnlyList<int>>();
        foreach (var campo in origens.EnumerateObject())
        {
            if (campo.Value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var numeros = campo.Value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out _))
                .Select(item => item.GetInt32())
                .Distinct()
                .ToList();

            if (numeros.Count > 0)
            {
                resultado[campo.Name] = numeros;
            }
        }

        return resultado;
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

        var conteudo = await ChamarAsync(payload, credenciais, "documento", cancellationToken);

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
    private static object Esquema(bool incremental)
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

        var propriedades = campos.ToDictionary(
            campo => campo.Key,
            campo => (object)new
            {
                type = new[] { "string", "null" },
                description = incremental
                    ? $"Texto completo e atualizado do campo, somente se as falas novas o mudarem; senao null. {campo.Value}"
                    : campo.Value,
            });

        propriedades["origens"] = new
        {
            type = "object",
            description = incremental
                ? "Numeros das falas que sustentam cada campo alterado."
                : "Numeros dos trechos da transcricao que sustentam cada campo.",
            additionalProperties = false,
            required = campos.Keys.ToArray(),
            properties = campos.Keys.ToDictionary(
                campo => campo,
                _ => (object)new { type = "array", items = new { type = "integer" } }),
        };

        propriedades["falantes"] = new
        {
            type = "array",
            description = incremental ? "Quem disse cada fala nova." : "Quem disse cada trecho da transcricao.",
            items = new
            {
                type = "object",
                additionalProperties = false,
                required = new[] { "trecho", "falante" },
                properties = new Dictionary<string, object>
                {
                    ["trecho"] = new { type = "integer" },
                    ["falante"] = new { type = "string", @enum = new[] { "medico", "paciente", "acompanhante" } },
                },
            },
        };

        return new
        {
            type = "object",
            additionalProperties = false,
            required = propriedades.Keys.ToArray(),
            properties = propriedades,
        };
    }
}
