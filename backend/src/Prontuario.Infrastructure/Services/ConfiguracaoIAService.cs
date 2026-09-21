using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;
using Prontuario.Domain.Entities;
using Prontuario.Infrastructure.Persistence;

namespace Prontuario.Infrastructure.Services;

public class ConfiguracaoIAService : IConfiguracaoIAService
{
    /// <summary>
    /// Proposito do Data Protection. Trocar este texto invalida as chaves ja
    /// gravadas, entao ele nao deve mudar depois que o sistema estiver em uso.
    /// </summary>
    private const string Proposito = "Prontuario.ConfiguracaoIA.ChaveApi";

    private const string ModeloTranscricaoPadrao = "whisper-1";
    private const string ModeloTextoPadrao = "gpt-4o";

    private readonly ApplicationDbContext _db;
    private readonly IDataProtector _protetor;
    private readonly IAuditoriaService _auditoria;
    private readonly ILogger<ConfiguracaoIAService> _logger;

    public ConfiguracaoIAService(
        ApplicationDbContext db,
        IDataProtectionProvider protecao,
        IAuditoriaService auditoria,
        ILogger<ConfiguracaoIAService> logger)
    {
        _db = db;
        _protetor = protecao.CreateProtector(Proposito);
        _auditoria = auditoria;
        _logger = logger;
    }

    public async Task<ConfiguracaoIADto> ObterAsync(Guid clinicaId, CancellationToken cancellationToken = default)
    {
        var configuracao = await BuscarAsync(clinicaId, cancellationToken);

        return configuracao is null
            ? new ConfiguracaoIADto(false, null, ModeloTranscricaoPadrao, ModeloTextoPadrao, null)
            : new ConfiguracaoIADto(
                true,
                configuracao.ChaveApiSufixo,
                configuracao.ModeloTranscricao,
                configuracao.ModeloTexto,
                configuracao.AtualizadoEm);
    }

    public async Task<ConfiguracaoIADto> SalvarAsync(
        Guid clinicaId, Guid usuarioId, SalvarConfiguracaoIADto dados, CancellationToken cancellationToken = default)
    {
        var configuracao = await BuscarAsync(clinicaId, cancellationToken);
        var chave = dados.ChaveApi?.Trim();

        if (configuracao is null)
        {
            if (string.IsNullOrEmpty(chave))
            {
                throw new InvalidOperationException("Informe a chave da API para criar a configuracao.");
            }

            configuracao = new ConfiguracaoIA { ClinicaId = clinicaId };
            _db.ConfiguracoesIA.Add(configuracao);
        }

        if (!string.IsNullOrEmpty(chave))
        {
            configuracao.ChaveApiProtegida = _protetor.Protect(chave);
            configuracao.ChaveApiSufixo = chave.Length <= 4 ? chave : chave[^4..];
        }

        configuracao.ModeloTranscricao = Ou(dados.ModeloTranscricao, ModeloTranscricaoPadrao);
        configuracao.ModeloTexto = Ou(dados.ModeloTexto, ModeloTextoPadrao);
        configuracao.AtualizadoEm = DateTime.UtcNow;
        configuracao.AtualizadoPorId = usuarioId;

        await _db.SaveChangesAsync(cancellationToken);
        await _auditoria.RegistrarAsync(
            AcoesAuditoria.ConfiguracaoAlterada, detalhe: "credenciais de IA atualizadas",
            cancellationToken: cancellationToken);

        return new ConfiguracaoIADto(
            true, configuracao.ChaveApiSufixo, configuracao.ModeloTranscricao,
            configuracao.ModeloTexto, configuracao.AtualizadoEm);
    }

    public async Task<bool> RemoverChaveAsync(Guid clinicaId, CancellationToken cancellationToken = default)
    {
        var configuracao = await BuscarAsync(clinicaId, cancellationToken);
        if (configuracao is null)
        {
            return false;
        }

        _db.ConfiguracoesIA.Remove(configuracao);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            AcoesAuditoria.ConfiguracaoAlterada, detalhe: "chave de IA removida",
            cancellationToken: cancellationToken);

        return true;
    }

    public async Task<CredenciaisIA?> ObterCredenciaisAsync(Guid clinicaId, CancellationToken cancellationToken = default)
    {
        var configuracao = await BuscarAsync(clinicaId, cancellationToken);
        if (configuracao is null)
        {
            return null;
        }

        try
        {
            var chave = _protetor.Unprotect(configuracao.ChaveApiProtegida);
            return new CredenciaisIA(chave, configuracao.ModeloTranscricao, configuracao.ModeloTexto);
        }
        catch (Exception excecao)
        {
            // Acontece se o chaveiro do Data Protection for perdido (volume
            // recriado). A chave gravada e irrecuperavel e precisa ser redigitada.
            _logger.LogError(excecao, "Nao foi possivel decifrar a chave de IA da clinica {ClinicaId}.", clinicaId);
            return null;
        }
    }

    private Task<ConfiguracaoIA?> BuscarAsync(Guid clinicaId, CancellationToken cancellationToken)
        => _db.ConfiguracoesIA.SingleOrDefaultAsync(c => c.ClinicaId == clinicaId, cancellationToken);

    private static string Ou(string? valor, string padrao)
        => string.IsNullOrWhiteSpace(valor) ? padrao : valor.Trim();
}
