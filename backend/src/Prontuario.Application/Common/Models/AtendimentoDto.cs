namespace Prontuario.Application.Common.Models;

/// <summary>Linha da lista de atendimentos da clinica.</summary>
public sealed record AtendimentoResumoDto(
    Guid Id,
    Guid PacienteId,
    string PacienteNome,
    string MedicoNome,
    DateTime DataHora,
    string Status,
    bool ConsentimentoGravacao);

/// <summary>
/// Atendimento aberto na tela de acompanhamento. <paramref name="Rascunho"/> traz o
/// que ja existe de conteudo clinico (revisao salva ou sugestao da IA), para que o
/// medico possa retomar um atendimento sem perder o que ja havia sido preenchido.
/// </summary>
public sealed record AtendimentoDetalheDto(
    Guid Id,
    Guid PacienteId,
    string PacienteNome,
    string? PacienteCpf,
    DateOnly? PacienteDataNascimento,
    string MedicoNome,
    DateTime DataHora,
    string Status,
    bool ConsentimentoGravacao,
    DateTime? ConsentimentoEm,
    RascunhoClinicoDto Rascunho,
    string? Transcricao,
    string? ErroProcessamentoIA);
