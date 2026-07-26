using System.Collections.Concurrent;

namespace Adnd.Server.Services;

public interface IDeadLetterQueue
{
    bool ShouldRetry(Guid agentCallId);
    void RecordFailure(Guid agentCallId);
    void Clear(Guid agentCallId);
}

public class DeadLetterQueue : IDeadLetterQueue
{
    private readonly ConcurrentDictionary<Guid, int> _failures = new();

    public bool ShouldRetry(Guid agentCallId)
        => _failures.GetValueOrDefault(agentCallId) < 3;

    public void RecordFailure(Guid agentCallId)
        => _failures.AddOrUpdate(agentCallId, 1, (_, c) => c + 1);

    public void Clear(Guid agentCallId)
        => _failures.TryRemove(agentCallId, out _);
}
