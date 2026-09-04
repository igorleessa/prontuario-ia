using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;

namespace Prontuario.Infrastructure.Services;

/// <summary>
/// Implementacao provisoria enquanto o fornecedor de LLM nao e escolhido
/// (ver especificacao-mvp.md, secao 12, item 1). Substituir por um adaptador
/// real com prompt estruturado (JSON schema) sem alterar Application nem Domain.
/// </summary>
public class PlaceholderClinicalNoteGenerator : IClinicalNoteGenerator
{
    public Task<RascunhoClinicoDto> GerarRascunhoAsync(string transcricao, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(
            "Nenhum provedor de LLM configurado ainda. Ver especificacao-mvp.md, secao 12, item 1.");
    }
}
