using System.Text.RegularExpressions;
using Prontuario.Application.Privacidade;
using Prontuario.Domain.Entities;

namespace Prontuario.Infrastructure.Services;

/// <summary>Monta, a partir do cadastro, o dicionario que a sanitizacao usa em cada atendimento.</summary>
public static partial class IdentidadeDoAtendimento
{
    public static IdentidadeAtendimento De(PacienteRef paciente, Usuario medico) => new(
        paciente.Nome,
        paciente.Cpf,
        paciente.DataNascimento,
        paciente.Contato,
        medico.Nome);

    /// <summary>Quantos marcadores de dado pessoal o texto sanitizado carrega.</summary>
    public static int ContarMarcadores(string textoSanitizado) => Marcador().Count(textoSanitizado);

    [GeneratedRegex(@"\[(?:PACIENTE|MEDICO|CPF_PACIENTE|CONTATO_PACIENTE|DATA_NASCIMENTO|PESSOA|CPF|CNS|TELEFONE|EMAIL|CEP|ENDERECO|DOCUMENTO)\]")]
    private static partial Regex Marcador();
}
