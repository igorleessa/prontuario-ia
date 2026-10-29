using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;

namespace Prontuario.Application.Privacidade;

/// <summary>
/// Marcadores que substituem dado pessoal no texto. Os "_PACIENTE" e o do
/// medico sao reversiveis porque o valor vem do cadastro; os demais apagam o
/// dado de vez - o telefone de um terceiro nao tem lugar na nota clinica.
/// </summary>
public static class Marcadores
{
    public const string Paciente = "[PACIENTE]";
    public const string Medico = "[MEDICO]";
    public const string CpfPaciente = "[CPF_PACIENTE]";
    public const string ContatoPaciente = "[CONTATO_PACIENTE]";
    public const string DataNascimento = "[DATA_NASCIMENTO]";
    public const string Pessoa = "[PESSOA]";
    public const string Cpf = "[CPF]";
    public const string Cns = "[CNS]";
    public const string Telefone = "[TELEFONE]";
    public const string Email = "[EMAIL]";
    public const string Cep = "[CEP]";
    public const string Endereco = "[ENDERECO]";
    public const string Documento = "[DOCUMENTO]";
}

/// <summary>
/// Sanitizacao local, sem nenhum servico externo (a ideia e justamente que o
/// dado nao saia daqui para ser protegido). Tres camadas, da mais precisa para
/// a mais generica:
/// <list type="number">
/// <item>Dicionario do atendimento: nome, CPF, nascimento e contato do paciente e o nome do medico.</item>
/// <item>Padroes com validacao: CPF e CNS com digito verificador, telefone, e-mail, CEP, endereco e numeros longos.</item>
/// <item>Contexto de fala: "seu Joao", "minha filha Mariana", "me chamo Pedro".</item>
/// </list>
/// E sem estado: os marcadores do paciente sao reconstruidos do cadastro a cada
/// chamada, entao nenhum mapa "marcador -> valor real" precisa ser guardado.
/// </summary>
public sealed partial class SanitizadorDados : ISanitizadorDados
{
    /// <summary>Conectivos que podem aparecer no meio de um nome ("Maria das Dores").</summary>
    private static readonly HashSet<string> Conectivos = ["da", "de", "do", "das", "dos", "e", "di", "du"];

    /// <summary>
    /// Partes de nome que tambem sao palavras comuns em consulta. Sozinhas, so
    /// contam como nome fora do inicio de frase - "Dores nas costas" abrindo
    /// uma frase e queixa, nao a paciente Maria das Dores.
    /// </summary>
    private static readonly HashSet<string> PalavrasAmbiguas =
        ["dores", "dor", "luz", "paz", "rosa", "branca", "leite", "cruz", "fortes", "neves", "gloria", "vitoria", "graca", "pinto", "prado", "campos", "ramos", "rocha", "lima", "costa", "febre", "flor"];

    /// <summary>
    /// Palavras que seguem "seu", "dona" ou "doutor" sem serem nome de pessoa, e
    /// eponimos medicos - apagar "Parkinson" do prontuario e dano clinico.
    /// </summary>
    private static readonly HashSet<string> NaoSaoPessoa =
    [
        "deus", "senhor", "senhora", "nossa", "jesus", "cristo",
        "parkinson", "alzheimer", "chagas", "graves", "crohn", "hashimoto", "cushing", "addison", "down",
        "murphy", "blumberg", "giordano", "lasegue", "babinski", "romberg", "glasgow", "apgar", "gram",
        "papanicolau", "wilson", "raynaud", "kaposi", "hodgkin", "guillain", "barre", "sjogren", "behcet",
        "kawasaki", "meniere", "bell", "tourette", "asperger", "huntington", "paget", "bartholin", "holter",
        "doppler", "valsalva", "heimlich", "epley", "hallpike", "rinne", "weber", "lyme", "reiter", "still",
        "marfan", "turner", "klinefelter", "willis", "virchow", "rovsing", "basedow", "osler", "kernig",
        "brudzinski", "tinel", "phalen", "finkelstein", "mcmurray", "lachman", "trendelenburg", "homans",
    ];

    private static readonly string[] Meses =
    [
        "janeiro", "fevereiro", "mar[cç]o", "abril", "maio", "junho",
        "julho", "agosto", "setembro", "outubro", "novembro", "dezembro",
    ];

