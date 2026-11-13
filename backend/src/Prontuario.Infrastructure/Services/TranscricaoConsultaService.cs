using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;
using Prontuario.Application.Privacidade;
using Prontuario.Domain.Entities;
using Prontuario.Domain.Enums;
using Prontuario.Infrastructure.Persistence;

namespace Prontuario.Infrastructure.Services;

public class TranscricaoOptions
{
    public const string SectionName = "Transcricao";

    /// <summary>
    /// Desligar faz todas as consultas usarem o modo por trechos - para redes
    /// em que o WebRTC com o provedor e bloqueado.
    /// </summary>
    public bool TempoRealHabilitado { get; set; } = true;

    /// <summary>
    /// Durante a consulta, mandar a IA so as falas novas (com o rascunho atual)
    /// em vez da conversa inteira a cada atualizacao. Desligar volta ao
    /// comportamento anterior, mais caro.
    /// </summary>
    public bool RascunhoIncremental { get; set; } = true;

    /// <summary>
    /// Quantas falas ja consideradas acompanham as novas, para a IA interpretar
    /// respostas curtas ("sim", "faz tempo") pela pergunta que veio antes.
    /// </summary>
    public int FalasDeContexto { get; set; } = 6;
}

/// <summary>
/// Consulta transcrita ao vivo (RF06-RF08 sem gravacao). Todo texto que entra
/// passa pelo <see cref="ISanitizadorDados"/> antes de ser guardado, e o que vai
/// ao modelo de texto e sempre o texto ja sanitizado.
/// </summary>
public class TranscricaoConsultaService : ITranscricaoConsultaService
{
    private const string SemChave =
        "Nenhuma chave de API configurada para a clinica. Peca ao administrador para cadastrar em Configuracoes.";

    private const string ProtecaoIndisponivel =
        "A protecao de dados (reconhecimento de nomes) esta fora do ar; nada foi enviado a IA. "
        + "A fala continua sendo guardada - tente de novo em instantes.";

    /// <summary>Abaixo de qualquer sequencia que o navegador envia (que comeca em zero).</summary>
    private const int SequenciaLegada = -1;

    private readonly ApplicationDbContext _db;
    private readonly ISanitizadorDados _sanitizador;
    private readonly IConfiguracaoIAService _configuracoes;
    private readonly IProvedorTranscricaoTempoReal _tempoReal;
    private readonly ITranscriptionService _stt;
    private readonly IClinicalNoteGenerator _llm;
    private readonly IAuditoriaService _auditoria;
    private readonly TranscricaoOptions _opcoes;
    private readonly ILogger<TranscricaoConsultaService> _logger;
    private readonly CofreTermosProtegidos _termos;

    public TranscricaoConsultaService(
        ApplicationDbContext db,
        ISanitizadorDados sanitizador,
        IConfiguracaoIAService configuracoes,
        IProvedorTranscricaoTempoReal tempoReal,
        ITranscriptionService stt,
        IClinicalNoteGenerator llm,
        IAuditoriaService auditoria,
        IOptions<TranscricaoOptions> opcoes,
        ILogger<TranscricaoConsultaService> logger,
        CofreTermosProtegidos termos)
    {
        _db = db;
        _sanitizador = sanitizador;
        _configuracoes = configuracoes;
        _tempoReal = tempoReal;
        _stt = stt;
        _llm = llm;
        _auditoria = auditoria;
        _opcoes = opcoes.Value;
        _logger = logger;
        _termos = termos;
    }

