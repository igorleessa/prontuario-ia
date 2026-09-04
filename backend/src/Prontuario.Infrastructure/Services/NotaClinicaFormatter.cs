using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;

namespace Prontuario.Infrastructure.Services;

public class NotaClinicaFormatter : INotaClinicaFormatter
{
    private static readonly string[] SemConteudo = ["Nao informado"];

    public string Formatar(RascunhoClinicoDto d)
    {
        var hipotese = string.IsNullOrWhiteSpace(d.Cid10Sugerido)
            ? Preencher(d.HipoteseDiagnostica)
            : $"{Preencher(d.HipoteseDiagnostica)} (CID-10 {d.Cid10Sugerido!.Trim()})";

        return string.Join(Environment.NewLine + Environment.NewLine, new[]
        {
            $"QUEIXA PRINCIPAL{Environment.NewLine}{Preencher(d.QueixaPrincipal)}",
            $"HISTORIA DA DOENCA ATUAL{Environment.NewLine}{Preencher(d.Hda)}",
            $"ANTECEDENTES{Environment.NewLine}{Preencher(d.Antecedentes)}",
            $"EXAME FISICO{Environment.NewLine}{Preencher(d.ExameFisico)}",
            $"HIPOTESE DIAGNOSTICA{Environment.NewLine}{hipotese}",
            $"CONDUTA{Environment.NewLine}{Preencher(d.Conduta)}",
        });
    }

    private static string Preencher(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? SemConteudo[0] : valor.Trim();
}
