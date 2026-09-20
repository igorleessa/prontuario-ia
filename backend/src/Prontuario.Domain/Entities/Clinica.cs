using Prontuario.Domain.Common;
using Prontuario.Domain.Enums;

namespace Prontuario.Domain.Entities;

public class Clinica : BaseEntity
{
    public string Nome { get; set; } = string.Empty;
    public ModoOperacao ModoOperacao { get; set; } = ModoOperacao.Integrado;

    // Configuracao de exportacao - relevante apenas para ModoOperacao.Conector (RF19).
    public string? WebhookUrl { get; set; }

    /// <summary>
    /// Segredo do webhook cifrado pelo Data Protection. E com ele que o EMR de
    /// destino confere a assinatura HMAC do payload (RNF10); nenhuma rota devolve
    /// o valor em claro depois de gravado.
    /// </summary>
    public string? WebhookSecretProtegido { get; set; }

    /// <summary>
    /// SHA-256 da chave que o EMR do cliente usa para abrir atendimentos pela API
    /// de integracao. Guardar o hash - e nao a chave - mantem o segredo fora do
    /// banco e ainda permite a busca, porque o hash e deterministico.
    /// </summary>
    public string? ChaveIntegracaoHash { get; set; }

    /// <summary>Primeiros caracteres da chave de integracao, para identifica-la na tela sem expo-la.</summary>
    public string? ChaveIntegracaoPrefixo { get; set; }

    public DateTime? ChaveIntegracaoCriadaEm { get; set; }

    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
    public ICollection<PacienteRef> Pacientes { get; set; } = new List<PacienteRef>();
}
