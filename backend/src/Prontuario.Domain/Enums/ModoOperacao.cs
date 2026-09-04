namespace Prontuario.Domain.Enums;

/// <summary>
/// Define se a clinica opera com prontuario nativo (Integrado) ou como
/// camada de IA que exporta a nota para o EMR ja usado pela clinica (Conector).
/// Ver docs/especificacao-mvp.md, secao 2.
/// </summary>
public enum ModoOperacao
{
    Integrado = 1,
    Conector = 2
}
