using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Prontuario.Application.Common.Interfaces;

namespace Prontuario.Infrastructure.Services;

public class ArmazenamentoAudioOptions
{
    public const string SectionName = "ArmazenamentoAudio";

    public string Endpoint { get; set; } = "http://minio:9000";
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Bucket { get; set; } = "consultas";

    /// <summary>
    /// Dias que o audio bruto fica guardado depois de transcrito (LGPD:
    /// minimizacao). Zero desliga o expurgo e mantem o audio indefinidamente.
    /// </summary>
    public int RetencaoDias { get; set; }
}

/// <summary>
/// Implementacao S3-compatible apontada para o MinIO do compose. O bucket e
/// criado sob demanda para que "docker compose up" nao exija passo manual.
/// </summary>
public class ArmazenamentoAudioMinio : IArmazenamentoAudio
{
    private readonly IAmazonS3 _s3;
    private readonly ArmazenamentoAudioOptions _opcoes;

    public ArmazenamentoAudioMinio(IOptions<ArmazenamentoAudioOptions> opcoes)
    {
        _opcoes = opcoes.Value;

        _s3 = new AmazonS3Client(
            _opcoes.AccessKey,
            _opcoes.SecretKey,
            new AmazonS3Config
            {
                ServiceURL = _opcoes.Endpoint,
                // O MinIO nao usa bucket como subdominio.
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1",
            });
    }

    public async Task<string> SalvarAsync(
        Guid atendimentoId, Stream conteudo, string tipoConteudo, CancellationToken cancellationToken = default)
    {
        await GarantirBucketAsync(cancellationToken);

        var chave = $"atendimentos/{atendimentoId}/{Guid.NewGuid():N}.webm";

        await _s3.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = _opcoes.Bucket,
                Key = chave,
                InputStream = conteudo,
                ContentType = tipoConteudo,
            },
            cancellationToken);

        return chave;
    }

    public async Task<Stream> AbrirAsync(string chave, CancellationToken cancellationToken = default)
    {
        var resposta = await _s3.GetObjectAsync(_opcoes.Bucket, chave, cancellationToken);

        // O consumidor precisa de um stream com Length conhecido (upload multipart
        // para a OpenAI), entao o conteudo e materializado em memoria.
        var memoria = new MemoryStream();
        await resposta.ResponseStream.CopyToAsync(memoria, cancellationToken);
        memoria.Position = 0;
        return memoria;
    }

    public Task RemoverAsync(string chave, CancellationToken cancellationToken = default)
        => _s3.DeleteObjectAsync(_opcoes.Bucket, chave, cancellationToken);

    private async Task GarantirBucketAsync(CancellationToken cancellationToken)
    {
        var buckets = await _s3.ListBucketsAsync(cancellationToken);
        if (buckets.Buckets.Any(b => b.BucketName == _opcoes.Bucket))
        {
            return;
        }

        await _s3.PutBucketAsync(new PutBucketRequest { BucketName = _opcoes.Bucket }, cancellationToken);
    }
}
