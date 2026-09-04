namespace Prontuario.Application.Common.Interfaces;

/// <summary>
/// Abstrai o provedor de speech-to-text (RNF07) para permitir trocar de
/// fornecedor sem alterar a camada de dominio ou os casos de uso. As
/// credenciais chegam por parametro porque sao por clinica, nao globais.
/// </summary>
public interface ITranscriptionService
{
    Task<string> TranscreverAsync(Stream audio, CredenciaisIA credenciais, CancellationToken cancellationToken = default);
}