    public TextoSanitizado Sanitizar(string texto, IdentidadeAtendimento identidade)
    {
        ArgumentNullException.ThrowIfNull(identidade);

        var contagem = new Dictionary<string, int>();
        if (string.IsNullOrEmpty(texto))
        {
            return new TextoSanitizado(string.Empty, contagem);
        }

        var resultado = texto.Normalize(NormalizationForm.FormC);

        resultado = SubstituirEmails(resultado, identidade, contagem);
        resultado = SubstituirDataNascimento(resultado, identidade, contagem);
        resultado = Substituir(resultado, Endereco(), _ => Marcadores.Endereco, contagem);
        resultado = SubstituirNumeros(resultado, identidade, contagem);
        resultado = Substituir(resultado, DigitosPorExtenso(), _ => Marcadores.Documento, contagem);
        resultado = SubstituirNomes(resultado, PartesDoNome(identidade.PacienteNome), Marcadores.Paciente, contagem);

        if (!string.IsNullOrWhiteSpace(identidade.MedicoNome))
        {
            resultado = SubstituirNomes(resultado, PartesDoNome(identidade.MedicoNome), Marcadores.Medico, contagem);
        }

        resultado = SubstituirPessoasPorContexto(resultado, contagem);

        return new TextoSanitizado(resultado, contagem);
    }

    public RegistroSanitizado SanitizarRegistro(RascunhoClinicoDto registro, IdentidadeAtendimento identidade)
    {
        TextoSanitizado? Campo(string? valor) => valor is null ? null : Sanitizar(valor, identidade);

        return new RegistroSanitizado(
            Campo(registro.QueixaPrincipal),
            Campo(registro.Hda),
            Campo(registro.Antecedentes),
            Campo(registro.ExameFisico),
            Campo(registro.HipoteseDiagnostica),
            Campo(registro.Conduta));
    }

