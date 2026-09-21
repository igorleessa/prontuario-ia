namespace Prontuario.Application.Common.Models;

/// <summary>
/// Configuracao de saida da Modalidade B mostrada na tela. Nunca carrega o
/// segredo do webhook nem a chave de integracao - apenas se existem.
/// </summary>
public sealed record ConfiguracaoExportacaoDto(
    string ModoOperacao,
    string? WebhookUrl,
    bool SegredoConfigurado,
    string? ChaveIntegracaoPrefixo,
    DateTime? ChaveIntegracaoCriadaEm);

public sealed record SalvarConfiguracaoExportacaoDto(string? WebhookUrl, string? WebhookSecret);

/// <summary>Chave de integracao em claro - devolvida uma unica vez, no momento em que e gerada.</summary>
public sealed record ChaveIntegracaoGeradaDto(string Chave, string Prefixo, DateTime CriadaEm);

/// <summary>Resultado de um envio ao EMR, usado tanto pelo teste quanto pela exportacao real.</summary>
public sealed record ResultadoExportacaoDto(bool Sucesso, int? CodigoHttp, string? Erro, int Tentativas);

/// <summary>Estado da nota clinica de um atendimento na Modalidade B (RF20).</summary>
public sealed record NotaExportavelDto(
    string Conteudo,
    string Status,
    DateTime? RevisadaEm,
    DateTime? ExportadoEm,
    string? Destino,
    int TentativasExportacao,
    string? UltimoErroExportacao,
    bool WebhookConfigurado);
