namespace Prontuario.Application.Common.Models;

/// <summary>Paciente da clinica, para a lista de selecao ao abrir um atendimento.</summary>
public sealed record PacienteResumoDto(
    Guid Id,
    string Nome,
    string? Cpf,
    DateOnly? DataNascimento,
    string? Contato,
    string? IdExternoEmr);

/// <summary>
/// Cadastro minimo de paciente. Na Modalidade B (Conector) o paciente ja existe
/// no EMR externo, entao IdExternoEmr identifica o registro de origem (RF14/RF17).
/// </summary>
public sealed record NovoPacienteDto(
    string Nome,
    string? Cpf,
    DateOnly? DataNascimento,
    string? Contato,
    string? IdExternoEmr);
