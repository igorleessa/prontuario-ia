namespace Prontuario.Domain.Enums;

/// <summary>
/// Documentos que a IA redige a partir da consulta, alem da nota clinica.
/// Todos passam pela revisao do medico antes de valer - nenhum e emitido
/// automaticamente (ver "regra de ouro" na especificacao).
/// </summary>
public enum TipoDocumento
{
    Receita = 1,
    PedidoExame = 2,
    Atestado = 3,
    Encaminhamento = 4
}
