namespace Prontuario.Application.Common.Models;

public sealed record AuthResultDto(bool Sucesso, string? Token, string? Erro, UsuarioLogadoDto? Usuario = null);

/// <summary>Identificacao do medico para o cabecalho da aplicacao, devolvida junto com o token.</summary>
public sealed record UsuarioLogadoDto(string Nome, string Email, string Papel, string ClinicaNome, string ModoOperacao);
