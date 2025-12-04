using Cascade.UIAutomation.Session;
using Microsoft.Extensions.DependencyInjection;

namespace Cascade.UIAutomation.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCascadeUIAutomation(
        this IServiceCollection services,
        Action<UIAutomationOptions>? configure = null)
    {
        services.AddOptions<UIAutomationOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddSingleton<ISessionContextAccessor, SessionContextAccessor>();
        services.AddSingleton<IUIAutomationServiceFactory, UIAutomationServiceFactory>();

        return services;
    }
}


