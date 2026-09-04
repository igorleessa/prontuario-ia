using Prontuario.Application.Common.Models;

namespace Prontuario.Application.Common.Interfaces;

/// <summary>
/// Converte o conteudo clinico revisado no texto corrido enviado ao EMR externo
/// (RF18/RF20). E a unica fonte do formato: a pre-visualizacao mostrada ao medico
/// e a nota efetivamente exportada saem daqui, para nao divergirem.
/// </summary>
public interface INotaClinicaFormatter
{
    string Formatar(RascunhoClinicoDto revisado);
}
