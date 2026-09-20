using Prontuario.Domain.Entities;

namespace Prontuario.Infrastructure.Persistence;

/// <summary>
/// Catalogo de modelos de nota disponibilizado a todas as clinicas
/// (ClinicaId nulo). E o que diferencia, na pratica, uma nota de cardiologia
/// de uma de pediatria: o pipeline e o mesmo, o que muda e o que se espera
/// encontrar em cada campo. Clinicas podem cadastrar os proprios modelos por
/// cima destes.
/// </summary>
public static class CatalogoTemplatesSeed
{
    public static IReadOnlyList<TemplateNota> Modelos() =>
    [
        new()
        {
            Nome = "Consulta geral (SOAP)",
            Especialidade = "Clinica geral",
            Ordem = 1,
            Instrucoes = """
                Estruture a nota no formato SOAP tradicional. Na HDA, registre inicio, evolucao,
                fatores de melhora e piora e sintomas associados quando mencionados. Em exame
                fisico, registre apenas o que foi verbalizado durante a consulta.
                """,
        },
        new()
        {
            Nome = "Cardiologia",
            Especialidade = "Cardiologia",
            Ordem = 2,
            Instrucoes = """
                Na HDA, destaque dor toracica (tipo, irradiacao, relacao com esforco), dispneia e
                classe funcional NYHA, palpitacoes, sincope e edema. Em antecedentes, registre
                hipertensao, diabetes, dislipidemia, tabagismo, historia familiar de doenca
                coronariana e eventos cardiovasculares previos. Em exame fisico, priorize pressao
                arterial, frequencia e ritmo cardiaco, ausculta cardiaca e pulmonar, pulsos e edema.
                Em conduta, registre ajustes de medicacao cardiologica com dose.
                """,
        },
        new()
        {
            Nome = "Pediatria",
            Especialidade = "Pediatria",
            Ordem = 3,
            Instrucoes = """
                Registre quem acompanha a crianca e quem relata a historia. Na HDA, inclua febre
                (temperatura e duracao), aceitacao alimentar, diurese, evacuacoes, sono e
                irritabilidade quando mencionados. Em antecedentes, registre gestacao e parto,
                vacinacao em dia, marcos do desenvolvimento e alergias. Em exame fisico, priorize
                estado geral, hidratacao, peso e estatura quando ditos, e ausculta.
                """,
        },
        new()
        {
            Nome = "Ortopedia",
            Especialidade = "Ortopedia",
            Ordem = 4,
            Instrucoes = """
                Na HDA, registre mecanismo do trauma, tempo de evolucao, lado acometido,
                caracteristica e intensidade da dor, limitacao funcional e tratamentos ja tentados.
                Em exame fisico, priorize inspecao, palpacao, amplitude de movimento, testes
                especiais citados e exame neurovascular distal. Em conduta, registre imobilizacao,
                fisioterapia, analgesia e exames de imagem solicitados.
                """,
        },
        new()
        {
            Nome = "Psiquiatria",
            Especialidade = "Psiquiatria",
            Ordem = 5,
            Instrucoes = """
                Na HDA, registre humor, sono, apetite, energia, concentracao, ansiedade e uso de
                substancias, com tempo de evolucao. Registre explicitamente ideacao suicida quando
                abordada, inclusive quando negada. Em antecedentes, inclua episodios previos,
                internacoes, tentativas e medicacoes ja usadas com resposta. O exame fisico
                corresponde ao exame do estado mental descrito na consulta.
                """,
        },
        new()
        {
            Nome = "Ginecologia e obstetricia",
            Especialidade = "Ginecologia",
            Ordem = 6,
            Instrucoes = """
                Na HDA, registre ciclo menstrual (data da ultima menstruacao, regularidade, fluxo),
                queixas ginecologicas e sintomas associados. Em antecedentes, inclua gestacoes,
                partos e abortos, metodo contraceptivo, ultimo preventivo e cirurgias previas.
                Em gestantes, registre idade gestacional e intercorrencias quando mencionadas.
                """,
        },
    ];
}
