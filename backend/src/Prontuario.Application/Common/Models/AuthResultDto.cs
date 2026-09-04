namespace Prontuario.Application.Common.Models;

public sealed record AuthResultDto(bool Sucesso, string? Token, string? Erro);
