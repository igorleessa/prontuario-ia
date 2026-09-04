using Prontuario.Domain.Common;
using Prontuario.Domain.Enums;

namespace Prontuario.Domain.Entities;

public class Usuario : BaseEntity
{
    public Guid ClinicaId { get; set; }
    public Clinica? Clinica { get; set; }

    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string SenhaHash { get; set; } = string.Empty;
    public PapelUsuario Papel { get; set; } = PapelUsuario.Medico;
}
