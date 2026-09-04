using System.Text.Json;

namespace Prontuario.Infrastructure.Services;

/// <summary>Constantes e utilitarios comuns aos dois adaptadores da OpenAI.</summary>
public static class OpenAiCliente
{
    public const string Nome = "openai";
    public const string BaseUrl = "https://api.openai.com/v1/";

    /// <summary>
    /// Extrai a mensagem de erro da OpenAI para o log e para a tela. O corpo bruto
    /// nao e propagado porque pode ser longo demais e conter eco do payload.
    /// </summary>
    public static string DescreverErro(string corpo)
    {
        try
        {
            using var json = JsonDocument.Parse(corpo);
            if (json.RootElement.TryGetProperty("error", out var erro) &&
                erro.TryGetProperty("message", out var mensagem))
            {
                return mensagem.GetString() ?? corpo;
            }
        }
        catch (JsonException)
        {
            // Resposta nao-JSON (proxy, HTML de erro): usa o texto como veio.
        }

        return corpo.Length > 300 ? corpo[..300] : corpo;
    }
}
