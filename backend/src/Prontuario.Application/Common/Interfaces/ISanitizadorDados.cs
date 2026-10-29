using Prontuario.Application.Common.Models;
using Prontuario.Application.Privacidade;

namespace Prontuario.Application.Common.Interfaces;

/// <summary>
/// Camada intermediaria entre a transcricao e tudo o que vem depois dela: o
/// provedor de IA, o banco e os logs. Roda inteiramente aqui - mandar o texto
/// bruto a um servico externo para "detectar dado pessoal" anularia o proposito.
/// </summary>
public interface ISanitizadorDados
{
    /// <summary>Troca os dados pessoais do texto por marcadores.</summary>
    TextoSanitizado Sanitizar(string texto, IdentidadeAtendimento identidade);

    /// <summary>
    /// Todas as camadas, inclusive o reconhecimento estatistico de nomes quando
    /// habilitado. E a que deve preceder qualquer envio ao provedor de IA.
    /// Lanca <see cref="ProtecaoIndisponivelException"/> se o reconhecedor nao responder.
    /// </summary>
    Task<TextoSanitizado> SanitizarAsync(
        string texto, IdentidadeAtendimento identidade, CancellationToken cancellationToken = default);

    /// <summary>Registro clinico com todas as camadas, como <see cref="SanitizarAsync"/>.</summary>
    Task<RegistroSanitizado> SanitizarRegistroAsync(
        RascunhoClinicoDto registro, IdentidadeAtendimento identidade, CancellationToken cancellationToken = default);

    /// <summary>Se o reconhecimento estatistico de nomes esta em uso neste ambiente.</summary>
    bool ReconhecimentoHabilitado { get; }

    /// <summary>Sanitiza cada campo do registro clinico (que o medico pode ter digitado com nomes).</summary>
    RegistroSanitizado SanitizarRegistro(RascunhoClinicoDto registro, IdentidadeAtendimento identidade);

    /// <summary>
    /// Devolve os valores reais dos marcadores do paciente e do medico, a partir
    /// do cadastro. Marcadores de terceiros ([PESSOA], [TELEFONE]...) ficam como
    /// estao: o dado apagado nao volta.
    /// </summary>
    string Reidentificar(string texto, IdentidadeAtendimento identidade);
}
