using Cascade.Grpc.Server.Sessions;
using Microsoft.Extensions.DependencyInjection;

namespace Cascade.Grpc.Server.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGrpcServerInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IGrpcSessionContextAccessor, GrpcSessionContextAccessor>();
        services.AddSingleton<UiElementRegistry>();
        services.AddSingleton<ISessionEventDispatcher, SessionEventDispatcher>();
        services.AddScoped<ISessionRuntimeResolver, SessionRuntimeResolver>();
        services.AddScoped<ISessionLifecycleManager, SessionLifecycleManager>();
        services.AddSingleton<IUiAutomationSessionManager, UiAutomationSessionManager>();

        return services;
    }
}

