using Prontuario.Application.Common.Models;
using Prontuario.Application.Privacidade;

namespace Prontuario.Application.Tests;

/// <summary>
/// A camada de sanitizacao tem dois jeitos de falhar: deixar passar dado
/// pessoal para o provedor de IA (vazamento) ou apagar informacao clinica
/// (dano ao prontuario). Os testes cobrem os dois lados.
/// </summary>
public class SanitizadorDadosTests
{
    private readonly SanitizadorDados _sanitizador = new();

    private static readonly IdentidadeAtendimento Identidade = new(
        "Helena Souza Lima",
        "529.982.247-25",
        new DateOnly(1961, 3, 12),
        "(11) 98765-4321",
        "Dra. Ana Ribeiro");

    private string Sanitizar(string texto) => _sanitizador.Sanitizar(texto, Identidade).Valor;

    // ---------- Vazamento: o que precisa sair ----------

    [Theory]
    [InlineData("Bom dia, dona Helena, pode sentar.", "Bom dia, dona [PACIENTE], pode sentar.")]
    [InlineData("A senhora é a Helena Souza Lima?", "A senhora é a [PACIENTE]?")]
    [InlineData("Helena Lima, certo?", "[PACIENTE], certo?")]
    [InlineData("Obrigada, doutora Ana.", "Obrigada, doutora [MEDICO].")]
    public void NomeDoPacienteEDoMedico_ViramMarcador(string texto, string esperado)
        => Assert.Equal(esperado, Sanitizar(texto));

    [Fact]
    public void Nome_ComGrafiaDiferenteDoCadastro_TambemEhReconhecido()
    {
        var identidade = Identidade with { PacienteNome = "Thaís Sylva" };

        var resultado = _sanitizador.Sanitizar("A Tais Silva chegou.", identidade).Valor;

        Assert.Equal("A [PACIENTE] chegou.", resultado);
    }

    [Theory]
    [InlineData("Meu CPF é 529.982.247-25.", "Meu CPF é [CPF_PACIENTE].")]
    [InlineData("O CPF do meu marido é 111.444.777-35.", "O CPF do meu marido é [CPF].")]
    [InlineData("Anota aí: 11144477735", "Anota aí: [CPF]")]
    [InlineData("Cartão do SUS 898 0010 0000 0001 2", "Cartão do SUS [DOCUMENTO]")]
    [InlineData("Meu telefone é (11) 98765-4321.", "Meu telefone é [CONTATO_PACIENTE].")]
    [InlineData("Liga para a minha filha no 21 99876-5544.", "Liga para a minha filha no [TELEFONE].")]
    [InlineData("O CEP é 04567-120.", "O CEP é [CEP].")]
    [InlineData("O RG é 12.345.678-9.", "O RG é [DOCUMENTO].")]
    [InlineData("Manda para helena.lima@gmail.com por favor", "Manda para [EMAIL] por favor")]
    public void IdentificadoresNumericosEContato_ViramMarcador(string texto, string esperado)
        => Assert.Equal(esperado, Sanitizar(texto));

    [Fact]
    public void CnsValido_ViraMarcadorProprio()
        => Assert.Equal("CNS [CNS].", Sanitizar("CNS 700000000000005."));

    [Fact]
    public void DataDeNascimento_ViraMarcador()
    {
        Assert.Equal("Nasci em [DATA_NASCIMENTO].", Sanitizar("Nasci em 12/03/1961."));
        Assert.Equal("Nasci em [DATA_NASCIMENTO].", Sanitizar("Nasci em 12 de março de 1961."));
    }

    [Fact]
    public void NumeroDitoPorExtenso_ViraMarcador()
        => Assert.Equal(
            "É [DOCUMENTO].",
            Sanitizar("É um dois três quatro cinco seis sete oito nove."));

    [Theory]
    [InlineData("Moro na Rua das Flores, 123, apto 45.", "Moro na [ENDERECO].")]
    [InlineData("Fica na Avenida Paulista 1500 perto do metrô.", "Fica na [ENDERECO] perto do metrô.")]
    public void Endereco_ViraMarcador(string texto, string esperado)
        => Assert.Equal(esperado, Sanitizar(texto));

    [Theory]
    [InlineData("Quem veio foi o seu Joaquim.", "Quem veio foi o seu [PESSOA].")]
    [InlineData("Minha filha Mariana trouxe os exames.", "Minha filha [PESSOA] trouxe os exames.")]
    [InlineData("O Dr. Paulo Mendes pediu o exame.", "O Dr. [PESSOA] pediu o exame.")]
    [InlineData("Muito prazer, me chamo Roberto.", "Muito prazer, me chamo [PESSOA].")]
    public void TerceiroCitadoNaConsulta_ViraMarcador(string texto, string esperado)
        => Assert.Equal(esperado, Sanitizar(texto));

    [Theory]
    [InlineData("A Mariana trouxe os exames ontem.", "A [PESSOA] trouxe os exames ontem.")]
    [InlineData("Quem vem buscar é o Rafael Gonçalves.", "Quem vem buscar é o [PESSOA].")]
    [InlineData("Thiago Ferreira da Silva ligou cedo.", "[PESSOA] ligou cedo.")]
    [InlineData("A Clara Mendes é a cuidadora.", "A [PESSOA] é a cuidadora.")]
    public void NomeComumSemPistaNoContexto_ViraMarcador(string texto, string esperado)
        => Assert.Equal(esperado, Sanitizar(texto));