    public string Reidentificar(string texto, IdentidadeAtendimento identidade)
    {
        if (string.IsNullOrEmpty(texto))
        {
            return texto;
        }

        var resultado = texto.Replace(Marcadores.Paciente, identidade.PacienteNome);

        if (!string.IsNullOrWhiteSpace(identidade.MedicoNome))
        {
            resultado = resultado.Replace(Marcadores.Medico, identidade.MedicoNome);
        }

        if (!string.IsNullOrWhiteSpace(identidade.PacienteCpf))
        {
            resultado = resultado.Replace(Marcadores.CpfPaciente, identidade.PacienteCpf);
        }

        if (!string.IsNullOrWhiteSpace(identidade.PacienteContato))
        {
            resultado = resultado.Replace(Marcadores.ContatoPaciente, identidade.PacienteContato);
        }

        if (identidade.PacienteDataNascimento is { } nascimento)
        {
            resultado = resultado.Replace(
                Marcadores.DataNascimento, nascimento.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
        }

        return resultado;
    }

    // ---------- Camada 2: padroes ----------

    private static string SubstituirEmails(string texto, IdentidadeAtendimento identidade, Dictionary<string, int> contagem)
        => Substituir(texto, EmailRegex(), achado =>
            identidade.PacienteContato is { } contato
                && contato.Contains(achado.Value, StringComparison.OrdinalIgnoreCase)
                ? Marcadores.ContatoPaciente
                : Marcadores.Email,
            contagem);

    private static string SubstituirDataNascimento(
        string texto, IdentidadeAtendimento identidade, Dictionary<string, int> contagem)
    {
        if (identidade.PacienteDataNascimento is not { } data)
        {
            return texto;
        }

        var ano = data.Year.ToString(CultureInfo.InvariantCulture);
        var anoCurto = ano[2..];
        var numerica = new Regex(
            $@"(?<!\d)0?{data.Day}[/.\-]0?{data.Month}[/.\-](?:{ano}|{anoCurto})(?!\d)");
        var porExtenso = new Regex(
            $@"(?<!\d)0?{data.Day}\s+de\s+{Meses[data.Month - 1]}\s+de\s+{ano}(?!\d)",
            RegexOptions.IgnoreCase);

        texto = Substituir(texto, numerica, _ => Marcadores.DataNascimento, contagem);
        return Substituir(texto, porExtenso, _ => Marcadores.DataNascimento, contagem);
    }

    private static string SubstituirNumeros(string texto, IdentidadeAtendimento identidade, Dictionary<string, int> contagem)
    {
        var cpfPaciente = SoDigitos(identidade.PacienteCpf);
        var contatoPaciente = SoDigitos(identidade.PacienteContato);

        return Substituir(texto, SequenciaNumerica(), achado =>
        {
            var digitos = SoDigitos(achado.Value);

            if (cpfPaciente.Length > 0 && digitos == cpfPaciente)
            {
                return Marcadores.CpfPaciente;
            }

            // Telefone dito com ou sem DDD: compara os 8 ultimos digitos.
            if (contatoPaciente.Length >= 8 && digitos.Length >= 8
                && contatoPaciente[^8..] == digitos[^8..])
            {
                return Marcadores.ContatoPaciente;
            }

            if (CepFormatado().IsMatch(achado.Value.Trim()))
            {
                return Marcadores.Cep;
            }

            if (digitos.Length == 11 && CpfValido(digitos))
            {
                return Marcadores.Cpf;
            }

            if (digitos.Length == 15 && CnsValido(digitos))
            {
                return Marcadores.Cns;
            }

            // Numero seguido de unidade e dado clinico (penicilina benzatina
            // 1.200.000 UI, por exemplo), mesmo quando longo.
            var depois = texto.AsSpan(achado.Index + achado.Length);
            if (UnidadeClinica().IsMatch(depois.Length > 12 ? depois[..12].ToString() : depois.ToString()))
            {
                return null;
            }

            if (PareceTelefone(digitos))
            {
                return Marcadores.Telefone;
            }

            // RG, carteirinha de convenio, protocolo: nenhum numero de 8 ou mais
            // digitos tem papel clinico na consulta, e todos identificam alguem.
            return digitos.Length >= 8 ? Marcadores.Documento : null;
        }, contagem);
    }

    private static bool PareceTelefone(string digitos)
    {
        if (digitos.Length == 13 && digitos.StartsWith("55", StringComparison.Ordinal))
        {
            digitos = digitos[2..];
        }

        if (digitos.Length == 12 && digitos[0] == '0')
        {
            digitos = digitos[1..];
        }

        static bool DddValido(string d) => d[0] is >= '1' and <= '9' && d[1] is >= '1' and <= '9';

        return digitos.Length switch
        {
            11 => DddValido(digitos) && digitos[2] == '9',
            10 => DddValido(digitos) && digitos[2] is >= '2' and <= '5',
            9 => digitos[0] == '9',
            8 => digitos[0] is >= '2' and <= '9',
            _ => false,
        };
    }

    internal static bool CpfValido(string digitos)
    {
        if (digitos.Length != 11 || digitos.Distinct().Count() == 1)
        {
            return false;
        }

        int Verificador(int tamanho)
        {
            var soma = 0;
            for (var i = 0; i < tamanho; i++)
            {
                soma += (digitos[i] - '0') * (tamanho + 1 - i);
            }

            var resto = soma % 11;
            return resto < 2 ? 0 : 11 - resto;
        }

        return Verificador(9) == digitos[9] - '0' && Verificador(10) == digitos[10] - '0';
    }

    /// <summary>Cartao Nacional de Saude: a soma ponderada dos 15 digitos (pesos 15 a 1) e multipla de 11.</summary>
    internal static bool CnsValido(string digitos)
    {
        if (digitos.Length != 15 || digitos[0] is not ('1' or '2' or '7' or '8' or '9'))
        {
            return false;
        }

        var soma = 0;
        for (var i = 0; i < 15; i++)
        {
            soma += (digitos[i] - '0') * (15 - i);
        }

        return soma % 11 == 0;
    }

    // ---------- Camada 1: dicionario do atendimento ----------

    private static HashSet<string> PartesDoNome(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            return [];
        }

        return nome
            .Split(new[] { ' ', '-', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(parte => parte.Trim('.', ','))
            .Where(parte => !EhTitulo(parte))
            .Select(Chave)
            .Where(chave => chave.Length >= 3 && !Conectivos.Contains(chave))
            .ToHashSet();
    }

    private static bool EhTitulo(string parte)
        => Chave(parte) is "dr" or "dra" or "doutor" or "doutora" or "prof" or "profa" or "sr" or "sra";

    /// <summary>
    /// Troca cada sequencia de partes do nome por um marcador so: "Helena Souza
    /// Lima" vira "[PACIENTE]", e nao tres marcadores seguidos. Uma parte
    /// sozinha so conta com inicial maiuscula - o reconhecimento de fala grafa
    /// nome proprio com maiuscula, e isso evita apagar palavras comuns.
    /// </summary>
    private static string SubstituirNomes(
        string texto, HashSet<string> partes, string marcador, Dictionary<string, int> contagem)
    {
        if (partes.Count == 0)
        {
            return texto;
        }

        var palavras = Palavra().Matches(texto);
        var trechos = new List<(int Inicio, int Fim)>();

        for (var i = 0; i < palavras.Count; i++)
        {
            if (EhMarcador(texto, palavras[i]) || !partes.Contains(Chave(palavras[i].Value)))
            {
                continue;
            }

            var fim = i;
            var quantidade = 1;
            var k = i + 1;

            while (k < palavras.Count)
            {
                if (SoEspaco(texto, palavras[fim], palavras[k]) && partes.Contains(Chave(palavras[k].Value)))
                {
                    fim = k;
                    quantidade++;
                    k++;
                }
                else if (k + 1 < palavras.Count
                    && SoEspaco(texto, palavras[fim], palavras[k])
                    && SoEspaco(texto, palavras[k], palavras[k + 1])
                    && Conectivos.Contains(Chave(palavras[k].Value))
                    && partes.Contains(Chave(palavras[k + 1].Value)))
                {
                    fim = k + 1;
                    quantidade++;
                    k += 2;
                }
                else
                {
                    break;
                }
            }

            var primeira = palavras[i].Value;
            var aceita = quantidade >= 2
                || (char.IsUpper(primeira[0])
                    && !(PalavrasAmbiguas.Contains(Chave(primeira)) && InicioDeFrase(texto, palavras[i].Index)));

            if (aceita)
            {
                trechos.Add((palavras[i].Index, palavras[fim].Index + palavras[fim].Length));
                i = fim;
            }
        }

        if (trechos.Count == 0)
        {
            return texto;
        }

        Contar(contagem, marcador, trechos.Count);

        var construtor = new StringBuilder(texto);
        foreach (var (inicio, fimTrecho) in trechos.AsEnumerable().Reverse())
        {
            construtor.Remove(inicio, fimTrecho - inicio).Insert(inicio, marcador);
        }

        return construtor.ToString();
    }

    // ---------- Camada 3: contexto de fala ----------

    private static string SubstituirPessoasPorContexto(string texto, Dictionary<string, int> contagem)
        => PessoaPorContexto().Replace(texto, achado =>
        {
            var nome = achado.Groups["nome"];
            var primeira = nome.Value.Split(' ', 2)[0];

            if (NaoSaoPessoa.Contains(Chave(primeira)))
            {
                return achado.Value;
            }

            Contar(contagem, Marcadores.Pessoa, 1);
            return achado.Groups["prefixo"].Value + Marcadores.Pessoa;
        });

    // ---------- Utilitarios ----------

    private static string Substituir(
        string texto, Regex padrao, Func<Match, string?> marcador, Dictionary<string, int> contagem)
        => padrao.Replace(texto, achado =>
        {
            var substituto = marcador(achado);
            if (substituto is null)
            {
                return achado.Value;
            }

            Contar(contagem, substituto, 1);

            // A sequencia numerica pode engolir o espaco final; devolve-lo evita colar o marcador na palavra seguinte.
            return char.IsWhiteSpace(achado.Value[^1]) ? substituto + " " : substituto;
        });

    private static void Contar(Dictionary<string, int> contagem, string marcador, int quantidade)
    {
        var categoria = marcador.Trim('[', ']');
        contagem[categoria] = contagem.GetValueOrDefault(categoria) + quantidade;
    }

    /// <summary>Palavra que faz parte de um marcador ja aplicado ("[CPF_PACIENTE]").</summary>
    private static bool EhMarcador(string texto, Match palavra)
    {
        var antes = palavra.Index > 0 ? texto[palavra.Index - 1] : ' ';
        var fim = palavra.Index + palavra.Length;
        var depois = fim < texto.Length ? texto[fim] : ' ';
        return antes is '[' or '_' || depois is ']' or '_';
    }

    private static bool SoEspaco(string texto, Match anterior, Match seguinte)
    {
        var inicio = anterior.Index + anterior.Length;
        var entre = texto.AsSpan(inicio, seguinte.Index - inicio);
        return entre.Length > 0 && entre.IsWhiteSpace();
    }

    private static bool InicioDeFrase(string texto, int indice)
    {
        for (var i = indice - 1; i >= 0; i--)
        {
            var c = texto[i];
            if (char.IsWhiteSpace(c) || c is '"' or '\'' or '-' or '—')
            {
                continue;
            }

            return c is '.' or '!' or '?' or ':' or '\n';
        }

        return true;
    }

    private static string SoDigitos(string? valor)
        => valor is null ? string.Empty : new string(valor.Where(char.IsAsciiDigit).ToArray());

    /// <summary>
    /// Chave de comparacao tolerante a grafia: sem acento, minuscula e com as
    /// variacoes mais comuns de nome proprio igualadas (Sylvia/Silvia,
    /// Thais/Tais, Luiz/Luis, Kátia/Cátia).
    /// </summary>
    internal static string Chave(string palavra)
    {
        var semAcento = new StringBuilder(palavra.Length);
        foreach (var c in palavra.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                semAcento.Append(char.ToLowerInvariant(c));
            }
        }

        var chave = semAcento.ToString()
            .Replace("ph", "f")
            .Replace("th", "t")
            .Replace('y', 'i')
            .Replace('k', 'c')
            .Replace('w', 'v');

        var semDuplas = new StringBuilder(chave.Length);
        foreach (var c in chave)
        {
            if (semDuplas.Length == 0 || semDuplas[^1] != c)
            {
                semDuplas.Append(c);
            }
        }

        if (semDuplas.Length > 3 && semDuplas[^1] == 'z')
        {
            semDuplas[^1] = 's';
        }

        return semDuplas.ToString();
    }

    [GeneratedRegex(@"\p{L}[\p{L}\p{M}']*")]
    private static partial Regex Palavra();

    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}")]
    private static partial Regex EmailRegex();

