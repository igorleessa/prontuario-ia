namespace Prontuario.Application.Common.Interfaces;

/// <summary>
/// Abstrai o provedor de speech-to-text (RNF07) para permitir trocar de
/// fornecedor sem alterar a camada de dominio ou os casos de uso. As
/// credenciais chegam por parametro porque sao por clinica, nao globais.
/// Recebe trechos curtos: o audio existe so na memoria durante a chamada.
/// </summary>
public interface ITranscriptionService
{
    /// <summary>Texto reconhecido, ou vazio quando o trecho nao tinha fala.</summary>
    Task<string> TranscreverAsync(Stream audio, CredenciaisIA credenciais, CancellationToken cancellationToken = default);
}
