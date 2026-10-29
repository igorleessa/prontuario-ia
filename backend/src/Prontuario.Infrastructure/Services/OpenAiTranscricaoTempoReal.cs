using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;

namespace Prontuario.Infrastructure.Services;

/// <summary>
/// Emite a chave temporaria da Realtime API da OpenAI para uma sessao so de
/// transcricao. O navegador usa essa chave para abrir a conexao WebRTC direto
/// com a OpenAI: o audio vai do microfone ao provedor sem passar por aqui, e a
/// chave da clinica nunca sai do servidor.
/// </summary>
public class OpenAiTranscricaoTempoReal : IProvedorTranscricaoTempoReal
{
    /// <summary>Endpoint que o navegador chama com a oferta SDP do WebRTC.</summary>
    public const string UrlConexao = "https://api.openai.com/v1/realtime/calls";

    /// <summary>
    /// A chave so serve para abrir a conexao; uma vez aberta, a sessao segue.
    /// Prazo curto reduz o estrago se ela vazar do navegador.
    /// </summary>
    private const int ValidadeSegundos = 600;

    private readonly IHttpClientFactory _fabrica;

    public OpenAiTranscricaoTempoReal(IHttpClientFactory fabrica) => _fabrica = fabrica;

    public async Task<CredencialTempoReal> CriarSessaoAsync(
        CredenciaisIA credenciais, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            expires_after = new { anchor = "created_at", seconds = ValidadeSegundos },
            session = new
            {
                type = "transcription",
                audio = new
                {
                    input = new
                    {
                        format = new { type = "audio/pcm", rate = 24000 },
                        // Microfone de mesa no consultorio, perto de quem fala.
                        noise_reduction = new { type = "near_field" },
                        transcription = new
                        {
                            model = credenciais.ModeloTranscricao,
                            language = "pt",
                            prompt = OpenAiTranscriptionService.PromptVocabulario,
                        },
                        // Cada pausa de fala fecha um trecho: e o ritmo em que o
                        // texto chega e o formulario e atualizado.
                        turn_detection = new
                        {
                            type = "server_vad",
                            threshold = 0.5,
                            prefix_padding_ms = 300,
                            silence_duration_ms = 700,
                        },
                    },
                },
            },
        };

        using var requisicao = new HttpRequestMessage(HttpMethod.Post, "realtime/client_secrets")
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
                $"A Realtime API respondeu {(int)resposta.StatusCode}: {OpenAiCliente.DescreverErro(corpo)}");
        }

        using var json = JsonDocument.Parse(corpo);
        var raiz = json.RootElement;

        var chave = raiz.GetProperty("value").GetString();
        if (string.IsNullOrWhiteSpace(chave))
        {
            throw new InvalidOperationException("A Realtime API nao devolveu a chave temporaria.");
        }

        var expiraEm = raiz.TryGetProperty("expires_at", out var expira) && expira.TryGetInt64(out var segundos)
            ? DateTimeOffset.FromUnixTimeSeconds(segundos).UtcDateTime
            : DateTime.UtcNow.AddSeconds(ValidadeSegundos);

        return new CredencialTempoReal(chave, expiraEm, UrlConexao);
    }
}
