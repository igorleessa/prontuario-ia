using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;
using Prontuario.Application.Ocupacional;
using Prontuario.Application.Privacidade;
using Prontuario.Domain.Entities;
using Prontuario.Domain.Enums;
using Prontuario.Infrastructure.Persistence;
using Prontuario.Infrastructure.Services;

namespace Prontuario.Application.Tests;

/// <summary>Regras do prontuario ocupacional: funcoes puras, o mesmo valor da sempre o mesmo resultado.</summary>
public class RegrasOcupacionaisTests
{
    private static Dictionary<string, string> R(params (string Campo, string Valor)[] pares)
        => pares.ToDictionary(par => par.Campo, par => par.Valor);

    private static CampoOcupacional Campo(string id) => EsquemaOcupacional.Campos("F").Single(c => c.Id == id);

    [Fact]
    public void Esquema_TemNoveSecoesEASecaoDeSaudeSegueOSexo()
    {
        Assert.Equal(9, EsquemaOcupacional.Secoes("F").Count);
        Assert.Contains(EsquemaOcupacional.Secoes("F"), secao => secao.Id == "saude_mulher");
        Assert.Contains(EsquemaOcupacional.Secoes("M"), secao => secao.Id == "saude_homem");
        Assert.DoesNotContain(EsquemaOcupacional.Campos("M"), campo => campo.Id == "mamografia");
    }

    [Fact]
    public void Esquema_TokenEAsoFicamForaDaVoz()
        => Assert.All(
            EsquemaOcupacional.Secoes("F").Where(s => s.Id is "token" or "aso").SelectMany(s => s.Campos),
            campo => Assert.False(campo.PorVoz));

    [Fact]
    public void Condicional_SoEntraNoChecklistDepoisDeLiberado()
    {
        var historico = EsquemaOcupacional.Secoes("F")[0];

        var semResposta = RegrasOcupacionais.Pendentes(historico, R(), "F").Select(c => c.Id);
        var comAcidente = RegrasOcupacionais.Pendentes(historico, R(("acidente", "Sim")), "F").Select(c => c.Id);

        Assert.Equal(["acidente", "afastamento_inss", "experiencia_funcao"], semResposta);
        Assert.Contains("acidente_qual", comAcidente);
        Assert.Contains("acidente_quando", comAcidente);
    }

    [Fact]
    public void Condicional_EmCadeia_DependeDoPaiDoPai()
    {
        var outra = Campo("outra_especialidade");

        Assert.True(RegrasOcupacionais.Visivel(outra, R(("encaminhar", "Sim"), ("qual_especialidade", "Outra")), "F"));
        Assert.False(RegrasOcupacionais.Visivel(outra, R(("encaminhar", "Não"), ("qual_especialidade", "Outra")), "F"));
    }

    [Theory]
    [InlineData("tabagismo", "nao fumante", "Não fumante")]
    [InlineData("tabagismo", "Fuma às vezes", null)]
    [InlineData("acidente", "SIM", "Sim")]
    [InlineData("altura", "1,68", "168")]
    [InlineData("altura", "168", "168")]
    [InlineData("peso", "78.5", "78.5")]
    [InlineData("peso", "setenta", null)]
    [InlineData("intensidade_dor", "11", null)]
    [InlineData("intensidade_dor", "7", "7")]
    public void Normalizar_SoAceitaValorDoDominioDoCampo(string campo, string valor, string? esperado)
        => Assert.Equal(esperado, RegrasOcupacionais.Normalizar(Campo(campo), valor));

    [Fact]
    public void Alertas_ValorForaDaFaixaEPressaoInvertida()
    {
        var alertas = RegrasOcupacionais.Alertas(R(("altura", "268"), ("pas", "80"), ("pad", "120")));

        Assert.Equal(["r_alt", "pa_inv"], alertas.Select(a => a.Chave));
        Assert.All(alertas, alerta => Assert.True(alerta.Critico));
    }

