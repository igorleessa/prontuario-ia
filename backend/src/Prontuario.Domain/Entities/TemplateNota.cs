using Prontuario.Domain.Common;

namespace Prontuario.Domain.Entities;

/// <summary>
/// Modelo de nota por especialidade. O que muda entre uma consulta de
/// cardiologia e uma de pediatria nao e o pipeline, e o que se espera encontrar
/// em cada campo - por isso o template entra como instrucao adicional no prompt
/// de extracao, e nao como um fluxo separado.
/// </summary>
public class TemplateNota : BaseEntity
{
    /// <summary>Nulo significa template de catalogo, disponivel a todas as clinicas.</summary>
    public Guid? ClinicaId { get; set; }
    public Clinica? Clinica { get; set; }

    public string Nome { get; set; } = string.Empty;
    public string Especialidade { get; set; } = string.Empty;

    /// <summary>Orientacoes acrescentadas ao prompt de extracao para esta especialidade.</summary>
    public string Instrucoes { get; set; } = string.Empty;

    /// <summary>Ordena o catalogo na tela de abertura do atendimento.</summary>
    public int Ordem { get; set; }

    public bool Ativo { get; set; } = true;
}
