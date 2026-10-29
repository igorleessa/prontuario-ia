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

/// <summary>
/// Rascunho com a origem de cada campo: as sequencias dos trechos da
/// transcricao que sustentam o que a IA escreveu. Deixa o medico conferir a
/// sugestao na propria fala, em vez de confiar nela.
/// </summary>
public sealed record RascunhoComOrigensDto(
    RascunhoClinicoDto Rascunho,
    IReadOnlyDictionary<string, IReadOnlyList<int>> Origens)
{
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<int>> SemOrigens =
        new Dictionary<string, IReadOnlyList<int>>();
}
