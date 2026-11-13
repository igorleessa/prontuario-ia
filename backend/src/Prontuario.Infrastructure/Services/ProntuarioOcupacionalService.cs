using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;
using Prontuario.Application.Ocupacional;
using Prontuario.Application.Privacidade;
using Prontuario.Domain.Entities;
using Prontuario.Domain.Enums;
using Prontuario.Infrastructure.Persistence;

namespace Prontuario.Infrastructure.Services;

/// <summary>
/// Exame ocupacional. A conversa nunca e guardada: cada lote de falas chega do
/// navegador, e sanitizado, vai a IA e e esquecido na mesma requisicao. No banco
/// ficam so as respostas, a origem de cada uma e os sinais do exame fisico.
/// </summary>
public class ProntuarioOcupacionalService : IProntuarioOcupacionalService
{
    private const string OrigemIa = "ia";
    private const string OrigemMedico = "medico";
    private const string OrigemTradicional = "tradicional";

    private const int MaximoDeFalasNovas = 40;
    private const int MaximoDeFalasDeContexto = 12;
    private const int TamanhoMaximoDaFala = 2000;
    private const int JustificativaMinima = 5;

    private const string SemChave =
        "Nenhuma chave de API configurada para a clinica. Peca ao administrador para cadastrar em Configuracoes.";

    private const string ProtecaoIndisponivel =
        "A protecao de dados (reconhecimento de nomes) esta fora do ar; nada foi enviado a IA. Tente de novo em instantes.";

