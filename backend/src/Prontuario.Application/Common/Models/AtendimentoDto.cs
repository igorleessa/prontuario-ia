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
    /// <summary>
    /// Sugestao original da IA, preservada mesmo depois de o medico editar.
    /// Permite mostrar lado a lado o que a IA propos e o que ficou no registro -
    /// a revisao humana deixa de ser promessa e vira evidencia.
    /// </summary>
    RascunhoClinicoDto? SugestaoIA,
    string? Transcricao,
    string? ErroProcessamentoIA,
    string? TemplateNome,
    int? DuracaoGravacaoSegundos);

/// <summary>
/// Numeros da clinica para a tela de atendimentos. O tempo economizado e uma
/// estimativa declarada: compara o tempo real entre o fim da consulta e a
/// confirmacao do registro com uma linha de base de digitacao manual,
/// configuravel por ambiente.
/// </summary>
public sealed record MetricasClinicaDto(
    int AtendimentosFinalizados,
    int MinutosDeConsultaDocumentados,
    int? TempoMedioRevisaoSegundos,
    int MinutosDocumentacaoManualEstimados,
    int? EconomiaPercentualEstimada);

/// <summary>Historico de atendimentos de um paciente (RF16).</summary>
public sealed record HistoricoPacienteDto(
    Guid PacienteId,
    string PacienteNome,
    string? Cpf,
    DateOnly? DataNascimento,
    string? IdExternoEmr,
    IReadOnlyList<HistoricoAtendimentoDto> Atendimentos);

public sealed record HistoricoAtendimentoDto(
    Guid Id,
    DateTime DataHora,
    string Status,
    string MedicoNome,
    string? QueixaPrincipal,
    string? HipoteseDiagnostica,
    bool Finalizado);