    [Fact]
    public void Alertas_ImcIncompativelComACinturaTemPrecedenciaSobreARelacaoCinturaAltura()
    {
        // IMC 34,6 com 60 cm de cintura: dispara imc_circ, e nao tambem rce.
        var alertas = RegrasOcupacionais.Alertas(R(("peso", "100"), ("altura", "170"), ("circ_abd", "60")));
        Assert.Equal(["imc_circ"], alertas.Select(a => a.Chave));

        var improvavel = RegrasOcupacionais.Alertas(R(("altura", "170"), ("circ_abd", "160"), ("peso", "70")));
        Assert.Equal(["rce"], improvavel.Select(a => a.Chave));
    }

    [Fact]
    public void Alertas_MedidasNormaisNaoGeramNada()
        => Assert.Empty(RegrasOcupacionais.Alertas(
            R(("peso", "71"), ("altura", "170"), ("circ_abd", "88"), ("pas", "120"), ("pad", "80"), ("fc", "72"), ("fr", "16"))));

    [Fact]
    public void Diferencas_VitaisSoAcimaDoLimiarEDemaisCamposEmQualquerMudanca()
    {
        var anteriores = R(("peso", "71"), ("pas", "122"), ("altura", "170"), ("tabagismo", "Não fumante"), ("alcool", "Nega"));
        var atuais = R(("peso", "74"), ("pas", "145"), ("altura", "171"), ("tabagismo", "Fumante"), ("alcool", "Nega"));

        var diferencas = RegrasOcupacionais.Diferencas(atuais, anteriores, 2025, "F");

        Assert.Equal(["altura", "pas", "tabagismo"], diferencas.Select(d => d.Campo));
        Assert.Equal(
            "Tabagismo — Diferente de 2025: \"Não fumante\" → \"Fumante\"",
            diferencas.Single(d => d.Campo == "tabagismo").Mensagem);
    }

    [Theory]
    [InlineData("Ausculta cardíaca com bulhas rítmicas normofonéticas, sem sopros.", true, false)]
    [InlineData("Murmúrio vesicular presente, sem ruídos adventícios.", false, true)]
    [InlineData("Ainda não auscultei o pulmão.", false, false)]
    [InlineData("Sem dor. Vou auscultar o coração agora.", true, false)]
    [InlineData("Falta a ausculta cardíaca e pulmonar.", false, false)]
    public void Ausculta_ContaSoQuandoAfirmada(string fala, bool cardiaca, bool pulmonar)
    {
        Assert.Equal(cardiaca, DeteccaoAusculta.Cardiaca(fala));
        Assert.Equal(pulmonar, DeteccaoAusculta.Pulmonar(fala));
    }
}

