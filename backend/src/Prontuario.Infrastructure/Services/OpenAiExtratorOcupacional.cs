using System.Text;
using System.Text.Json;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;
using Prontuario.Application.Ocupacional;
using Prontuario.Application.Privacidade;

namespace Prontuario.Infrastructure.Services;

/// <summary>
/// Extracao dos campos do exame ocupacional a partir das falas novas.
///
/// Pensada para custar pouco: a instrucao e o esquema inteiro dos campos vao na
/// mensagem de sistema, identicos em todas as chamadas do atendimento (o
/// provedor cobra menos pelo prefixo repetido); o que muda - respostas ja
/// dadas e falas novas - vai depois, e e curto. A resposta traz so o que as
/// falas novas mudaram.
/// </summary>
public class OpenAiExtratorOcupacional : IExtratorOcupacional
{
    private const string Instrucao = """
        Voce preenche o prontuario de um exame medico ocupacional a partir da conversa entre
        medico e paciente, em portugues do Brasil, enquanto ela acontece.

        A cada chamada voce recebe as respostas ja registradas, as ultimas falas ja consideradas
        (apenas contexto) e as falas novas. Devolva somente o que as falas novas permitem
        preencher ou corrigir.

        Sobre as falas:
        - Vem de reconhecimento automatico de fala, sem marcar quem fala, e podem ter erros
          foneticos. Deduza pelo conteudo: quem pergunta, examina e dita achados e o medico;
          quem responde sobre si e o paciente.
        - Dados pessoais chegam trocados por marcadores entre colchetes ([PACIENTE], [PESSOA],
          [CPF]...). Nunca tente adivinhar o valor original.
        - Resposta curta ("sim", "nao", "faz uns dois anos") so tem sentido com a pergunta que
          veio antes: use as falas de contexto para interpreta-la.

        Regras obrigatorias:
        - Em "atualizacoes", "valor" tem de ser exatamente uma das opcoes do campo; em campo
          numerico, so o numero em algarismos, sem unidade; em campo de texto, uma frase curta
          e objetiva com o que foi dito.
        - Nunca invente. So preencha o que foi dito. Negacao conta: "nao fumo" e
          tabagismo = Não fumante; "nunca tive pressao alta" e cond_0 = Não.
        - Numeros falados viram algarismos: "um metro e sessenta e oito" e altura = 168;
          "cento e vinte por oitenta" ou "12 por 8" e pas = 120 e pad = 80; "setenta e oito
          quilos" e peso = 78. Altura sempre em centimetros.
        - Campos de anamnese (historico, habitos, antecedentes, exames ja feitos) vem do relato
          do paciente. Sinais vitais, exame fisico, PCD, conduta, recomendacao, encaminhamento
          e resultado do exame so valem quando ditos pelo medico. Voce nunca decide conduta,
          aptidao ou diagnostico: so registra o que o medico disse em voz alta.
        - Campo marcado "so se X = Y" so pode ser preenchido quando X ja vale Y nas respostas
          registradas ou nas suas atualizacoes desta chamada.
        - Nao repita campo que ja esta registrado com o mesmo valor. So mande de novo se a fala
          nova corrigir a resposta ("na verdade parei de fumar ano passado").
        - Se a fala toca num campo mas nao permite escolher um valor com seguranca, nao
          preencha: ponha em "esclarecimentos" o campo e uma pergunta curta que o medico pode
          fazer ao paciente. Sem ambiguidade, lista vazia.
        - "exame": ausculta_cardiaca e ausculta_pulmonar sao true somente se, nas falas novas,
          o medico disser que auscultou ou descrever o achado (bulhas, murmurio vesicular).
          "Nao auscultei", "falta auscultar" ou so a intencao de auscultar: false.
        - "falantes": quem disse cada fala NOVA ("medico", "paciente" ou "acompanhante").
        - Falas novas sem informacao para o prontuario: listas vazias e exame false.
        """;

    private readonly OpenAiChat _chat;

    public OpenAiExtratorOcupacional(OpenAiChat chat) => _chat = chat;

    public async Task<ExtracaoOcupacionalDto> ExtrairAsync(
        string sexo,
        string? secaoEmFoco,
        TextoSanitizado respostasAtuais,
        TextoSanitizado falasDeContexto,
        TextoSanitizado falasNovas,
        CredenciaisIA credenciais,
        CancellationToken cancellationToken = default)
    {
        var campos = EsquemaOcupacional.Campos(sexo).Where(campo => campo.PorVoz).Select(campo => campo.Id).ToArray();

        var payload = new
        {
            model = credenciais.ModeloTexto,
            temperature = 0,
            messages = new object[]
            {
                new { role = "system", content = Instrucao + "\n\n" + DescreverEsquema(sexo) },
                new { role = "user", content = Mensagem(secaoEmFoco, respostasAtuais, falasDeContexto, falasNovas) },
            },
            response_format = new
            {
                type = "json_schema",
                json_schema = new { name = "extracao_ocupacional", strict = true, schema = EsquemaDaResposta(campos) },
            },
        };

        var conteudo = await _chat.EnviarAsync(payload, credenciais, "ocupacional", cancellationToken);
        if (string.IsNullOrWhiteSpace(conteudo))
        {
            throw new InvalidOperationException("A API de texto devolveu uma resposta vazia.");
        }

        return Ler(conteudo);
    }

