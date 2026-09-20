using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;

namespace Prontuario.Api.Controllers;

/// <summary>
/// Preferencias do proprio usuario. Diferente das configuracoes de clinica,
/// cada medico ajusta as suas - inclusive o estilo de redacao que a IA segue.
/// </summary>
[ApiController]
[Authorize]
[Route("api/perfil")]
public class PerfilController : ApiControllerBase
{
    private readonly IPerfilService _perfil;

    public PerfilController(IPerfilService perfil) => _perfil = perfil;

    public record SalvarEstiloRequest(string? InstrucoesEstilo);

    [HttpGet]
    public async Task<ActionResult<PerfilMedicoDto>> Obter(CancellationToken cancellationToken)
    {
        var perfil = await _perfil.ObterAsync(UsuarioLogadoId, cancellationToken);
        return perfil is null ? NotFound() : Ok(perfil);
    }

    [HttpPut("estilo")]
    public async Task<ActionResult<PerfilMedicoDto>> SalvarEstilo(
        SalvarEstiloRequest pedido, CancellationToken cancellationToken)
    {
        try
        {
            var perfil = await _perfil.SalvarEstiloAsync(
                UsuarioLogadoId, pedido.InstrucoesEstilo, cancellationToken);

            return perfil is null ? NotFound() : Ok(perfil);
        }
        catch (InvalidOperationException excecao)
        {
            return BadRequest(new { erro = excecao.Message });
        }
    }
}