/// <summary>
/// Exame ocupacional de ponta a ponta no servico: a IA sugere, o servico
/// confere cada sugestao, o medico tem a ultima palavra, e nada da conversa
/// fica guardado.
/// </summary>
public class ProntuarioOcupacionalServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguracaoIAService _configuracoes = Substitute.For<IConfiguracaoIAService>();
    private readonly IExtratorOcupacional _extrator = Substitute.For<IExtratorOcupacional>();
    private readonly ProntuarioOcupacionalService _servico;
    private readonly Clinica _clinica = new() { Nome = "Clinica A" };
    private readonly PacienteRef _paciente;
    private readonly Usuario _medico;
    private readonly Atendimento _atendimento;

    public ProntuarioOcupacionalServiceTests()
    {
        _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ocupacional-{Guid.NewGuid()}")
            .Options);

        _medico = new Usuario { ClinicaId = _clinica.Id, Nome = "Dra. Ana Ribeiro", Email = "ana@clinica.test", SenhaHash = "x" };
        _paciente = new PacienteRef { ClinicaId = _clinica.Id, Nome = "Helena Souza", Cpf = "529.982.247-25" };
        _atendimento = NovoAtendimento();

        _db.Clinicas.Add(_clinica);
        _db.Usuarios.Add(_medico);
        _db.Pacientes.Add(_paciente);
        _db.SaveChanges();

        _configuracoes.ObterCredenciaisAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new CredenciaisIA("sk-teste", "gpt-4o-transcribe", "gpt-4o", "gpt-4o-mini"));

        _servico = new ProntuarioOcupacionalService(
            _db, new SanitizadorDados(), _configuracoes, _extrator,
            Substitute.For<ITranscriptionService>(), Substitute.For<IAuditoriaService>(),
            NullLogger<ProntuarioOcupacionalService>.Instance);
    }

    private Atendimento NovoAtendimento()
    {
        var atendimento = new Atendimento { PacienteRefId = _paciente.Id, MedicoId = _medico.Id };
        _db.Atendimentos.Add(atendimento);
        return atendimento;
    }

    [Fact]
    public async Task Iniciar_NenhumCampoVemPreenchido()
    {
        var estado = (await Iniciar(ModosOcupacional.Tradicional)).Valor!;

        Assert.Equal(9, estado.Secoes.Count);
        Assert.All(estado.Secoes.SelectMany(s => s.Campos), campo => Assert.Null(campo.Valor));
        Assert.False(estado.PodeConcluir);
        // Forma atual nao pede consentimento de escuta.
        Assert.Equal(StatusAtendimento.EmRevisao, _db.Atendimentos.Single().Status);
    }

    [Fact]
    public async Task Responder_LiberaOCondicionalEApagaOFilhoQuandoOPaiMuda()
    {
        await Iniciar(ModosOcupacional.Tradicional);

        var bloqueado = await Responder("volume_tabagismo", "1 a 3 cigarros por dia");
        await Responder("tabagismo", "Fumante");
        await Responder("volume_tabagismo", "1 a 3 cigarros por dia");
        var depois = (await Responder("tabagismo", "Não fumante")).Valor!;

        Assert.Equal(ResultadoAtendimento.Conflito, bloqueado.Resultado);
        var volume = Campo(depois, "volume_tabagismo");
        Assert.False(volume.Liberado);
        Assert.Null(volume.Valor);
        Assert.Equal("tradicional", Campo(depois, "tabagismo").Origem);
    }

    [Fact]
    public async Task Responder_ValorForaDoDominio_EhRecusado()
    {
        await Iniciar(ModosOcupacional.Tradicional);

        var resultado = await Responder("tabagismo", "Às vezes");

        Assert.Equal(ResultadoAtendimento.Conflito, resultado.Resultado);
    }

    [Fact]
    public async Task Falas_SemConsentimento_NaoVaoAIA()
    {
        await Iniciar(ModosOcupacional.Escriba);

        var resultado = await Falas((0, "Você fuma?"), (1, "Não, nunca fumei."));

        Assert.Equal(ResultadoAtendimento.Conflito, resultado.Resultado);
        await _extrator.DidNotReceiveWithAnyArgs().ExtrairAsync(default!, default, default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task Falas_PreenchemSoOQueEValido_ESoVaiTextoSanitizadoAIA()
    {
        await IniciarComConsentimento();
        TextoSanitizado? enviado = null;
        _extrator.ExtrairAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<TextoSanitizado>(), Arg.Any<TextoSanitizado>(),
                Arg.Do<TextoSanitizado>(texto => enviado = texto), Arg.Any<CredenciaisIA>(), Arg.Any<CancellationToken>())
            .Returns(Extracao(
                ("tempo_tabagismo", "1 a 5 anos"),      // condicional que chega antes do pai
                ("tabagismo", "fumante"),
                ("alcool", "de vez em quando"),         // fora das opcoes
                ("situacao_aso", "Apto"),               // campo fora da voz
                ("campo_inventado", "x"),
                ("altura", "1,68")));

        var resultado = (await Falas((0, "Dona Helena, a senhora fuma?"), (1, "Fumo, faz uns três anos."))).Valor!;

        Assert.Equal("[0] Dona [PACIENTE], a senhora fuma?\n[1] Fumo, faz uns três anos.", enviado!.Valor);
        Assert.Equal(["tabagismo", "altura", "tempo_tabagismo"], resultado.Preenchidos);
        Assert.Equal("Fumante", Campo(resultado.Estado, "tabagismo").Valor);
        Assert.Equal("ia", Campo(resultado.Estado, "tabagismo").Origem);
        Assert.Equal("168", Campo(resultado.Estado, "altura").Valor);
        Assert.Null(Campo(resultado.Estado, "alcool").Valor);
        Assert.Null(Campo(resultado.Estado, "situacao_aso").Valor);
    }

    [Fact]
    public async Task Falas_NaoSobrescrevemOQueOMedicoRespondeu_MasCorrigemAPropriaIA()
    {
        await IniciarComConsentimento();
        await Responder("tabagismo", "Ex-fumante");
        RetornarExtracao(("tabagismo", "Fumante"), ("alcool", "Nega"));
        await Falas((0, "Eu fumo um pouco e não bebo."));

        RetornarExtracao(("alcool", "Socialmente"));
        var estado = (await Falas((1, "Na verdade bebo socialmente, sim."))).Valor!.Estado;

        Assert.Equal("Ex-fumante", Campo(estado, "tabagismo").Valor);
        Assert.Equal("medico", Campo(estado, "tabagismo").Origem);
        Assert.Equal("Socialmente", Campo(estado, "alcool").Valor);
    }

    [Fact]
    public async Task Falas_NadaDaConversaFicaNoBanco()
    {
        await IniciarComConsentimento();
        RetornarExtracao(("tabagismo", "Fumante"));

        await Falas((0, "Eu fumo escondido da minha esposa, doutora."));

        var guardado = _db.ProntuariosOcupacionais.Single();
        Assert.DoesNotContain("escondido", guardado.RespostasJson);
        Assert.Empty(_db.TrechosTranscricao);
        Assert.Empty(_db.Transcricoes);
    }

    [Fact]
    public async Task Falas_SoComConfirmacoes_NaoChamamAIA()
    {
        await IniciarComConsentimento();

        var resultado = (await Falas((0, "Bom dia!"), (1, "Uhum, tá bom."))).Valor!;

        Assert.False(resultado.Consideradas);
        await _extrator.DidNotReceiveWithAnyArgs().ExtrairAsync(default!, default, default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task Falas_MandamAsRespostasAtuaisEOContextoComQuemFalou()
    {
        await IniciarComConsentimento();
        await Responder("tabagismo", "Fumante");
        TextoSanitizado? respostas = null, contexto = null;
        string? secao = null;
        _extrator.ExtrairAsync(
                Arg.Any<string>(), Arg.Do<string?>(valor => secao = valor), Arg.Do<TextoSanitizado>(texto => respostas = texto),
                Arg.Do<TextoSanitizado>(texto => contexto = texto), Arg.Any<TextoSanitizado>(),
                Arg.Any<CredenciaisIA>(), Arg.Any<CancellationToken>())
            .Returns(Extracao());

        await _servico.ProcessarFalasAsync(_atendimento.Id, _clinica.Id, new ProcessarFalasDto(
            [new FalaDto(5, "Uns dez por dia.")],
            [new FalaDto(4, "Quantos cigarros por dia?", "Medico")],
            "habitos"));

        Assert.Equal("tabagismo = Fumante", respostas!.Valor);
        Assert.Equal("[4] (medico) Quantos cigarros por dia?", contexto!.Valor);
        Assert.Equal("habitos", secao);
    }

    [Fact]
    public async Task Ausculta_VemDaIAOuDaFalaDoMedico_NuncaDaDoPaciente()
    {
        await IniciarComConsentimento();
        _extrator.ExtrairAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<TextoSanitizado>(), Arg.Any<TextoSanitizado>(),
                Arg.Any<TextoSanitizado>(), Arg.Any<CredenciaisIA>(), Arg.Any<CancellationToken>())
            .Returns(new ExtracaoOcupacionalDto([], [], false, false, new Dictionary<int, string> { [0] = "Paciente", [1] = "Medico" }));

        var estado = (await Falas(
            (0, "Da outra vez fizeram ausculta pulmonar em mim."),
            (1, "Bulhas rítmicas normofonéticas, sem sopros."))).Valor!.Estado;

        Assert.True(estado.AuscultaCardiaca);
        Assert.False(estado.AuscultaPulmonar);
    }

    [Fact]
    public async Task Esclarecimento_SoParaCampoAindaPendente()
    {
        await IniciarComConsentimento();
        await Responder("tabagismo", "Fumante");
        _extrator.ExtrairAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<TextoSanitizado>(), Arg.Any<TextoSanitizado>(),
                Arg.Any<TextoSanitizado>(), Arg.Any<CredenciaisIA>(), Arg.Any<CancellationToken>())
            .Returns(new ExtracaoOcupacionalDto(
                [], [("alcool", "Com que frequência você bebe?"), ("tabagismo", "Fuma?")], false, false,
                new Dictionary<int, string>()));

        var resultado = (await Falas((0, "Bebo de vez em quando, depende."))).Valor!;

        var esclarecimento = Assert.Single(resultado.Esclarecimentos);
        Assert.Equal("alcool", esclarecimento.Campo);
        Assert.Equal("Bebidas alcoólicas", esclarecimento.Rotulo);
    }

    [Fact]
    public async Task FalhaDaIA_NaoPerdeOQueJaFoiPreenchido()
    {
        await IniciarComConsentimento();
        await Responder("tabagismo", "Fumante");
        _extrator.ExtrairAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<TextoSanitizado>(), Arg.Any<TextoSanitizado>(),
                Arg.Any<TextoSanitizado>(), Arg.Any<CredenciaisIA>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("A API de texto respondeu 429: limite"));

        var falha = await Falas((0, "Eu bebo socialmente."));
        var estado = (await _servico.ObterAsync(_atendimento.Id, _clinica.Id)).Valor!;

        Assert.Equal(ResultadoAtendimento.Falha, falha.Resultado);
        Assert.Equal("Fumante", Campo(estado, "tabagismo").Valor);
    }

    [Fact]
    public async Task Concluir_BloqueiaComPendenciaEComAlertaCriticoSemJustificativa()
    {
        await Iniciar(ModosOcupacional.Tradicional);

        var comPendencias = await _servico.ConcluirAsync(_atendimento.Id, _clinica.Id);
        await ResponderTudo(alturaErrada: true);
        var comAlerta = await _servico.ConcluirAsync(_atendimento.Id, _clinica.Id);
        var curta = await _servico.JustificarAsync(_atendimento.Id, _clinica.Id, "r_alt", "ok");
        await _servico.JustificarAsync(_atendimento.Id, _clinica.Id, "r_alt", "Paciente com gigantismo, medida conferida.");
        var concluido = await _servico.ConcluirAsync(_atendimento.Id, _clinica.Id);
        var depois = await Responder("tabagismo", "Fumante");

        Assert.Equal(ResultadoAtendimento.Conflito, comPendencias.Resultado);
        Assert.Equal(ResultadoAtendimento.Conflito, comAlerta.Resultado);
        Assert.Equal(ResultadoAtendimento.Conflito, curta.Resultado);
        Assert.True(concluido.Valor!.Concluido);
        Assert.Equal(StatusAtendimento.Finalizado, _db.Atendimentos.Single(a => a.Id == _atendimento.Id).Status);
        // Atendimento salvo nao aceita mais alteracao.
        Assert.Equal(ResultadoAtendimento.Conflito, depois.Resultado);
    }

    [Fact]
    public async Task ExameAnterior_MostraOQueMudouNoCampo()
    {
        await Iniciar(ModosOcupacional.Tradicional);
        await ResponderTudo();
        await _servico.ConcluirAsync(_atendimento.Id, _clinica.Id);

        var novo = NovoAtendimento();
        await _db.SaveChangesAsync();
        await _servico.IniciarAsync(novo.Id, _clinica.Id, new IniciarOcupacionalDto(ModosOcupacional.Tradicional, "F"));
        var estado = (await _servico.ResponderAsync(novo.Id, _clinica.Id, "tabagismo", "Fumante")).Valor!;

        Assert.Equal(DateTime.UtcNow.Year, estado.AnoExameAnterior);
        Assert.Contains("\"Não fumante\" → \"Fumante\"", Campo(estado, "tabagismo").Diferenca);
    }

    [Fact]
    public async Task OutraClinica_NaoAlcancaOProntuario()
    {
        await Iniciar(ModosOcupacional.Tradicional);

        var resultado = await _servico.ObterAsync(_atendimento.Id, Guid.NewGuid());

        Assert.Equal(ResultadoAtendimento.NaoEncontrado, resultado.Resultado);
    }

    [Fact]
    public void Extrator_LeARespostaEIgnoraOQueVemForaDoFormato()
    {
        var extracao = OpenAiExtratorOcupacional.Ler("""
            {"atualizacoes":[{"campo":"tabagismo","valor":"Fumante"},{"campo":"x"},7],
             "esclarecimentos":[{"campo":"alcool","pergunta":"Com que frequência?"}],
             "exame":{"ausculta_cardiaca":true,"ausculta_pulmonar":false},
             "falantes":[{"trecho":3,"falante":"paciente"},{"trecho":"4","falante":"medico"}]}
            """);

        Assert.Equal([("tabagismo", "Fumante")], extracao.Atualizacoes);
        Assert.Equal([("alcool", "Com que frequência?")], extracao.Esclarecimentos);
        Assert.True(extracao.AuscultaCardiaca);
        Assert.False(extracao.AuscultaPulmonar);
        Assert.Equal("Paciente", Assert.Single(extracao.Falantes).Value);
    }

    [Fact]
    public void Extrator_DescreveSoOsCamposQueAVozPodePreencher()
    {
        var esquema = OpenAiExtratorOcupacional.DescreverEsquema("M");

        Assert.Contains("- tabagismo — Tabagismo: Não fumante | Fumante | Ex-fumante", esquema);
        Assert.Contains("- volume_tabagismo — Volume Tabagismo: 1 a 3 cigarros por dia | 4 a 10 cigarros por dia | Mais de 10 cigarros por dia [so se tabagismo = Fumante]", esquema);
        Assert.Contains("- pas — Pressão arterial — sistólica: numero (mmHg)", esquema);
        Assert.Contains("psa", esquema);
        Assert.DoesNotContain("mamografia", esquema);
        Assert.DoesNotContain("situacao_aso", esquema);
    }

    private Task<ResultadoOperacao<EstadoOcupacionalDto>> Iniciar(string modo)
    {
        _db.SaveChanges();
        return _servico.IniciarAsync(_atendimento.Id, _clinica.Id, new IniciarOcupacionalDto(modo, "F"));
    }

    private async Task IniciarComConsentimento()
    {
        await Iniciar(ModosOcupacional.Escriba);
        _atendimento.ConsentimentoTranscricao = true;
        _atendimento.Status = StatusAtendimento.EmConsulta;
        await _db.SaveChangesAsync();
    }

    private Task<ResultadoOperacao<EstadoOcupacionalDto>> Responder(string campo, string valor)
        => _servico.ResponderAsync(_atendimento.Id, _clinica.Id, campo, valor);

    private Task<ResultadoOperacao<FalasProcessadasDto>> Falas(params (int Sequencia, string Texto)[] falas)
        => _servico.ProcessarFalasAsync(
            _atendimento.Id, _clinica.Id,
            new ProcessarFalasDto(falas.Select(fala => new FalaDto(fala.Sequencia, fala.Texto)).ToList()));

    private void RetornarExtracao(params (string Campo, string Valor)[] atualizacoes)
        => _extrator.ExtrairAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<TextoSanitizado>(), Arg.Any<TextoSanitizado>(),
                Arg.Any<TextoSanitizado>(), Arg.Any<CredenciaisIA>(), Arg.Any<CancellationToken>())
            .Returns(Extracao(atualizacoes));

    private static ExtracaoOcupacionalDto Extracao(params (string Campo, string Valor)[] atualizacoes)
        => new(atualizacoes, [], false, false, new Dictionary<int, string>());

    private static CampoEstadoDto Campo(EstadoOcupacionalDto estado, string id)
        => estado.Secoes.SelectMany(secao => secao.Campos).Single(campo => campo.Id == id);

    /// <summary>Responde todo obrigatorio com a primeira opcao que nao abre condicional.</summary>
    private async Task ResponderTudo(bool alturaErrada = false)
    {
        var numeros = new Dictionary<string, string>
        {
            ["pas"] = "120", ["pad"] = "80", ["fc"] = "72", ["fr"] = "16", ["peso"] = "70",
            ["altura"] = alturaErrada ? "268" : "170", ["circ_abd"] = "100",
        };

        foreach (var campo in EsquemaOcupacional.Campos("F").Where(c => c.Obrigatorio && c.DependeDe is null))
        {
            var valor = campo.Tipo switch
            {
                TiposCampo.SimNao => "Não",
                TiposCampo.Numero => numeros[campo.Id],
                TiposCampo.Data => "2026-11-13",
                TiposCampo.Lista or TiposCampo.Opcao => campo.Opcoes[0],
                _ => "Sem alterações.",
            };

            var resultado = await Responder(campo.Id, valor);
            Assert.Equal(ResultadoAtendimento.Ok, resultado.Resultado);
        }
    }

    public void Dispose() => _db.Dispose();
}
