using Prontuario.Domain.Common;

namespace Prontuario.Domain.Entities;

/// <summary>
/// Credenciais e modelos de IA de uma clinica (RNF07). A chave nunca e gravada
/// em claro: <see cref="ChaveApiProtegida"/> guarda o texto cifrado pelo
/// Data Protection do ASP.NET Core, e nenhuma rota devolve o valor original.
/// </summary>
public class ConfiguracaoIA : BaseEntity
{
    public Guid ClinicaId { get; set; }
    public Clinica? Clinica { get; set; }

    public string ChaveApiProtegida { get; set; } = string.Empty;

    /// <summary>Ultimos caracteres da chave, para o medico reconhecer qual esta ativa sem expo-la.</summary>
    public string ChaveApiSufixo { get; set; } = string.Empty;

    public string ModeloTranscricao { get; set; } = "whisper-1";
    public string ModeloTexto { get; set; } = "gpt-4o";

    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;
    public Guid? AtualizadoPorId { get; set; }
}
