using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;
using Prontuario.Domain.Enums;
using Prontuario.Infrastructure.Persistence;

namespace Prontuario.Infrastructure.Services;

/// <summary>
/// Envia a nota revisada ao EMR do cliente (RF19). O corpo vai assinado em
/// HMAC-SHA256 com o segredo da clinica (RNF10), para que o destino consiga
/// provar que a requisicao partiu daqui e nao foi alterada no caminho.
/// </summary>
public class WebhookExportador : IExportadorNota
{
    public const string NomeCliente = "emr-webhook";

    /// <summary>Cabecalhos que o EMR de destino le para validar o envio.</summary>
    public const string CabecalhoAssinatura = "X-Prontuario-Assinatura";
    public const string CabecalhoTimestamp = "X-Prontuario-Timestamp";
    public const string CabecalhoEvento = "X-Prontuario-Evento";

    /// <summary>Uma tentativa imediata e duas de retentativa: cobre indisponibilidade curta sem prender o medico na tela.</summary>
    private static readonly TimeSpan[] Esperas = [TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5)];

    private static readonly JsonSerializerOptions OpcoesJson = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _db;
    private readonly IConfiguracaoExportacaoService _configuracoes;
    private readonly IHttpClientFactory _fabrica;
    private readonly ILogger<WebhookExportador> _logger;

    public WebhookExportador(
        ApplicationDbContext db,
        IConfiguracaoExportacaoService configuracoes,
        IHttpClientFactory fabrica,
        ILogger<WebhookExportador> logger)
    {
        _db = db;
        _configuracoes = configuracoes;
        _fabrica = fabrica;
        _logger = logger;
    }

    public async Task<ResultadoExportacaoDto> EnviarAsync(Guid atendimentoId, CancellationToken cancellationToken = default)
    {
        var atendimento = await _db.Atendimentos
            .Include(a => a.PacienteRef)
            .Include(a => a.Medico!.Clinica)
            .Include(a => a.NotaExportavel)
            .SingleOrDefaultAsync(a => a.Id == atendimentoId, cancellationToken);

        if (atendimento?.NotaExportavel is not { } nota)
        {
            return new ResultadoExportacaoDto(false, null, "Este atendimento ainda nao tem nota revisada.", 0);
        }

        var clinica = atendimento.Medico!.Clinica!;
        if (string.IsNullOrEmpty(clinica.WebhookUrl))
        {
            return new ResultadoExportacaoDto(
                false, null, "Nenhum webhook configurado. Exporte manualmente por copia ou PDF.", nota.TentativasExportacao);
        }

        var payload = new
        {
            evento = "nota.exportada",
            ocorridoEm = DateTime.UtcNow,
            atendimento = new
            {
                id = atendimento.Id,
                dataHora = atendimento.DataHora,
                medico = atendimento.Medico.Nome,
            },
            paciente = new
            {
                nome = atendimento.PacienteRef!.Nome,
                cpf = atendimento.PacienteRef.Cpf,
                idExternoEmr = atendimento.PacienteRef.IdExternoEmr,
            },
            nota = new
            {
                formato = "texto",
                conteudo = nota.ConteudoFormatado,
            },
        };

        var resultado = await EntregarAsync(clinica.Id, clinica.WebhookUrl, "nota.exportada", payload, cancellationToken);

        nota.TentativasExportacao += resultado.Tentativas;
        nota.UltimoErroExportacao = resultado.Erro;

        if (resultado.Sucesso)
        {
            nota.Status = StatusNota.Exportada;
            nota.ExportadoEm = DateTime.UtcNow;
            nota.Destino = clinica.WebhookUrl;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return resultado with { Tentativas = nota.TentativasExportacao };
    }

    public async Task<ResultadoExportacaoDto> TestarAsync(Guid clinicaId, CancellationToken cancellationToken = default)
    {
        var url = await _db.Clinicas
            .Where(c => c.Id == clinicaId)
            .Select(c => c.WebhookUrl)
            .SingleOrDefaultAsync(cancellationToken);

        if (string.IsNullOrEmpty(url))
        {
            return new ResultadoExportacaoDto(false, null, "Configure a URL do webhook antes de testar.", 0);
        }

        // Dados ficticios de proposito: o teste de conectividade nao pode
        // trafegar informacao de saude de um paciente real.
        var payload = new
        {
            evento = "teste",
            ocorridoEm = DateTime.UtcNow,
            atendimento = new { id = Guid.Empty, dataHora = DateTime.UtcNow, medico = "Dra. Exemplo" },
            paciente = new { nome = "Paciente de Teste", cpf = (string?)null, idExternoEmr = "EMR-000" },
            nota = new
            {
                formato = "texto",
                conteudo = "Envio de teste do Prontuario IA. Nenhum dado real de paciente foi transmitido.",
            },
        };

        return await EntregarAsync(clinicaId, url, "teste", payload, cancellationToken);
    }

    private async Task<ResultadoExportacaoDto> EntregarAsync(
        Guid clinicaId, string url, string evento, object payload, CancellationToken cancellationToken)
    {
        var corpo = JsonSerializer.Serialize(payload, OpcoesJson);
        var segredo = await _configuracoes.ObterSegredoWebhookAsync(clinicaId, cancellationToken);
        var cliente = _fabrica.CreateClient(NomeCliente);

        string? ultimoErro = null;
        int? ultimoCodigo = null;

        for (var tentativa = 0; tentativa < Esperas.Length; tentativa++)
        {
            if (Esperas[tentativa] > TimeSpan.Zero)
            {
                await Task.Delay(Esperas[tentativa], cancellationToken);
            }

            try
            {
                using var requisicao = MontarRequisicao(url, evento, corpo, segredo);
                using var resposta = await cliente.SendAsync(requisicao, cancellationToken);
                ultimoCodigo = (int)resposta.StatusCode;

                if (resposta.IsSuccessStatusCode)
                {
                    return new ResultadoExportacaoDto(true, ultimoCodigo, null, tentativa + 1);
                }

                ultimoErro = $"O EMR de destino respondeu {ultimoCodigo}.";

                // 4xx e configuracao errada, nao indisponibilidade: repetir so atrasaria o medico.
                if (ultimoCodigo is >= 400 and < 500)
                {
                    return new ResultadoExportacaoDto(false, ultimoCodigo, ultimoErro, tentativa + 1);
                }
            }
            catch (Exception excecao) when (excecao is not OperationCanceledException)
            {
                ultimoErro = $"Nao foi possivel alcancar o EMR de destino: {excecao.Message}";
                _logger.LogWarning(excecao, "Falha ao enviar webhook para {Url} (tentativa {Tentativa}).", url, tentativa + 1);
            }
        }

        return new ResultadoExportacaoDto(false, ultimoCodigo, ultimoErro, Esperas.Length);
    }

    private static HttpRequestMessage MontarRequisicao(string url, string evento, string corpo, string? segredo)
    {
        var requisicao = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(corpo, Encoding.UTF8, "application/json"),
        };

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        requisicao.Headers.Add(CabecalhoEvento, evento);
        requisicao.Headers.Add(CabecalhoTimestamp, timestamp);

        if (!string.IsNullOrEmpty(segredo))
        {
            requisicao.Headers.Add(CabecalhoAssinatura, Assinar(segredo, timestamp, corpo));
        }

        return requisicao;
    }

    /// <summary>
    /// Assina "timestamp.corpo" em vez do corpo sozinho: assim o destino pode
    /// recusar uma requisicao antiga reapresentada por terceiros.
    /// </summary>
    public static string Assinar(string segredo, string timestamp, string corpo)
    {
        var assinatura = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(segredo), Encoding.UTF8.GetBytes($"{timestamp}.{corpo}"));

        return "sha256=" + Convert.ToHexString(assinatura).ToLowerInvariant();
    }
}
