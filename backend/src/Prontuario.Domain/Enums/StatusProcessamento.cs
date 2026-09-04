namespace Prontuario.Domain.Enums;

/// <summary>Usado por Transcricao e RascunhoIA para acompanhar o pipeline assincrono.</summary>
public enum StatusProcessamento
{
    Pendente = 1,
    Processando = 2,
    Concluido = 3,
    Falhou = 4
}
