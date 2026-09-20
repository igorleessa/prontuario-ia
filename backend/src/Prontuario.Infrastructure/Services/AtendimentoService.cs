using Microsoft.EntityFrameworkCore;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;
using Prontuario.Domain.Entities;
using Prontuario.Domain.Enums;
using Prontuario.Infrastructure.Persistence;

namespace Prontuario.Infrastructure.Services;

public class AtendimentoService : IAtendimentoService
{
    private readonly ApplicationDbContext _db;
    private readonly IRegistroClinicoOutput _output;
    private readonly IArmazenamentoAudio _armazenamento;
    private readonly IFilaProcessamentoIA _fila;
    private readonly IExportadorNota _exportador;
    private readonly INotaClinicaFormatter _formatador;
    private readonly IAuditoriaService _auditoria;

    public AtendimentoService(
        ApplicationDbContext db,
        IRegistroClinicoOutput output,
        IArmazenamentoAudio armazenamento,
        IFilaProcessamentoIA fila,
        IExportadorNota exportador,
        INotaClinicaFormatter formatador,
        IAuditoriaService auditoria)
    {
        _db = db;
        _output = output;
        _armazenamento = armazenamento;
        _fila = fila;
        _exportador = exportador;
        _formatador = formatador;
        _auditoria = auditoria;
    }

    public async Task<Guid?> AbrirAsync(
        Guid pacienteRefId, Guid medicoId, Guid clinicaId, Guid? templateNotaId = null,
        CancellationToken cancellationToken = default)
    {
        var pacienteEhDaClinica = await _db.Pacientes
            .AnyAsync(p => p.Id == pacienteRefId && p.ClinicaId == clinicaId, cancellationToken);

        if (!pacienteEhDaClinica)
        {
            return null;
        }

        // Template inexistente ou de outra clinica cai no modelo generico, em vez
        // de impedir a abertura do atendimento.
        var template = templateNotaId is { } id
            && await _db.TemplatesNota.AnyAsync(
                t => t.Id == id && t.Ativo && (t.ClinicaId == null || t.ClinicaId == clinicaId), cancellationToken)
                ? templateNotaId
                : null;

        var atendimento = new Atendimento
        {
            PacienteRefId = pacienteRefId,
            MedicoId = medicoId,
            TemplateNotaId = template,
            Status = StatusAtendimento.AguardandoConsentimento,
        };

        _db.Atendimentos.Add(atendimento);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(AcoesAuditoria.AtendimentoAberto, atendimento.Id, cancellationToken: cancellationToken);
        return atendimento.Id;
    }

    public async Task<IReadOnlyList<AtendimentoResumoDto>> ListarAsync(Guid clinicaId, CancellationToken cancellationToken = default)
        => await DaClinica(clinicaId)
            .OrderByDescending(a => a.DataHora)
            .Select(a => new AtendimentoResumoDto(
                a.Id,
                a.PacienteRefId,
                a.PacienteRef!.Nome,
                a.Medico!.Nome,
                a.DataHora,
                a.Status.ToString(),
                a.ConsentimentoGravacao))
            .ToListAsync(cancellationToken);

    public async Task<AtendimentoDetalheDto?> ObterAsync(Guid atendimentoId, Guid clinicaId, CancellationToken cancellationToken = default)
    {
        var atendimento = await DaClinica(clinicaId)
            .Include(a => a.PacienteRef)
            .Include(a => a.Medico)
            .Include(a => a.Prontuario)
            .Include(a => a.TemplateNota)
            .Include(a => a.GravacaoAudio)
            .SingleOrDefaultAsync(a => a.Id == atendimentoId, cancellationToken);

        if (atendimento is null)
        {
            return null;
        }

        await _auditoria.RegistrarAsync(AcoesAuditoria.AtendimentoLido, atendimento.Id, cancellationToken: cancellationToken);

        return new AtendimentoDetalheDto(
            atendimento.Id,
            atendimento.PacienteRefId,
            atendimento.PacienteRef!.Nome,
            atendimento.PacienteRef.Cpf,
            atendimento.PacienteRef.DataNascimento,
            atendimento.Medico!.Nome,
            atendimento.DataHora,
            atendimento.Status.ToString(),
            atendimento.ConsentimentoGravacao,
            atendimento.ConsentimentoEm,
            await ObterRascunhoAsync(atendimento, cancellationToken),
            await ObterSugestaoIAAsync(atendimento.Id, cancellationToken),
            await ObterTranscricaoAsync(atendimento.Id, cancellationToken),
            atendimento.ErroProcessamentoIA,
            atendimento.TemplateNota?.Nome,
            atendimento.GravacaoAudio?.DuracaoSegundos);
    }

