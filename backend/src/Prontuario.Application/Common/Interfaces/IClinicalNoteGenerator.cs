using Prontuario.Application.Common.Models;

namespace Prontuario.Application.Common.Interfaces;

/// <summary>
/// Abstrai o LLM que extrai os campos estruturados do formulario a partir
/// da transcricao (RF08). Identica nas duas modalidades - o que muda depois
/// da revisao do medico e a saida (ver IRegistroClinicoOutput).
/// </summary>
public interface IClinicalNoteGenerator
{
    Task<RascunhoClinicoDto> GerarRascunhoAsync(
        string transcricao,
        CredenciaisIA credenciais,
        ContextoGeracao contexto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Redige um documento auxiliar (receita, pedido de exame, atestado) a partir
    /// da consulta. Como a nota, sai como rascunho: so vale depois da revisao.
    /// </summary>
    Task<string> GerarDocumentoAsync(
        string tipo,
        string transcricao,
        RascunhoClinicoDto registroClinico,
        CredenciaisIA credenciais,
        ContextoGeracao contexto,
        CancellationToken cancellationToken = default);
}
