using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Prontuario.Application.Common.Interfaces;

namespace Prontuario.Api;

/// <summary>
/// Le a identidade do JWT da requisicao em curso. Fora de uma requisicao - no
/// worker de IA, por exemplo - todas as propriedades sao nulas.
/// </summary>
public class UsuarioAtual : IUsuarioAtual
{
    private readonly IHttpContextAccessor _acessor;

    public UsuarioAtual(IHttpContextAccessor acessor) => _acessor = acessor;

    public Guid? Id => Ler(JwtRegisteredClaimNames.Sub) ?? Ler(ClaimTypes.NameIdentifier);

    public Guid? ClinicaId => Ler("clinicaId");

    public string? Papel => Usuario?.FindFirstValue(ClaimTypes.Role);

    private ClaimsPrincipal? Usuario => _acessor.HttpContext?.User;

    private Guid? Ler(string tipo)
        => Guid.TryParse(Usuario?.FindFirstValue(tipo), out var valor) ? valor : null;
}
