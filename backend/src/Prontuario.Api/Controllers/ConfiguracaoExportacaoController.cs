using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;

namespace Prontuario.Api.Controllers;

/// <summary>
/// Configuracao de saida da Modalidade B: para onde a nota revisada e enviada
/// (RF19) e com que chave o EMR do cliente abre atendimentos por API. Nem o
/// segredo do webhook nem a chave de integracao voltam em consultas - a chave
/// so aparece em claro na resposta que a gera.
/// </summary>
[ApiController]
[Authorize]
[Route("api/configuracao-exportacao")]
public class ConfiguracaoExportacaoController : ApiControllerBase
{
    private readonly IConfiguracaoExportacaoService _configuracoes;
    private readonly IExportadorNota _exportador;

    public ConfiguracaoExportacaoController(
        IConfiguracaoExportacaoService configuracoes, IExportadorNota exportador)
    {
        _configuracoes = configuracoes;
        _exportador = exportador;
    }

    [HttpGet]
    public async Task<ActionResult<ConfiguracaoExportacaoDto>> Obter(CancellationToken cancellationToken)
        => Ok(await _configuracoes.ObterAsync(ClinicaId, cancellationToken));

    [HttpPut]
    [Authorize(Roles = PapeisAutorizacao.Administrador)]
    public async Task<ActionResult<ConfiguracaoExportacaoDto>> Salvar(
        SalvarConfiguracaoExportacaoDto dados, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _configuracoes.SalvarAsync(ClinicaId, dados, cancellationToken));
        }
        catch (InvalidOperationException excecao)
        {
            return BadRequest(new { erro = excecao.Message });
        }
    }

    /// <summary>Envia um evento ficticio ao webhook para o cliente confirmar a integracao sem usar dado real.</summary>
    [HttpPost("testar")]
    [Authorize(Roles = PapeisAutorizacao.Administrador)]
    public async Task<ActionResult<ResultadoExportacaoDto>> Testar(CancellationToken cancellationToken)
        => Ok(await _exportador.TestarAsync(ClinicaId, cancellationToken));

    [HttpPost("chave-integracao")]
    [Authorize(Roles = PapeisAutorizacao.Administrador)]
    public async Task<ActionResult<ChaveIntegracaoGeradaDto>> GerarChave(CancellationToken cancellationToken)
        => Ok(await _configuracoes.GerarChaveIntegracaoAsync(ClinicaId, cancellationToken));

    [HttpDelete("chave-integracao")]
    [Authorize(Roles = PapeisAutorizacao.Administrador)]
    public async Task<IActionResult> RevogarChave(CancellationToken cancellationToken)
        => await _configuracoes.RevogarChaveIntegracaoAsync(ClinicaId, cancellationToken) ? NoContent() : NotFound();
}