    public async Task<ResultadoOperacao<SessaoTranscricaoDto>> IniciarAsync(
        Guid atendimentoId, Guid clinicaId, CancellationToken cancellationToken = default)
    {
        var atendimento = await CarregarAsync(atendimentoId, clinicaId, cancellationToken);
        if (atendimento is null)
        {
            return ResultadoOperacao<SessaoTranscricaoDto>.NaoEncontrado();
        }

        // Sem o aceite registrado nao se escuta nada (RF05).
        if (!atendimento.ConsentimentoTranscricao || atendimento.Status != StatusAtendimento.EmConsulta)
        {
            return ResultadoOperacao<SessaoTranscricaoDto>.Conflito(
                "A transcricao so comeca depois do consentimento do paciente, com a consulta em andamento.");
        }

        var credenciais = await _configuracoes.ObterCredenciaisAsync(clinicaId, cancellationToken);
        if (credenciais is null)
        {
            return ResultadoOperacao<SessaoTranscricaoDto>.Conflito(SemChave);
        }

        var transcricao = GarantirTranscricao(atendimento);
        await _db.SaveChangesAsync(cancellationToken);

        var proxima = await _db.TrechosTranscricao
            .Where(t => t.TranscricaoId == transcricao.Id)
            .Select(t => (int?)t.Sequencia)
            .MaxAsync(cancellationToken) is { } ultima ? ultima + 1 : 0;

        await _auditoria.RegistrarAsync(
            AcoesAuditoria.TranscricaoIniciada, atendimentoId, cancellationToken: cancellationToken);

        if (!_opcoes.TempoRealHabilitado)
        {
            return ResultadoOperacao<SessaoTranscricaoDto>.Ok(new SessaoTranscricaoDto(
                ModosTranscricao.Trechos, null, null, null, credenciais.ModeloTranscricao, proxima,
                "Transcricao em tempo real desligada neste ambiente."));
        }

        try
        {
            var credencial = await _tempoReal.CriarSessaoAsync(credenciais, cancellationToken);
            return ResultadoOperacao<SessaoTranscricaoDto>.Ok(new SessaoTranscricaoDto(
                ModosTranscricao.TempoReal, credencial.Chave, credencial.ExpiraEm, credencial.UrlConexao,
                credenciais.ModeloTranscricao, proxima, null));
        }
        catch (Exception excecao) when (excecao is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            // O tempo real e o melhor caminho, nao o unico: sem ele a consulta
            // continua por trechos, e o medico nao fica sem transcricao.
            _logger.LogWarning(excecao, "Tempo real indisponivel para o atendimento {AtendimentoId}; usando trechos.", atendimentoId);
            return ResultadoOperacao<SessaoTranscricaoDto>.Ok(new SessaoTranscricaoDto(
                ModosTranscricao.Trechos, null, null, null, credenciais.ModeloTranscricao, proxima,
                "Transcricao em tempo real indisponivel agora; usando envio por trechos."));
        }
    }

    public async Task<ResultadoOperacao<TrechoRegistradoDto>> RegistrarTrechoAsync(
        Guid atendimentoId, Guid clinicaId, RegistrarTrechoDto trecho, CancellationToken cancellationToken = default)
    {
        var atendimento = await CarregarAsync(atendimentoId, clinicaId, cancellationToken);
        if (atendimento is null)
        {
            return ResultadoOperacao<TrechoRegistradoDto>.NaoEncontrado();
        }

        if (BloqueioDeFala(atendimento) is { } motivo)
        {
            return ResultadoOperacao<TrechoRegistradoDto>.Conflito(motivo);
        }

        if (trecho.Sequencia < 0)
        {
            return ResultadoOperacao<TrechoRegistradoDto>.Conflito("Sequencia de trecho invalida.");
        }

        return ResultadoOperacao<TrechoRegistradoDto>.Ok(
            await GuardarAsync(atendimento, trecho.Sequencia, trecho.Texto, cancellationToken));
    }

    public async Task<ResultadoOperacao<TrechoRegistradoDto>> TranscreverTrechoAsync(
        Guid atendimentoId, Guid clinicaId, int sequencia, Stream audio, CancellationToken cancellationToken = default)
    {
        var atendimento = await CarregarAsync(atendimentoId, clinicaId, cancellationToken);
        if (atendimento is null)
        {
            return ResultadoOperacao<TrechoRegistradoDto>.NaoEncontrado();
        }

        if (BloqueioDeFala(atendimento) is { } motivo)
        {
            return ResultadoOperacao<TrechoRegistradoDto>.Conflito(motivo);
        }

        var credenciais = await _configuracoes.ObterCredenciaisAsync(clinicaId, cancellationToken);
        if (credenciais is null)
        {
            return ResultadoOperacao<TrechoRegistradoDto>.Conflito(SemChave);
        }

        string texto;
        try
        {
            // O stream vem do corpo da requisicao e morre com ela: o audio nao
            // e copiado para disco, storage ou log em nenhum momento.
            texto = await _stt.TranscreverAsync(audio, credenciais, cancellationToken);
        }
        catch (Exception excecao) when (excecao is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            _logger.LogWarning(excecao, "Falha ao transcrever trecho do atendimento {AtendimentoId}.", atendimentoId);
            return ResultadoOperacao<TrechoRegistradoDto>.Falha(excecao.Message);
        }

        if (string.IsNullOrWhiteSpace(texto))
        {
            return ResultadoOperacao<TrechoRegistradoDto>.Ok(null);
        }

        return ResultadoOperacao<TrechoRegistradoDto>.Ok(
            await GuardarAsync(atendimento, sequencia, texto, cancellationToken));
    }

