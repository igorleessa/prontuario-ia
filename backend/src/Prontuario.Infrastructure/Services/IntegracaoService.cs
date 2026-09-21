using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;
using Prontuario.Domain.Entities;
using Prontuario.Domain.Enums;
using Prontuario.Infrastructure.Persistence;

namespace Prontuario.Infrastructure.Services;

public class IntegracaoService : IIntegracaoService
{
    private readonly ApplicationDbContext _db;
    private readonly IntegracaoOptions _opcoes;

    public IntegracaoService(ApplicationDbContext db, IOptions<IntegracaoOptions> opcoes)
    {
        _db = db;
        _opcoes = opcoes.Value;
    }

    public async Task<AtendimentoExternoCriadoDto> AbrirAtendimentoAsync(
        Guid clinicaId, AbrirAtendimentoExternoDto pedido, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pedido.IdExternoEmr) && string.IsNullOrWhiteSpace(pedido.Cpf))
        {
            throw new InvalidOperationException("Informe idExternoEmr ou cpf para identificar o paciente.");
        }

        var paciente = await LocalizarPacienteAsync(clinicaId, pedido, cancellationToken)
            ?? await CriarPacienteAsync(clinicaId, pedido, cancellationToken);

        var medico = await LocalizarMedicoAsync(clinicaId, pedido.MedicoEmail, cancellationToken);

        var atendimento = new Atendimento
        {
            PacienteRefId = paciente.Id,
            MedicoId = medico.Id,
            Status = StatusAtendimento.AguardandoConsentimento,
        };

        _db.Atendimentos.Add(atendimento);
        await _db.SaveChangesAsync(cancellationToken);

        var url = $"{_opcoes.UrlBaseFrontend.TrimEnd('/')}/atendimentos/{atendimento.Id}";
        return new AtendimentoExternoCriadoDto(atendimento.Id, paciente.Id, url);
    }

    private Task<PacienteRef?> LocalizarPacienteAsync(
        Guid clinicaId, AbrirAtendimentoExternoDto pedido, CancellationToken cancellationToken)
    {
        var consulta = _db.Pacientes.Where(p => p.ClinicaId == clinicaId);

        // A referencia do EMR tem prioridade sobre o CPF: e ela que amarra o
        // atendimento ao registro do sistema de origem.
        if (!string.IsNullOrWhiteSpace(pedido.IdExternoEmr))
        {
            var idExterno = pedido.IdExternoEmr.Trim();
            return consulta.FirstOrDefaultAsync(p => p.IdExternoEmr == idExterno, cancellationToken);
        }

        var cpf = pedido.Cpf!.Trim();
        return consulta.FirstOrDefaultAsync(p => p.Cpf == cpf, cancellationToken);
    }

    private async Task<PacienteRef> CriarPacienteAsync(
        Guid clinicaId, AbrirAtendimentoExternoDto pedido, CancellationToken cancellationToken)
    {
        var paciente = new PacienteRef
        {
            ClinicaId = clinicaId,
            // Sem nome, o medico ainda precisa reconhecer o paciente na lista.
            Nome = string.IsNullOrWhiteSpace(pedido.Nome)
                ? $"Paciente {pedido.IdExternoEmr ?? pedido.Cpf}"
                : pedido.Nome.Trim(),
            Cpf = string.IsNullOrWhiteSpace(pedido.Cpf) ? null : pedido.Cpf.Trim(),
            IdExternoEmr = string.IsNullOrWhiteSpace(pedido.IdExternoEmr) ? null : pedido.IdExternoEmr.Trim(),
            DataNascimento = pedido.DataNascimento,
        };

        _db.Pacientes.Add(paciente);
        await _db.SaveChangesAsync(cancellationToken);
        return paciente;
    }

    private async Task<Usuario> LocalizarMedicoAsync(Guid clinicaId, string? email, CancellationToken cancellationToken)
    {
        var medicos = _db.Usuarios.Where(u => u.ClinicaId == clinicaId && u.Papel == PapelUsuario.Medico);

        if (!string.IsNullOrWhiteSpace(email))
        {
            var informado = email.Trim();
            return await medicos.FirstOrDefaultAsync(u => u.Email == informado, cancellationToken)
                ?? throw new InvalidOperationException($"Nenhum medico com o e-mail {informado} nesta clinica.");
        }

        return await medicos.OrderBy(u => u.Nome).FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("A clinica nao tem nenhum medico cadastrado.");
    }
}

public class IntegracaoOptions
{
    public const string SectionName = "Integracao";

    /// <summary>Base usada para montar o link que o EMR do cliente abre para o medico.</summary>
    public string UrlBaseFrontend { get; set; } = "http://localhost:4200";
}
