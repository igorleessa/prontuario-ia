using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;
using Prontuario.Application.Privacidade;
using Prontuario.Domain.Entities;
using Prontuario.Domain.Enums;
using Prontuario.Infrastructure.Persistence;
using Prontuario.Infrastructure.Services;

namespace Prontuario.Application.Tests;

/// <summary>
/// Consulta transcrita ao vivo: o que chega do provedor de transcricao so e
/// guardado sanitizado, o modelo de texto so recebe texto sanitizado, e a
/// consulta nunca fica sem caminho quando o provedor falha.
/// </summary>
public class TranscricaoConsultaServiceTests : IDisposable
{
    private const string CpfPaciente = "529.982.247-25";

    private readonly ApplicationDbContext _db;
    private readonly IConfiguracaoIAService _configuracoes = Substitute.For<IConfiguracaoIAService>();
    private readonly IProvedorTranscricaoTempoReal _tempoReal = Substitute.For<IProvedorTranscricaoTempoReal>();
    private readonly ITranscriptionService _stt = Substitute.For<ITranscriptionService>();
    private readonly IClinicalNoteGenerator _llm = Substitute.For<IClinicalNoteGenerator>();
    private readonly TranscricaoConsultaService _servico;

    private readonly Clinica _clinica = new() { Nome = "Clinica A" };
    private readonly Atendimento _atendimento;

