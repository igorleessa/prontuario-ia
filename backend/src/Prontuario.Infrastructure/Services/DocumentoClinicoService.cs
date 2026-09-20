using Microsoft.EntityFrameworkCore;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;
using Prontuario.Domain.Entities;
using Prontuario.Domain.Enums;
using Prontuario.Infrastructure.Persistence;

namespace Prontuario.Infrastructure.Services;

public class DocumentoClinicoService : IDocumentoClinicoService
{
    private readonly ApplicationDbContext _db;
    private readonly IClinicalNoteGenerator _llm;
    private readonly IConfiguracaoIAService _configuracoes;
    private readonly IUsuarioAtual _usuario;
    private readonly IAuditoriaService _auditoria;

    public DocumentoClinicoService(
        ApplicationDbContext db,
        IClinicalNoteGenerator llm,
        IConfiguracaoIAService configuracoes,
        IUsuarioAtual usuario,
        IAuditoriaService auditoria)
    {
        _db = db;
        _llm = llm;
        _configuracoes = configuracoes;
        _usuario = usuario;
        _auditoria = auditoria;
    }

    public async Task<IReadOnlyList<DocumentoClinicoDto>> ListarAsync(
        Guid atendimentoId, Guid clinicaId, CancellationToken cancellationToken = default)
        => await _db.Documentos
            .Where(d => d.AtendimentoId == atendimentoId && d.Atendimento!.Medico!.ClinicaId == clinicaId)
            .OrderBy(d => d.Tipo)
            .Select(d => new DocumentoClinicoDto(d.Id, d.Tipo.ToString(), d.Conteudo, d.CriadoEm))
            .ToListAsync(cancellationToken);

    public async Task<DocumentoClinicoDto?> GerarAsync(
        Guid atendimentoId, Guid clinicaId, string tipo, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<TipoDocumento>(tipo, ignoreCase: true, out var tipoDocumento))
        {
            throw new InvalidOperationException($"Tipo de documento desconhecido: {tipo}.");
        }

        var atendimento = await _db.Atendimentos
            .Include(a => a.Medico)
            .Include(a => a.TemplateNota)
            .Include(a => a.Prontuario)
            .FirstOrDefaultAsync(
                a => a.Id == atendimentoId && a.Medico!.ClinicaId == clinicaId, cancellationToken);

        if (atendimento is null)
        {
            return null;
        }

        var credenciais = await _configuracoes.ObterCredenciaisAsync(clinicaId, cancellationToken)
            ?? throw new InvalidOperationException(
                "Nenhuma chave de API configurada para a clinica. Cadastre a chave em Configuracoes.");

        var transcricao = await _db.Transcricoes
            .Where(t => t.GravacaoAudio!.AtendimentoId == atendimentoId)
            .OrderByDescending(t => t.CriadoEm)
            .Select(t => t.Texto)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        var registro = await ObterRegistroClinicoAsync(atendimento, cancellationToken);
        var contexto = new ContextoGeracao(
            atendimento.TemplateNota?.Instrucoes, atendimento.Medico!.InstrucoesEstilo);

        var conteudo = await _llm.GerarDocumentoAsync(
            tipoDocumento.ToString(), transcricao, registro, credenciais, contexto, cancellationToken);

        // Um documento por tipo: regerar substitui, para o medico nao acabar com
        // duas receitas divergentes do mesmo atendimento.
        var documento = await _db.Documentos
            .SingleOrDefaultAsync(d => d.AtendimentoId == atendimentoId && d.Tipo == tipoDocumento, cancellationToken)
            ?? new DocumentoClinico { AtendimentoId = atendimentoId, Tipo = tipoDocumento };

        documento.Conteudo = conteudo;
        documento.GeradoPorId = _usuario.Id;

        if (_db.Entry(documento).State == EntityState.Detached)
        {
            _db.Documentos.Add(documento);
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _auditoria.RegistrarAsync(
            AcoesAuditoria.DocumentoGerado, atendimentoId, tipoDocumento.ToString(), cancellationToken);

        return new DocumentoClinicoDto(
            documento.Id, documento.Tipo.ToString(), documento.Conteudo, documento.CriadoEm);
    }

    public async Task<DocumentoClinicoDto?> SalvarAsync(
        Guid atendimentoId, Guid clinicaId, Guid documentoId, string conteudo, CancellationToken cancellationToken = default)
    {
        var documento = await _db.Documentos
            .FirstOrDefaultAsync(
                d => d.Id == documentoId
                    && d.AtendimentoId == atendimentoId
                    && d.Atendimento!.Medico!.ClinicaId == clinicaId,
                cancellationToken);

        if (documento is null)
        {
            return null;
        }

        documento.Conteudo = conteudo;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            AcoesAuditoria.DocumentoRevisado, atendimentoId, documento.Tipo.ToString(), cancellationToken);

        return new DocumentoClinicoDto(
            documento.Id, documento.Tipo.ToString(), documento.Conteudo, documento.CriadoEm);
    }

    /// <summary>
    /// O documento parte do que o medico ja revisou; so cai no rascunho da IA
    /// quando o prontuario ainda nao foi confirmado.
    /// </summary>
    private async Task<RascunhoClinicoDto> ObterRegistroClinicoAsync(
        Atendimento atendimento, CancellationToken cancellationToken)
    {
        if (atendimento.Prontuario is { } p)
        {
            return new RascunhoClinicoDto(
                p.QueixaPrincipal, p.Hda, p.Antecedentes, p.ExameFisico, p.HipoteseDiagnostica, p.Cid10, p.Conduta);
        }

        var rascunho = await _db.RascunhosIA
            .Where(r => r.Transcricao!.GravacaoAudio!.AtendimentoId == atendimento.Id)
            .OrderByDescending(r => r.CriadoEm)
            .FirstOrDefaultAsync(cancellationToken);

        return rascunho is null
            ? new RascunhoClinicoDto(null, null, null, null, null, null, null)
            : new RascunhoClinicoDto(
                rascunho.QueixaPrincipal, rascunho.Hda, rascunho.Antecedentes, rascunho.ExameFisico,
                rascunho.HipoteseDiagnostica, rascunho.Cid10Sugerido, rascunho.Conduta);
    }
}
