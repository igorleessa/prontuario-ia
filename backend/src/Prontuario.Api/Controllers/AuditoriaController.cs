using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;

namespace Prontuario.Api.Controllers;

/// <summary>
/// Trilha de auditoria da clinica (RF11). Restrita ao administrador e apenas de
/// leitura: mostra quem acessou qual atendimento e quando, nunca o conteudo clinico.
/// </summary>
[ApiController]
[Authorize(Roles = PapeisAutorizacao.Administrador)]
[Route("api/auditoria")]
public class AuditoriaController : ApiControllerBase
{
    private readonly IAuditoriaService _auditoria;

    public AuditoriaController(IAuditoriaService auditoria) => _auditoria = auditoria;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LogAuditoriaDto>>> Listar(
        [FromQuery] string? acao, [FromQuery] int limite = 200, CancellationToken cancellationToken = default)
        => Ok(await _auditoria.ListarAsync(ClinicaId, acao, limite, cancellationToken));
}
