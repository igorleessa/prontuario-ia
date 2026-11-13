namespace Prontuario.Application.Ocupacional;

public static class TiposCampo
{
    public const string SimNao = "simnao";
    public const string Lista = "lista";
    public const string Opcao = "opcao";
    public const string Numero = "numero";
    public const string Texto = "texto";
    public const string TextoLongo = "textolongo";
    public const string Data = "data";
}

/// <summary>
/// Um campo do prontuario ocupacional. <paramref name="DependeDe"/> e
/// <paramref name="DependeDeValor"/> descrevem o campo condicional: so existe
/// (e so e obrigatorio) depois que o campo-pai recebe aquele valor.
/// <paramref name="PorVoz"/> falso tira o campo do alcance da IA.
/// </summary>
public sealed record CampoOcupacional(
    string Id,
    string Rotulo,
    string Tipo,
    IReadOnlyList<string> Opcoes,
    bool Obrigatorio = true,
    string? DependeDe = null,
    string? DependeDeValor = null,
    bool PorVoz = true,
    string? Unidade = null);

public sealed record SecaoOcupacional(string Id, string Rotulo, IReadOnlyList<CampoOcupacional> Campos);

/// <summary>
/// As 9 secoes do prontuario ocupacional. E dado, nao logica: tela, checklist,
/// validacao e o que a IA pode preencher saem todos daqui.
/// </summary>
public static class EsquemaOcupacional
{
    public const string Feminino = "F";
    public const string Masculino = "M";

    private static readonly string[] SimNao = ["Sim", "Não"];
    private static readonly string[] Nenhuma = [];
    private static readonly string[] Certificadoras = ["Soluti", "Valid", "HUB Soluti"];

    private static readonly string[] Condicoes =
    [
        "Hipertensão Arterial Sistêmica",
        "Doença Cardíaca",
        "Doença Respiratória Crônica (Ex: DPOC / Asma / Bronquite)",
        "Diabetes Mellitus",
        "Doenças Renais Crônicas",
        "Depressão",
        "Doença Psiquiátrica",
        "Epilepsia, Convulsões, desmaios",
        "Doenças Ortopédicas (Ex: Tendinites, Bursites, Lesões Ligamentares, Lombalgias etc)",
        "Câncer",
        "Hepatopatia",
    ];

    private static CampoOcupacional Sn(string id, string rotulo, string? de = null, string? valor = null)
        => new(id, rotulo, TiposCampo.SimNao, SimNao, DependeDe: de, DependeDeValor: valor);

    private static CampoOcupacional Lista(string id, string rotulo, string[] opcoes, string? de = null, string? valor = null)
        => new(id, rotulo, TiposCampo.Lista, opcoes, DependeDe: de, DependeDeValor: valor);

    private static CampoOcupacional Numero(string id, string rotulo, string unidade, string? de = null, string? valor = null)
        => new(id, rotulo, TiposCampo.Numero, Nenhuma, DependeDe: de, DependeDeValor: valor, Unidade: unidade);

    private static CampoOcupacional Texto(
        string id, string rotulo, bool longo = true, bool obrigatorio = true, string? de = null, string? valor = null)
        => new(id, rotulo, longo ? TiposCampo.TextoLongo : TiposCampo.Texto, Nenhuma, obrigatorio, de, valor);

    private static readonly SecaoOcupacional Historico = new("historico", "Histórico Ocupacional",
    [
        Sn("acidente", "Acidente de Trabalho"),
        Texto("acidente_qual", "Qual", de: "acidente", valor: "Sim"),
        Lista("acidente_quando", "Quando", ["Menos de 1 ano", "1 a 5 anos", "Mais de 5 anos"], "acidente", "Sim"),
        Sn("afastamento_inss", "Afastamento pelo INSS"),
        Lista("motivo_afastamento", "Motivo do Afastamento",
            ["Doença comum", "Acidente de trabalho", "Doença ocupacional"], "afastamento_inss", "Sim"),
        Lista("experiencia_funcao", "Experiência na Função", ["Menos de 6 meses", "6 meses a 2 anos", "Mais de 2 anos"]),
    ]);

    private static readonly SecaoOcupacional Habitos = new("habitos", "Hábitos de Vida / Antecedentes Familiares",
    [
        Lista("atividade_fisica", "Atividade física", ["Sedentário", "Ativo", "Muito ativo"]),
        Lista("alimentacao", "Considera a sua alimentação", ["Saudável", "Regular", "Não saudável"]),
        Lista("tabagismo", "Tabagismo", ["Não fumante", "Fumante", "Ex-fumante"]),
        Lista("volume_tabagismo", "Volume Tabagismo",
            ["1 a 3 cigarros por dia", "4 a 10 cigarros por dia", "Mais de 10 cigarros por dia"], "tabagismo", "Fumante"),
        Lista("tempo_tabagismo", "Tempo Tabagismo",
            ["Menos de 1 ano", "1 a 5 anos", "5 a 10 anos", "Mais de 10 anos"], "tabagismo", "Fumante"),
        Lista("alcool", "Bebidas alcoólicas", ["Nega", "Socialmente", "Frequentemente"]),
        Sn("sono", "Problemas com Sono"),
        Lista("qual_sono", "Qual Problema Sono", ["Insônia", "Apneia do sono", "Sono não reparador"], "sono", "Sim"),
        Sn("estresse", "Encontra-se Estressado(a)"),
    ]);

