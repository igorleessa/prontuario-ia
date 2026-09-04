using Prontuario.Application.Common.Models;

namespace Prontuario.Application.Common.Interfaces;

public interface IPacienteService
{
    /// <summary>Lista os pacientes da clinica, opcionalmente filtrados por nome ou CPF.</summary>
    Task<IReadOnlyList<PacienteResumoDto>> ListarAsync(
        Guid clinicaId, string? busca = null, CancellationToken cancellationToken = default);

    Task<PacienteResumoDto> CriarAsync(
        Guid clinicaId, NovoPacienteDto novo, CancellationToken cancellationToken = default);
}
