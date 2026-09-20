namespace Prontuario.Infrastructure.Services;

/// <summary>
/// Consulta ficticia usada no modo demonstracao. Existe para que a apresentacao
/// ao cliente nao dependa de microfone, de permissao do navegador nem da
/// latencia do provedor de IA - tres coisas que costumam falhar justamente na
/// frente de quem se quer convencer. O texto e inventado; nenhum dado real de
/// paciente entra aqui.
/// </summary>
public static class ConsultaExemplo
{
    /// <summary>Prefixo da chave de storage que marca uma gravacao simulada.</summary>
    public const string PrefixoStorage = "demonstracao:";

    /// <summary>Duracao aproximada da consulta simulada, em segundos.</summary>
    public const int DuracaoSegundos = 512;

    public const string Transcricao = """
        Medica: Bom dia, dona Helena, pode sentar. O que a trouxe hoje?
        Paciente: Bom dia, doutora. E que faz uns quatro dias que eu estou com uma tosse que nao
        passa, e comecou uma dor de garganta junto.
        Medica: Entendi. A tosse e seca ou sai secrecao?
        Paciente: No comeco era seca, agora de manha sai um catarro meio amarelado.
        Medica: A senhora teve febre?
        Paciente: Tive, anteontem a noite deu trinta e sete e oito. Ontem nao medi, mas senti o corpo
        quente de novo.
        Medica: Falta de ar, chiado no peito, dor no peito?
        Paciente: Falta de ar nao. As vezes da uma dor aqui nas costas quando eu tusso muito.
        Medica: A senhora continua usando o losartana para a pressao?
        Paciente: Continuo, cinquenta miligramas de manha, todo dia.
        Medica: E a diabetes, esta controlada?
        Paciente: Estou tomando a metformina duas vezes ao dia. O ultimo exame deu cento e trinta em
        jejum.
        Medica: Alguma alergia a medicamento?
        Paciente: Nao que eu saiba.
        Medica: Vou examinar. Respira fundo pela boca... de novo... Pode falar trinta e tres.
        Medica: Pressao esta cento e trinta por oitenta, temperatura trinta e sete e dois, saturacao
        noventa e sete por cento. Ausculta pulmonar com alguns roncos difusos, sem sibilos e sem
        estertores. Orofaringe hiperemiada, sem placas. Ausculta cardiaca normal, dois tempos,
        sem sopros.
        Paciente: E grave, doutora?
        Medica: Nao. O quadro e compativel com uma infeccao respiratoria alta, provavelmente viral,
        com um componente de bronquite. Nao vou passar antibiotico agora.
        Paciente: E a tosse?
        Medica: Vou prescrever dipirona quinhentos miligramas, um comprimido de seis em seis horas
        se tiver dor ou febre, e um xarope expectorante, quinze mililitros de oito em oito horas por
        cinco dias. Beba bastante agua e mantenha o ambiente umido.
        Medica: Se em setenta e duas horas a febre nao ceder, ou se aparecer falta de ar, volte
        antes. Vou pedir um hemograma e uma radiografia de torax para a gente conferir.
        Paciente: Esta bem, doutora. Preciso de atestado de dois dias para o trabalho.
        Medica: Claro, vou fazer o atestado de dois dias. Retorno em uma semana com os exames.
        """;
}
