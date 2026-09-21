namespace Prontuario.Application.Common.Models;

/// <summary>
/// Pedido vindo do EMR do cliente para abrir um atendimento (Modalidade B).
/// O paciente e identificado pela referencia externa - nao duplicamos o
/// cadastro clinico que ja existe do outro lado (RF17).
/// </summary>
public sealed record AbrirAtendimentoExternoDto(
    string? IdExternoEmr,
    string? Cpf,
    string? Nome,
    DateOnly? DataNascimento,
    string? MedicoEmail);

/// <summary>
/// Resposta ao EMR: o identificador do atendimento e o endereco que o sistema
/// do cliente abre em nova aba para o medico gravar a consulta.
/// </summary>
public sealed record AtendimentoExternoCriadoDto(Guid AtendimentoId, Guid PacienteId, string Url);
