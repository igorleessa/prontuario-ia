using Prontuario.Application.Common.Models;

namespace Prontuario.Application.Common.Interfaces;

/// <summary>
/// Trilha de auditoria de acesso a dado clinico (RF11/RNF06). As entradas so
/// sao criadas - nenhuma rota da aplicacao altera ou apaga um registro.
/// </summary>
public interface IAuditoriaService
{
    /// <summary>
    /// Registra uma acao sobre um atendimento. Nunca lanca: auditoria com falha
    /// e registrada no log da aplicacao, mas nao pode derrubar o atendimento.
    /// </summary>
    Task RegistrarAsync(
        string acao, Guid? atendimentoId = null, string? detalhe = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Trilha da clinica, do mais recente para o mais antigo. Consulta de
    /// administrador: mostra quem acessou o que, nunca o conteudo clinico.
    /// </summary>
    Task<IReadOnlyList<LogAuditoriaDto>> ListarAsync(
        Guid clinicaId, string? acao = null, int limite = 200, CancellationToken cancellationToken = default);
}

/// <summary>Vocabulario fechado das acoes auditadas, para o filtro da tela de auditoria nao depender de texto livre.</summary>
public static class AcoesAuditoria
{
    public const string AtendimentoAberto = "atendimento.aberto";
    public const string AtendimentoLido = "atendimento.lido";
    public const string AtendimentoListado = "atendimento.listado";
    public const string ConsentimentoRegistrado = "consentimento.registrado";
    public const string AudioEnviado = "audio.enviado";
    public const string ProcessamentoSolicitado = "processamento.solicitado";
    public const string RegistroConfirmado = "registro.confirmado";
    public const string AtendimentoCancelado = "atendimento.cancelado";
    public const string NotaExportada = "nota.exportada";
    public const string NotaBaixadaPdf = "nota.pdf";
    public const string ConsultaSimulada = "consulta.simulada";
    public const string DocumentoGerado = "documento.gerado";
    public const string DocumentoRevisado = "documento.revisado";
    public const string ConfiguracaoAlterada = "configuracao.alterada";
    public const string AudioExpurgado = "audio.expurgado";
}
