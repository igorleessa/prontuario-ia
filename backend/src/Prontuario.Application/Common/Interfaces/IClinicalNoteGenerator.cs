using Prontuario.Application.Common.Models;
using Prontuario.Application.Privacidade;

namespace Prontuario.Application.Common.Interfaces;

/// <summary>
/// Abstrai o LLM que extrai os campos estruturados do formulario a partir
/// da transcricao (RF08). Identica nas duas modalidades - o que muda depois
/// da revisao do medico e a saida (ver IRegistroClinicoOutput).
/// So aceita texto sanitizado: nenhum caminho do codigo consegue mandar o
/// texto bruto da consulta ao provedor.
/// </summary>
public interface IClinicalNoteGenerator
{
    /// <summary>
    /// A transcricao chega com cada trecho numerado ("[12] ..."); as origens
    /// devolvidas citam esses numeros.
    /// </summary>
    Task<RascunhoComOrigensDto> GerarRascunhoAsync(
        TextoSanitizado transcricao,
        CredenciaisIA credenciais,
        ContextoGeracao contexto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atualizacao durante a consulta: em vez de reler a conversa inteira, a IA
    /// recebe o rascunho atual, as ultimas falas ja consideradas (contexto) e
    /// as falas novas, e devolve so os campos que mudaram. O custo de cada
    /// chamada deixa de crescer com a duracao da consulta.
    /// </summary>
    Task<AtualizacaoRascunhoDto> AtualizarRascunhoAsync(
        RegistroSanitizado rascunhoAtual,
        string? cidAtual,
        TextoSanitizado falasDeContexto,
        TextoSanitizado falasNovas,
        CredenciaisIA credenciais,
        ContextoGeracao contexto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Redige um documento auxiliar (receita, pedido de exame, atestado) a partir
    /// da consulta. Como a nota, sai como rascunho: so vale depois da revisao.
    /// </summary>
    Task<string> GerarDocumentoAsync(
        string tipo,
        TextoSanitizado transcricao,
        RegistroSanitizado registroClinico,
        CredenciaisIA credenciais,
        ContextoGeracao contexto,
        CancellationToken cancellationToken = default);
}