    public async Task<ResultadoOperacao<RascunhoComOrigensDto>> GerarRascunhoAsync(
        Guid atendimentoId, Guid clinicaId, bool final = false, CancellationToken cancellationToken = default)
    {
        var atendimento = await CarregarAsync(atendimentoId, clinicaId, cancellationToken);
        if (atendimento is null)
        {
            return ResultadoOperacao<RascunhoComOrigensDto>.NaoEncontrado();
        }

        // A IA nao mexe no que ja foi assinado ou exportado.
        if (Bloqueio(atendimento) is { } motivo)
        {
            return ResultadoOperacao<RascunhoComOrigensDto>.Conflito(motivo);
        }

        var transcricao = atendimento.SessaoTranscricao?.Transcricao;
        if (transcricao is null || string.IsNullOrWhiteSpace(transcricao.Texto))
        {
            return ResultadoOperacao<RascunhoComOrigensDto>.Conflito("Ainda nao ha fala transcrita nesta consulta.");
        }

        var credenciais = await _configuracoes.ObterCredenciaisAsync(clinicaId, cancellationToken);
        if (credenciais is null)
        {
            return ResultadoOperacao<RascunhoComOrigensDto>.Conflito(SemChave);
        }

        var identidade = Identidade(atendimento);

        // Nenhum texto vai a IA sem ter passado por todas as camadas da protecao.
        try
        {
            await CompletarReconhecimentoAsync(transcricao, identidade, cancellationToken);
        }
        catch (ProtecaoIndisponivelException excecao)
        {
            _logger.LogWarning(excecao, "Rascunho do atendimento {AtendimentoId} adiado: protecao incompleta.", atendimentoId);
            return ResultadoOperacao<RascunhoComOrigensDto>.Falha(ProtecaoIndisponivel);
        }

        // Durante a consulta so as falas novas vao a IA. A passada final, e a
        // transcricao do fluxo antigo (sem trechos), seguem pela consulta inteira.
        if (!final && _opcoes.RascunhoIncremental && transcricao.Trechos.Count > 0)
        {
            return await AtualizarRascunhoAsync(atendimento, transcricao, credenciais, identidade, cancellationToken);
        }

        // Cada trecho vai numerado pela sua sequencia, para a IA citar de onde
        // tirou cada campo. O texto guardado ja esta sanitizado; passar de novo
        // e defesa em profundidade e o que produz o tipo que o gerador exige.
        var sanitizado = _sanitizador.Sanitizar(TextoNumerado(transcricao), identidade);
        var contexto = new ContextoGeracao(atendimento.TemplateNota?.Instrucoes, atendimento.Medico!.InstrucoesEstilo);

        // Durante a consulta o rascunho e refeito muitas vezes, com o medico
        // esperando: modelo rapido. A passada final usa o modelo completo.
        var modelo = final ? credenciais.ModeloTexto : credenciais.ModeloTextoAoVivo ?? credenciais.ModeloTexto;

        RascunhoComOrigensDto gerado;
        try
        {
            gerado = await _llm.GerarRascunhoAsync(
                sanitizado, credenciais with { ModeloTexto = modelo }, contexto, cancellationToken);
        }
        catch (Exception excecao) when (FalhaDoProvedor(excecao))
        {
            return await RegistrarFalhaAsync(atendimento, transcricao, excecao, cancellationToken);
        }

        VerificarSaida(gerado.Rascunho, identidade, atendimentoId);
        var rascunho = Reidentificar(gerado.Rascunho, identidade);
        var origens = FiltrarOrigens(gerado.Origens, rascunho, transcricao);

        var registro = await _db.RascunhosIA
            .SingleOrDefaultAsync(r => r.TranscricaoId == transcricao.Id, cancellationToken);

        if (registro is null)
        {
            registro = new RascunhoIA { TranscricaoId = transcricao.Id };
            _db.RascunhosIA.Add(registro);
        }

        registro.QueixaPrincipal = rascunho.QueixaPrincipal;
        registro.Hda = rascunho.Hda;
        registro.Antecedentes = rascunho.Antecedentes;
        registro.ExameFisico = rascunho.ExameFisico;
        registro.HipoteseDiagnostica = rascunho.HipoteseDiagnostica;
        registro.Cid10Sugerido = rascunho.Cid10Sugerido;
        registro.Conduta = rascunho.Conduta;
        registro.OrigensJson = JsonSerializer.Serialize(origens);
        AplicarFalantes(transcricao, gerado.Falantes);

        // A consulta inteira foi lida: a proxima atualizacao parte daqui.
        foreach (var trecho in transcricao.Trechos)
        {
            trecho.ConsideradoNoRascunho = true;
        }

        transcricao.Status = StatusProcessamento.Concluido;
        atendimento.ErroProcessamentoIA = null;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            AcoesAuditoria.RascunhoGerado, atendimentoId, $"{sanitizado.TotalSubstituicoes} dados protegidos",
            cancellationToken);

