using Prontuario.Application.Common.Models;

namespace Prontuario.Application.Common.Interfaces;

/// <summary>Modelos de nota por especialidade disponiveis para a clinica.</summary>
public interface ICatalogoTemplatesService
{
    Task<IReadOnlyList<TemplateNotaDto>> ListarAsync(Guid clinicaId, CancellationToken cancellationToken = default);
}
