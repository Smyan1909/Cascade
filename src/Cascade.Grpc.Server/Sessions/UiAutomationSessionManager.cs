using System.Collections.Concurrent;
using Cascade.UIAutomation.Services;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;

namespace Cascade.Grpc.Server.Sessions;

internal sealed class UiAutomationSessionManager : IUiAutomationSessionManager
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConcurrentDictionary<string, Lazy<Task<IUIAutomationService>>> _services = new(StringComparer.Ordinal);

    public UiAutomationSessionManager(
        IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    public Task<IUIAutomationService> GetServiceAsync(GrpcSessionContext? session, CancellationToken cancellationToken = default)
    {
        var key = string.IsNullOrWhiteSpace(session?.SessionId) ? "local" : session!.SessionId;
        var lazy = _services.GetOrAdd(key, _ => new Lazy<Task<IUIAutomationService>>(
            () => CreateServiceAsync(session, cancellationToken),
            LazyThreadSafetyMode.ExecutionAndPublication));

        return lazy.Value;
    }

    public void Invalidate(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        _services.TryRemove(sessionId, out _);
    }

    private async Task<IUIAutomationService> CreateServiceAsync(GrpcSessionContext? context, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var runtimeResolver = scope.ServiceProvider.GetRequiredService<ISessionRuntimeResolver>();
        var runtime = await runtimeResolver.ResolveAsync(context, cancellationToken).ConfigureAwait(false);
        var factory = scope.ServiceProvider.GetRequiredService<IUIAutomationServiceFactory>();
        return factory.Create(runtime.Handle, runtime.RootElement, runtime.InputChannel);
    }
}

