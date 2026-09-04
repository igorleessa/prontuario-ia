using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;

namespace Prontuario.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/atendimentos")]
public class AtendimentosController : ControllerBase
{
    private readonly IAtendimentoService _atendimentos;

    public AtendimentosController(IAtendimentoService atendimentos) => _atendimentos = atendimentos;

    public record AbrirAtendimentoRequest(Guid PacienteRefId);

    [HttpPost]
    public async Task<IActionResult> Abrir(AbrirAtendimentoRequest request, CancellationToken cancellationToken)
    {
        var medicoId = ObterUsuarioLogadoId();
        var id = await _atendimentos.AbrirAsync(request.PacienteRefId, medicoId, cancellationToken);
        return CreatedAtAction(nameof(Abrir), new { id }, new { id });
    }

    [HttpPost("{id:guid}/consentimento")]
    public async Task<IActionResult> RegistrarConsentimento(Guid id, CancellationToken cancellationToken)
    {
        await _atendimentos.RegistrarConsentimentoAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/confirmar")]
    public async Task<IActionResult> Confirmar(Guid id, RascunhoClinicoDto revisado, CancellationToken cancellationToken)
    {
        await _atendimentos.ConfirmarAsync(id, revisado, cancellationToken);
        return NoContent();
    }

    private Guid ObterUsuarioLogadoId()
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.Parse(sub!);
    }
}
