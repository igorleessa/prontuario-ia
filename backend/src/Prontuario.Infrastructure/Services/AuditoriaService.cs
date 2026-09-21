using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;
using Prontuario.Domain.Entities;
using Prontuario.Infrastructure.Persistence;

namespace Prontuario.Infrastructure.Services;

public class AuditoriaService : IAuditoriaService
{
    private readonly ApplicationDbContext _db;
    private readonly IUsuarioAtual _usuario;
    private readonly ILogger<AuditoriaService> _logger;

    public AuditoriaService(ApplicationDbContext db, IUsuarioAtual usuario, ILogger<AuditoriaService> logger)
    {
        _db = db;
        _usuario = usuario;
        _logger = logger;
    }

    public async Task RegistrarAsync(
        string acao, Guid? atendimentoId = null, string? detalhe = null, CancellationToken cancellationToken = default)
    {
        if (_usuario.Id is not { } usuarioId)
        {
            // Acoes de worker nao tem usuario humano por tras; o log estruturado
            // da aplicacao ja registra o que o pipeline fez.
            return;
        }

        try
        {
            _db.LogsAuditoria.Add(new LogAuditoria
            {
                UsuarioId = usuarioId,
                AtendimentoId = atendimentoId,
                Acao = acao,
                Detalhe = detalhe is { Length: > 500 } ? detalhe[..500] : detalhe,
            });

            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception excecao)
        {
            // Perder a trilha e grave, mas travar o atendimento por causa dela
            // seria pior: o medico ficaria sem documentar a consulta.
            _logger.LogError(excecao, "Nao foi possivel gravar a auditoria da acao {Acao}.", acao);
        }
    }

    public async Task<IReadOnlyList<LogAuditoriaDto>> ListarAsync(
        Guid clinicaId, string? acao = null, int limite = 200, CancellationToken cancellationToken = default)
    {
        var consulta = _db.LogsAuditoria.Where(l => l.Usuario!.ClinicaId == clinicaId);

        if (!string.IsNullOrWhiteSpace(acao))
        {
            var filtro = acao.Trim();
            consulta = consulta.Where(l => l.Acao == filtro);
        }

        return await consulta
            .OrderByDescending(l => l.CriadoEm)
            .Take(Math.Clamp(limite, 1, 500))
            .Select(l => new LogAuditoriaDto(
                l.Id,
                l.CriadoEm,
                l.Usuario!.Nome,
                l.Acao,
                l.AtendimentoId,
                l.Atendimento!.PacienteRef!.Nome,
                l.Detalhe))
            .ToListAsync(cancellationToken);
    }
}