    private static readonly SecaoOcupacional Antecedentes = new("antecedentes", "Antecedentes Pessoais / Queixa de Dor",
    [
        .. Condicoes.Select((rotulo, indice) => Sn($"cond_{indice}", rotulo)),
        Sn("queixa_dor", "Possui queixa de dor no momento"),
        Texto("local_dor", "Localização da dor", longo: false, de: "queixa_dor", valor: "Sim"),
        Numero("intensidade_dor", "Intensidade da dor (0 a 10)", "0 a 10", "queixa_dor", "Sim"),
    ]);

    private static readonly SecaoOcupacional Screening = new("screening", "Screening de Exames",
    [
        Lista("ldl", "Níveis de LDL-Colesterol", ["Normal", "Limítrofe", "Alto", "Não sabe informar"]),
        Sn("oftalmo", "Realizou avaliação oftalmológica completa nos últimos 12 meses"),
        Sn("dif_enxergar", "Apresenta alguma dificuldade em enxergar no momento"),
        Sn("doenca_oftalmo", "Apresenta alguma doença oftalmológica diagnosticada"),
        Lista("colonoscopia", "Realização de Colonoscopia preventiva", ["Não se Aplica", "Sim, realizada", "Não realizada"]),
    ]);

    private static readonly SecaoOcupacional SaudeMulher = new("saude_mulher", "Saúde da Mulher",
    [
        Lista("mamografia", "Mamografia nos últimos 24 meses", SimNao),
        Lista("papanicolau", "Exame preventivo (Papanicolau) nos últimos 12 meses", SimNao),
    ]);

    private static readonly SecaoOcupacional SaudeHomem = new("saude_homem", "Saúde do Homem",
    [
        Lista("psa", "Dosagem de PSA nos últimos 12 meses", SimNao),
        Lista("toque_retal", "Exames de próstata (toque retal) nos últimos 12 meses", SimNao),
    ]);

    private static readonly SecaoOcupacional Vitais = new("vitais", "Sinais Vitais / Exame Físico",
    [
        Numero("pas", "Pressão arterial — sistólica", "mmHg"),
        Numero("pad", "Pressão arterial — diastólica", "mmHg"),
        Numero("fc", "Frequência cardíaca (FC)", "bpm"),
        Numero("fr", "Frequência respiratória (FR)", "irpm"),
        Numero("peso", "Peso", "kg"),
        Numero("altura", "Altura", "cm"),
        Numero("circ_abd", "Circunferência abdominal", "cm"),
        Lista("regiao_cervical", "Região Cervical", ["Normal", "Alterada"]),
        Texto("observacoes_exame", "Observações", obrigatorio: false),
        Lista("marcha", "Marcha", ["Normal", "Alterada"]),
    ]);

    private static readonly SecaoOcupacional Pcd = new("pcd", "Avaliação PCD / Conclusão",
    [
        Sn("pcd", "O avaliado apresenta características que o enquadrem na condição PCD"),
        Texto("cid", "CID", de: "pcd", valor: "Sim"),
        Lista("deficiencia", "Deficiência", ["Física", "Auditiva", "Visual", "Intelectual", "Múltipla"], "pcd", "Sim"),
        Texto("conduta", "Conduta"),
        Texto("obs_gerais", "Observações Gerais", obrigatorio: false),
        Sn("recomendacao", "Funcionário apresenta alguma recomendação laboral"),
        Texto("qual_recomendacao", "Qual", de: "recomendacao", valor: "Sim"),
        Texto("tempo_recomendacao", "Tempo estimado de recomendação", de: "recomendacao", valor: "Sim"),
        Sn("encaminhar", "Deseja encaminhar para uma consulta com o especialista?"),
        Lista("qual_especialidade", "Qual Especialidade",
            ["Cardiologia", "Ortopedia", "Psiquiatria", "Oftalmologia", "Outra"], "encaminhar", "Sim"),
        Texto("outra_especialidade", "Outra Especialidade", longo: false, de: "qual_especialidade", valor: "Outra"),
        Texto("motivo_encaminhamento", "Motivo do Encaminhamento", de: "encaminhar", valor: "Sim"),
        Lista("resultado_exame", "Resultado Exame Clínico Ocupacional", ["Apto", "Inapto", "Apto com restrições"]),
    ]);

    // Assinatura e ASO sao do medico, nunca da conversa: ficam fora da voz.
    private static readonly SecaoOcupacional Token = new("token", "Token",
    [
        new("certificadora_token", "Qual Certificadora deseja utilizar?", TiposCampo.Opcao, Certificadoras, PorVoz: false),
    ]);

    private static readonly SecaoOcupacional Aso = new("aso", "ASO",
    [
        new("situacao_aso", "Situação ASO", TiposCampo.Opcao, ["Apto", "Inapto", "Pendente"], PorVoz: false),
        new("data_conclusao_aso", "Data Conclusão ASO", TiposCampo.Data, Nenhuma, PorVoz: false),
    ]);

    /// <summary>Secoes na ordem do atendimento, com a de saude conforme o sexo do paciente.</summary>
    public static IReadOnlyList<SecaoOcupacional> Secoes(string sexo) =>
    [
        Historico, Habitos, Antecedentes, Screening,
        sexo == Masculino ? SaudeHomem : SaudeMulher,
        Vitais, Pcd, Token, Aso,
    ];

    public static IEnumerable<CampoOcupacional> Campos(string sexo) => Secoes(sexo).SelectMany(secao => secao.Campos);

    public static bool SexoValido(string? sexo) => sexo is Feminino or Masculino;
}
