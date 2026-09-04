namespace Prontuario.Application.Common.Interfaces;

/// <summary>
/// Abstrai o provedor de speech-to-text (RNF07) para permitir trocar de
/// fornecedor (Azure Speech / Google Cloud STT / Whisper API) sem alterar
/// a camada de dominio ou os casos de uso.
/// </summary>
public interface ITranscriptionService
{
    Task<string> TranscreverAsync(Stream audio, CancellationToken cancellationToken = default);
}
