namespace Prontuario.Infrastructure.Services;

/// <summary>
/// Conversa ficticia de um exame ocupacional, para o modo demonstracao: a tela
/// envia as falas uma a uma, como se viessem do microfone, e o resto do fluxo
/// (sanitizacao, extracao, validacao) e o real. Nenhum dado real entra aqui.
/// </summary>
public static class ExameOcupacionalExemplo
{
    public static readonly IReadOnlyList<string> Falas =
    [
        "Bom dia, pode sentar. Vamos fazer o seu exame periódico.",
        "Bom dia, doutora.",
        "Já sofreu algum acidente de trabalho?",
        "Não, nunca.",
        "E já ficou afastado pelo INSS?",
        "Também não.",
        "Há quanto tempo trabalha nessa função?",
        "Vai fazer quatro anos.",
        "Pratica atividade física?",
        "Não, estou bem parado, só trabalho mesmo.",
        "E a alimentação, como considera?",
        "Mais ou menos, regular.",
        "Fuma?",
        "Fumo, uns cinco cigarros por dia, faz uns oito anos.",
        "Bebida alcoólica?",
        "Só socialmente, no fim de semana.",
        "Dorme bem? Tem se sentido estressado?",
        "Durmo bem. Estressado eu ando, sim, bastante.",
        "Tem pressão alta, diabetes, problema de coração?",
        "Pressão alta eu tenho, tomo remédio. Diabetes e coração, não.",
        "Alguma dor no momento?",
        "Dor na lombar, uns seis de dez.",
        "Vamos ao exame. Pressão cento e trinta e cinco por oitenta e cinco, frequência cardíaca setenta e seis, respiratória dezesseis.",
        "Peso oitenta e quatro quilos, altura um metro e setenta e dois, circunferência abdominal noventa e nove.",
        "Ausculta cardíaca com bulhas rítmicas normofonéticas, sem sopros. Murmúrio vesicular presente, sem ruídos adventícios.",
        "Região cervical normal, marcha normal.",
    ];
}
