using System.Security.Cryptography;
using System.Text;
using Prontuario.Infrastructure.Services;

namespace Prontuario.Application.Tests;

/// <summary>
/// A assinatura e o que prova ao EMR do cliente que a nota partiu daqui. O
/// formato esta publicado em docs/integracao-emr.md e implementado do outro
/// lado por quem integra: mudar o calculo quebra integracoes em producao.
/// </summary>
public class WebhookAssinaturaTests
{
    private const string Segredo = "segredo-de-teste";
    private const string Timestamp = "1758312000";
    private const string Corpo = """{"evento":"nota.exportada"}""";

    [Fact]
    public void Assinar_SegueOFormatoPublicado()
    {
        var assinatura = WebhookExportador.Assinar(Segredo, Timestamp, Corpo);

        var esperado = "sha256=" + Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(Segredo),
            Encoding.UTF8.GetBytes($"{Timestamp}.{Corpo}"))).ToLowerInvariant();

        Assert.Equal(esperado, assinatura);
        Assert.StartsWith("sha256=", assinatura);
    }

    [Fact]
    public void Assinar_CobreOTimestamp_ParaQueUmEnvioAntigoNaoSejaReapresentado()
    {
        var agora = WebhookExportador.Assinar(Segredo, Timestamp, Corpo);
        var depois = WebhookExportador.Assinar(Segredo, "1758315600", Corpo);

        Assert.NotEqual(agora, depois);
    }

    [Fact]
    public void Assinar_MudaQuandoOCorpoMuda()
    {
        var original = WebhookExportador.Assinar(Segredo, Timestamp, Corpo);
        var adulterado = WebhookExportador.Assinar(Segredo, Timestamp, Corpo.Replace("nota", "outra"));

        Assert.NotEqual(original, adulterado);
    }

    [Fact]
    public void Assinar_MudaQuandoOSegredoMuda()
    {
        var certa = WebhookExportador.Assinar(Segredo, Timestamp, Corpo);
        var errada = WebhookExportador.Assinar("outro-segredo", Timestamp, Corpo);

        Assert.NotEqual(certa, errada);
    }
}
