using System.Net.Http.Headers;
using System.Text.Json;
using Prontuario.Application.Common.Interfaces;

namespace Prontuario.Infrastructure.Services;

/// <summary>Transcricao via Whisper API da OpenAI (RF07).</summary>
public class OpenAiTranscriptionService : ITranscriptionService
{
    private readonly IHttpClientFactory _fabrica;

    public OpenAiTranscriptionService(IHttpClientFactory fabrica) => _fabrica = fabrica;

    public async Task<string> TranscreverAsync(
        Stream audio, CredenciaisIA credenciais, CancellationToken cancellationToken = default)
    {
        using var conteudo = new MultipartFormDataContent();

        var arquivo = new StreamContent(audio);
        arquivo.Headers.ContentType = new MediaTypeHeaderValue("audio/webm");
        conteudo.Add(arquivo, "file", "consulta.webm");
        conteudo.Add(new StringContent(credenciais.ModeloTranscricao), "model");

        // O audio e sempre de consulta em portugues; fixar o idioma evita que o
        // modelo tente adivinhar e melhora a acuracia em trechos curtos.
        conteudo.Add(new StringContent("pt"), "language");

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
        return json.RootElement.GetProperty("text").GetString() ?? string.Empty;
    }
}
