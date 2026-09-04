namespace Prontuario.Application.Common.Interfaces;

/// <summary>
/// Guarda o audio das consultas fora do banco (MinIO/S3), conforme a decisao de
/// arquitetura da especificacao. Devolve e recebe a chave do objeto.
/// </summary>
public interface IArmazenamentoAudio
{
    Task<string> SalvarAsync(Guid atendimentoId, Stream conteudo, string tipoConteudo, CancellationToken cancellationToken = default);

    Task<Stream> AbrirAsync(string chave, CancellationToken cancellationToken = default);
}
