namespace Prontuario.Application.Common.Models;

/// <summary>
/// Estado da configuracao de IA mostrado na tela. Nunca carrega a chave: apenas
/// se existe uma e os ultimos caracteres dela, o suficiente para o usuario
/// reconhecer qual credencial esta ativa.
/// </summary>
public sealed record ConfiguracaoIADto(
    bool ChaveConfigurada,
    string? ChaveApiSufixo,
    string ModeloTranscricao,
    string ModeloTexto,
    DateTime? AtualizadoEm);

public sealed record SalvarConfiguracaoIADto(
    string? ChaveApi,
    string ModeloTranscricao,
    string ModeloTexto);
