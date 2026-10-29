using Prontuario.Application.Common.Models;
using Prontuario.Infrastructure.Services;

namespace Prontuario.Application.Tests;

/// <summary>
/// O que separa uma transcricao que funciona no Safari de uma que falha, e um
/// CID que entra limpo no prontuario de um que entra com a descricao colada.
/// </summary>
public class PrecisaoIATests
{
    [Theory]
    [InlineData(new byte[] { 0x1A, 0x45, 0xDF, 0xA3, 0x01 }, "webm")]
    [InlineData(new byte[] { 0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70, 0x4D, 0x34, 0x41 }, "mp4")]
    [InlineData(new byte[] { 0x4F, 0x67, 0x67, 0x53, 0x00 }, "ogg")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x41, 0x56, 0x45 }, "wav")]
    [InlineData(new byte[] { 0x49, 0x44, 0x33, 0x04 }, "mp3")]
    [InlineData(new byte[] { 0x01, 0x02 }, "webm")]
    public void Detecta_o_formato_pelo_cabecalho(byte[] cabecalho, string extensao)
    {
        Assert.Equal(extensao, FormatoAudio.Detectar(cabecalho).Extensao);
    }

    [Theory]
    [InlineData("J06.9", "J06.9")]
    [InlineData("j06.9 - Infeccao aguda das vias aereas superiores", "J06.9")]
    [InlineData("CID J069", "J06.9")]
    [InlineData("I10", "I10")]
    [InlineData("nao se aplica", null)]
    [InlineData("  ", null)]
    [InlineData(null, null)]
    public void Reduz_o_cid_ao_codigo(string? entrada, string? esperado)
    {
        Assert.Equal(esperado, OpenAiClinicalNoteGenerator.NormalizarCid(entrada));
    }

    [Fact]
    public void Campo_em_branco_vira_null_para_o_medico_preencher()
    {
        var normalizado = OpenAiClinicalNoteGenerator.Normalizar(
            new RascunhoClinicoDto("  Tosse ha 4 dias. ", "", "null", null, "IVAS", "J06.9", " "));

        Assert.Equal("Tosse ha 4 dias.", normalizado.QueixaPrincipal);
        Assert.Null(normalizado.Hda);
        Assert.Null(normalizado.Antecedentes);
        Assert.Null(normalizado.Conduta);
        Assert.Equal("J06.9", normalizado.Cid10Sugerido);
    }
}

public class OrigensRascunhoTests
{
    [Fact]
    public void LerOrigens_AceitaListasDeInteirosEIgnoraOResto()
    {
        const string resposta = """
            {"queixaPrincipal":"Tosse.","origens":{"queixaPrincipal":[3,3,5],"hda":[],"conduta":"x","exameFisico":[1.5,"2"]}}
            """;

        var origens = Prontuario.Infrastructure.Services.OpenAiClinicalNoteGenerator.LerOrigens(resposta);

        Assert.Equal([3, 5], origens["queixaPrincipal"]);
        Assert.Single(origens);
    }

    [Fact]
    public void LerOrigens_SemOrigens_DevolveVazio()
        => Assert.Empty(Prontuario.Infrastructure.Services.OpenAiClinicalNoteGenerator.LerOrigens("""{"hda":null}"""));
}
