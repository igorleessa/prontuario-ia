using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;

namespace Prontuario.Api.Controllers;

/// <summary>
/// Exame ocupacional. Nenhuma rota guarda a conversa: as falas chegam em lotes
/// pequenos, viram respostas do prontuario e sao esquecidas.
/// </summary>
[ApiController]
[Authorize]
[Route("api/atendimentos/{id:guid}/ocupacional")]
public class OcupacionalController : ApiControllerBase
{
    private const long TamanhoMaximoTrechoBytes = 5L * 1024 * 1024;

    private readonly IProntuarioOcupacionalService _ocupacional;

    public OcupacionalController(IProntuarioOcupacionalService ocupacional) => _ocupacional = ocupacional;

    [HttpPost]
    public async Task<IActionResult> Iniciar(Guid id, IniciarOcupacionalDto pedido, CancellationToken cancellationToken)
        => Responder(await _ocupacional.IniciarAsync(id, ClinicaId, pedido, cancellationToken));

    [HttpGet]
    public async Task<IActionResult> Obter(Guid id, CancellationToken cancellationToken)
        => Responder(await _ocupacional.ObterAsync(id, ClinicaId, cancellationToken));

    /// <summary>Resposta dada pelo medico na tela.</summary>
    [HttpPut("respostas/{campo}")]
    public async Task<IActionResult> ResponderCampo(Guid id, string campo, ResponderCampoDto pedido, CancellationToken cancellationToken)
        => Responder(await _ocupacional.ResponderAsync(id, ClinicaId, campo, pedido.Valor, cancellationToken));

    /// <summary>Lote de falas novas da conversa: a IA preenche o que elas dizem.</summary>
    [HttpPost("falas")]
    public async Task<IActionResult> ProcessarFalas(Guid id, ProcessarFalasDto falas, CancellationToken cancellationToken)
        => Responder(await _ocupacional.ProcessarFalasAsync(id, ClinicaId, falas, cancellationToken));

    /// <summary>Modo alternativo ao tempo real: alguns segundos de audio, transcritos em memoria e descartados.</summary>
    [HttpPost("transcrever")]
    [RequestSizeLimit(TamanhoMaximoTrechoBytes)]
    public async Task<IActionResult> Transcrever(Guid id, IFormFile audio, CancellationToken cancellationToken)
    {
        await using var conteudo = audio.OpenReadStream();
        var resultado = await _ocupacional.TranscreverAsync(id, ClinicaId, conteudo, cancellationToken);
        return resultado.Resultado == ResultadoAtendimento.Ok ? Ok(new { texto = resultado.Valor ?? string.Empty }) : Responder(resultado);
    }

    [HttpPut("justificativas/{chave}")]
    public async Task<IActionResult> Justificar(Guid id, string chave, JustificarAlertaDto pedido, CancellationToken cancellationToken)
        => Responder(await _ocupacional.JustificarAsync(id, ClinicaId, chave, pedido.Texto, cancellationToken));

    [HttpPost("concluir")]
    public async Task<IActionResult> Concluir(Guid id, CancellationToken cancellationToken)
        => Responder(await _ocupacional.ConcluirAsync(id, ClinicaId, cancellationToken));

    private IActionResult Responder<T>(ResultadoOperacao<T> resultado) => resultado.Resultado switch
    {
        ResultadoAtendimento.Ok => Ok(resultado.Valor),
        ResultadoAtendimento.Conflito => Conflict(new { erro = resultado.Erro }),
        ResultadoAtendimento.Falha => StatusCode(StatusCodes.Status502BadGateway, new { erro = resultado.Erro }),
        _ => NotFound(),
    };
}
