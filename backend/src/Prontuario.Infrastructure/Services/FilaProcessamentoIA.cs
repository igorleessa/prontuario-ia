using System.Threading.Channels;
using Prontuario.Application.Common.Interfaces;

namespace Prontuario.Infrastructure.Services;

public class FilaProcessamentoIA : IFilaProcessamentoIA
{
    private readonly Channel<Guid> _canal = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions { SingleReader = true });

    public ValueTask EnfileirarAsync(Guid atendimentoId, CancellationToken cancellationToken = default)
        => _canal.Writer.WriteAsync(atendimentoId, cancellationToken);

    public ValueTask<Guid> ProximoAsync(CancellationToken cancellationToken)
        => _canal.Reader.ReadAsync(cancellationToken);
}
