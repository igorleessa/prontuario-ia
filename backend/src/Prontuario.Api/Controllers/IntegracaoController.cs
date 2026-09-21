using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;

namespace Prontuario.Api.Controllers;

/// <summary>
/// API que o EMR do cliente chama para abrir um atendimento (Modalidade B).
/// Nao usa o JWT do medico: quem chama e um sistema, autenticado pela chave de
/// integracao da clinica no cabecalho X-Api-Key. Nenhum dado clinico e devolvido
/// por aqui - apenas o link para o medico gravar a consulta.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/integracao")]
public class IntegracaoController : ControllerBase
{
    public const string CabecalhoChave = "X-Api-Key";

    private readonly IConfiguracaoExportacaoService _configuracoes;
    private readonly IIntegracaoService _integracao;

    public IntegracaoController(IConfiguracaoExportacaoService configuracoes, IIntegracaoService integracao)
    {
        _configuracoes = configuracoes;
        _integracao = integracao;
    }

    [HttpPost("atendimentos")]
    public async Task<ActionResult<AtendimentoExternoCriadoDto>> Abrir(
        AbrirAtendimentoExternoDto pedido, CancellationToken cancellationToken)
    {
        var clinicaId = await ResolverClinicaAsync(cancellationToken);
        if (clinicaId is null)
        {
            return Unauthorized(new { erro = "Chave de integracao ausente ou invalida." });
        }

        try
        {
            var criado = await _integracao.AbrirAtendimentoAsync(clinicaId.Value, pedido, cancellationToken);
            return Created(criado.Url, criado);
        }
        catch (InvalidOperationException excecao)
        {
            return BadRequest(new { erro = excecao.Message });
        }
    }

    /// <summary>Permite ao cliente validar a chave na implantacao, sem criar nada.</summary>
    [HttpGet("ping")]
    public async Task<IActionResult> Ping(CancellationToken cancellationToken)
    {
        var clinicaId = await ResolverClinicaAsync(cancellationToken);
        return clinicaId is null
            ? Unauthorized(new { erro = "Chave de integracao ausente ou invalida." })
            : Ok(new { status = "ok", clinicaId });
    }

    private async Task<Guid?> ResolverClinicaAsync(CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue(CabecalhoChave, out var chave) || string.IsNullOrWhiteSpace(chave))
        {
            return null;
        }

        return await _configuracoes.ResolverClinicaPorChaveAsync(chave.ToString(), cancellationToken);
    }
}