        return ResultadoOperacao<RascunhoComOrigensDto>.Ok(
            new RascunhoComOrigensDto(rascunho, origens, FalantesAtuais(transcricao)));
    }

    /// <summary>
    /// Atualizacao durante a consulta. A IA recebe o rascunho atual, as ultimas
    /// falas ja consideradas e as novas, e devolve so o que mudou: o tamanho da
    /// chamada acompanha a fala nova, e nao a duracao da consulta.
    /// </summary>
    private async Task<ResultadoOperacao<RascunhoComOrigensDto>> AtualizarRascunhoAsync(
        Atendimento atendimento,
        Transcricao transcricao,
        CredenciaisIA credenciais,
        IdentidadeAtendimento identidade,
        CancellationToken cancellationToken)
    {
        var registro = await _db.RascunhosIA
            .SingleOrDefaultAsync(r => r.TranscricaoId == transcricao.Id, cancellationToken);

        var novas = transcricao.Trechos
            .Where(t => !t.ConsideradoNoRascunho && t.Texto.Length > 0)
            .OrderBy(t => t.Sequencia)
            .ToList();

        // Sem fala nova, ou so com confirmacoes e cumprimentos, nao ha o que
        // preencher: nenhuma chamada. As falas ficam para o proximo lote.
        if (novas.Count == 0 || !TriagemDeFalas.TemConteudo(novas.Select(t => t.Texto)))
        {
            return ResultadoOperacao<RascunhoComOrigensDto>.Ok(new RascunhoComOrigensDto(
                ParaDto(registro), FiltrarOrigens(LerOrigens(registro), ParaDto(registro), transcricao),
                FalantesAtuais(transcricao)));
        }

        var contexto = transcricao.Trechos
            .Where(t => t.ConsideradoNoRascunho && t.Texto.Length > 0)
            .OrderByDescending(t => t.Sequencia)
            .Take(Math.Max(0, _opcoes.FalasDeContexto))
            .ToList();

        // O rascunho guardado tem o nome do paciente de volta (e o que o medico
        // ve): antes de retornar a IA, passa de novo por toda a protecao.
        RegistroSanitizado atual;
        try
        {
            atual = await _sanitizador.SanitizarRegistroAsync(ParaDto(registro), identidade, cancellationToken);
        }
        catch (ProtecaoIndisponivelException excecao)
        {
            _logger.LogWarning(excecao, "Rascunho do atendimento {AtendimentoId} adiado: protecao incompleta.", atendimento.Id);
            return ResultadoOperacao<RascunhoComOrigensDto>.Falha(ProtecaoIndisponivel);
        }

        var falasDeContexto = _sanitizador.Sanitizar(TextoDaConsulta.Numerado(contexto, rotularTodos: true), identidade);
        var falasNovas = _sanitizador.Sanitizar(TextoDaConsulta.Numerado(novas, rotularTodos: false), identidade);
        var modelo = credenciais.ModeloTextoAoVivo ?? credenciais.ModeloTexto;

        AtualizacaoRascunhoDto atualizacao;
        try
        {
            atualizacao = await _llm.AtualizarRascunhoAsync(
                atual, registro?.Cid10Sugerido, falasDeContexto, falasNovas,
                credenciais with { ModeloTexto = modelo },
                new ContextoGeracao(atendimento.TemplateNota?.Instrucoes, atendimento.Medico!.InstrucoesEstilo),
                cancellationToken);
        }
        catch (Exception excecao) when (FalhaDoProvedor(excecao))
        {
            return await RegistrarFalhaAsync(atendimento, transcricao, excecao, cancellationToken);
        }

        VerificarSaida(atualizacao.Alteracoes, identidade, atendimento.Id);
        var alteracoes = Reidentificar(atualizacao.Alteracoes, identidade);

        if (registro is null)
        {
            registro = new RascunhoIA { TranscricaoId = transcricao.Id };
            _db.RascunhosIA.Add(registro);
        }

        // Campo nulo na resposta e campo que nao mudou: fica como estava.
        var origens = LerOrigens(registro);
        registro.QueixaPrincipal = alteracoes.QueixaPrincipal ?? registro.QueixaPrincipal;
        registro.Hda = alteracoes.Hda ?? registro.Hda;
        registro.Antecedentes = alteracoes.Antecedentes ?? registro.Antecedentes;
        registro.ExameFisico = alteracoes.ExameFisico ?? registro.ExameFisico;
        registro.HipoteseDiagnostica = alteracoes.HipoteseDiagnostica ?? registro.HipoteseDiagnostica;
        registro.Cid10Sugerido = alteracoes.Cid10Sugerido ?? registro.Cid10Sugerido;
        registro.Conduta = alteracoes.Conduta ?? registro.Conduta;

        // A origem de um campo alterado soma as falas citadas agora as que ja o
        // sustentavam. So vale citacao de fala que a IA de fato recebeu.
        var enviadas = novas.Concat(contexto).Select(t => t.Sequencia).ToHashSet();
        foreach (var (campo, valor) in PorCampo(alteracoes))
        {
            if (valor is null || !atualizacao.Origens.TryGetValue(campo, out var citadas))
            {
                continue;
            }

            origens[campo] = origens.GetValueOrDefault(campo, [])
                .Concat(citadas.Where(enviadas.Contains))
                .Distinct()
                .Order()
                .ToList();
        }

        var rascunho = ParaDto(registro);
        var origensValidas = FiltrarOrigens(origens, rascunho, transcricao);
        registro.OrigensJson = JsonSerializer.Serialize(origensValidas);

        var sequenciasNovas = novas.Select(t => t.Sequencia).ToHashSet();
        AplicarFalantes(
            transcricao,
            atualizacao.Falantes?.Where(par => sequenciasNovas.Contains(par.Key)).ToDictionary(par => par.Key, par => par.Value));

        // So o que foi enviado: fala que chegou durante a chamada vai na proxima.
        foreach (var trecho in novas)
        {
            trecho.ConsideradoNoRascunho = true;
        }

        transcricao.Status = StatusProcessamento.Concluido;
        atendimento.ErroProcessamentoIA = null;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            AcoesAuditoria.RascunhoGerado, atendimento.Id,
            $"atualizacao com {novas.Count} falas novas, {falasNovas.TotalSubstituicoes} dados protegidos",
            cancellationToken);

        return ResultadoOperacao<RascunhoComOrigensDto>.Ok(
            new RascunhoComOrigensDto(rascunho, origensValidas, FalantesAtuais(transcricao)));
    }

    private static bool FalhaDoProvedor(Exception excecao)
        => excecao is HttpRequestException or InvalidOperationException or TaskCanceledException or JsonException;

    private async Task<ResultadoOperacao<RascunhoComOrigensDto>> RegistrarFalhaAsync(
        Atendimento atendimento, Transcricao transcricao, Exception excecao, CancellationToken cancellationToken)
    {
        _logger.LogWarning(excecao, "Falha ao gerar o rascunho do atendimento {AtendimentoId}.", atendimento.Id);
        atendimento.ErroProcessamentoIA = excecao.Message.Length > 500 ? excecao.Message[..500] : excecao.Message;
        transcricao.Status = StatusProcessamento.Falhou;
        await _db.SaveChangesAsync(cancellationToken);
        return ResultadoOperacao<RascunhoComOrigensDto>.Falha(atendimento.ErroProcessamentoIA);
    }

    private static RascunhoClinicoDto ParaDto(RascunhoIA? registro) => new(
        registro?.QueixaPrincipal,
        registro?.Hda,
        registro?.Antecedentes,
        registro?.ExameFisico,
        registro?.HipoteseDiagnostica,
        registro?.Cid10Sugerido,
        registro?.Conduta);

    private static Dictionary<string, IReadOnlyList<int>> LerOrigens(RascunhoIA? registro)
    {
        if (string.IsNullOrWhiteSpace(registro?.OrigensJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, List<int>>>(registro.OrigensJson)?
                .ToDictionary(par => par.Key, par => (IReadOnlyList<int>)par.Value) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static Dictionary<string, string?> PorCampo(RascunhoClinicoDto rascunho) => new()
    {
        ["queixaPrincipal"] = rascunho.QueixaPrincipal,
        ["hda"] = rascunho.Hda,
        ["antecedentes"] = rascunho.Antecedentes,
        ["exameFisico"] = rascunho.ExameFisico,
        ["hipoteseDiagnostica"] = rascunho.HipoteseDiagnostica,
        ["cid10Sugerido"] = rascunho.Cid10Sugerido,
        ["conduta"] = rascunho.Conduta,
    };

    private static string TextoNumerado(Transcricao transcricao) => TextoDaConsulta.Numerado(transcricao);

    /// <summary>
    /// Leva aos trechos quem a IA disse que falou. O que o medico confirmou nao
    /// muda, e sequencia que nao existe e ignorada.
    /// </summary>
    private static void AplicarFalantes(Transcricao transcricao, IReadOnlyDictionary<int, string>? falantes)
    {
        foreach (var trecho in transcricao.Trechos.Where(t => !t.FalanteConfirmado))
        {
            if (falantes is not null && falantes.TryGetValue(trecho.Sequencia, out var nome)
                && Enum.TryParse<Falante>(nome, ignoreCase: true, out var falante))
            {
                trecho.Falante = falante;
            }
        }
    }

    private static Dictionary<int, string> FalantesAtuais(Transcricao transcricao)
        => transcricao.Trechos
            .Where(t => t.Falante != Falante.Indefinido)
            .ToDictionary(t => t.Sequencia, t => t.Falante.ToString());

    private TrechoRegistradoDto ParaTela(TrechoTranscricao trecho, IdentidadeAtendimento identidade) => new(
        trecho.Sequencia,
        _sanitizador.Reidentificar(trecho.Texto, identidade),
        IdentidadeDoAtendimento.ContarMarcadores(trecho.Texto),
        trecho.Falante.ToString(),
        trecho.FalanteConfirmado);

    /// <summary>
    /// So ficam origens que apontam para trechos que existem e para campos que
    /// a IA de fato preencheu: uma citacao inventada nao pode virar evidencia.
    /// </summary>
    private static Dictionary<string, IReadOnlyList<int>> FiltrarOrigens(
        IReadOnlyDictionary<string, IReadOnlyList<int>> origens, RascunhoClinicoDto rascunho, Transcricao transcricao)
    {
        var existentes = transcricao.Trechos.Select(t => t.Sequencia).ToHashSet();
        var preenchidos = PorCampo(rascunho);

        return origens
            .Where(par => preenchidos.TryGetValue(par.Key, out var valor) && !string.IsNullOrWhiteSpace(valor))
            .Select(par => (par.Key, Sequencias: (IReadOnlyList<int>)par.Value.Where(existentes.Contains).ToList()))
            .Where(par => par.Sequencias.Count > 0)
            .ToDictionary(par => par.Key, par => par.Sequencias);
    }

    public async Task<ResultadoOperacao<TranscricaoAtualizadaDto>> ProtegerTermoAsync(
        Guid atendimentoId, Guid clinicaId, string termo, CancellationToken cancellationToken = default)
    {
        termo = termo.Trim();
        if (termo.Length is < 2 or > 120)
        {
            return ResultadoOperacao<TranscricaoAtualizadaDto>.Conflito("Selecione de 2 a 120 caracteres.");
        }

        var atendimento = await CarregarAsync(atendimentoId, clinicaId, cancellationToken);
        if (atendimento is null)
        {
            return ResultadoOperacao<TranscricaoAtualizadaDto>.NaoEncontrado();
        }

        var transcricao = atendimento.SessaoTranscricao?.Transcricao;
        if (transcricao is null)
        {
            return ResultadoOperacao<TranscricaoAtualizadaDto>.Conflito("Esta consulta não tem transcrição.");
        }

        // So o termo novo e aplicado a cada trecho: o texto ja guardado esta
        // sanitizado, e passar de novo pelas outras camadas nao o muda.
        var soOTermo = Identidade(atendimento) with { TermosProtegidos = [termo] };
        var trocados = 0;

        foreach (var trecho in transcricao.Trechos)
        {
            var sanitizado = _sanitizador.Sanitizar(trecho.Texto, soOTermo);
            trocados += sanitizado.Substituicoes.GetValueOrDefault("DADO_PESSOAL");
            trecho.Texto = sanitizado.Valor;
        }

        if (transcricao.Trechos.Count == 0)
        {
            var sanitizado = _sanitizador.Sanitizar(transcricao.Texto, soOTermo);
            trocados += sanitizado.Substituicoes.GetValueOrDefault("DADO_PESSOAL");
            transcricao.Texto = sanitizado.Valor;
        }
        else
        {
            transcricao.Texto = TextoDosTrechos(transcricao);
        }

        if (trocados == 0)
        {
            return ResultadoOperacao<TranscricaoAtualizadaDto>.Conflito(
                "O trecho selecionado não foi encontrado na transcrição.");
        }

        // O termo vale tambem para as falas que ainda vao chegar e para os
        // documentos redigidos depois.
        _termos.Adicionar(atendimento, termo);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            AcoesAuditoria.TermoProtegido, atendimentoId, $"{trocados} ocorrencias", cancellationToken);

        var identidade = Identidade(atendimento);
        var trechos = transcricao.Trechos
            .OrderBy(t => t.Sequencia)
            .Where(t => t.Texto.Length > 0)
            .Select(t => ParaTela(t, identidade))
            .ToList();

        return ResultadoOperacao<TranscricaoAtualizadaDto>.Ok(new TranscricaoAtualizadaDto(
            trechos, IdentidadeDoAtendimento.ContarMarcadores(transcricao.Texto)));
    }

    public async Task<ResultadoOperacao<TrechoRegistradoDto>> DefinirFalanteAsync(
        Guid atendimentoId, Guid clinicaId, int sequencia, string falante, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<Falante>(falante, ignoreCase: true, out var valor) || !Enum.IsDefined(valor))
        {
            return ResultadoOperacao<TrechoRegistradoDto>.Conflito("Falante invalido.");
        }

        var atendimento = await CarregarAsync(atendimentoId, clinicaId, cancellationToken);
        var trecho = atendimento?.SessaoTranscricao?.Transcricao?.Trechos.SingleOrDefault(t => t.Sequencia == sequencia);
        if (atendimento is null || trecho is null)
        {
            return ResultadoOperacao<TrechoRegistradoDto>.NaoEncontrado();
        }

        // Indefinido devolve o trecho a IA; qualquer outro valor e a palavra final do medico.
        trecho.Falante = valor;
        trecho.FalanteConfirmado = valor != Falante.Indefinido;
        await _db.SaveChangesAsync(cancellationToken);

        return ResultadoOperacao<TrechoRegistradoDto>.Ok(ParaTela(trecho, Identidade(atendimento)));
    }

    public async Task<ResultadoAtendimento> EncerrarAsync(
        Guid atendimentoId, Guid clinicaId, int duracaoSegundos, CancellationToken cancellationToken = default)
    {
        var atendimento = await CarregarAsync(atendimentoId, clinicaId, cancellationToken);
        if (atendimento is null)
        {
            return ResultadoAtendimento.NaoEncontrado;
        }

        if (atendimento.Status is StatusAtendimento.Finalizado or StatusAtendimento.Cancelado)
        {
            return ResultadoAtendimento.Conflito;
        }

        if (atendimento.SessaoTranscricao is { } sessao)
        {
            sessao.EncerradaEm = DateTime.UtcNow;
            sessao.DuracaoSegundos = Math.Max(0, duracaoSegundos);
        }

        atendimento.Status = StatusAtendimento.EmRevisao;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            AcoesAuditoria.TranscricaoEncerrada, atendimentoId, $"{Math.Max(0, duracaoSegundos)}s", cancellationToken);

        return ResultadoAtendimento.Ok;
    }

    private async Task<TrechoRegistradoDto> GuardarAsync(
        Atendimento atendimento, int sequencia, string textoBruto, CancellationToken cancellationToken)
    {
        var identidade = Identidade(atendimento);

        // Daqui em diante so existe o texto sanitizado: e ele que vai para o
        // banco, e o bruto morre no fim desta requisicao.
        var (sanitizado, reconhecido) = await SanitizarFalaAsync(textoBruto.Trim(), identidade, cancellationToken);

        var transcricao = GarantirTranscricao(atendimento);

        // Transcricao do fluxo antigo (gravacao em lote) nao tem trechos: o texto
        // inteiro vira o trecho de abertura, para nao se perder na recomposicao.
        if (transcricao.Trechos.Count == 0 && transcricao.Texto.Length > 0)
        {
            _db.TrechosTranscricao.Add(new TrechoTranscricao
            {
                TranscricaoId = transcricao.Id, Sequencia = SequenciaLegada, Texto = transcricao.Texto,
            });
        }

        var existente = transcricao.Trechos.SingleOrDefault(t => t.Sequencia == sequencia);

        if (existente is null)
        {
            // Add explicito: com a chave ja gerada no construtor, o EF trataria
            // um trecho achado so pela navegacao como existente e tentaria um
            // UPDATE. O proprio Add o coloca na colecao da transcricao.
            _db.TrechosTranscricao.Add(new TrechoTranscricao
            {
                TranscricaoId = transcricao.Id,
                Sequencia = sequencia,
                Texto = sanitizado.Valor,
                ReconhecimentoAplicado = reconhecido,
            });
        }
        else
        {
            // Reenvio do mesmo trecho (rede instavel) nao o devolve a IA; texto
            // diferente, sim.
            existente.ConsideradoNoRascunho &= existente.Texto == sanitizado.Valor;
            existente.Texto = sanitizado.Valor;
            existente.ReconhecimentoAplicado = reconhecido;
        }

        transcricao.Texto = TextoDosTrechos(transcricao);
        transcricao.Status = StatusProcessamento.Processando;

        await _db.SaveChangesAsync(cancellationToken);

        var guardado = transcricao.Trechos.Single(t => t.Sequencia == sequencia);
        return ParaTela(guardado, identidade) with { DadosProtegidos = sanitizado.TotalSubstituicoes };
    }

    /// <summary>
    /// Sanitiza a fala que acabou de chegar. Com o reconhecimento de nomes fora
    /// do ar, a fala nao se perde: e guardada com as outras camadas e marcada
    /// como pendente - o reconhecimento roda nela antes de qualquer envio a IA.
    /// </summary>
    private async Task<(TextoSanitizado Texto, bool Reconhecido)> SanitizarFalaAsync(
        string texto, IdentidadeAtendimento identidade, CancellationToken cancellationToken)
    {
        if (!_sanitizador.ReconhecimentoHabilitado)
        {
            return (_sanitizador.Sanitizar(texto, identidade), false);
        }

        try
        {
            return (await _sanitizador.SanitizarAsync(texto, identidade, cancellationToken), true);
        }
        catch (ProtecaoIndisponivelException excecao)
        {
            _logger.LogWarning(excecao, "Reconhecimento de nomes indisponivel; trecho guardado como pendente.");
            return (_sanitizador.Sanitizar(texto, identidade), false);
        }
    }

    /// <summary>
    /// Passa o reconhecimento de nomes nos trechos que ficaram pendentes. Lanca
    /// <see cref="ProtecaoIndisponivelException"/> se ele continuar fora do ar.
    /// </summary>
    private async Task CompletarReconhecimentoAsync(
        Transcricao transcricao, IdentidadeAtendimento identidade, CancellationToken cancellationToken)
    {
        if (!_sanitizador.ReconhecimentoHabilitado)
        {
            return;
        }

        // Transcricao do fluxo antigo, sem trechos: o texto inteiro passa por ele.
        if (transcricao.Trechos.Count == 0)
        {
            transcricao.Texto = (await _sanitizador.SanitizarAsync(transcricao.Texto, identidade, cancellationToken)).Valor;
            return;
        }

        var pendentes = transcricao.Trechos.Where(t => !t.ReconhecimentoAplicado).ToList();
        foreach (var trecho in pendentes)
        {
            trecho.Texto = (await _sanitizador.SanitizarAsync(trecho.Texto, identidade, cancellationToken)).Valor;
            trecho.ReconhecimentoAplicado = true;
        }

        if (pendentes.Count > 0)
        {
            transcricao.Texto = TextoDosTrechos(transcricao);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private static string TextoDosTrechos(Transcricao transcricao)
        => string.Join(
            "\n",
            transcricao.Trechos.OrderBy(t => t.Sequencia).Select(t => t.Texto).Where(t => t.Length > 0));

    private IdentidadeAtendimento Identidade(Atendimento atendimento)
        => IdentidadeDoAtendimento.De(atendimento.PacienteRef!, atendimento.Medico!, _termos.Ler(atendimento));

    private Transcricao GarantirTranscricao(Atendimento atendimento)
    {
        if (atendimento.SessaoTranscricao is null)
        {
            atendimento.SessaoTranscricao = new SessaoTranscricao { AtendimentoId = atendimento.Id };
            _db.SessoesTranscricao.Add(atendimento.SessaoTranscricao);
        }

        var sessao = atendimento.SessaoTranscricao;

        // Retomar a escuta depois de encerrar (o medico lembrou de algo) reabre a sessao.
        sessao.EncerradaEm = null;

        if (sessao.Transcricao is null)
        {
            sessao.Transcricao = new Transcricao { SessaoTranscricaoId = sessao.Id };
            _db.Transcricoes.Add(sessao.Transcricao);
        }

        return sessao.Transcricao;
    }

    /// <summary>Fala so entra com o aceite do paciente registrado (RF05).</summary>
    private static string? BloqueioDeFala(Atendimento atendimento)
        => !atendimento.ConsentimentoTranscricao
            ? "A transcricao so comeca depois do consentimento do paciente."
            : Bloqueio(atendimento);

    /// <summary>Nulo quando o atendimento ainda aceita fala e rascunho da IA.</summary>
    private static string? Bloqueio(Atendimento atendimento)
        => atendimento.Status is StatusAtendimento.Finalizado or StatusAtendimento.Cancelado
            ? "Este atendimento ja foi encerrado."
            : null;

    /// <summary>
    /// A resposta do modelo deveria vir sem dado pessoal, porque a entrada veio
    /// sem. Se algo aparecer, e sinal de que a sanitizacao deixou escapar: fica
    /// o alerta no log (so as categorias, nunca o valor).
    /// </summary>
    private void VerificarSaida(RascunhoClinicoDto sugestao, IdentidadeAtendimento identidade, Guid atendimentoId)
    {
        var texto = string.Join("\n", new[]
        {
            sugestao.QueixaPrincipal, sugestao.Hda, sugestao.Antecedentes, sugestao.ExameFisico,
            sugestao.HipoteseDiagnostica, sugestao.Conduta,
        }.Where(campo => campo is not null));

        var detectado = _sanitizador.Sanitizar(texto, identidade).Substituicoes.ToList();

        if (detectado.Count > 0)
        {
            _logger.LogWarning(
                "Possivel dado pessoal na resposta da IA do atendimento {AtendimentoId}: {Categorias}.",
                atendimentoId, string.Join(", ", detectado.Select(par => $"{par.Key}={par.Value}")));
        }
    }

    private RascunhoClinicoDto Reidentificar(RascunhoClinicoDto rascunho, IdentidadeAtendimento identidade)
    {
        string? Campo(string? valor) => valor is null ? null : _sanitizador.Reidentificar(valor, identidade);

        return new RascunhoClinicoDto(
            Campo(rascunho.QueixaPrincipal),
            Campo(rascunho.Hda),
            Campo(rascunho.Antecedentes),
            Campo(rascunho.ExameFisico),
            Campo(rascunho.HipoteseDiagnostica),
            rascunho.Cid10Sugerido,
            Campo(rascunho.Conduta));
    }

    // A clinica do atendimento e a do medico responsavel - mesmo criterio do AtendimentoService.
    private Task<Atendimento?> CarregarAsync(Guid atendimentoId, Guid clinicaId, CancellationToken cancellationToken)
        => _db.Atendimentos
            .Include(a => a.PacienteRef)
            .Include(a => a.Medico)
            .Include(a => a.TemplateNota)
            .Include(a => a.SessaoTranscricao!.Transcricao!.Trechos)
            .SingleOrDefaultAsync(a => a.Id == atendimentoId && a.Medico!.ClinicaId == clinicaId, cancellationToken);
}
