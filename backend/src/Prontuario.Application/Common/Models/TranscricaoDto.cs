namespace Prontuario.Application.Common.Models;

/// <summary>Como o navegador deve transcrever a consulta.</summary>
public static class ModosTranscricao
{
    /// <summary>
    /// O navegador fala direto com o provedor (WebRTC), com uma chave
    /// temporaria. O audio nao passa pelos nossos servidores.
    /// </summary>
    public const string TempoReal = "TempoReal";

    /// <summary>
    /// Alternativa quando o tempo real nao esta disponivel: trechos curtos de
    /// audio sao enviados ao backend, transcritos em memoria e descartados.
    /// </summary>
    public const string Trechos = "Trechos";
}

/// <summary>
/// Sessao de escuta aberta para o atendimento. <paramref name="ChaveTemporaria"/>
/// so existe no modo tempo real: e uma credencial de curta duracao emitida pelo
/// provedor - a chave da clinica nunca chega ao navegador.
/// </summary>
public sealed record SessaoTranscricaoDto(
    string Modo,
    string? ChaveTemporaria,
    DateTime? ChaveExpiraEm,
    string? UrlConexao,
    string Modelo,
    /// <summary>Proxima sequencia livre: retomar a consulta nao sobrescreve o que ja foi dito.</summary>
    int ProximaSequencia,
    string? MotivoModoAlternativo);

/// <summary>Trecho de fala ja transcrito pelo provedor, enviado pelo navegador para sanitizar e guardar.</summary>
public sealed record RegistrarTrechoDto(int Sequencia, string Texto);

/// <summary>
/// Trecho como o medico o ve: com os dados do paciente devolvidos a partir do
/// cadastro e os de terceiros apagados.
/// </summary>
public sealed record TrechoRegistradoDto(int Sequencia, string Texto, int DadosProtegidos);

/// <summary>Credencial temporaria emitida pelo provedor de transcricao em tempo real.</summary>
public sealed record CredencialTempoReal(string Chave, DateTime ExpiraEm, string UrlConexao);

/// <summary>
/// Desfecho de uma operacao que devolve um valor. <paramref name="Erro"/>
/// carrega a mensagem para o medico quando o estado nao permite a operacao ou o
/// provedor de IA falhou.
/// </summary>
public sealed record ResultadoOperacao<T>(ResultadoAtendimento Resultado, T? Valor = default, string? Erro = null)
{
    public static ResultadoOperacao<T> Ok(T? valor) => new(ResultadoAtendimento.Ok, valor);
    public static ResultadoOperacao<T> NaoEncontrado() => new(ResultadoAtendimento.NaoEncontrado);
    public static ResultadoOperacao<T> Conflito(string erro) => new(ResultadoAtendimento.Conflito, default, erro);
    public static ResultadoOperacao<T> Falha(string erro) => new(ResultadoAtendimento.Falha, default, erro);
}
