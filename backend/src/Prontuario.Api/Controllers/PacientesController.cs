using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;

namespace Prontuario.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/pacientes")]
public class PacientesController : ApiControllerBase
{
    private readonly IPacienteService _pacientes;

    public PacientesController(IPacienteService pacientes) => _pacientes = pacientes;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PacienteResumoDto>>> Listar(
        [FromQuery] string? busca, CancellationToken cancellationToken)
        => Ok(await _pacientes.ListarAsync(ClinicaId, busca, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<PacienteResumoDto>> Criar(NovoPacienteDto novo, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(novo.Nome))
        {
            return BadRequest(new { erro = "Informe o nome do paciente." });
        }

        var paciente = await _pacientes.CriarAsync(ClinicaId, novo, cancellationToken);
        return CreatedAtAction(nameof(Listar), new { id = paciente.Id }, paciente);
    }
}
