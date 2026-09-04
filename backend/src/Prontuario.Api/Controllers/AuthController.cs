using Microsoft.AspNetCore.Mvc;
using Prontuario.Application.Common.Interfaces;

namespace Prontuario.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) => _authService = authService;

    public record LoginRequest(string Email, string Senha);

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var resultado = await _authService.LoginAsync(request.Email, request.Senha, cancellationToken);
        if (!resultado.Sucesso)
        {
            return Unauthorized(new { erro = resultado.Erro });
        }

        return Ok(new { token = resultado.Token });
    }
}
