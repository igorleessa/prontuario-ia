using System.Globalization;
using System.Text;

namespace Prontuario.Application.Ocupacional;

/// <summary>Inconsistencia nos valores. Critica: bloqueia salvar ate corrigir ou justificar.</summary>
public sealed record AlertaOcupacional(string Chave, string Mensagem, IReadOnlyList<string> Campos, bool Critico = true);

/// <summary>Resposta diferente da do exame anterior. Informativa, nao bloqueia.</summary>
public sealed record DiferencaOcupacional(string Campo, string Mensagem);

/// <summary>
/// Regras do prontuario ocupacional: o que esta visivel, o que falta, o que e
/// inconsistente e o que mudou desde o ultimo exame. Funcoes puras, sem IA: o
/// mesmo valor da sempre o mesmo resultado, e da para auditar.
/// </summary>
public static class RegrasOcupacionais
{
    private static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Faixa plausivel de cada medida: fora dela, e erro de digitacao ou de escuta.</summary>
    private static readonly (string Campo, string Chave, string Nome, double Min, double Max, string Unidade)[] Faixas =
    [
        ("altura", "r_alt", "Altura", 120, 220, "cm"),
        ("peso", "r_peso", "Peso", 30, 250, "kg"),
        ("circ_abd", "r_circ", "Circunferência abdominal", 40, 200, "cm"),
        ("pas", "r_pas", "PA sistólica", 70, 250, "mmHg"),
        ("pad", "r_pad", "PA diastólica", 40, 150, "mmHg"),
        ("fc", "r_fc", "FC", 35, 200, "bpm"),
        ("fr", "r_fr", "FR", 8, 40, "irpm"),
    ];

    /// <summary>Variacao que merece aviso em relacao ao exame anterior. Zero: qualquer mudanca.</summary>
    private static readonly (string Campo, string Nome, string Unidade, double Delta)[] VariacoesVitais =
    [
        ("altura", "Altura", "cm", 0),
        ("peso", "Peso", "kg", 5),
        ("pas", "Pressão Arterial (sistólica)", "mmHg", 20),
        ("pad", "Pressão Arterial (diastólica)", "mmHg", 15),
        ("fc", "Frequência Cardíaca", "bpm", 20),
        ("circ_abd", "Circunferência Abdominal", "cm", 8),
    ];

    private static readonly HashSet<string> ForaDaComparacao =
    [
        "pas", "pad", "fc", "fr", "peso", "altura", "circ_abd",
        "observacoes_exame", "obs_gerais", "conduta", "certificadora_token", "situacao_aso", "data_conclusao_aso",
    ];

    public static bool Visivel(CampoOcupacional campo, IReadOnlyDictionary<string, string> respostas, string sexo)
    {
        if (campo.DependeDe is null)
        {
            return true;
        }

        // O pai tambem precisa estar liberado: "Outra especialidade" depende de
        // "Qual especialidade", que depende de "Encaminhar".
        var pai = EsquemaOcupacional.Campos(sexo).First(c => c.Id == campo.DependeDe);
        return Visivel(pai, respostas, sexo)
            && respostas.TryGetValue(campo.DependeDe, out var valor) && valor == campo.DependeDeValor;
    }

    /// <summary>Obrigatorios liberados que ainda nao tem resposta.</summary>
    public static IReadOnlyList<CampoOcupacional> Pendentes(
        SecaoOcupacional secao, IReadOnlyDictionary<string, string> respostas, string sexo)
        => secao.Campos
            .Where(campo => campo.Obrigatorio && Visivel(campo, respostas, sexo) && !respostas.ContainsKey(campo.Id))
            .ToList();

    /// <summary>
    /// Valor como deve ser guardado, ou nulo quando nao serve para o campo:
    /// opcao fora da lista, numero que nao e numero. Vale para a IA e para a tela.
    /// </summary>
    public static string? Normalizar(CampoOcupacional campo, string? valor)
    {
        valor = valor?.Trim();
        if (string.IsNullOrEmpty(valor))
        {
            return null;
        }

        switch (campo.Tipo)
        {
            case TiposCampo.SimNao or TiposCampo.Lista or TiposCampo.Opcao:
                return campo.Opcoes.FirstOrDefault(opcao => SemAcento(opcao) == SemAcento(valor));

            case TiposCampo.Numero:
                if (Numero(valor) is not { } numero || numero < 0)
                {
                    return null;
                }

                // "1,68" dito em metros: o campo e em centimetros.
                if (campo.Id == "altura" && numero < 3)
                {
                    numero *= 100;
                }

                if (campo.Id == "intensidade_dor" && (numero > 10 || numero != Math.Floor(numero)))
                {
                    return null;
                }

                return Math.Round(numero, 1).ToString("0.#", CultureInfo.InvariantCulture);

            case TiposCampo.Data:
                return DateOnly.TryParseExact(valor, "yyyy-MM-dd", out var data) ? data.ToString("yyyy-MM-dd") : null;

            default:
                var limite = campo.Tipo == TiposCampo.Texto ? 200 : 2000;
                return valor.Length > limite ? valor[..limite] : valor;
        }
    }

