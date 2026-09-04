using Prontuario.Domain.Common;

namespace Prontuario.Domain.Entities;

/// <summary>
/// Representa o paciente. Na Modalidade A (Integrado) carrega o cadastro clinico
/// completo; na Modalidade B (Conector) carrega apenas a referencia minima ao
/// paciente do EMR externo (IdExternoEmr e/ou Cpf). Ver especificacao-mvp.md, RF14/RF17.
/// </summary>
public class PacienteRef : BaseEntity
{
    public Guid ClinicaId { get; set; }
    public Clinica? Clinica { get; set; }

    public string Nome { get; set; } = string.Empty;
    public string? Cpf { get; set; }
    public string? IdExternoEmr { get; set; }
    public DateOnly? DataNascimento { get; set; }
    public string? Contato { get; set; }
    public string? HistoricoClinicoResumido { get; set; }

    public ICollection<Atendimento> Atendimentos { get; set; } = new List<Atendimento>();
}