    /// <summary>Le a resposta sem confiar nela: o que vier fora do formato e ignorado.</summary>
    internal static ExtracaoOcupacionalDto Ler(string conteudo)
    {
        using var json = JsonDocument.Parse(conteudo);
        var raiz = json.RootElement;

        List<(string, string)> Pares(string lista, string chave)
        {
            var pares = new List<(string, string)>();
            if (raiz.TryGetProperty(lista, out var itens) && itens.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in itens.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object
                        && item.TryGetProperty("campo", out var campo) && campo.ValueKind == JsonValueKind.String
                        && item.TryGetProperty(chave, out var valor) && valor.ValueKind == JsonValueKind.String)
                    {
                        pares.Add((campo.GetString()!, valor.GetString()!));
                    }
                }
            }

            return pares;
        }

        bool Exame(string item)
            => raiz.TryGetProperty("exame", out var exame) && exame.ValueKind == JsonValueKind.Object
                && exame.TryGetProperty(item, out var feito) && feito.ValueKind == JsonValueKind.True;

        return new ExtracaoOcupacionalDto(
            Pares("atualizacoes", "valor"),
            Pares("esclarecimentos", "pergunta"),
            Exame("ausculta_cardiaca"),
            Exame("ausculta_pulmonar"),
            OpenAiClinicalNoteGenerator.LerFalantes(raiz));
    }

    /// <summary>Os campos, por secao, no formato mais curto que ainda diz tudo o que a IA precisa.</summary>
    internal static string DescreverEsquema(string sexo)
    {
        var texto = new StringBuilder("Campos do prontuario (id — rotulo: valores aceitos):\n");

        foreach (var secao in EsquemaOcupacional.Secoes(sexo))
        {
            var campos = secao.Campos.Where(campo => campo.PorVoz).ToList();
            if (campos.Count == 0)
            {
                continue;
            }

            texto.Append("\nSecao ").Append(secao.Id).Append(" — ").Append(secao.Rotulo).Append('\n');

            foreach (var campo in campos)
            {
                var valores = campo.Tipo switch
                {
                    TiposCampo.Numero => $"numero ({campo.Unidade})",
                    TiposCampo.Texto or TiposCampo.TextoLongo => "texto",
                    _ => string.Join(" | ", campo.Opcoes),
                };

                texto.Append("- ").Append(campo.Id).Append(" — ").Append(campo.Rotulo).Append(": ").Append(valores);

                if (campo.DependeDe is not null)
                {
                    texto.Append(" [so se ").Append(campo.DependeDe).Append(" = ").Append(campo.DependeDeValor).Append(']');
                }

                texto.Append('\n');
            }
        }

        return texto.ToString().TrimEnd();
    }

    internal static string Mensagem(
        string? secaoEmFoco, TextoSanitizado respostasAtuais, TextoSanitizado falasDeContexto, TextoSanitizado falasNovas)
    {
        var partes = new List<string>();

        if (!string.IsNullOrWhiteSpace(secaoEmFoco))
        {
            partes.Add($"A entrevista esta na secao \"{secaoEmFoco}\": de preferencia aos campos dela.");
        }

        partes.Add("Respostas ja registradas:\n\n<respostas>\n"
            + (string.IsNullOrWhiteSpace(respostasAtuais.Valor) ? "(nenhuma)" : respostasAtuais.Valor) + "\n</respostas>");

        if (!string.IsNullOrWhiteSpace(falasDeContexto.Valor))
        {
            partes.Add($"Falas anteriores, ja consideradas (apenas contexto):\n\n<contexto>\n{falasDeContexto.Valor}\n</contexto>");
        }

        partes.Add($"Falas novas:\n\n<falas_novas>\n{falasNovas.Valor}\n</falas_novas>");
        return string.Join("\n\n", partes);
    }

    private static object EsquemaDaResposta(string[] campos)
    {
        static object Lista(string[] campos, string chave) => new
        {
            type = "array",
            items = new
            {
                type = "object",
                additionalProperties = false,
                required = new[] { "campo", chave },
                properties = new Dictionary<string, object>
                {
                    ["campo"] = new { type = "string", @enum = campos },
                    [chave] = new { type = "string" },
                },
            },
        };

        return new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "atualizacoes", "esclarecimentos", "exame", "falantes" },
            properties = new Dictionary<string, object>
            {
                ["atualizacoes"] = Lista(campos, "valor"),
                ["esclarecimentos"] = Lista(campos, "pergunta"),
                ["exame"] = new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[] { "ausculta_cardiaca", "ausculta_pulmonar" },
                    properties = new Dictionary<string, object>
                    {
                        ["ausculta_cardiaca"] = new { type = "boolean" },
                        ["ausculta_pulmonar"] = new { type = "boolean" },
                    },
                },
                ["falantes"] = new
                {
                    type = "array",
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
                },
            },
        };
    }
}