    public static double? Imc(IReadOnlyDictionary<string, string> respostas)
        => Valor(respostas, "peso") is { } peso && Valor(respostas, "altura") is { } altura && altura > 0
            ? Math.Round(peso / Math.Pow(altura / 100, 2), 1)
            : null;

    public static IReadOnlyList<AlertaOcupacional> Alertas(IReadOnlyDictionary<string, string> respostas)
    {
        var alertas = new List<AlertaOcupacional>();

        foreach (var (campo, chave, nome, min, max, unidade) in Faixas)
        {
            if (Valor(respostas, campo) is { } valor && (valor < min || valor > max))
            {
                alertas.Add(new AlertaOcupacional(
                    chave,
                    $"{nome} {Texto(valor)} {unidade} fora da faixa plausível ({min}–{max} {unidade}) — conferir a medida",
                    [campo]));
            }
        }

        if (Valor(respostas, "pas") is { } pas && Valor(respostas, "pad") is { } pad && pas <= pad)
        {
            alertas.Add(new AlertaOcupacional(
                "pa_inv", $"PA {Texto(pas)}/{Texto(pad)}: sistólica deve ser maior que a diastólica", ["pas", "pad"]));
        }

        var circ = Valor(respostas, "circ_abd");
        var imcCirc = false;

        if (Imc(respostas) is { } imc && circ is { } cintura && ((imc >= 30 && cintura < 80) || (imc < 20 && cintura > 100)))
        {
            imcCirc = true;
            alertas.Add(new AlertaOcupacional(
                "imc_circ",
                $"IMC {Texto(imc)} (peso/altura) incompatível com circunferência abdominal de {Texto(cintura)} cm — conferir as medidas",
                ["peso", "altura", "circ_abd"]));
        }

        if (!imcCirc && circ is { } c && Valor(respostas, "altura") is { } alt && alt > 0 && (c / alt < 0.35 || c / alt > 0.9))
        {
            alertas.Add(new AlertaOcupacional(
                "rce",
                $"Relação cintura/altura de {(c / alt).ToString("0.00", Br)} é improvável — conferir as medidas",
                ["circ_abd", "altura"]));
        }

        return alertas;
    }

    public static IReadOnlyList<DiferencaOcupacional> Diferencas(
        IReadOnlyDictionary<string, string> respostas,
        IReadOnlyDictionary<string, string> anteriores,
        int anoAnterior,
        string sexo)
    {
        var diferencas = new List<DiferencaOcupacional>();

        foreach (var (campo, nome, unidade, delta) in VariacoesVitais)
        {
            if (Valor(respostas, campo) is { } atual && Valor(anteriores, campo) is { } antes
                && Math.Abs(atual - antes) >= Math.Max(delta, 0.05))
            {
                diferencas.Add(new DiferencaOcupacional(
                    campo, $"{nome} — em {anoAnterior}: {Texto(antes)} {unidade}; agora: {Texto(atual)} {unidade}"));
            }
        }

        foreach (var campo in EsquemaOcupacional.Campos(sexo).Where(c => !ForaDaComparacao.Contains(c.Id)))
        {
            if (respostas.TryGetValue(campo.Id, out var atual) && anteriores.TryGetValue(campo.Id, out var antes) && atual != antes)
            {
                diferencas.Add(new DiferencaOcupacional(
                    campo.Id, $"{campo.Rotulo} — Diferente de {anoAnterior}: \"{antes}\" → \"{atual}\""));
            }
        }

        return diferencas;
    }

    private static double? Valor(IReadOnlyDictionary<string, string> respostas, string campo)
        => respostas.TryGetValue(campo, out var texto) ? Numero(texto) : null;

    private static double? Numero(string texto)
        => double.TryParse(
            texto.Replace(',', '.'), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
            CultureInfo.InvariantCulture, out var numero) && double.IsFinite(numero)
            ? numero
            : null;

    private static string Texto(double valor) => valor.ToString("0.#", Br);

    private static string SemAcento(string texto)
    {
        var limpo = new StringBuilder();
        foreach (var letra in texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(letra) != UnicodeCategory.NonSpacingMark)
            {
                limpo.Append(letra);
            }
        }

        return limpo.ToString();
    }
}
