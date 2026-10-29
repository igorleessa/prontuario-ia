namespace Prontuario.Domain.Enums;

/// <summary>
/// Quem disse o trecho da consulta. Atribuido pelo conteudo da fala (a IA le
/// os trechos; nao ha audio para separar vozes) e corrigivel pelo medico.
/// </summary>
public enum Falante
{
    Indefinido = 0,
    Medico = 1,
    Paciente = 2,
    Acompanhante = 3
}
