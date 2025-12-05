namespace Cascade.Grpc.Server.Sessions;

internal interface ISessionEventDispatcher
{
    IAsyncEnumerable<SessionEventMessage> SubscribeAsync(string agentId, CancellationToken cancellationToken = default);
    void Publish(SessionEventMessage message);
}

