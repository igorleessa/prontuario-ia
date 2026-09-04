namespace Prontuario.Application.Common.Models;

/// <summary>
/// Forma estruturada que a extracao por IA (RF08) precisa produzir a partir
/// da transcricao. Usado tanto para persistir RascunhoIA quanto para alimentar
/// a tela de revisao do medico (RF09).
/// </summary>
public sealed record RascunhoClinicoDto(
    string? QueixaPrincipal,
    string? Hda,
    string? Antecedentes,
    string? ExameFisico,
    string? HipoteseDiagnostica,
    string? Cid10Sugerido,
    string? Conduta);
