using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;

namespace Prontuario.Api.Controllers;

/// <summary>
/// Credenciais de IA da clinica. A chave entra por aqui e nunca sai: as respostas
/// carregam apenas os ultimos caracteres, para o usuario reconhecer qual esta ativa.
/// </summary>
[ApiController]
[Authorize]
[Route("api/configuracao-ia")]
public class ConfiguracaoIAController : ApiControllerBase
{
    private readonly IConfiguracaoIAService _configuracoes;

    public ConfiguracaoIAController(IConfiguracaoIAService configuracoes) => _configuracoes = configuracoes;

    [HttpGet]
    public async Task<ActionResult<ConfiguracaoIADto>> Obter(CancellationToken cancellationToken)
        => Ok(await _configuracoes.ObterAsync(ClinicaId, cancellationToken));

    [HttpPut]
    public async Task<ActionResult<ConfiguracaoIADto>> Salvar(
        SalvarConfiguracaoIADto dados, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _configuracoes.SalvarAsync(ClinicaId, UsuarioLogadoId, dados, cancellationToken));
        }
        catch (InvalidOperationException excecao)
        {
            return BadRequest(new { erro = excecao.Message });
        }
    }

    [HttpDelete]
    public async Task<IActionResult> Remover(CancellationToken cancellationToken)
        => await _configuracoes.RemoverChaveAsync(ClinicaId, cancellationToken) ? NoContent() : NotFound();
}
