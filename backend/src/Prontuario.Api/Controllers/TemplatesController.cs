using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;

namespace Prontuario.Api.Controllers;

/// <summary>Modelos de nota por especialidade oferecidos na abertura do atendimento.</summary>
[ApiController]
[Authorize]
[Route("api/templates")]
public class TemplatesController : ApiControllerBase
{
    private readonly ICatalogoTemplatesService _catalogo;

    public TemplatesController(ICatalogoTemplatesService catalogo) => _catalogo = catalogo;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TemplateNotaDto>>> Listar(CancellationToken cancellationToken)
        => Ok(await _catalogo.ListarAsync(ClinicaId, cancellationToken));
}
