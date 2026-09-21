using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;
using Prontuario.Domain.Entities;
using Prontuario.Infrastructure.Persistence;

namespace Prontuario.Infrastructure.Services;

public class ConfiguracaoExportacaoService : IConfiguracaoExportacaoService
{
    /// <summary>
    /// Proposito do Data Protection. Trocar este texto invalida os segredos ja
    /// gravados, entao ele nao deve mudar depois que o sistema estiver em uso.
    /// </summary>
    private const string Proposito = "Prontuario.Exportacao.WebhookSecret";

    private const string PrefixoChave = "pia_";

    private readonly ApplicationDbContext _db;
    private readonly IDataProtector _protetor;
    private readonly IAuditoriaService _auditoria;
    private readonly ILogger<ConfiguracaoExportacaoService> _logger;

    public ConfiguracaoExportacaoService(
        ApplicationDbContext db,
        IDataProtectionProvider protecao,
        IAuditoriaService auditoria,
        ILogger<ConfiguracaoExportacaoService> logger)
    {
        _db = db;
        _protetor = protecao.CreateProtector(Proposito);
        _auditoria = auditoria;
        _logger = logger;
    }

    public async Task<ConfiguracaoExportacaoDto> ObterAsync(Guid clinicaId, CancellationToken cancellationToken = default)
    {
        var clinica = await BuscarAsync(clinicaId, cancellationToken);

        return new ConfiguracaoExportacaoDto(
            clinica.ModoOperacao.ToString(),
            clinica.WebhookUrl,
            !string.IsNullOrEmpty(clinica.WebhookSecretProtegido),
            clinica.ChaveIntegracaoPrefixo,
            clinica.ChaveIntegracaoCriadaEm);
    }

    public async Task<ConfiguracaoExportacaoDto> SalvarAsync(
        Guid clinicaId, SalvarConfiguracaoExportacaoDto dados, CancellationToken cancellationToken = default)
    {
        var clinica = await BuscarAsync(clinicaId, cancellationToken);
        var url = dados.WebhookUrl?.Trim();

        if (!string.IsNullOrEmpty(url) && !Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("Informe uma URL absoluta, comecando por http:// ou https://.");
        }

        clinica.WebhookUrl = string.IsNullOrEmpty(url) ? null : url;

        var segredo = dados.WebhookSecret?.Trim();
        if (!string.IsNullOrEmpty(segredo))
        {
            clinica.WebhookSecretProtegido = _protetor.Protect(segredo);
        }

        // Apagar a URL desliga a exportacao automatica; guardar o segredo orfao
        // so aumentaria a superficie de exposicao.
        if (clinica.WebhookUrl is null)
        {
            clinica.WebhookSecretProtegido = null;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _auditoria.RegistrarAsync(
            AcoesAuditoria.ConfiguracaoAlterada, detalhe: $"destino de exportacao: {clinica.WebhookUrl ?? "desligado"}",
            cancellationToken: cancellationToken);

        return await ObterAsync(clinicaId, cancellationToken);
    }

    public async Task<ChaveIntegracaoGeradaDto> GerarChaveIntegracaoAsync(
        Guid clinicaId, CancellationToken cancellationToken = default)
    {
        var clinica = await BuscarAsync(clinicaId, cancellationToken);

        var chave = PrefixoChave + Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        clinica.ChaveIntegracaoHash = Hash(chave);
        clinica.ChaveIntegracaoPrefixo = chave[..12];
        clinica.ChaveIntegracaoCriadaEm = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            AcoesAuditoria.ConfiguracaoAlterada, detalhe: "chave de integracao gerada",
            cancellationToken: cancellationToken);

        return new ChaveIntegracaoGeradaDto(chave, clinica.ChaveIntegracaoPrefixo, clinica.ChaveIntegracaoCriadaEm.Value);
    }

    public async Task<bool> RevogarChaveIntegracaoAsync(Guid clinicaId, CancellationToken cancellationToken = default)
    {
        var clinica = await BuscarAsync(clinicaId, cancellationToken);
        if (clinica.ChaveIntegracaoHash is null)
        {
            return false;
        }

        clinica.ChaveIntegracaoHash = null;
        clinica.ChaveIntegracaoPrefixo = null;
        clinica.ChaveIntegracaoCriadaEm = null;

        await _db.SaveChangesAsync(cancellationToken);
        await _auditoria.RegistrarAsync(
            AcoesAuditoria.ConfiguracaoAlterada, detalhe: "chave de integracao revogada",
            cancellationToken: cancellationToken);

        return true;
    }

    public async Task<Guid?> ResolverClinicaPorChaveAsync(string chave, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(chave))
        {
            return null;
        }

        var hash = Hash(chave.Trim());

        return await _db.Clinicas
            .Where(c => c.ChaveIntegracaoHash == hash)
            .Select(c => (Guid?)c.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<string?> ObterSegredoWebhookAsync(Guid clinicaId, CancellationToken cancellationToken = default)
    {
        var protegido = await _db.Clinicas
            .Where(c => c.Id == clinicaId)
            .Select(c => c.WebhookSecretProtegido)
            .SingleOrDefaultAsync(cancellationToken);

        if (string.IsNullOrEmpty(protegido))
        {
            return null;
        }

        try
        {
            return _protetor.Unprotect(protegido);
        }
        catch (Exception excecao)
        {
            // Acontece se o chaveiro do Data Protection for perdido; o segredo
            // gravado e irrecuperavel e precisa ser redigitado na tela.
            _logger.LogError(excecao, "Nao foi possivel decifrar o segredo de webhook da clinica {ClinicaId}.", clinicaId);
            return null;
        }
    }

    private static string Hash(string chave)
        => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(chave))).ToLowerInvariant();

    private async Task<Clinica> BuscarAsync(Guid clinicaId, CancellationToken cancellationToken)
        => await _db.Clinicas.SingleOrDefaultAsync(c => c.Id == clinicaId, cancellationToken)
            ?? throw new InvalidOperationException("Clinica nao encontrada.");
}
