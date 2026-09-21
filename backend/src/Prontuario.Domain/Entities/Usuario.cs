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

    /// <summary>
    /// Preferencias de redacao deste medico, acrescentadas ao prompt de extracao.
    /// E o que aproxima a nota do jeito como ele ja escreve, sem precisar de
    /// treinamento de modelo: "prefiro HDA em paragrafo unico", "sempre citar
    /// negativas relevantes", e assim por diante.
    /// </summary>
    public string? InstrucoesEstilo { get; set; }
}