    private static readonly Dictionary<string, string> Falantes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["medico"] = "Medico", ["paciente"] = "Paciente", ["acompanhante"] = "Acompanhante",
    };

    private sealed record Resposta(string Valor, string Origem, DateTime Em);

    private readonly ApplicationDbContext _db;
    private readonly ISanitizadorDados _sanitizador;
    private readonly IConfiguracaoIAService _configuracoes;
    private readonly IExtratorOcupacional _extrator;
    private readonly ITranscriptionService _stt;
    private readonly IAuditoriaService _auditoria;
    private readonly ILogger<ProntuarioOcupacionalService> _logger;

    public ProntuarioOcupacionalService(
        ApplicationDbContext db,
        ISanitizadorDados sanitizador,
        IConfiguracaoIAService configuracoes,
        IExtratorOcupacional extrator,
        ITranscriptionService stt,
        IAuditoriaService auditoria,
        ILogger<ProntuarioOcupacionalService> logger)
    {
        _db = db;
        _sanitizador = sanitizador;
        _configuracoes = configuracoes;
        _extrator = extrator;
        _stt = stt;
        _auditoria = auditoria;
        _logger = logger;
    }

    public async Task<ResultadoOperacao<EstadoOcupacionalDto>> IniciarAsync(
        Guid atendimentoId, Guid clinicaId, IniciarOcupacionalDto pedido, CancellationToken cancellationToken = default)
    {
        var atendimento = await CarregarAsync(atendimentoId, clinicaId, cancellationToken);
        if (atendimento is null)
        {
            return ResultadoOperacao<EstadoOcupacionalDto>.NaoEncontrado();
        }

        if (atendimento.Ocupacional is not null)
        {
            return Ok(await EstadoAsync(atendimento, cancellationToken));
        }

        if (!ModosOcupacional.Valido(pedido.Modo) || !EsquemaOcupacional.SexoValido(pedido.Sexo))
        {
            return Conflito("Informe a forma de atendimento e o sexo do paciente.");
        }

        if (Encerrado(atendimento))
        {
            return Conflito("Este atendimento ja foi encerrado.");
        }

        atendimento.Ocupacional = new ProntuarioOcupacional
        {
            AtendimentoId = atendimento.Id, Modo = pedido.Modo, Sexo = pedido.Sexo,
        };
        _db.ProntuariosOcupacionais.Add(atendimento.Ocupacional);

        // A forma atual nao usa microfone: nao ha consentimento de escuta a pedir.
        if (pedido.Modo == ModosOcupacional.Tradicional)
        {
            atendimento.Status = StatusAtendimento.EmRevisao;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _auditoria.RegistrarAsync(AcoesAuditoria.OcupacionalIniciado, atendimentoId, pedido.Modo, cancellationToken);

        return Ok(await EstadoAsync(atendimento, cancellationToken));
    }

    public async Task<ResultadoOperacao<EstadoOcupacionalDto>> ObterAsync(
        Guid atendimentoId, Guid clinicaId, CancellationToken cancellationToken = default)
    {
        var atendimento = await CarregarAsync(atendimentoId, clinicaId, cancellationToken);
        return atendimento?.Ocupacional is null
            ? ResultadoOperacao<EstadoOcupacionalDto>.NaoEncontrado()
            : Ok(await EstadoAsync(atendimento, cancellationToken));
    }

    public async Task<ResultadoOperacao<EstadoOcupacionalDto>> ResponderAsync(
        Guid atendimentoId, Guid clinicaId, string campo, string? valor, CancellationToken cancellationToken = default)
    {
        var atendimento = await CarregarAsync(atendimentoId, clinicaId, cancellationToken);
        if (atendimento?.Ocupacional is not { } prontuario)
        {
            return ResultadoOperacao<EstadoOcupacionalDto>.NaoEncontrado();
        }

        if (Bloqueio(atendimento) is { } motivo)
        {
            return Conflito(motivo);
        }

        var definicao = EsquemaOcupacional.Campos(prontuario.Sexo).FirstOrDefault(c => c.Id == campo);
        if (definicao is null)
        {
            return ResultadoOperacao<EstadoOcupacionalDto>.NaoEncontrado();
        }

        var respostas = Ler(prontuario);

        if (string.IsNullOrWhiteSpace(valor))
        {
            respostas.Remove(campo);
        }
        else
        {
            if (!RegrasOcupacionais.Visivel(definicao, Valores(respostas), prontuario.Sexo))
            {
                return Conflito("Este campo ainda nao foi liberado pela resposta anterior.");
            }

            if (RegrasOcupacionais.Normalizar(definicao, valor) is not { } normalizado)
            {
                return Conflito($"Valor invalido para \"{definicao.Rotulo}\".");
            }

            var origem = prontuario.Modo == ModosOcupacional.Tradicional ? OrigemTradicional : OrigemMedico;
            respostas[campo] = new Resposta(normalizado, origem, DateTime.UtcNow);
        }

        Guardar(prontuario, respostas);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(await EstadoAsync(atendimento, cancellationToken));
    }

    public async Task<ResultadoOperacao<FalasProcessadasDto>> ProcessarFalasAsync(
        Guid atendimentoId, Guid clinicaId, ProcessarFalasDto falas, CancellationToken cancellationToken = default)
    {
        var atendimento = await CarregarAsync(atendimentoId, clinicaId, cancellationToken);
        if (atendimento?.Ocupacional is not { } prontuario)
        {
            return ResultadoOperacao<FalasProcessadasDto>.NaoEncontrado();
        }

        if (BloqueioDeEscuta(atendimento) is { } motivo)
        {
            return ResultadoOperacao<FalasProcessadasDto>.Conflito(motivo);
        }

        var novas = (falas.Novas ?? [])
            .Where(fala => !string.IsNullOrWhiteSpace(fala.Texto))
            .OrderBy(fala => fala.Sequencia)
            .Take(MaximoDeFalasNovas)
            .ToList();

        // Triagem local, sem custo: lote so de confirmacoes e cumprimentos nao
        // muda o prontuario. A tela reenvia essas falas com as proximas.
        if (novas.Count == 0 || !TriagemDeFalas.TemConteudo(novas.Select(fala => fala.Texto)))
        {
            return ResultadoOperacao<FalasProcessadasDto>.Ok(new FalasProcessadasDto(
                await EstadoAsync(atendimento, cancellationToken), [], [], new Dictionary<int, string>(), Consideradas: false));
        }

        var credenciais = await _configuracoes.ObterCredenciaisAsync(clinicaId, cancellationToken);
        if (credenciais is null)
        {
            return ResultadoOperacao<FalasProcessadasDto>.Conflito(SemChave);
        }

        var contexto = (falas.Contexto ?? [])
            .Where(fala => !string.IsNullOrWhiteSpace(fala.Texto))
            .OrderBy(fala => fala.Sequencia)
            .TakeLast(MaximoDeFalasDeContexto)
            .ToList();

        var identidade = IdentidadeDoAtendimento.De(atendimento.PacienteRef!, atendimento.Medico!);
        var respostas = Ler(prontuario);
        var campos = EsquemaOcupacional.Campos(prontuario.Sexo).ToDictionary(campo => campo.Id);

        // Nada vai a IA sem passar por todas as camadas da protecao - inclusive
        // as respostas de texto livre, que o medico pode ter digitado com nomes.
        TextoSanitizado respostasSeguras, contextoSeguro, novasSeguras;
        try
        {
            respostasSeguras = await _sanitizador.SanitizarAsync(
                string.Join("\n", respostas.Where(par => campos[par.Key].PorVoz).Select(par => $"{par.Key} = {par.Value.Valor}")),
                identidade, cancellationToken);
            contextoSeguro = await _sanitizador.SanitizarAsync(Numeradas(contexto, comFalante: true), identidade, cancellationToken);
            novasSeguras = await _sanitizador.SanitizarAsync(Numeradas(novas, comFalante: false), identidade, cancellationToken);
        }
        catch (ProtecaoIndisponivelException excecao)
        {
            _logger.LogWarning(excecao, "Falas do atendimento {AtendimentoId} nao enviadas: protecao incompleta.", atendimentoId);
            return ResultadoOperacao<FalasProcessadasDto>.Falha(ProtecaoIndisponivel);
        }

        var secaoEmFoco = EsquemaOcupacional.Secoes(prontuario.Sexo).FirstOrDefault(secao => secao.Id == falas.SecaoEmFoco)?.Id;

        ExtracaoOcupacionalDto extracao;
        try
        {
            extracao = await _extrator.ExtrairAsync(
                prontuario.Sexo, secaoEmFoco, respostasSeguras, contextoSeguro, novasSeguras,
                credenciais with { ModeloTexto = credenciais.ModeloTextoAoVivo ?? credenciais.ModeloTexto },
                cancellationToken);
        }
        catch (Exception excecao) when (excecao is HttpRequestException or InvalidOperationException or TaskCanceledException or JsonException)
        {
            // O log leva so o erro do provedor, nunca o que foi dito.
            _logger.LogWarning(excecao, "Falha ao extrair campos do atendimento {AtendimentoId}.", atendimentoId);
            return ResultadoOperacao<FalasProcessadasDto>.Falha("A IA nao respondeu. Os campos seguem editaveis.");
        }

        var preenchidos = Aplicar(extracao, respostas, campos, prontuario.Sexo, identidade);
        Guardar(prontuario, respostas);

        var falantes = extracao.Falantes
            .Where(par => novas.Any(fala => fala.Sequencia == par.Key))
            .ToDictionary(par => par.Key, par => par.Value);

        // A deteccao local reforca a da IA, so nas falas que nao sao do paciente.
        var doMedico = novas
            .Where(fala => !falantes.TryGetValue(fala.Sequencia, out var quem) || quem == "Medico")
            .Select(fala => fala.Texto)
            .ToList();
        prontuario.AuscultaCardiaca |= extracao.AuscultaCardiaca || doMedico.Any(DeteccaoAusculta.Cardiaca);
        prontuario.AuscultaPulmonar |= extracao.AuscultaPulmonar || doMedico.Any(DeteccaoAusculta.Pulmonar);

        await _db.SaveChangesAsync(cancellationToken);
        await _auditoria.RegistrarAsync(
            AcoesAuditoria.OcupacionalFalasProcessadas, atendimentoId,
            $"{novas.Count} falas, {preenchidos.Count} campos, {novasSeguras.TotalSubstituicoes} dados protegidos",
            cancellationToken);

        var valores = Valores(respostas);
        var esclarecimentos = extracao.Esclarecimentos
            .Where(par => campos.TryGetValue(par.Campo, out var campo) && campo.PorVoz
                && !valores.ContainsKey(par.Campo) && RegrasOcupacionais.Visivel(campo, valores, prontuario.Sexo)
                && !string.IsNullOrWhiteSpace(par.Pergunta))
            .DistinctBy(par => par.Campo)
            .Select(par => new EsclarecimentoDto(
                par.Campo, campos[par.Campo].Rotulo, Cortar(_sanitizador.Reidentificar(par.Pergunta.Trim(), identidade), 200)))
            .ToList();

        return ResultadoOperacao<FalasProcessadasDto>.Ok(new FalasProcessadasDto(
            await EstadoAsync(atendimento, cancellationToken), preenchidos, esclarecimentos, falantes, Consideradas: true));
    }

    public async Task<ResultadoOperacao<string>> TranscreverAsync(
        Guid atendimentoId, Guid clinicaId, Stream audio, CancellationToken cancellationToken = default)
    {
        var atendimento = await CarregarAsync(atendimentoId, clinicaId, cancellationToken);
        if (atendimento?.Ocupacional is null)
        {
            return ResultadoOperacao<string>.NaoEncontrado();
        }

        if (BloqueioDeEscuta(atendimento) is { } motivo)
        {
            return ResultadoOperacao<string>.Conflito(motivo);
        }

        var credenciais = await _configuracoes.ObterCredenciaisAsync(clinicaId, cancellationToken);
        if (credenciais is null)
        {
            return ResultadoOperacao<string>.Conflito(SemChave);
        }

        try
        {
            // O audio vem do corpo da requisicao e morre com ela; o texto volta
            // a tela e tambem nao e guardado aqui.
            return ResultadoOperacao<string>.Ok(await _stt.TranscreverAsync(audio, credenciais, cancellationToken));
        }
        catch (Exception excecao) when (excecao is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            _logger.LogWarning(excecao, "Falha ao transcrever trecho do atendimento {AtendimentoId}.", atendimentoId);
            return ResultadoOperacao<string>.Falha(excecao.Message);
        }
    }

    public async Task<ResultadoOperacao<EstadoOcupacionalDto>> JustificarAsync(
        Guid atendimentoId, Guid clinicaId, string chave, string? texto, CancellationToken cancellationToken = default)
    {
        var atendimento = await CarregarAsync(atendimentoId, clinicaId, cancellationToken);
        if (atendimento?.Ocupacional is not { } prontuario)
        {
            return ResultadoOperacao<EstadoOcupacionalDto>.NaoEncontrado();
        }

        if (Bloqueio(atendimento) is { } motivo)
        {
            return Conflito(motivo);
        }

        if (RegrasOcupacionais.Alertas(Valores(Ler(prontuario))).All(alerta => alerta.Chave != chave))
        {
            return ResultadoOperacao<EstadoOcupacionalDto>.NaoEncontrado();
        }

        var justificativas = LerJustificativas(prontuario);
        texto = texto?.Trim();

        if (string.IsNullOrEmpty(texto))
        {
            justificativas.Remove(chave);
        }
        else if (texto.Length < JustificativaMinima)
        {
            return Conflito($"A justificativa precisa de pelo menos {JustificativaMinima} caracteres.");
        }
        else
        {
            justificativas[chave] = Cortar(texto, 500);
        }

        prontuario.JustificativasJson = JsonSerializer.Serialize(justificativas);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(await EstadoAsync(atendimento, cancellationToken));
    }

    public async Task<ResultadoOperacao<EstadoOcupacionalDto>> ConcluirAsync(
        Guid atendimentoId, Guid clinicaId, CancellationToken cancellationToken = default)
    {
        var atendimento = await CarregarAsync(atendimentoId, clinicaId, cancellationToken);
        if (atendimento?.Ocupacional is not { } prontuario)
        {
            return ResultadoOperacao<EstadoOcupacionalDto>.NaoEncontrado();
        }

        if (Bloqueio(atendimento) is { } motivo)
        {
            return Conflito(motivo);
        }

        var estado = await EstadoAsync(atendimento, cancellationToken);
        if (!estado.PodeConcluir)
        {
            var pendentes = estado.Secoes.Sum(secao => secao.Pendentes.Count);
            var criticos = estado.Alertas.Count(alerta => alerta.Critico && alerta.Justificativa is null);
            return Conflito(
                $"Ainda nao e possivel salvar: {pendentes} pergunta(s) obrigatoria(s) sem resposta e "
                + $"{criticos} alerta(s) critico(s) sem correcao ou justificativa.");
        }

        prontuario.ConcluidoEm = DateTime.UtcNow;
        atendimento.Status = StatusAtendimento.Finalizado;
        await _db.SaveChangesAsync(cancellationToken);
        await _auditoria.RegistrarAsync(AcoesAuditoria.OcupacionalConcluido, atendimentoId, cancellationToken: cancellationToken);

        return Ok(await EstadoAsync(atendimento, cancellationToken));
    }

    /// <summary>
    /// Leva ao prontuario o que a IA extraiu, depois de conferir cada item: campo
    /// que existe e aceita voz, valor dentro do dominio, condicional ja liberado.
    /// Resposta dada pelo medico nao e sobrescrita. Devolve os campos alterados.
    /// </summary>
    private List<string> Aplicar(
        ExtracaoOcupacionalDto extracao,
        Dictionary<string, Resposta> respostas,
        Dictionary<string, CampoOcupacional> campos,
        string sexo,
        IdentidadeAtendimento identidade)
    {
        var preenchidos = new List<string>();
        var fila = extracao.Atualizacoes.ToList();

        // Duas passadas: o condicional pode chegar antes do pai que o libera.
        for (var passada = 0; passada < 2 && fila.Count > 0; passada++)
        {
            var adiados = new List<(string Campo, string Valor)>();

            foreach (var (id, valor) in fila)
            {
                if (!campos.TryGetValue(id, out var campo) || !campo.PorVoz
                    || respostas.GetValueOrDefault(id) is { Origem: OrigemMedico or OrigemTradicional }
                    || RegrasOcupacionais.Normalizar(campo, valor) is not { } normalizado)
                {
                    continue;
                }

                if (!RegrasOcupacionais.Visivel(campo, Valores(respostas), sexo))
                {
                    adiados.Add((id, valor));
                    continue;
                }

                if (campo.Tipo is TiposCampo.Texto or TiposCampo.TextoLongo)
                {
                    normalizado = _sanitizador.Reidentificar(normalizado, identidade);
                }

                if (respostas.GetValueOrDefault(id)?.Valor != normalizado)
                {
                    respostas[id] = new Resposta(normalizado, OrigemIa, DateTime.UtcNow);
                    preenchidos.Add(id);
                }
            }

            fila = adiados;
        }

        return preenchidos.Distinct().ToList();
    }

    private async Task<EstadoOcupacionalDto> EstadoAsync(Atendimento atendimento, CancellationToken cancellationToken)
    {
        var prontuario = atendimento.Ocupacional!;
        var respostas = Ler(prontuario);
        var valores = Valores(respostas);
        var justificativas = LerJustificativas(prontuario);

        // Exame anterior: o ultimo exame ocupacional concluido do mesmo paciente.
        var anterior = await _db.ProntuariosOcupacionais
            .Where(o => o.ConcluidoEm != null && o.AtendimentoId != atendimento.Id
                && o.Atendimento!.PacienteRefId == atendimento.PacienteRefId)
            .OrderByDescending(o => o.ConcluidoEm)
            .Select(o => new { o.RespostasJson, o.ConcluidoEm })
            .FirstOrDefaultAsync(cancellationToken);

        var diferencas = anterior is null
            ? []
            : RegrasOcupacionais
                .Diferencas(valores, Valores(Ler(anterior.RespostasJson)), anterior.ConcluidoEm!.Value.Year, prontuario.Sexo)
                .ToDictionary(diferenca => diferenca.Campo, diferenca => diferenca.Mensagem);

        var secoes = EsquemaOcupacional.Secoes(prontuario.Sexo).Select(secao =>
        {
            var liberados = secao.Campos.Where(campo => RegrasOcupacionais.Visivel(campo, valores, prontuario.Sexo)).ToList();
            var obrigatorios = liberados.Where(campo => campo.Obrigatorio).ToList();

            return new SecaoEstadoDto(
                secao.Id,
                secao.Rotulo,
                secao.Campos.Select(campo => new CampoEstadoDto(
                    campo.Id, campo.Rotulo, campo.Tipo, campo.Opcoes, campo.Obrigatorio,
                    liberados.Contains(campo), campo.Unidade,
                    respostas.GetValueOrDefault(campo.Id)?.Valor, respostas.GetValueOrDefault(campo.Id)?.Origem,
                    diferencas.GetValueOrDefault(campo.Id), campo.PorVoz)).ToList(),
                obrigatorios.Count,
                obrigatorios.Count(campo => valores.ContainsKey(campo.Id)),
                RegrasOcupacionais.Pendentes(secao, valores, prontuario.Sexo).Select(campo => campo.Rotulo).ToList());
        }).ToList();

        var alertas = RegrasOcupacionais.Alertas(valores)
            .Select(alerta => new AlertaEstadoDto(
                alerta.Chave, alerta.Mensagem, alerta.Campos, alerta.Critico, justificativas.GetValueOrDefault(alerta.Chave)))
            .ToList();

        // Nos modos com voz, exame fisico dado como concluido sem ausculta
        // identificada na conversa vira aviso (nao bloqueia).
        if (prontuario.Modo != ModosOcupacional.Tradicional && valores.ContainsKey("marcha"))
        {
            var faltando = new[]
            {
                prontuario.AuscultaCardiaca ? null : "cardíaca",
                prontuario.AuscultaPulmonar ? null : "pulmonar",
            }.Where(item => item is not null).ToList();

            if (faltando.Count > 0)
            {
                alertas.Add(new AlertaEstadoDto(
                    "ausculta", $"Ausculta {string.Join(" e ", faltando)} não identificada na conversa.", [], false, null));
            }
        }

        var concluido = prontuario.ConcluidoEm is not null;

        return new EstadoOcupacionalDto(
            prontuario.Modo,
            prontuario.Sexo,
            secoes,
            alertas,
            RegrasOcupacionais.Imc(valores),
            prontuario.AuscultaCardiaca,
            prontuario.AuscultaPulmonar,
            anterior?.ConcluidoEm!.Value.Year,
            !concluido && secoes.All(secao => secao.Pendentes.Count == 0)
                && alertas.All(alerta => !alerta.Critico || alerta.Justificativa is not null),
            concluido);
    }

    private static string Numeradas(IEnumerable<FalaDto> falas, bool comFalante)
        => string.Join("\n", falas.Select(fala =>
        {
            var texto = Cortar(fala.Texto.Trim().ReplaceLineEndings(" "), TamanhoMaximoDaFala);
            return comFalante && fala.Falante is { } quem && Falantes.ContainsKey(quem)
                ? $"[{fala.Sequencia}] ({quem.ToLowerInvariant()}) {texto}"
                : $"[{fala.Sequencia}] {texto}";
        }));

    private static string Cortar(string texto, int limite) => texto.Length > limite ? texto[..limite] : texto;

    private static Dictionary<string, Resposta> Ler(ProntuarioOcupacional prontuario) => Ler(prontuario.RespostasJson);

    private static Dictionary<string, Resposta> Ler(string json)
        => JsonSerializer.Deserialize<Dictionary<string, Resposta>>(json) ?? [];

    private static Dictionary<string, string> LerJustificativas(ProntuarioOcupacional prontuario)
        => JsonSerializer.Deserialize<Dictionary<string, string>>(prontuario.JustificativasJson) ?? [];

    private static Dictionary<string, string> Valores(Dictionary<string, Resposta> respostas)
        => respostas.ToDictionary(par => par.Key, par => par.Value.Valor);

    /// <summary>
    /// Guarda as respostas sem as de campos que deixaram de estar liberados:
    /// trocar "Fumante" por "Não fumante" apaga volume e tempo de tabagismo.
    /// </summary>
    private static void Guardar(ProntuarioOcupacional prontuario, Dictionary<string, Resposta> respostas)
    {
        var campos = EsquemaOcupacional.Campos(prontuario.Sexo).ToList();
        bool removeu;

        do
        {
            var valores = Valores(respostas);
            removeu = false;

            foreach (var campo in campos.Where(c => respostas.ContainsKey(c.Id) && !RegrasOcupacionais.Visivel(c, valores, prontuario.Sexo)))
            {
                respostas.Remove(campo.Id);
                removeu = true;
            }
        }
        while (removeu);

        prontuario.RespostasJson = JsonSerializer.Serialize(respostas);
    }

    private static bool Encerrado(Atendimento atendimento)
        => atendimento.Status is StatusAtendimento.Finalizado or StatusAtendimento.Cancelado;

    private static string? Bloqueio(Atendimento atendimento)
        => Encerrado(atendimento) || atendimento.Ocupacional?.ConcluidoEm is not null
            ? "Este atendimento ja foi encerrado."
            : null;

    private static string? BloqueioDeEscuta(Atendimento atendimento)
        => Bloqueio(atendimento)
            ?? (atendimento.Ocupacional!.Modo == ModosOcupacional.Tradicional
                ? "Este atendimento esta na forma atual, sem IA."
                : !atendimento.ConsentimentoTranscricao
                    ? "A escuta so comeca depois do consentimento do paciente."
                    : null);

    private static ResultadoOperacao<EstadoOcupacionalDto> Ok(EstadoOcupacionalDto estado)
        => ResultadoOperacao<EstadoOcupacionalDto>.Ok(estado);

    private static ResultadoOperacao<EstadoOcupacionalDto> Conflito(string erro)
        => ResultadoOperacao<EstadoOcupacionalDto>.Conflito(erro);

    private Task<Atendimento?> CarregarAsync(Guid atendimentoId, Guid clinicaId, CancellationToken cancellationToken)
        => _db.Atendimentos
            .Include(a => a.PacienteRef)
            .Include(a => a.Medico)
            .Include(a => a.Ocupacional)
            .SingleOrDefaultAsync(a => a.Id == atendimentoId && a.Medico!.ClinicaId == clinicaId, cancellationToken);
}
