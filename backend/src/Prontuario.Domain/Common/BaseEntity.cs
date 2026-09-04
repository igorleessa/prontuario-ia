namespace Prontuario.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime CriadoEm { get; init; } = DateTime.UtcNow;
}
