namespace Prontuario.Api.Controllers;

/// <summary>
/// Nomes dos papeis como saem do claim do JWT. Centralizados para que uma
/// mudanca no enum PapelUsuario nao deixe um [Authorize] com texto antigo.
/// </summary>
public static class PapeisAutorizacao
{
    public const string Administrador = nameof(Domain.Enums.PapelUsuario.Administrador);
    public const string Medico = nameof(Domain.Enums.PapelUsuario.Medico);
}
