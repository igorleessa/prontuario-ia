using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;
using Prontuario.Infrastructure.Services;

namespace Prontuario.Api.Controllers;

/// <summary>
/// Consulta transcrita ao vivo. Nenhuma rota recebe a gravacao da consulta:
/// no modo tempo real chega so texto, e no modo por trechos cada pedaco de
/// poucos segundos e transcrito em memoria e descartado na mesma requisicao.
/// </summary>
[ApiController]
[Authorize]
[Route("api/atendimentos/{id:guid}/transcricao")]
public class TranscricaoController : ApiControllerBase
{
    /// <summary>Um trecho tem poucos segundos de fala em opus: 5 MB e folga, nao porta aberta.</summary>
    private const long TamanhoMaximoTrechoBytes = 5L * 1024 * 1024;

    private readonly ITranscricaoConsultaService _transcricao;

    public TranscricaoController(ITranscricaoConsultaService transcricao) => _transcricao = transcricao;

    public record EncerrarRequest(int DuracaoSegundos);

    /// <summary>Abre a escuta e devolve como o navegador deve transcrever (tempo real ou trechos).</summary>
    [HttpPost("sessao")]
    public async Task<IActionResult> Iniciar(Guid id, CancellationToken cancellationToken)
        => Responder(await _transcricao.IniciarAsync(id, ClinicaId, cancellationToken));

    /// <summary>Trecho ja transcrito pelo provedor em tempo real: so texto.</summary>
    [HttpPost("trechos")]
    public async Task<IActionResult> RegistrarTrecho(
        Guid id, RegistrarTrechoDto trecho, CancellationToken cancellationToken)
        => Responder(await _transcricao.RegistrarTrechoAsync(id, ClinicaId, trecho, cancellationToken));

    /// <summary>Modo por trechos: alguns segundos de audio, transcritos em memoria e descartados.</summary>
    [HttpPost("trechos-audio")]
    [RequestSizeLimit(TamanhoMaximoTrechoBytes)]
    public async Task<IActionResult> TranscreverTrecho(
        Guid id, IFormFile audio, [FromForm] int sequencia, CancellationToken cancellationToken)
    {
        await using var conteudo = audio.OpenReadStream();
        var resultado = await _transcricao.TranscreverTrechoAsync(id, ClinicaId, sequencia, conteudo, cancellationToken);

        // Trecho sem fala: nada a mostrar, e nao e erro.
        return resultado is { Resultado: ResultadoAtendimento.Ok, Valor: null } ? NoContent() : Responder(resultado);
    }

    /// <summary>Refaz o rascunho do prontuario sobre o que ja foi dito (preenchimento progressivo).</summary>
    [HttpPost("rascunho")]
    public async Task<IActionResult> GerarRascunho(Guid id, CancellationToken cancellationToken)
        => Responder(await _transcricao.GerarRascunhoAsync(id, ClinicaId, cancellationToken));

    [HttpPost("encerrar")]
    public async Task<IActionResult> Encerrar(Guid id, EncerrarRequest request, CancellationToken cancellationToken)
        => await _transcricao.EncerrarAsync(id, ClinicaId, request.DuracaoSegundos, cancellationToken) switch
        {
            ResultadoAtendimento.Ok => NoContent(),
            ResultadoAtendimento.Conflito => Conflict(new { erro = "Este atendimento ja foi encerrado." }),
            _ => NotFound(),
        };

    /// <summary>
    /// Falas da consulta ficticia, que a tela envia uma a uma como se viessem
    /// do provedor. So responde com Demonstracao:Habilitado.
    /// </summary>
    [HttpGet("/api/demonstracao/consulta")]
    public IActionResult ConsultaDemonstracao([FromServices] IConfiguration configuracao)
        => configuracao.GetValue<bool>("Demonstracao:Habilitado")
            ? Ok(new { trechos = ConsultaExemplo.Trechos() })
            : NotFound();

    private IActionResult Responder<T>(ResultadoOperacao<T> resultado) => resultado.Resultado switch
    {
        ResultadoAtendimento.Ok => Ok(resultado.Valor),
        ResultadoAtendimento.Conflito => Conflict(new { erro = resultado.Erro }),
        // O provedor de IA falhou: o atendimento segue, preenchivel a mao.
        ResultadoAtendimento.Falha => StatusCode(StatusCodes.Status502BadGateway, new { erro = resultado.Erro }),
        _ => NotFound(),
    };
}
