using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;
using Prontuario.Domain.Entities;
using Prontuario.Domain.Enums;
using Prontuario.Infrastructure.Persistence;
using Prontuario.Infrastructure.Services;

namespace Prontuario.Application.Tests;

/// <summary>
/// O reprocessamento (RF13) roda sobre a mesma gravacao. Como gravacao e
/// transcricao tem relacao um-para-um no banco, criar uma segunda transcricao
/// estoura o indice unico - e o medico ve "erro ao salvar" em vez do rascunho.
/// </summary>
public class ProcessamentoIAWorkerTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly ServiceProvider _provedor;
    private readonly ProcessamentoIAWorker _worker;
    private readonly IClinicalNoteGenerator _llm = Substitute.For<IClinicalNoteGenerator>();

    private readonly Guid _atendimentoId;

    public ProcessamentoIAWorkerTests()
    {
        var opcoes = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"worker-{Guid.NewGuid()}")
            .Options;

        _db = new ApplicationDbContext(opcoes);

        var clinica = new Clinica { Nome = "Clinica" };
        var medico = new Usuario
        {
            ClinicaId = clinica.Id, Nome = "Dra. Ana", Email = "ana@clinica.test", SenhaHash = "x",
        };
        var paciente = new PacienteRef { ClinicaId = clinica.Id, Nome = "Helena" };
        var atendimento = new Atendimento
        {
            PacienteRefId = paciente.Id, MedicoId = medico.Id, Status = StatusAtendimento.ProcessandoIA,
        };

        _db.Clinicas.Add(clinica);
        _db.Usuarios.Add(medico);
        _db.Pacientes.Add(paciente);
        _db.Atendimentos.Add(atendimento);
        _db.GravacoesAudio.Add(new GravacaoAudio
        {
            AtendimentoId = atendimento.Id, StoragePath = "atendimentos/consulta.webm", DuracaoSegundos = 420,
        });
        _db.SaveChanges();

        _atendimentoId = atendimento.Id;

        var stt = Substitute.For<ITranscriptionService>();
        stt.TranscreverAsync(Arg.Any<Stream>(), Arg.Any<CredenciaisIA>(), Arg.Any<CancellationToken>())
            .Returns("Paciente refere tosse ha quatro dias.");

        _llm.GerarRascunhoAsync(
                Arg.Any<string>(), Arg.Any<CredenciaisIA>(), Arg.Any<ContextoGeracao>(), Arg.Any<CancellationToken>())
            .Returns(new RascunhoClinicoDto("Tosse ha 4 dias", null, null, null, null, null, null));

        var configuracoes = Substitute.For<IConfiguracaoIAService>();
        configuracoes.ObterCredenciaisAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new CredenciaisIA("sk-teste", "whisper-1", "gpt-4o"));

        var armazenamento = Substitute.For<IArmazenamentoAudio>();
        armazenamento.AbrirAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => new MemoryStream([1, 2, 3]));

        _provedor = new ServiceCollection()
            .AddSingleton(_db)
            .AddSingleton(stt)
            .AddSingleton(_llm)
            .AddSingleton(configuracoes)
            .AddSingleton(armazenamento)
            .BuildServiceProvider();

        _worker = new ProcessamentoIAWorker(
            Substitute.For<IFilaProcessamentoIA>(),
            _provedor.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ProcessamentoIAWorker>.Instance);
    }

    [Fact]
    public async Task Processar_DeixaOAtendimentoEmRevisaoComORascunho()
    {
        await _worker.ProcessarAsync(_atendimentoId, CancellationToken.None);

        Assert.Equal(StatusAtendimento.EmRevisao, _db.Atendimentos.Single().Status);
        Assert.Equal(StatusProcessamento.Concluido, _db.Transcricoes.Single().Status);
        Assert.Equal("Tosse ha 4 dias", _db.RascunhosIA.Single().QueixaPrincipal);
    }

    [Fact]
    public async Task Reprocessar_ReaproveitaATranscricaoDaMesmaGravacao()
    {
        await _worker.ProcessarAsync(_atendimentoId, CancellationToken.None);
        await _worker.ProcessarAsync(_atendimentoId, CancellationToken.None);

        // Duas transcricoes para a mesma gravacao violam o indice unico no
        // Postgres; dois rascunhos deixariam o medico com versoes divergentes.
        Assert.Single(_db.Transcricoes);
        Assert.Single(_db.RascunhosIA);
    }

    [Fact]
    public async Task Reprocessar_SubstituiORascunhoAnteriorPeloNovo()
    {
        await _worker.ProcessarAsync(_atendimentoId, CancellationToken.None);

        _llm.GerarRascunhoAsync(
                Arg.Any<string>(), Arg.Any<CredenciaisIA>(), Arg.Any<ContextoGeracao>(), Arg.Any<CancellationToken>())
            .Returns(new RascunhoClinicoDto("Tosse produtiva ha 4 dias", null, null, null, null, null, null));

        await _worker.ProcessarAsync(_atendimentoId, CancellationToken.None);

        Assert.Equal("Tosse produtiva ha 4 dias", _db.RascunhosIA.Single().QueixaPrincipal);
    }

    public void Dispose()
    {
        _db.Dispose();
        _provedor.Dispose();
    }
}
