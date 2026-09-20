namespace Prontuario.Application.Common.Models;

/// <summary>Opção de modelo de nota oferecida na abertura do atendimento.</summary>
public sealed record TemplateNotaDto(Guid Id, string Nome, string Especialidade);

/// <summary>
/// Contexto que personaliza a extracao: o template da especialidade e as
/// preferencias de redacao do medico. Ambos entram como instrucao adicional -
/// as regras de nao inventar informacao continuam valendo acima deles.
/// </summary>
public sealed record ContextoGeracao(string? InstrucoesTemplate, string? InstrucoesEstilo)
{
    public static readonly ContextoGeracao Padrao = new(null, null);
}

/// <summary>Documento clinico gerado a partir da consulta (receita, pedido de exame, atestado).</summary>
public sealed record DocumentoClinicoDto(Guid Id, string Tipo, string Conteudo, DateTime CriadoEm);

public sealed record PerfilMedicoDto(string Nome, string Email, string? InstrucoesEstilo);
