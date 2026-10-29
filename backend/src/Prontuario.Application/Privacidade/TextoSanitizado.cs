namespace Prontuario.Application.Privacidade;

/// <summary>
/// Texto que ja passou pela camada de sanitizacao. So o
/// <see cref="SanitizadorDados"/> consegue criar uma instancia (construtor
/// interno), e os servicos de IA so aceitam este tipo: esquecer a sanitizacao
/// antes de chamar um provedor externo vira erro de compilacao, nao incidente.
/// </summary>
public sealed class TextoSanitizado
{
    internal TextoSanitizado(string valor, IReadOnlyDictionary<string, int> substituicoes)
    {
        Valor = valor;
        Substituicoes = substituicoes;
    }

    /// <summary>Texto com os dados pessoais trocados por marcadores como [PACIENTE] e [CPF].</summary>
    public string Valor { get; }

    /// <summary>
    /// Quantas substituicoes houve por categoria. E o que vai para log e
    /// auditoria - nunca o texto, nem o valor substituido.
    /// </summary>
    public IReadOnlyDictionary<string, int> Substituicoes { get; }

    public int TotalSubstituicoes => Substituicoes.Values.Sum();

    public override string ToString() => Valor;
}

/// <summary>
/// Campos clinicos ja sanitizados, para o que vai ao provedor de IA alem da
/// transcricao (o registro revisado usado na redacao dos documentos).
/// </summary>
public sealed class RegistroSanitizado
{
    internal RegistroSanitizado(
        TextoSanitizado? queixaPrincipal,
        TextoSanitizado? hda,
        TextoSanitizado? antecedentes,
        TextoSanitizado? exameFisico,
        TextoSanitizado? hipoteseDiagnostica,
        TextoSanitizado? conduta)
    {
        QueixaPrincipal = queixaPrincipal?.Valor;
        Hda = hda?.Valor;
        Antecedentes = antecedentes?.Valor;
        ExameFisico = exameFisico?.Valor;
        HipoteseDiagnostica = hipoteseDiagnostica?.Valor;
        Conduta = conduta?.Valor;
    }

    public string? QueixaPrincipal { get; }
    public string? Hda { get; }
    public string? Antecedentes { get; }
    public string? ExameFisico { get; }
    public string? HipoteseDiagnostica { get; }
    public string? Conduta { get; }
}

/// <summary>
/// O que ja se sabe sobre as pessoas do atendimento, vindo do cadastro. E a
/// camada mais precisa da sanitizacao: o nome do paciente nao precisa ser
/// adivinhado por um reconhecedor generico, ele esta no <c>PacienteRef</c>.
/// </summary>
public sealed record IdentidadeAtendimento(
    string PacienteNome,
    string? PacienteCpf,
    DateOnly? PacienteDataNascimento,
    string? PacienteContato,
    string? MedicoNome,
    /// <summary>Trechos que o medico marcou como dado pessoal neste atendimento.</summary>
    IReadOnlyList<string>? TermosProtegidos = null);