    public TranscricaoConsultaServiceTests()
    {
        _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"transcricao-{Guid.NewGuid()}")
            .Options);

        var medico = new Usuario
        {
            ClinicaId = _clinica.Id, Nome = "Dra. Ana Ribeiro", Email = "ana@clinica.test", SenhaHash = "x",
        };
        var paciente = new PacienteRef { ClinicaId = _clinica.Id, Nome = "Helena Souza", Cpf = CpfPaciente };
        _atendimento = new Atendimento
        {
            PacienteRefId = paciente.Id,
            MedicoId = medico.Id,
            Status = StatusAtendimento.EmConsulta,
            ConsentimentoTranscricao = true,
            ConsentimentoEm = DateTime.UtcNow,
        };

        _db.Clinicas.Add(_clinica);
        _db.Usuarios.Add(medico);
        _db.Pacientes.Add(paciente);
        _db.Atendimentos.Add(_atendimento);
        _db.SaveChanges();

        _configuracoes.ObterCredenciaisAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new CredenciaisIA("sk-teste", "gpt-4o-transcribe", "gpt-4o"));

        _servico = new TranscricaoConsultaService(
            _db,
            new SanitizadorDados(),
            _configuracoes,
            _tempoReal,
            _stt,
            _llm,
            Substitute.For<IAuditoriaService>(),
            Options.Create(new TranscricaoOptions()),
            NullLogger<TranscricaoConsultaService>.Instance);
    }

    [Fact]
    public async Task Iniciar_SemConsentimento_NaoAbreAEscuta()
    {
        _atendimento.ConsentimentoTranscricao = false;
        _atendimento.Status = StatusAtendimento.AguardandoConsentimento;
        await _db.SaveChangesAsync();

        var resultado = await _servico.IniciarAsync(_atendimento.Id, _clinica.Id);

        Assert.Equal(ResultadoAtendimento.Conflito, resultado.Resultado);
        await _tempoReal.DidNotReceiveWithAnyArgs().CriarSessaoAsync(default!, default);
    }

    [Fact]
    public async Task Iniciar_ComTempoReal_DevolveSoAChaveTemporaria()
    {
        _tempoReal.CriarSessaoAsync(Arg.Any<CredenciaisIA>(), Arg.Any<CancellationToken>())
            .Returns(new CredencialTempoReal("ek_temporaria", DateTime.UtcNow.AddMinutes(10), "https://exemplo/calls"));

        var resultado = await _servico.IniciarAsync(_atendimento.Id, _clinica.Id);

        Assert.Equal(ModosTranscricao.TempoReal, resultado.Valor!.Modo);
        Assert.Equal("ek_temporaria", resultado.Valor.ChaveTemporaria);
        Assert.Single(_db.SessoesTranscricao);
    }

    [Fact]
    public async Task Iniciar_ComTempoRealFora_CaiNoModoPorTrechos()
    {
        _tempoReal.CriarSessaoAsync(Arg.Any<CredenciaisIA>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("sem rota"));

        var resultado = await _servico.IniciarAsync(_atendimento.Id, _clinica.Id);

        Assert.Equal(ResultadoAtendimento.Ok, resultado.Resultado);
        Assert.Equal(ModosTranscricao.Trechos, resultado.Valor!.Modo);
        Assert.Null(resultado.Valor.ChaveTemporaria);
        Assert.NotNull(resultado.Valor.MotivoModoAlternativo);
    }

    [Fact]
    public async Task Trecho_EhGuardadoSanitizadoEMostradoComOPaciente()
    {
        var resultado = await _servico.RegistrarTrechoAsync(
            _atendimento.Id, _clinica.Id,
            new RegistrarTrechoDto(0, $"Dona Helena, seu CPF é {CpfPaciente}? E a filha Mariana?"));

        var guardado = _db.Transcricoes.Single().Texto;
        Assert.DoesNotContain("Helena", guardado);
        Assert.DoesNotContain("Mariana", guardado);
        Assert.DoesNotContain(CpfPaciente, guardado);

        Assert.Equal($"Dona Helena Souza, seu CPF é {CpfPaciente}? E a filha [PESSOA]?", resultado.Valor!.Texto);
        Assert.Equal(3, resultado.Valor.DadosProtegidos);
    }

    [Fact]
    public async Task Trechos_ForaDeOrdemEReenviados_FicamNaOrdemDaFalaSemDuplicar()
    {
        await Registrar(1, "Faz quatro dias.");
        await Registrar(0, "Estou com tosse.");
        await Registrar(1, "Faz quatro dias.");

        Assert.Equal("Estou com tosse.\nFaz quatro dias.", _db.Transcricoes.Single().Texto);
        Assert.Equal(2, _db.TrechosTranscricao.Count());
    }

    [Fact]
    public async Task TranscricaoDoFluxoAntigo_NaoSePerdeQuandoAConsultaRecebeNovaFala()
    {
        var sessao = new SessaoTranscricao { AtendimentoId = _atendimento.Id };
        _db.SessoesTranscricao.Add(sessao);
        _db.Transcricoes.Add(new Transcricao { SessaoTranscricaoId = sessao.Id, Texto = "Texto da gravacao antiga." });
        await _db.SaveChangesAsync();

        await Registrar(0, "Complemento dito agora.");

        Assert.Equal("Texto da gravacao antiga.\nComplemento dito agora.", _db.Transcricoes.Single().Texto);
    }

    [Fact]
    public async Task Rascunho_SoMandaTextoSanitizadoAoModelo()
    {
        await Registrar(0, $"Dona Helena, CPF {CpfPaciente}, tosse há 4 dias. Ligue no 21 99876-5544.");

        TextoSanitizado? enviado = null;
        _llm.GerarRascunhoAsync(
                Arg.Do<TextoSanitizado>(texto => enviado = texto),
                Arg.Any<CredenciaisIA>(), Arg.Any<ContextoGeracao>(), Arg.Any<CancellationToken>())
            .Returns(new RascunhoClinicoDto("[PACIENTE] com tosse há 4 dias.", null, null, null, null, "J06.9", null));

        var resultado = await _servico.GerarRascunhoAsync(_atendimento.Id, _clinica.Id);

        Assert.NotNull(enviado);
        Assert.DoesNotContain("Helena", enviado!.Valor);
        Assert.DoesNotContain(CpfPaciente, enviado.Valor);
        Assert.DoesNotContain("99876", enviado.Valor);
        Assert.Contains("tosse há 4 dias", enviado.Valor);

        // O marcador volta com o nome do cadastro para a tela do medico.
        Assert.Equal("Helena Souza com tosse há 4 dias.", resultado.Valor!.QueixaPrincipal);
        Assert.Equal("Helena Souza com tosse há 4 dias.", _db.RascunhosIA.Single().QueixaPrincipal);
    }

    [Fact]
    public async Task Rascunho_RefeitoDuranteAConsulta_SubstituiOAnterior()
    {
        await Registrar(0, "Tosse há 4 dias.");
        RetornarQueixa("Tosse há 4 dias.");
        await _servico.GerarRascunhoAsync(_atendimento.Id, _clinica.Id);

        await Registrar(1, "Com catarro amarelado.");
        RetornarQueixa("Tosse produtiva há 4 dias.");
        await _servico.GerarRascunhoAsync(_atendimento.Id, _clinica.Id);

        Assert.Equal("Tosse produtiva há 4 dias.", _db.RascunhosIA.Single().QueixaPrincipal);
    }

    [Fact]
    public async Task Rascunho_ComFalhaDaIA_RegistraOErroSemTravarOAtendimento()
    {
        await Registrar(0, "Tosse há 4 dias.");
        _llm.GerarRascunhoAsync(
                Arg.Any<TextoSanitizado>(), Arg.Any<CredenciaisIA>(), Arg.Any<ContextoGeracao>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("A API de texto respondeu 429: limite"));

        var resultado = await _servico.GerarRascunhoAsync(_atendimento.Id, _clinica.Id);

        Assert.Equal(ResultadoAtendimento.Falha, resultado.Resultado);
        Assert.Equal(StatusAtendimento.EmConsulta, _db.Atendimentos.Single().Status);
        Assert.Contains("429", _db.Atendimentos.Single().ErroProcessamentoIA);
    }

    [Fact]
    public async Task Rascunho_SemFalaTranscrita_NaoChamaAIA()
    {
        var resultado = await _servico.GerarRascunhoAsync(_atendimento.Id, _clinica.Id);

        Assert.Equal(ResultadoAtendimento.Conflito, resultado.Resultado);
        await _llm.DidNotReceiveWithAnyArgs().GerarRascunhoAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task AtendimentoFinalizado_NaoAceitaTrechoNemRascunho()
    {
        _atendimento.Status = StatusAtendimento.Finalizado;
        await _db.SaveChangesAsync();

        var trecho = await _servico.RegistrarTrechoAsync(_atendimento.Id, _clinica.Id, new RegistrarTrechoDto(0, "texto"));
        var rascunho = await _servico.GerarRascunhoAsync(_atendimento.Id, _clinica.Id);

        Assert.Equal(ResultadoAtendimento.Conflito, trecho.Resultado);
        Assert.Equal(ResultadoAtendimento.Conflito, rascunho.Resultado);
    }

    [Fact]
    public async Task Trecho_SemConsentimento_NaoEhGuardado()
    {
        _atendimento.ConsentimentoTranscricao = false;
        await _db.SaveChangesAsync();

        var resultado = await Registrar(0, "Tosse.");

        Assert.Equal(ResultadoAtendimento.Conflito, resultado.Resultado);
        Assert.Empty(_db.TrechosTranscricao);
    }

    [Fact]
    public async Task Trecho_DeOutraClinica_NaoEhEncontrado()
    {
        var resultado = await _servico.RegistrarTrechoAsync(
            _atendimento.Id, Guid.NewGuid(), new RegistrarTrechoDto(0, "texto"));

        Assert.Equal(ResultadoAtendimento.NaoEncontrado, resultado.Resultado);
    }

    [Fact]
    public async Task TrechoDeAudio_SemFala_NaoGuardaNada()
    {
        _stt.TranscreverAsync(Arg.Any<Stream>(), Arg.Any<CredenciaisIA>(), Arg.Any<CancellationToken>())
            .Returns(string.Empty);

        var resultado = await _servico.TranscreverTrechoAsync(_atendimento.Id, _clinica.Id, 0, new MemoryStream([1, 2, 3]));

        Assert.Equal(ResultadoAtendimento.Ok, resultado.Resultado);
        Assert.Null(resultado.Valor);
        Assert.Empty(_db.TrechosTranscricao);
    }

    [Fact]
    public async Task TrechoDeAudio_ComFala_EhSanitizadoComoOTexto()
    {
        _stt.TranscreverAsync(Arg.Any<Stream>(), Arg.Any<CredenciaisIA>(), Arg.Any<CancellationToken>())
            .Returns("Meu nome é Helena Souza e estou com febre.");

        var resultado = await _servico.TranscreverTrechoAsync(_atendimento.Id, _clinica.Id, 0, new MemoryStream([1, 2, 3]));

        Assert.Equal("Meu nome é [PACIENTE] e estou com febre.", _db.Transcricoes.Single().Texto);
        Assert.Equal("Meu nome é Helena Souza e estou com febre.", resultado.Valor!.Texto);
    }

    [Fact]
    public async Task Encerrar_LevaARevisaoComADuracaoDaEscuta()
    {
        await Registrar(0, "Tosse.");

        var resultado = await _servico.EncerrarAsync(_atendimento.Id, _clinica.Id, 480);

        Assert.Equal(ResultadoAtendimento.Ok, resultado);
        Assert.Equal(StatusAtendimento.EmRevisao, _db.Atendimentos.Single().Status);
        var sessao = _db.SessoesTranscricao.Single();
        Assert.Equal(480, sessao.DuracaoSegundos);
        Assert.NotNull(sessao.EncerradaEm);
    }

    [Fact]
    public void ConsultaDeDemonstracao_SanitizadaPreservaOConteudoClinico()
    {
        var trechos = ConsultaExemplo.Trechos();
        var identidade = new IdentidadeAtendimento("Helena", null, null, null, "Dra. Ana");

        var texto = new SanitizadorDados().Sanitizar(string.Join("\n", trechos), identidade).Valor;

        Assert.True(trechos.Count > 20);
        Assert.DoesNotContain("529.982.247-25", texto);
        Assert.DoesNotContain("98765-4321", texto);
        Assert.DoesNotContain("Mariana", texto);
        Assert.Contains("losartana", texto);
        Assert.Contains("cinquenta miligramas", texto);
        Assert.Contains("dipirona quinhentos miligramas", texto);
    }

    private Task<ResultadoOperacao<TrechoRegistradoDto>> Registrar(int sequencia, string texto)
        => _servico.RegistrarTrechoAsync(_atendimento.Id, _clinica.Id, new RegistrarTrechoDto(sequencia, texto));

    private void RetornarQueixa(string queixa)
        => _llm.GerarRascunhoAsync(
                Arg.Any<TextoSanitizado>(), Arg.Any<CredenciaisIA>(), Arg.Any<ContextoGeracao>(), Arg.Any<CancellationToken>())
            .Returns(new RascunhoClinicoDto(queixa, null, null, null, null, null, null));

    public void Dispose() => _db.Dispose();
}
