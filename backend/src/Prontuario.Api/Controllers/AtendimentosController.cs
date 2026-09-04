using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;

namespace Prontuario.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/atendimentos")]
public class AtendimentosController : ApiControllerBase
{
    private readonly IAtendimentoService _atendimentos;
    private readonly INotaClinicaFormatter _formatador;

    public AtendimentosController(IAtendimentoService atendimentos, INotaClinicaFormatter formatador)
    {
        _atendimentos = atendimentos;
        _formatador = formatador;
    }

    /// <summary>Consulta longa em webm/opus fica na casa de poucos MB; 100 MB da folga sem virar porta aberta.</summary>
    private const long TamanhoMaximoAudioBytes = 100L * 1024 * 1024;

    public record AbrirAtendimentoRequest(Guid PacienteRefId);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AtendimentoResumoDto>>> Listar(CancellationToken cancellationToken)
        => Ok(await _atendimentos.ListarAsync(ClinicaId, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AtendimentoDetalheDto>> Obter(Guid id, CancellationToken cancellationToken)
    {
        var atendimento = await _atendimentos.ObterAsync(id, ClinicaId, cancellationToken);
        return atendimento is null ? NotFound() : Ok(atendimento);
    }

    [HttpPost]
    public async Task<IActionResult> Abrir(AbrirAtendimentoRequest request, CancellationToken cancellationToken)
    {
        var id = await _atendimentos.AbrirAsync(request.PacienteRefId, UsuarioLogadoId, ClinicaId, cancellationToken);
        if (id is null)
        {
            return NotFound(new { erro = "Paciente nao encontrado nesta clinica." });
        }

        return CreatedAtAction(nameof(Obter), new { id }, new { id });
    }

    [HttpPost("{id:guid}/consentimento")]
    public async Task<IActionResult> RegistrarConsentimento(Guid id, CancellationToken cancellationToken)
        => await _atendimentos.RegistrarConsentimentoAsync(id, ClinicaId, cancellationToken) ? NoContent() : NotFound();

    [HttpPost("{id:guid}/revisao")]
    public async Task<IActionResult> IniciarRevisao(Guid id, CancellationToken cancellationToken)
        => await _atendimentos.IniciarRevisaoAsync(id, ClinicaId, cancellationToken) ? NoContent() : NotFound();

    /// <summary>
    /// Recebe o audio da consulta (RF06) e dispara a transcricao e a extracao
    /// estruturada em segundo plano (RF07/RF08).
    /// </summary>
    [HttpPost("{id:guid}/audio")]
    [RequestSizeLimit(TamanhoMaximoAudioBytes)]
    public async Task<IActionResult> EnviarAudio(
        Guid id, IFormFile audio, [FromForm] int duracaoSegundos, CancellationToken cancellationToken)
    {
        if (audio.Length == 0)
        {
            return BadRequest(new { erro = "Audio vazio." });
        }

        await using var conteudo = audio.OpenReadStream();
        var registrado = await _atendimentos.RegistrarAudioAsync(
            id, ClinicaId, conteudo, audio.ContentType, duracaoSegundos, cancellationToken);

        return registrado ? Accepted() : NotFound();
    }

    [HttpPost("{id:guid}/confirmar")]
    public async Task<IActionResult> Confirmar(Guid id, RascunhoClinicoDto revisado, CancellationToken cancellationToken)
        => await _atendimentos.ConfirmarAsync(id, revisado, ClinicaId, cancellationToken) ? NoContent() : NotFound();

    /// <summary>
    /// Pre-visualiza a nota clinica de texto corrido (Modalidade B) para o conteudo
    /// em edicao, sem persistir nada. Permite ao medico ver o mesmo atendimento nos
    /// dois formatos - prontuario estruturado e nota para o EMR - independentemente
    /// da modalidade em que a clinica opera.
    /// </summary>
    [HttpPost("nota-previa")]
    public ActionResult<object> PreverNota(RascunhoClinicoDto revisado)
        => Ok(new { conteudo = _formatador.Formatar(revisado) });

    [HttpPost("{id:guid}/cancelar")]
    public async Task<IActionResult> Cancelar(Guid id, CancellationToken cancellationToken)
        => await _atendimentos.CancelarAsync(id, ClinicaId, cancellationToken) ? NoContent() : NotFound();
}
