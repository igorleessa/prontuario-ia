using Prontuario.Application.Common.Interfaces;

namespace Prontuario.Infrastructure.Services;

/// <summary>
/// Implementacao provisoria enquanto o fornecedor de STT nao e escolhido
/// (ver especificacao-mvp.md, secao 12, item 1). Substituir por um adaptador
/// real (Azure Speech / Google Cloud STT / Whisper API) sem alterar
/// Application nem Domain.
/// </summary>
public class PlaceholderTranscriptionService : ITranscriptionService
{
    public Task<string> TranscreverAsync(Stream audio, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(
            "Nenhum provedor de STT configurado ainda. Ver especificacao-mvp.md, secao 12, item 1.");
    }
}