    public async Task<bool> RegistrarConsentimentoAsync(Guid atendimentoId, Guid clinicaId, CancellationToken cancellationToken = default)
    {
        var atendimento = await BuscarAsync(atendimentoId, clinicaId, cancellationToken);
        if (atendimento is null)
        {
            return false;
        }

        atendimento.ConsentimentoGravacao = true;
        atendimento.ConsentimentoEm = DateTime.UtcNow;
        atendimento.Status = StatusAtendimento.EmGravacao;

        await _db.SaveChangesAsync(cancellationToken);
        await _auditoria.RegistrarAsync(
            AcoesAuditoria.ConsentimentoRegistrado, atendimentoId, cancellationToken: cancellationToken);

        return true;
    }

    public async Task<bool> IniciarRevisaoAsync(Guid atendimentoId, Guid clinicaId, CancellationToken cancellationToken = default)
    {
        var atendimento = await BuscarAsync(atendimentoId, clinicaId, cancellationToken);
        if (atendimento is null)
        {
            return false;
        }

        atendimento.Status = StatusAtendimento.EmRevisao;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RegistrarAudioAsync(
        Guid atendimentoId, Guid clinicaId, Stream audio, string tipoConteudo, int duracaoSegundos,
        CancellationToken cancellationToken = default)
    {
        var atendimento = await DaClinica(clinicaId)
            .Include(a => a.GravacaoAudio)
            .SingleOrDefaultAsync(a => a.Id == atendimentoId, cancellationToken);

        if (atendimento is null)
        {
            return false;
        }

        var chave = await _armazenamento.SalvarAsync(atendimentoId, audio, tipoConteudo, cancellationToken);

        // Regravar substitui o audio anterior: o atendimento tem uma gravacao so.
        if (atendimento.GravacaoAudio is { } existente)
        {
            existente.StoragePath = chave;
            existente.DuracaoSegundos = duracaoSegundos;
        }
        else
        {
            _db.GravacoesAudio.Add(new GravacaoAudio
            {
                AtendimentoId = atendimentoId,
                StoragePath = chave,
                DuracaoSegundos = duracaoSegundos,
            });
        }

        atendimento.Status = StatusAtendimento.ProcessandoIA;
        atendimento.ErroProcessamentoIA = null;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            AcoesAuditoria.AudioEnviado, atendimentoId, $"{duracaoSegundos}s", cancellationToken);

        await _fila.EnfileirarAsync(atendimentoId, cancellationToken);
        return true;
    }

    public async Task<ResultadoAtendimento> ConfirmarAsync(
        Guid atendimentoId, RascunhoClinicoDto revisado, Guid clinicaId, CancellationToken cancellationToken = default)
    {
        var atendimento = await BuscarAsync(atendimentoId, clinicaId, cancellationToken);
        if (atendimento is null)
        {
            return ResultadoAtendimento.NaoEncontrado;
        }

        // Prontuario assinado e somente leitura (RF15): uma segunda confirmacao
        // sobrescreveria o registro que o medico ja assinou. Correcao posterior
        // se faz por adendo, nao por edicao do original.
        if (atendimento.Status is StatusAtendimento.Finalizado or StatusAtendimento.Cancelado)
        {
            return ResultadoAtendimento.Conflito;
        }

        await _output.ConfirmarAsync(atendimentoId, revisado, cancellationToken);

        atendimento.Status = StatusAtendimento.Finalizado;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            AcoesAuditoria.RegistroConfirmado, atendimentoId, cancellationToken: cancellationToken);

