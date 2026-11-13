namespace Prontuario.Application.Common.Models;

public static class ModosOcupacional
{
    /// <summary>Forma atual: o medico preenche tudo, sem IA.</summary>
    public const string Tradicional = "Tradicional";

    /// <summary>Entrevista secao a secao; a IA preenche a secao em foco.</summary>
    public const string Guiada = "Guiada";

    /// <summary>Conversa livre; a IA distribui cada resposta na secao certa.</summary>
    public const string Escriba = "Escriba";

    public static bool Valido(string? modo) => modo is Tradicional or Guiada or Escriba;
}

public sealed record IniciarOcupacionalDto(string Modo, string Sexo);

public sealed record ResponderCampoDto(string? Valor);

public sealed record JustificarAlertaDto(string? Texto);

/// <summary>
/// Fala como o navegador a envia. O texto existe so durante a requisicao: e
/// sanitizado, vai a IA e e esquecido. Nada da conversa e gravado.
/// </summary>
public sealed record FalaDto(int Sequencia, string Texto, string? Falante = null);

/// <summary>
/// Lote de falas novas, com as ultimas ja consideradas como contexto. E o
/// navegador que guarda a conversa (em memoria) e manda so o necessario.
/// </summary>
public sealed record ProcessarFalasDto(
    IReadOnlyList<FalaDto> Novas,
    IReadOnlyList<FalaDto>? Contexto = null,
    string? SecaoEmFoco = null);

public sealed record CampoEstadoDto(
    string Id,
    string Rotulo,
    string Tipo,
    IReadOnlyList<string> Opcoes,
    bool Obrigatorio,
    /// <summary>Falso para campo condicional ainda nao liberado pelo campo-pai.</summary>
    bool Liberado,
    string? Unidade,
    string? Valor,
    /// <summary>"ia", "medico" ou "tradicional"; nulo sem resposta.</summary>
    string? Origem,
    /// <summary>Resposta diferente da do exame anterior.</summary>
    string? Diferenca,
    bool PorVoz);

public sealed record SecaoEstadoDto(
    string Id,
    string Rotulo,
    IReadOnlyList<CampoEstadoDto> Campos,
    int Obrigatorias,
    int Respondidas,
    IReadOnlyList<string> Pendentes);

public sealed record AlertaEstadoDto(
    string Chave, string Mensagem, IReadOnlyList<string> Campos, bool Critico, string? Justificativa);

/// <summary>O prontuario ocupacional inteiro, como a tela precisa: devolvido a cada alteracao.</summary>
public sealed record EstadoOcupacionalDto(
    string Modo,
    string Sexo,
    IReadOnlyList<SecaoEstadoDto> Secoes,
    IReadOnlyList<AlertaEstadoDto> Alertas,
    double? Imc,
    bool AuscultaCardiaca,
    bool AuscultaPulmonar,
    int? AnoExameAnterior,
    /// <summary>Sem pendencia obrigatoria e sem alerta critico sem justificativa.</summary>
    bool PodeConcluir,
    bool Concluido);

public sealed record EsclarecimentoDto(string Campo, string Rotulo, string Pergunta);

/// <summary>Resultado de um lote de falas: o estado novo e o que a tela precisa mostrar na conversa.</summary>
public sealed record FalasProcessadasDto(
    EstadoOcupacionalDto Estado,
    /// <summary>Campos que a IA preencheu agora, para a tela destacar.</summary>
    IReadOnlyList<string> Preenchidos,
    IReadOnlyList<EsclarecimentoDto> Esclarecimentos,
    /// <summary>Quem disse cada fala nova: "Medico", "Paciente" ou "Acompanhante".</summary>
    IReadOnlyDictionary<int, string> Falantes,
    /// <summary>
    /// Falso quando o lote so tinha confirmacoes ou cumprimentos e a IA nao foi
    /// chamada: a tela reenvia essas falas junto com as proximas.
    /// </summary>
    bool Consideradas);

/// <summary>O que a IA tirou de um lote de falas. Tudo passa pela validacao antes de valer.</summary>
public sealed record ExtracaoOcupacionalDto(
    IReadOnlyList<(string Campo, string Valor)> Atualizacoes,
    IReadOnlyList<(string Campo, string Pergunta)> Esclarecimentos,
    bool AuscultaCardiaca,
    bool AuscultaPulmonar,
    IReadOnlyDictionary<int, string> Falantes);
