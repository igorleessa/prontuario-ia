using Prontuario.Application.Common.Models;
using Prontuario.Infrastructure.Services;

namespace Prontuario.Application.Tests;

/// <summary>
/// O formatador e a unica fonte do texto que sai daqui: a previa mostrada ao
/// medico e a nota efetivamente exportada ao EMR passam por ele. Divergir seria
/// mostrar ao medico algo diferente do que o cliente recebe.
/// </summary>
public class NotaClinicaFormatterTests
{
    private readonly NotaClinicaFormatter _formatador = new();

    [Fact]
    public void Formatar_ComTodosOsCampos_MantemAOrdemDoProntuario()
    {
        var texto = _formatador.Formatar(new RascunhoClinicoDto(
            "Tosse ha 4 dias", "Inicio subito", "Hipertensao", "Roncos difusos",
            "Infeccao respiratoria alta", "J06.9", "Sintomaticos"));

        var secoes = new[]
        {
            "QUEIXA PRINCIPAL", "HISTORIA DA DOENCA ATUAL", "ANTECEDENTES",
            "EXAME FISICO", "HIPOTESE DIAGNOSTICA", "CONDUTA",
        };

        var posicoes = secoes.Select(secao => texto.IndexOf(secao, StringComparison.Ordinal)).ToList();

        Assert.DoesNotContain(-1, posicoes);
        Assert.Equal(posicoes.OrderBy(p => p), posicoes);
    }

    [Fact]
    public void Formatar_ComCid_AnexaOCodigoNaHipotese()
    {
        var texto = _formatador.Formatar(new RascunhoClinicoDto(
            null, null, null, null, "Infeccao respiratoria alta", "J06.9", null));

        Assert.Contains("Infeccao respiratoria alta (CID-10 J06.9)", texto);
    }

    [Fact]
    public void Formatar_SemCid_NaoInventaParenteses()
    {
        var texto = _formatador.Formatar(new RascunhoClinicoDto(
            null, null, null, null, "Infeccao respiratoria alta", null, null));

        Assert.Contains("Infeccao respiratoria alta", texto);
        Assert.DoesNotContain("CID-10", texto);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Formatar_CampoVazio_DizQueNaoFoiInformado(string? valor)
    {
        var texto = _formatador.Formatar(new RascunhoClinicoDto(
            valor, valor, valor, valor, valor, valor, valor));

        // Secao em branco no prontuario e ambigua: nao se sabe se foi esquecida
        // ou se nao havia o que registrar.
        Assert.Equal(6, texto.Split("Nao informado").Length - 1);
    }
}