    /// <summary>Digitos com espaco, ponto, hifen ou parenteses no meio - nunca barra, que e data.</summary>
    [GeneratedRegex(@"(?<![\w,])\(?\d(?:\d|[ .\-()]{1,2}(?=[\d(]))*\d\)?")]
    private static partial Regex SequenciaNumerica();

    [GeneratedRegex(@"^\d{5}-\d{3}$")]
    private static partial Regex CepFormatado();

    [GeneratedRegex(@"^\s*(?:mg|mcg|µg|ml|g\b|kg|ui\b|u\b|unidades|mmhg|bpm|%|mm³|/mm|células|celulas)", RegexOptions.IgnoreCase)]
    private static partial Regex UnidadeClinica();

    [GeneratedRegex(
        @"\b(?:(?:zero|um|uma|dois|duas|tr[eê]s|quatro|cinco|seis|meia|sete|oito|nove)\b[\s,\-]*){7,}(?:zero|um|uma|dois|duas|tr[eê]s|quatro|cinco|seis|meia|sete|oito|nove)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex DigitosPorExtenso();

    /// <summary>
    /// Logradouro seguido de nome com maiuscula ou numero ("Rua das Flores, 123",
    /// "Avenida Brasil 500, apto 12"). "Caiu na rua" nao casa: falta o nome.
    /// </summary>
    [GeneratedRegex(
        @"\b(?i:rua|r\.|avenida|av\.|travessa|tv\.|alameda|al\.|estrada|rodovia|pra[çc]a|largo|viela|beco|condom[íi]nio|residencial|bairro|loteamento|quadra|conjunto)\s+(?=[\p{Lu}\d]|(?i:d[aeo]s?)\s+\p{Lu})(?:(?:(?i:d[aeo]s?)|\p{Lu}[\p{L}'.]*|\d{1,5})(?:\s+|(?=[,.;]|$))){1,6}(?:,?\s*(?i:n[º°o]\.?|n[uú]mero)?\s*\d{1,5})?(?:,?\s*(?i:apto?\.?|apartamento|casa|bloco|bl\.?)\s*\w{1,5})?")]
    private static partial Regex Endereco();

    /// <summary>
    /// Nome proprio introduzido por tratamento ("seu Joao", "dona Maria", "Dr.
    /// Paulo"), por apresentacao ("me chamo Pedro") ou por parentesco ("minha
    /// filha Mariana") - a forma mais comum de um terceiro aparecer na consulta.
    /// </summary>
    [GeneratedRegex(
        @"(?<prefixo>\b(?i:(?:(?:minha|meu|minhas|meus|sua|seu|a|o|da|do)\s+)?(?:filh[ao]s?|net[ao]s?|sobrinh[ao]s?|espos[ao]|irm[ãa]o?s?|irm[ãa]s|prim[ao]s?|cunhad[ao]|vizinh[ao]|amig[ao]|namorad[ao]|enteado|enteada|afilhad[ao]|m[ãa]e|pai|marido|mulher|av[óôo]|tia|tio|genro|nora|chefe|patr[ãa]o|patroa|cuidador|cuidadora|acompanhante))\s+|\b(?i:seu|sr\.?|senhor|dona|sra\.?|senhora|dr\.?|dra\.?|doutor|doutora)\s+|\b(?i:me\s+chamo|meu\s+nome\s+[ée]|se\s+chama|chamad[ao])\s+)(?<nome>\p{Lu}\p{Ll}+(?:\s+(?:d[aeo]s?\s+)?\p{Lu}\p{Ll}+){0,3})")]
    private static partial Regex PessoaPorContexto();
}
