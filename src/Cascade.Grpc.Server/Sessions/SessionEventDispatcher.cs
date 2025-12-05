using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Cascade.Grpc.Server.Sessions;

internal sealed class SessionEventDispatcher : ISessionEventDispatcher
{
    private readonly ConcurrentDictionary<string, List<Channel<SessionEventMessage>>> _subscribers = new(StringComparer.OrdinalIgnoreCase);

    public IAsyncEnumerable<SessionEventMessage> SubscribeAsync(string agentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(agentId))
        {
            throw new ArgumentException("AgentId is required.", nameof(agentId));
        }

        var channel = Channel.CreateUnbounded<SessionEventMessage>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        var list = _subscribers.GetOrAdd(agentId, _ => new List<Channel<SessionEventMessage>>());
        lock (list)
        {
            list.Add(channel);
        }

        return ReadAsync(agentId, channel, cancellationToken);
    }

    public void Publish(SessionEventMessage message)
    {
        if (message is null || string.IsNullOrWhiteSpace(message.AgentId))
        {
            return;
        }

        if (!_subscribers.TryGetValue(message.AgentId, out var channels))
        {
            return;
        }

        lock (channels)
        {
            foreach (var channel in channels.ToArray())
            {
                channel.Writer.TryWrite(message);
            }
        }
    }

    private async IAsyncEnumerable<SessionEventMessage> ReadAsync(
        string agentId,
        Channel<SessionEventMessage> channel,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        try
        {
            while (await channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                while (channel.Reader.TryRead(out var message))
                {
                    yield return message;
                }
            }
        }
        finally
        {
            if (_subscribers.TryGetValue(agentId, out var channels))
            {
                lock (channels)
                {
                    channels.Remove(channel);
                    if (channels.Count == 0)
                    {
                        _subscribers.TryRemove(agentId, out _);
                    }
                }
            }
        }
    }
}

