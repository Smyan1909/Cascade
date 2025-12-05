using Cascade.UIAutomation.Services;

namespace Cascade.Grpc.Server.Sessions;

public interface IUiAutomationSessionManager
{
    Task<IUIAutomationService> GetServiceAsync(GrpcSessionContext session, CancellationToken cancellationToken = default);
    void Invalidate(string sessionId);
}

