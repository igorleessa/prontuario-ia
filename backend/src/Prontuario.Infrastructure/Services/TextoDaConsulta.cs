using Prontuario.Domain.Entities;
using Prontuario.Domain.Enums;

namespace Prontuario.Infrastructure.Services;

/// <summary>Como a transcricao e apresentada a IA, com quem disse cada trecho quando se sabe.</summary>
public static class TextoDaConsulta
{
    /// <summary>
    /// Trechos numerados pela sequencia, para a IA citar a origem de cada campo
    /// e dizer quem falou. Falante confirmado pelo medico vai entre parenteses.
    /// </summary>
    public static string Numerado(Transcricao transcricao)
        => transcricao.Trechos.Count == 0
            ? transcricao.Texto
            : string.Join(
                "\n",
                Ordenados(transcricao).Select(t =>
                    t.FalanteConfirmado && t.Falante != Falante.Indefinido
                        ? $"[{t.Sequencia}] ({Rotulo(t.Falante).ToLowerInvariant()}) {t.Texto}"
                        : $"[{t.Sequencia}] {t.Texto}"));

    /// <summary>Em forma de dialogo ("Medico: ..."), para a redacao dos documentos.</summary>
    public static string Dialogo(Transcricao transcricao)
        => transcricao.Trechos.Count == 0
            ? transcricao.Texto
            : string.Join(
                "\n",
                Ordenados(transcricao).Select(t =>
                    t.Falante == Falante.Indefinido ? t.Texto : $"{Rotulo(t.Falante)}: {t.Texto}"));

    public static string Rotulo(Falante falante) => falante switch
    {
        Falante.Medico => "Medico",
        Falante.Paciente => "Paciente",
        Falante.Acompanhante => "Acompanhante",
        _ => "Indefinido",
    };

    private static IEnumerable<TrechoTranscricao> Ordenados(Transcricao transcricao)
        => transcricao.Trechos.OrderBy(t => t.Sequencia).Where(t => t.Texto.Length > 0);
}
