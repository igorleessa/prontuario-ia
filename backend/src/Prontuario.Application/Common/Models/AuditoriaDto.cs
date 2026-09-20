namespace Prontuario.Application.Common.Models;

/// <summary>Linha da trilha de auditoria exibida ao administrador (RF11).</summary>
public sealed record LogAuditoriaDto(
    Guid Id,
    DateTime Ocorrido,
    string UsuarioNome,
    string Acao,
    Guid? AtendimentoId,
    string? PacienteNome,
    string? Detalhe);
