using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;
using Prontuario.Domain.Entities;
using Prontuario.Domain.Enums;
using Prontuario.Infrastructure.Persistence;
using Prontuario.Infrastructure.Services;

namespace Prontuario.Application.Tests;

/// <summary>
/// Regras que protegem o registro clinico: escopo por clinica, trava do
/// atendimento encerrado e reprocessamento sem duplicacao. Sao as que, se
/// quebrarem, deixam o sistema perder ou sobrescrever documentacao medica.
/// </summary>
public class AtendimentoServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly IRegistroClinicoOutput _output = Substitute.For<IRegistroClinicoOutput>();
    private readonly IFilaProcessamentoIA _fila = Substitute.For<IFilaProcessamentoIA>();
    private readonly AtendimentoService _servico;

    private readonly Clinica _clinica = new() { Nome = "Clinica A", ModoOperacao = ModoOperacao.Integrado };
    private readonly Clinica _outraClinica = new() { Nome = "Clinica B" };
    private Usuario _medico = null!;
    private PacienteRef _paciente = null!;

    public AtendimentoServiceTests()
    {
        _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"atendimentos-{Guid.NewGuid()}")
            .Options);

        _medico = new Usuario
        {
            ClinicaId = _clinica.Id, Nome = "Dra. Ana", Email = "ana@clinica.test", SenhaHash = "x",
        };
        _paciente = new PacienteRef { ClinicaId = _clinica.Id, Nome = "Helena" };

        _db.Clinicas.AddRange(_clinica, _outraClinica);
        _db.Usuarios.Add(_medico);
        _db.Pacientes.Add(_paciente);
        _db.SaveChanges();

        _servico = new AtendimentoService(
            _db,
            _output,
            Substitute.For<IArmazenamentoAudio>(),
            _fila,
            Substitute.For<IExportadorNota>(),
            new NotaClinicaFormatter(),
            Substitute.For<IAuditoriaService>());
    }

    [Fact]
    public async Task Abrir_ComPacienteDeOutraClinica_NaoAbreOAtendimento()
    {
        var alheio = new PacienteRef { ClinicaId = _outraClinica.Id, Nome = "Paciente de outra clinica" };
        _db.Pacientes.Add(alheio);
        await _db.SaveChangesAsync();

        var id = await _servico.AbrirAsync(alheio.Id, _medico.Id, _clinica.Id);

        Assert.Null(id);
        Assert.Empty(_db.Atendimentos);
    }

    [Fact]
    public async Task Abrir_ComTemplateDeOutraClinica_CaiNoModeloGenerico()
    {
        var template = new TemplateNota
        {
            ClinicaId = _outraClinica.Id, Nome = "Cardiologia", Especialidade = "Cardiologia", Instrucoes = "…",
        };
        _db.TemplatesNota.Add(template);
        await _db.SaveChangesAsync();

        var id = await _servico.AbrirAsync(_paciente.Id, _medico.Id, _clinica.Id, template.Id);

        // Template invalido nao pode impedir o atendimento - so deixa de ser aplicado.
        Assert.NotNull(id);
        Assert.Null(_db.Atendimentos.Single().TemplateNotaId);
    }

    [Fact]
    public async Task Confirmar_AtendimentoJaFinalizado_NaoSobrescreveORegistro()
    {
        var atendimento = await CriarAtendimentoAsync(StatusAtendimento.Finalizado);

        var resultado = await _servico.ConfirmarAsync(
            atendimento.Id, Revisado("texto novo"), _clinica.Id);

        Assert.Equal(ResultadoAtendimento.Conflito, resultado);
        await _output.DidNotReceiveWithAnyArgs().ConfirmarAsync(default, default!, default);
    }

    [Fact]
    public async Task Confirmar_AtendimentoEmRevisao_GravaEFinaliza()
    {
        var atendimento = await CriarAtendimentoAsync(StatusAtendimento.EmRevisao);

        var resultado = await _servico.ConfirmarAsync(
            atendimento.Id, Revisado("tosse ha 4 dias"), _clinica.Id);

        Assert.Equal(ResultadoAtendimento.Ok, resultado);
        Assert.Equal(StatusAtendimento.Finalizado, _db.Atendimentos.Single().Status);
        await _output.Received(1).ConfirmarAsync(atendimento.Id, Arg.Any<RascunhoClinicoDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Confirmar_DeOutraClinica_NaoEncontraOAtendimento()
    {
        var atendimento = await CriarAtendimentoAsync(StatusAtendimento.EmRevisao);

        var resultado = await _servico.ConfirmarAsync(
            atendimento.Id, Revisado("texto"), _outraClinica.Id);

        Assert.Equal(ResultadoAtendimento.NaoEncontrado, resultado);
    }

    [Fact]
    public async Task Reprocessar_SemAudio_NaoEnfileiraNada()
    {
        var atendimento = await CriarAtendimentoAsync(StatusAtendimento.EmRevisao);

        var resultado = await _servico.ReprocessarAsync(atendimento.Id, _clinica.Id);

        Assert.Equal(ResultadoAtendimento.Conflito, resultado);
        await _fila.DidNotReceiveWithAnyArgs().EnfileirarAsync(default, default);
    }

    [Fact]
    public async Task Reprocessar_ComAudio_ReenfileiraOMesmoAtendimento()
    {
        var atendimento = await CriarAtendimentoAsync(StatusAtendimento.EmRevisao);
        atendimento.ErroProcessamentoIA = "A API de texto respondeu 429";
        _db.GravacoesAudio.Add(new GravacaoAudio
        {
            AtendimentoId = atendimento.Id, StoragePath = "atendimentos/x.webm", DuracaoSegundos = 300,
        });
        await _db.SaveChangesAsync();

        var resultado = await _servico.ReprocessarAsync(atendimento.Id, _clinica.Id);

        Assert.Equal(ResultadoAtendimento.Ok, resultado);
        Assert.Single(_db.Atendimentos);
        Assert.Null(_db.Atendimentos.Single().ErroProcessamentoIA);
        await _fila.Received(1).EnfileirarAsync(atendimento.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Metricas_ComparamRevisaoRealComALinhaDeBase()
    {
        var atendimento = await CriarAtendimentoAsync(StatusAtendimento.Finalizado);
        var inicio = DateTime.UtcNow.AddMinutes(-10);

        _db.GravacoesAudio.Add(new GravacaoAudio
        {
            AtendimentoId = atendimento.Id, StoragePath = "x", DuracaoSegundos = 600, CriadoEm = inicio,
        });
        _db.Prontuarios.Add(new ProntuarioRegistro
        {
            AtendimentoId = atendimento.Id, Finalizado = true, AssinadoEm = inicio.AddMinutes(2),
        });
        await _db.SaveChangesAsync();

        var metricas = await _servico.ObterMetricasAsync(_clinica.Id, minutosDocumentacaoManual: 10);

        Assert.Equal(1, metricas.AtendimentosFinalizados);
        Assert.Equal(10, metricas.MinutosDeConsultaDocumentados);
        Assert.Equal(120, metricas.TempoMedioRevisaoSegundos);
        // 2 minutos de revisao contra 10 de digitacao: 80% menos.
        Assert.Equal(80, metricas.EconomiaPercentualEstimada);
    }

    [Fact]
    public async Task Metricas_SemAtendimentoFinalizado_NaoInventaPercentual()
    {
        await CriarAtendimentoAsync(StatusAtendimento.EmRevisao);

        var metricas = await _servico.ObterMetricasAsync(_clinica.Id, minutosDocumentacaoManual: 7);

        Assert.Equal(0, metricas.AtendimentosFinalizados);
        Assert.Null(metricas.EconomiaPercentualEstimada);
    }

    private async Task<Atendimento> CriarAtendimentoAsync(StatusAtendimento status)
    {
        var atendimento = new Atendimento
        {
            PacienteRefId = _paciente.Id,
            MedicoId = _medico.Id,
            Status = status,
        };

        _db.Atendimentos.Add(atendimento);
        await _db.SaveChangesAsync();
        return atendimento;
    }

    private static RascunhoClinicoDto Revisado(string queixa)
        => new(queixa, null, null, null, null, null, null);

    public void Dispose() => _db.Dispose();
}
