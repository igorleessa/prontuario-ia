using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Prontuario.Application.Common.Interfaces;

namespace Prontuario.Infrastructure.Services;

/// <summary>
/// Chamada ao modelo de texto da OpenAI, comum a nota clinica e ao prontuario
/// ocupacional. O consumo de tokens de cada chamada vai para o log (so numeros,
/// nunca conteudo): e o que permite medir o custo real por atendimento.
/// </summary>
public class OpenAiChat
{
    private readonly IHttpClientFactory _fabrica;
    private readonly ILogger<OpenAiChat> _logger;

    public OpenAiChat(IHttpClientFactory fabrica, ILogger<OpenAiChat> logger)
    {
        _fabrica = fabrica;
        _logger = logger;
    }

    public async Task<string?> EnviarAsync(
        object payload, CredenciaisIA credenciais, string operacao, CancellationToken cancellationToken)
    {
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
        RegistrarConsumo(json.RootElement, credenciais.ModeloTexto, operacao);

        return json.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();
    }

    private void RegistrarConsumo(JsonElement raiz, string modelo, string operacao)
    {
        if (!raiz.TryGetProperty("usage", out var uso) || uso.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        static int Numero(JsonElement objeto, string nome)
            => objeto.ValueKind == JsonValueKind.Object && objeto.TryGetProperty(nome, out var valor)
                && valor.ValueKind == JsonValueKind.Number && valor.TryGetInt32(out var numero) ? numero : 0;

        var emCache = uso.TryGetProperty("prompt_tokens_details", out var detalhes) ? Numero(detalhes, "cached_tokens") : 0;

        _logger.LogInformation(
            "Consumo de IA ({Operacao}, {Modelo}): {Entrada} tokens de entrada ({EmCache} em cache), {Saida} de saida.",
            operacao, modelo, Numero(uso, "prompt_tokens"), emCache, Numero(uso, "completion_tokens"));
    }
}
