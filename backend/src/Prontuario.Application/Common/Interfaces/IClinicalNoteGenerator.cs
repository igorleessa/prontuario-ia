using Prontuario.Application.Common.Models;

namespace Prontuario.Application.Common.Interfaces;

/// <summary>
/// Abstrai o LLM que extrai os campos estruturados do formulario a partir
/// da transcricao (RF08). Identica nas duas modalidades - o que muda depois
/// da revisao do medico e a saida (ver IRegistroClinicoOutput).
/// </summary>
public interface IClinicalNoteGenerator
{
    Task<RascunhoClinicoDto> GerarRascunhoAsync(string transcricao, CancellationToken cancellationToken = default);
}
