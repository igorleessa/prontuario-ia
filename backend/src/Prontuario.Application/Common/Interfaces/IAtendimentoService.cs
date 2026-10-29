using Prontuario.Application.Common.Models;

namespace Prontuario.Application.Common.Interfaces;

/// <summary>
/// Todas as operacoes recebem a clinica do usuario logado e so enxergam
/// atendimentos dela: o identificador vindo da URL nunca e suficiente para
/// alcancar o dado de outra clinica.
/// </summary>
public interface IAtendimentoService
{
    /// <summary>Abre um atendimento vinculado a um paciente e ao medico logado (RF04). Null se o paciente nao e da clinica.</summary>
    Task<Guid?> AbrirAsync(
        Guid pacienteRefId, Guid medicoId, Guid clinicaId, Guid? templateNotaId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Lista os atendimentos da clinica, do mais recente para o mais antigo.</summary>
    Task<IReadOnlyList<AtendimentoResumoDto>> ListarAsync(Guid clinicaId, CancellationToken cancellationToken = default);

    Task<AtendimentoDetalheDto?> ObterAsync(Guid atendimentoId, Guid clinicaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra a aceitacao do consentimento antes de permitir a transcricao
    /// (RF05). O atendimento passa a EmConsulta.
    /// </summary>
    Task<bool> RegistrarConsentimentoAsync(Guid atendimentoId, Guid clinicaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Leva o atendimento direto a revisao, sem transcricao - paciente que
    /// recusou, teleconsulta por escrito, retomada de atendimento (RF09).
    /// </summary>
    Task<bool> IniciarRevisaoAsync(Guid atendimentoId, Guid clinicaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirma a revisao do medico (RF10) e delega a persistencia para
    /// IRegistroClinicoOutput, que escolhe Modalidade A ou B conforme a clinica.
    /// Devolve Conflito quando o atendimento ja foi finalizado ou cancelado:
    /// um registro assinado nao se sobrescreve (RF15).
    /// </summary>
    Task<ResultadoAtendimento> ConfirmarAsync(
        Guid atendimentoId, RascunhoClinicoDto revisado, Guid clinicaId, CancellationToken cancellationToken = default);

    Task<bool> CancelarAsync(Guid atendimentoId, Guid clinicaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Numeros da clinica para a tela inicial. O tempo economizado e estimativa
    /// declarada, comparada com a linha de base recebida por parametro.
    /// </summary>
    Task<MetricasClinicaDto> ObterMetricasAsync(
        Guid clinicaId, int minutosDocumentacaoManual, CancellationToken cancellationToken = default);

    /// <summary>Estado da nota clinica exportavel do atendimento (RF20). Null quando o atendimento nao existe na clinica.</summary>
    Task<NotaExportavelDto?> ObterNotaAsync(Guid atendimentoId, Guid clinicaId, CancellationToken cancellationToken = default);

    /// <summary>Reenvia ao EMR de destino uma nota ja revisada (RF19), para o caso de o envio automatico ter falhado.</summary>
    Task<ResultadoExportacaoDto?> ExportarAsync(Guid atendimentoId, Guid clinicaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dados do PDF da nota (RF18). Disponivel nas duas modalidades: mesmo no modo
    /// integrado o medico pode precisar do documento em papel ou anexo.
    /// </summary>
    Task<DadosPdfNota?> ObterDadosPdfAsync(Guid atendimentoId, Guid clinicaId, CancellationToken cancellationToken = default);
}