    [Theory]
    [InlineData("Clara hipótese de sinusite bacteriana.")]
    [InlineData("Dores nas costas e no joelho.")]
    [InlineData("FAN e ANA não reagentes.")]
    [InlineData("Íris sem alterações, pupilas isocóricas.")]
    [InlineData("mariana é só uma palavra minúscula aqui.")]
    public void PalavraQueTambemEhNome_NaoEhApagadaSozinha(string texto)
        => Assert.Equal(texto, Sanitizar(texto));

    [Fact]
    public void TermoMarcadoPeloMedico_EhSanitizadoEmQualquerGrafia()
    {
        var identidade = Identidade with { TermosProtegidos = ["Escola Estadual Monteiro Lobato", "MAT-55821"] };

        var resultado = _sanitizador.Sanitizar(
            "O filho estuda na escola estadual Monteiro Lobato, matrícula MAT 55821.", identidade).Valor;

        Assert.Equal("O filho estuda na [DADO_PESSOAL], matrícula [DADO_PESSOAL].", resultado);
    }

    // ---------- Dano clinico: o que precisa ficar ----------

    [Theory]
    [InlineData("Pressão 130 por 80 mmHg, frequência 78 bpm, saturação 97%.")]
    [InlineData("Dipirona 500 mg de 6 em 6 horas por 5 dias.")]
    [InlineData("Penicilina benzatina 1.200.000 UI intramuscular, dose única.")]
    [InlineData("Glicemia de jejum 130, hemoglobina glicada 7,2%.")]
    [InlineData("Plaquetas 250.000, leucócitos 12.500.")]
    [InlineData("A dor começou em 03/10/2026 e piorou ontem.")]
    [InlineData("Caiu na rua ontem e bateu o joelho.")]
    [InlineData("Suspeita de doença de Parkinson; avaliar sinal de Murphy.")]
    [InlineData("Ai, meu Deus, doutora, que dor.")]
    [InlineData("Retorno em 7 dias com hemograma e PCR.")]
    public void InformacaoClinica_FicaIntacta(string texto)
        => Assert.Equal(texto, Sanitizar(texto));

    [Fact]
    public void PalavraComumQueTambemEhNome_NaoEhApagadaNoInicioDaFrase()
    {
        var identidade = Identidade with { PacienteNome = "Maria das Dores" };

        var resultado = _sanitizador.Sanitizar(
            "Dores nas costas há 3 dias. A Maria das Dores toma losartana.", identidade).Valor;

        Assert.Equal("Dores nas costas há 3 dias. A [PACIENTE] toma losartana.", resultado);
    }

    // ---------- Garantias da camada ----------

    [Fact]
    public void SanitizarDeNovo_NaoAlteraOTextoJaSanitizado()
    {
        var uma = Sanitizar("Dona Helena, CPF 529.982.247-25, filha Mariana no 21 99876-5544.");
        var duas = Sanitizar(uma);

        Assert.Equal(uma, duas);
    }

    [Fact]
    public void Reidentificar_DevolveSoOsDadosDoCadastro()
    {
        var sanitizado = Sanitizar("Dona Helena, CPF 529.982.247-25. A vizinha Joana ligou do 21 99876-5544.");

        var exibicao = _sanitizador.Reidentificar(sanitizado, Identidade);

        Assert.Equal(
            "Dona Helena Souza Lima, CPF 529.982.247-25. A vizinha [PESSOA] ligou do [TELEFONE].",
            exibicao);
    }

    [Fact]
    public void Contagem_InformaAsCategoriasSemExporOValor()
    {
        var resultado = _sanitizador.Sanitizar(
            "Dona Helena, liga no 21 99876-5544 ou no 21 98888-7777.", Identidade);

        Assert.Equal(1, resultado.Substituicoes["PACIENTE"]);
        Assert.Equal(2, resultado.Substituicoes["TELEFONE"]);
        Assert.Equal(3, resultado.TotalSubstituicoes);
    }

    [Fact]
    public void Registro_TemCadaCampoSanitizado()
    {
        var registro = new RascunhoClinicoDto(
            "Helena com tosse há 4 dias.", null, "Filha Mariana acompanha.", null, null, "J06.9", null);

        var sanitizado = _sanitizador.SanitizarRegistro(registro, Identidade);

        Assert.Equal("[PACIENTE] com tosse há 4 dias.", sanitizado.QueixaPrincipal);
        Assert.Equal("Filha [PESSOA] acompanha.", sanitizado.Antecedentes);
        Assert.Null(sanitizado.Hda);
    }

    [Theory]
    [InlineData("52998224725", true)]
    [InlineData("11144477735", true)]
    [InlineData("12345678900", false)]
    [InlineData("11111111111", false)]
    public void Cpf_ValidaDigitoVerificador(string cpf, bool valido)
        => Assert.Equal(valido, SanitizadorDados.CpfValido(cpf));
}
