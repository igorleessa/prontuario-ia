using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace Prontuario.Api.Controllers;

/// <summary>
/// Concentra a leitura da identidade do token. Toda consulta e escrita usa a
/// clinica vinda do JWT, nunca uma informada pelo cliente, para que um
/// identificador adivinhado nao alcance dados de outra clinica.
/// </summary>
public abstract class ApiControllerBase : ControllerBase
{
    protected Guid UsuarioLogadoId => Guid.Parse(
        User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    protected Guid ClinicaId => Guid.Parse(User.FindFirstValue("clinicaId")!);
}