        return ResultadoAtendimento.Ok;
    }

    public async Task<ResultadoAtendimento> SimularConsultaAsync(
        Guid atendimentoId, Guid clinicaId, CancellationToken cancellationToken = default)
    {
        var atendimento = await DaClinica(clinicaId)
            .Include(a => a.GravacaoAudio)
            .SingleOrDefaultAsync(a => a.Id == atendimentoId, cancellationToken);

        if (atendimento is null)
        {
            return ResultadoAtendimento.NaoEncontrado;
        }

        if (atendimento.Status is StatusAtendimento.Finalizado or StatusAtendimento.Cancelado)
        {
            return ResultadoAtendimento.Conflito;
        }

        var chave = ConsultaExemplo.PrefixoStorage + atendimentoId;

        if (atendimento.GravacaoAudio is { } existente)
        {
            existente.StoragePath = chave;
            existente.DuracaoSegundos = ConsultaExemplo.DuracaoSegundos;
        }
        else
        {
            _db.GravacoesAudio.Add(new GravacaoAudio
            {
                AtendimentoId = atendimentoId,
                StoragePath = chave,
                DuracaoSegundos = ConsultaExemplo.DuracaoSegundos,
            });
        }

        atendimento.ConsentimentoGravacao = true;
        atendimento.ConsentimentoEm ??= DateTime.UtcNow;
        atendimento.Status = StatusAtendimento.ProcessandoIA;
        atendimento.ErroProcessamentoIA = null;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            AcoesAuditoria.ConsultaSimulada, atendimentoId, cancellationToken: cancellationToken);

        await _fila.EnfileirarAsync(atendimentoId, cancellationToken);
        return ResultadoAtendimento.Ok;
    }

    public async Task<ResultadoAtendimento> ReprocessarAsync(
        Guid atendimentoId, Guid clinicaId, CancellationToken cancellationToken = default)
    {
        var atendimento = await DaClinica(clinicaId)
            .Include(a => a.GravacaoAudio)
            .SingleOrDefaultAsync(a => a.Id == atendimentoId, cancellationToken);

        if (atendimento is null)
        {
            return ResultadoAtendimento.NaoEncontrado;
        }

        // Sem audio nao ha o que reprocessar, e um registro encerrado nao volta
        // atras - a IA nao pode mexer no que ja foi assinado ou exportado.
        if (atendimento.GravacaoAudio is null
            || atendimento.Status is StatusAtendimento.Finalizado or StatusAtendimento.Cancelado)
        {
            return ResultadoAtendimento.Conflito;
        }

        atendimento.Status = StatusAtendimento.ProcessandoIA;
        atendimento.ErroProcessamentoIA = null;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            AcoesAuditoria.ProcessamentoSolicitado, atendimentoId, cancellationToken: cancellationToken);

        await _fila.EnfileirarAsync(atendimentoId, cancellationToken);
        return ResultadoAtendimento.Ok;
    }

    public async Task<bool> CancelarAsync(Guid atendimentoId, Guid clinicaId, CancellationToken cancellationToken = default)
    {
        var atendimento = await BuscarAsync(atendimentoId, clinicaId, cancellationToken);
        if (atendimento is null || atendimento.Status == StatusAtendimento.Finalizado)
        {
            return false;
        }

        atendimento.Status = StatusAtendimento.Cancelado;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            AcoesAuditoria.AtendimentoCancelado, atendimentoId, cancellationToken: cancellationToken);

        return true;
    }


    public async Task<NotaExportavelDto?> ObterNotaAsync(
        Guid atendimentoId, Guid clinicaId, CancellationToken cancellationToken = default)
    {
        var atendimento = await DaClinica(clinicaId)
            .Include(a => a.NotaExportavel)
            .Include(a => a.Medico!.Clinica)
            .SingleOrDefaultAsync(a => a.Id == atendimentoId, cancellationToken);

        if (atendimento is null)
        {
            return null;
        }

        var webhookConfigurado = !string.IsNullOrEmpty(atendimento.Medico!.Clinica!.WebhookUrl);

        // Antes da confirmacao ainda nao existe nota gravada: mostrar o conteudo
        // em revisao evita uma tela vazia e deixa claro o que sera exportado.
        if (atendimento.NotaExportavel is not { } nota)
        {
            var conteudo = _formatador.Formatar(await ObterRascunhoAsync(atendimento, cancellationToken));
            return new NotaExportavelDto(
                conteudo, StatusNota.Rascunho.ToString(), null, null, null, 0, null, webhookConfigurado);
        }

        return new NotaExportavelDto(
            nota.ConteudoFormatado,
            nota.Status.ToString(),
            nota.RevisadaEm,
            nota.ExportadoEm,
            nota.Destino,
            nota.TentativasExportacao,
            nota.UltimoErroExportacao,
            webhookConfigurado);
    }

    public async Task<ResultadoExportacaoDto?> ExportarAsync(
        Guid atendimentoId, Guid clinicaId, CancellationToken cancellationToken = default)
    {
        var existe = await DaClinica(clinicaId).AnyAsync(a => a.Id == atendimentoId, cancellationToken);
        if (!existe)
        {
            return null;
        }

        var resultado = await _exportador.EnviarAsync(atendimentoId, cancellationToken);
        await _auditoria.RegistrarAsync(
            AcoesAuditoria.NotaExportada,
            atendimentoId,
            resultado.Sucesso ? "entregue" : resultado.Erro,
            cancellationToken);

        return resultado;
    }

    public async Task<DadosPdfNota?> ObterDadosPdfAsync(
        Guid atendimentoId, Guid clinicaId, CancellationToken cancellationToken = default)
    {
        var atendimento = await DaClinica(clinicaId)
            .Include(a => a.PacienteRef)
            .Include(a => a.Medico!.Clinica)
            .Include(a => a.Prontuario)
            .Include(a => a.NotaExportavel)
            .SingleOrDefaultAsync(a => a.Id == atendimentoId, cancellationToken);

        if (atendimento is null)
        {
            return null;
        }

        var conteudo = atendimento.NotaExportavel?.ConteudoFormatado
            ?? _formatador.Formatar(await ObterRascunhoAsync(atendimento, cancellationToken));

        var documento = atendimento.PacienteRef!.Cpf
            ?? (atendimento.PacienteRef.IdExternoEmr is { } externo ? $"EMR {externo}" : null);

        await _auditoria.RegistrarAsync(
            AcoesAuditoria.NotaBaixadaPdf, atendimentoId, cancellationToken: cancellationToken);

        return new DadosPdfNota(
            atendimento.Medico!.Clinica!.Nome,
            atendimento.PacienteRef.Nome,
            documento,
            atendimento.Medico.Nome,
            atendimento.DataHora,
            conteudo);
    }

    // A clinica do atendimento e a do medico responsavel - mesmo criterio usado
    // por RegistroClinicoOutputResolver para escolher a modalidade.
    private IQueryable<Atendimento> DaClinica(Guid clinicaId)
        => _db.Atendimentos.Where(a => a.Medico!.ClinicaId == clinicaId);

    private Task<Atendimento?> BuscarAsync(Guid atendimentoId, Guid clinicaId, CancellationToken cancellationToken)
        => DaClinica(clinicaId).SingleOrDefaultAsync(a => a.Id == atendimentoId, cancellationToken);

    /// <summary>
    /// Sugestao original da IA, independentemente do que o medico gravou depois.
    /// E o outro lado do comparativo da tela de revisao.
    /// </summary>
    private async Task<RascunhoClinicoDto?> ObterSugestaoIAAsync(Guid atendimentoId, CancellationToken cancellationToken)
    {
        var rascunho = await _db.RascunhosIA
            .Where(r => r.Transcricao!.GravacaoAudio!.AtendimentoId == atendimentoId)
            .OrderByDescending(r => r.CriadoEm)
            .FirstOrDefaultAsync(cancellationToken);

        return rascunho is null
            ? null
            : new RascunhoClinicoDto(
                rascunho.QueixaPrincipal, rascunho.Hda, rascunho.Antecedentes, rascunho.ExameFisico,
                rascunho.HipoteseDiagnostica, rascunho.Cid10Sugerido, rascunho.Conduta);
    }

    public async Task<MetricasClinicaDto> ObterMetricasAsync(
        Guid clinicaId, int minutosDocumentacaoManual, CancellationToken cancellationToken = default)
    {
        var finalizados = await DaClinica(clinicaId)
            .Where(a => a.Status == StatusAtendimento.Finalizado)
            .Select(a => new
            {
                Duracao = a.GravacaoAudio != null ? a.GravacaoAudio.DuracaoSegundos : 0,
                InicioRevisao = a.GravacaoAudio != null ? (DateTime?)a.GravacaoAudio.CriadoEm : null,
                // O fim da revisao e a assinatura (Modalidade A) ou a revisao da
                // nota (Modalidade B) - so uma das duas existe por atendimento.
                Confirmacao = a.Prontuario != null ? a.Prontuario.AssinadoEm
                    : a.NotaExportavel != null ? a.NotaExportavel.RevisadaEm
                    : null,
            })
            .ToListAsync(cancellationToken);

        var revisoes = finalizados
            .Where(a => a.InicioRevisao is not null && a.Confirmacao is not null)
            .Select(a => (a.Confirmacao!.Value - a.InicioRevisao!.Value).TotalSeconds)
            .Where(segundos => segundos is > 0 and < 7200)
            .ToList();

        var tempoMedio = revisoes.Count > 0 ? (int)revisoes.Average() : (int?)null;

        var economia = tempoMedio is { } medio && minutosDocumentacaoManual > 0
            ? (int?)Math.Round(
                Math.Clamp(100 - (medio * 100.0 / (minutosDocumentacaoManual * 60)), 0, 100))
            : null;

        return new MetricasClinicaDto(
            finalizados.Count,
            (int)Math.Round(finalizados.Sum(a => a.Duracao) / 60.0),
            tempoMedio,
            minutosDocumentacaoManual,
            economia);
    }

    private Task<string?> ObterTranscricaoAsync(Guid atendimentoId, CancellationToken cancellationToken)
        => _db.Transcricoes
            .Where(t => t.GravacaoAudio!.AtendimentoId == atendimentoId)
            .OrderByDescending(t => t.CriadoEm)
            .Select(t => (string?)t.Texto)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Devolve o conteudo clinico ja existente: a revisao gravada no prontuario
    /// nativo tem prioridade sobre a sugestao da IA, que e apenas ponto de partida.
    /// </summary>
    private async Task<RascunhoClinicoDto> ObterRascunhoAsync(Atendimento atendimento, CancellationToken cancellationToken)
    {
        if (atendimento.Prontuario is { } p)
        {
            return new RascunhoClinicoDto(
                p.QueixaPrincipal, p.Hda, p.Antecedentes, p.ExameFisico,
                p.HipoteseDiagnostica, p.Cid10, p.Conduta);
        }

        var rascunhoIa = await _db.RascunhosIA
            .Where(r => r.Transcricao!.GravacaoAudio!.AtendimentoId == atendimento.Id)
            .OrderByDescending(r => r.CriadoEm)
            .FirstOrDefaultAsync(cancellationToken);

        if (rascunhoIa is null)
        {
            return new RascunhoClinicoDto(null, null, null, null, null, null, null);
        }

        return new RascunhoClinicoDto(
            rascunhoIa.QueixaPrincipal, rascunhoIa.Hda, rascunhoIa.Antecedentes, rascunhoIa.ExameFisico,
            rascunhoIa.HipoteseDiagnostica, rascunhoIa.Cid10Sugerido, rascunhoIa.Conduta);
    }
}
