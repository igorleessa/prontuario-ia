namespace Prontuario.Application.Common.Interfaces;

/// <summary>Gera o PDF da nota clinica para o medico anexar ou imprimir no EMR (RF18).</summary>
public interface IGeradorPdfNota
{
    byte[] Gerar(DadosPdfNota dados);
}

public sealed record DadosPdfNota(
    string ClinicaNome,
    string PacienteNome,
    string? PacienteDocumento,
    string MedicoNome,
    DateTime DataHora,
    string Conteudo);
