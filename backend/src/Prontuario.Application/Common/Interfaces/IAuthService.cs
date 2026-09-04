using Prontuario.Application.Common.Models;

namespace Prontuario.Application.Common.Interfaces;

/// <summary>Login com e-mail/senha e emissao de JWT (RF01).</summary>
public interface IAuthService
{
    Task<AuthResultDto> LoginAsync(string email, string senha, CancellationToken cancellationToken = default);
}
