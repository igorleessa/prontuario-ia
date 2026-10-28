using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Prontuario.Application.Common.Interfaces;

namespace Prontuario.Infrastructure.Services;

/// <summary>Transcricao via Whisper API da OpenAI (RF07).</summary>
public class OpenAiTranscriptionService : ITranscriptionService
{
    /// <summary>Limite de upload da API de transcricao da OpenAI.</summary>
    internal const long TamanhoMaximoBytes = 25L * 1024 * 1024;

    /// <summary>
    /// Contexto passado ao modelo de transcricao. O Whisper usa o prompt como se
    /// fosse o trecho anterior do audio: um texto no registro de consultorio, com
    /// acentuacao, pontuacao e o vocabulario que mais aparece em consulta, faz o
    /// modelo grafar nomes de medicamento, doses e sinais vitais corretamente
    /// ("losartana 50 mg", "120 por 80") em vez de aproximacoes foneticas.
    /// </summary>
    internal const string PromptVocabulario =
        "Consulta médica em português do Brasil, conversa entre médico e paciente. " +
        "Queixa principal, história da doença atual, antecedentes, alergias, exame físico, " +
        "hipótese diagnóstica e conduta. Hipertensão arterial sistêmica, diabetes mellitus, " +
        "dislipidemia, asma, DPOC, dispneia, dor torácica, cefaleia, náuseas, febre de 37,8 °C, " +
        "tosse produtiva. Losartana 50 mg, metformina 850 mg, dipirona 500 mg de 6 em 6 horas, " +
        "amoxicilina, azitromicina, omeprazol, sinvastatina, levotiroxina, prednisona. " +
        "Pressão arterial 130 por 80 mmHg, frequência cardíaca 78 bpm, saturação 97%. " +
        "Ausculta pulmonar com murmúrio vesicular presente, sem sibilos ou estertores; " +
        "bulhas rítmicas normofonéticas. Hemograma, PCR, glicemia de jejum, hemoglobina glicada, " +
        "TSH, radiografia de tórax, eletrocardiograma, ultrassonografia. Retorno em 7 dias.";

    private readonly IHttpClientFactory _fabrica;

    public OpenAiTranscriptionService(IHttpClientFactory fabrica) => _fabrica = fabrica;

    public async Task<string> TranscreverAsync(
        Stream audio, CredenciaisIA credenciais, CancellationToken cancellationToken = default)
    {
        var bytes = await LerAsync(audio, cancellationToken);

        if (bytes.Length == 0)
        {
            throw new InvalidOperationException("A gravacao chegou vazia: verifique o microfone e grave de novo.");
        }

        if (bytes.Length > TamanhoMaximoBytes)
        {
            throw new InvalidOperationException(
                $"A gravacao tem {bytes.Length / (1024 * 1024)} MB, acima do limite de 25 MB do provedor de transcricao.");
        }

        var formato = FormatoAudio.Detectar(bytes);

        using var conteudo = new MultipartFormDataContent();

        var arquivo = new ByteArrayContent(bytes);
        arquivo.Headers.ContentType = new MediaTypeHeaderValue(formato.TipoConteudo);
        conteudo.Add(arquivo, "file", $"consulta.{formato.Extensao}");
        conteudo.Add(new StringContent(credenciais.ModeloTranscricao), "model");

        // O audio e sempre de consulta em portugues; fixar o idioma evita que o
        // modelo tente adivinhar e melhora a acuracia em trechos curtos.
        conteudo.Add(new StringContent("pt"), "language");
        conteudo.Add(new StringContent(PromptVocabulario), "prompt");

        // Temperatura zero: o texto deve refletir o que foi dito, sem variacao criativa.
        conteudo.Add(new StringContent(0.ToString(CultureInfo.InvariantCulture)), "temperature");

        using var requisicao = new HttpRequestMessage(HttpMethod.Post, "audio/transcriptions")
        {
            Content = conteudo,
        };
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credenciais.ChaveApi);

        var cliente = _fabrica.CreateClient(OpenAiCliente.Nome);
        using var resposta = await cliente.SendAsync(requisicao, cancellationToken);
        var corpo = await resposta.Content.ReadAsStringAsync(cancellationToken);

        if (!resposta.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Whisper respondeu {(int)resposta.StatusCode}: {OpenAiCliente.DescreverErro(corpo)}");
        }

        using var json = JsonDocument.Parse(corpo);
        var texto = json.RootElement.GetProperty("text").GetString()?.Trim() ?? string.Empty;

        if (texto.Length == 0)
        {
            throw new InvalidOperationException(
                "Nenhuma fala foi reconhecida na gravacao. Verifique o microfone e grave de novo.");
        }

        return texto;
    }

    private static async Task<byte[]> LerAsync(Stream audio, CancellationToken cancellationToken)
    {
        if (audio is MemoryStream memoria && memoria.Position == 0)
        {
            return memoria.ToArray();
        }

        using var copia = new MemoryStream();
        await audio.CopyToAsync(copia, cancellationToken);
        return copia.ToArray();
    }
}
