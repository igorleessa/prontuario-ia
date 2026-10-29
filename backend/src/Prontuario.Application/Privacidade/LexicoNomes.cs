namespace Prontuario.Application.Privacidade;

/// <summary>
/// Prenomes e sobrenomes mais frequentes no Brasil. E o reconhecedor de nomes
/// de terceiros que roda aqui, sem modelo nem servico externo: um nome comum
/// grafado com maiuscula ("a Mariana trouxe os exames") e trocado por
/// [PESSOA] mesmo sem "minha filha" ou "seu" antes dele. As chaves passam pela
/// mesma normalizacao do resto da sanitizacao, entao acento e grafia variante
/// (Thiago/Tiago, Luiz/Luis) nao escapam.
/// </summary>
internal static class LexicoNomes
{
    public static readonly HashSet<string> Prenomes = Chaves(
        // Femininos
        "Maria", "Ana", "Francisca", "Antônia", "Adriana", "Juliana", "Márcia", "Fernanda", "Patrícia",
        "Aline", "Sandra", "Camila", "Amanda", "Bruna", "Jéssica", "Letícia", "Júlia", "Luciana", "Vanessa",
        "Mariana", "Gabriela", "Larissa", "Rafaela", "Beatriz", "Isabela", "Isabel", "Carolina", "Daniela",
        "Paula", "Renata", "Simone", "Cristiane", "Cristina", "Tatiane", "Raquel", "Rosângela", "Rosana",
        "Sônia", "Vera", "Lúcia", "Luzia", "Terezinha", "Teresa", "Aparecida", "Fátima", "Regina", "Sueli",
        "Marlene", "Eliane", "Edna", "Elaine", "Débora", "Denise", "Sílvia", "Cláudia", "Mônica", "Andréa",
        "Andreia", "Kátia", "Priscila", "Natália", "Thaís", "Bianca", "Carla", "Viviane", "Michele", "Joana",
        "Josefa", "Laura", "Alice", "Sofia", "Manuela", "Valentina", "Heloísa", "Lívia", "Lorena", "Cecília",
        "Eduarda", "Yasmin", "Lara", "Lavínia", "Marina", "Nicole", "Rebeca", "Sara", "Stella", "Estela",
        "Vitória", "Alícia", "Ágatha", "Melissa", "Clara", "Luana", "Luíza", "Giovana", "Ester", "Eva",
        "Irene", "Ivone", "Jaqueline", "Jussara", "Kelly", "Leila", "Lilian", "Miriam", "Neusa", "Olga",
        "Rita", "Rosa", "Sebastiana", "Tânia", "Valéria", "Zilda", "Glória", "Graça", "Dores", "Socorro",
        "Conceição", "Aurora", "Celeste", "Íris", "Pérola", "Helena", "Marta", "Lourdes", "Rosemary",
        "Elisa", "Elizabete", "Margarida", "Fabiana", "Flávia", "Gisele", "Ingrid", "Karina", "Lorraine",
        "Milena", "Nathalia", "Pietra", "Sabrina", "Tamires", "Vitória", "Wanda", "Benedita", "Raimunda",
        // Masculinos
        "José", "João", "Antônio", "Francisco", "Carlos", "Paulo", "Pedro", "Lucas", "Luiz", "Marcos",
        "Gabriel", "Rafael", "Daniel", "Marcelo", "Bruno", "Eduardo", "Felipe", "Raimundo", "Rodrigo",
        "Manoel", "Mateus", "André", "Fernando", "Fábio", "Leonardo", "Gustavo", "Guilherme", "Leandro",
        "Tiago", "Anderson", "Ricardo", "Márcio", "Jorge", "Sebastião", "Alexandre", "Roberto", "Edson",
        "Diego", "Vitor", "Sérgio", "Cláudio", "Matheus", "Thiago", "Geraldo", "Adriano", "Luciano", "Júlio",
        "Renato", "Alex", "Vinícius", "Rogério", "Samuel", "Ronaldo", "Mário", "Flávio", "Igor", "Douglas",
        "Davi", "Arthur", "Bernardo", "Heitor", "Miguel", "Enzo", "Lorenzo", "Theo", "Benjamin", "Nicolas",
        "Henrique", "Murilo", "Caio", "Otávio", "Augusto", "César", "Hugo", "Emanuel", "Ian", "Kauã", "Ryan",
        "Yuri", "Wesley", "Willian", "Alan", "Maurício", "Nelson", "Osvaldo", "Valdir", "Wellington",
        "Everton", "Fabrício", "Gilberto", "Gilson", "Hélio", "Ivan", "Jair", "Jefferson", "Milton",
        "Moisés", "Nilton", "Orlando", "Reinaldo", "Rubens", "Sílvio", "Vicente", "Walter", "Washington",
        "Joaquim", "Benedito", "Severino", "Wagner", "Marcos", "Abel", "Elias", "Ezequiel", "Gerson",
        "Isaac", "Jonas", "Josué", "Leonel", "Lauro", "Mauro", "Noel", "Otacílio", "Raul", "Ruan", "Saulo");

    public static readonly HashSet<string> Sobrenomes = Chaves(
        "Silva", "Santos", "Oliveira", "Souza", "Rodrigues", "Ferreira", "Alves", "Pereira", "Lima", "Gomes",
        "Costa", "Ribeiro", "Martins", "Carvalho", "Almeida", "Lopes", "Soares", "Fernandes", "Vieira",
        "Barbosa", "Rocha", "Dias", "Nascimento", "Andrade", "Moreira", "Nunes", "Marques", "Machado",
        "Mendes", "Freitas", "Cardoso", "Ramos", "Gonçalves", "Santana", "Teixeira", "Araújo", "Pinto",
        "Moura", "Campos", "Correia", "Cavalcanti", "Monteiro", "Batista", "Barros", "Castro", "Duarte",
        "Farias", "Melo", "Miranda", "Nogueira", "Pires", "Reis", "Rezende", "Sampaio", "Siqueira",
        "Tavares", "Vasconcelos", "Xavier", "Azevedo", "Brandão", "Bezerra", "Borges", "Brito", "Coelho",
        "Cunha", "Fonseca", "Leite", "Macedo", "Medeiros", "Neves", "Pacheco", "Paiva", "Prado", "Queiroz",
        "Sales", "Toledo", "Vaz", "Viana", "Guimarães", "Magalhães", "Peixoto", "Rangel", "Bastos", "Assis");

    /// <summary>
    /// Prenomes que tambem sao palavras do dia a dia ou da consulta ("Clara",
    /// "Dores", "Socorro", "Íris"). Sozinhos nao contam: so viram [PESSOA]
    /// seguidos de outro nome ou sobrenome ("Clara Mendes").
    /// </summary>
    public static readonly HashSet<string> Ambiguos = Chaves(
        "Clara", "Dores", "Socorro", "Conceição", "Aurora", "Celeste", "Íris", "Pérola", "Glória", "Graça",
        "Vitória", "Rosa", "Sara", "Luz", "Vera", "Augusto", "Abel", "Jesus", "Mauro");

    private static HashSet<string> Chaves(params string[] nomes)
        => nomes.Select(SanitizadorDados.Chave).ToHashSet();
}
